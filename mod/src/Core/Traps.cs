using System;
using System.Collections.Generic;
using System.Linq;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The traps and bonuses (ROADMAP step 3) and DeathLink, on the host.
    //
    // Only the host talks to Archipelago, so only the host learns that a trap has arrived or a
    // DeathLink has come in. It queues them, fires one every few seconds once the session has
    // settled (a trap received while nobody played waits for the world, it is not lost), works
    // out what each player gets (where Big Trip sends them, who Big Meeting gathers around) and
    // tells every guest through the ModChannel; each machine then plays the effect on its own
    // player (Core/TrapEffects.cs). Every trap and bonus hits every player of the session
    // (player, 2026-10-05).
    //
    // DeathLink goes out from here too, as the slot's `death_link_triggers` say (a failed
    // puzzle), after `death_link_tolerance` of them have been let go.
    internal static class Traps
    {
        private const string Tag = "[" + nameof(Traps) + "]";

        // Between two effects fired from the queue, and how many may wait in it: a slot back after
        // a long time away does not get twenty traps in a row.
        private const float Spacing = 4f;
        private const int MaxPending = 10;

        // How far from its target a player gathered by Big Meeting lands.
        private const float MeetingRadius = 2.2f;

        // DeathLink: never two sent closer than this, nor two from one player's falls.
        private const float DeathLinkCooldown = 10f;

        // --- what the slot says ---
        private static Dictionary<long, string> _items = new();
        private static int _duration = 30;
        private static bool _spareGauntlet = true;
        private static bool _sendsDeathLink;
        private static bool _receivesDeathLink;
        private static HashSet<string> _triggers = new();
        private static int _tolerance;
        private static string _deathLinkEffect = "drop";
        private static Dictionary<string, int> _roulette = new();
        private static bool _trapOnSend;
        private static bool _trapLink;

        internal static int Duration => _duration;

        internal static void Configure(ApSlotData slot)
        {
            _items = new Dictionary<long, string>(slot.EffectItems);
            _duration = Mathf.Clamp(slot.TrapDuration, 5, 600);
            _spareGauntlet = slot.TrapsSpareTheGauntlet;
            _sendsDeathLink = slot.SendsDeathLink;
            _receivesDeathLink = slot.ReceivesDeathLink;
            _triggers = new HashSet<string>(slot.DeathLinkTriggers);
            _tolerance = Math.Max(0, slot.DeathLinkTolerance);
            _deathLinkEffect = slot.DeathLinkEffect;
            _trapOnSend = slot.DeathLinkTrapOnSend;
            _trapLink = slot.TrapLink;
            _roulette = new Dictionary<string, int>();
            foreach (var pair in slot.DeathLinkRoulette)
            {
                if (Array.IndexOf(TrapEffects.Known, pair.Key) >= 0)
                    _roulette[pair.Key] = pair.Value;
            }
            _missed = 0;
            _deathLinkOnePlayer = slot.DeathLinkTarget == "one_player";
        }

        // What the host's settings make of the slot's DeathLink (Settings > Archipelago, player
        // 2026-10-06): off sends and takes none; the amnesty can stand in for the seed's.
        internal static bool DeathLinkOn => ModConfig.DeathLinkEnabled.Value;
        internal static bool ReceivesDeathLink => _receivesDeathLink && DeathLinkOn;
        internal static bool SeedHasDeathLink => _sendsDeathLink || _receivesDeathLink;
        internal static int SeedAmnesty => _tolerance;
        internal static int Amnesty => ModConfig.DeathLinkAmnesty.Value >= 0 ? ModConfig.DeathLinkAmnesty.Value : _tolerance;

        internal static bool TryResolveItem(long itemId, out string key)
        {
            return _items.TryGetValue(itemId - ApLocationIds.Base, out key);
        }

        // ------------------------------------------------------------------
        // The queue
        // ------------------------------------------------------------------

        private static readonly Queue<string> Pending = new();

        // Beside each pending effect: whether it hits one player only (a DeathLink received with
        // `death_link_target: one_player`, players 2026-10-07). Not kept in the save: an effect
        // still waiting when the game closed comes back for everyone.
        private static readonly Queue<bool> PendingSingle = new();

        private static bool _deathLinkOnePlayer;

        // How many DeathLinks each player took this session, by their object's name: the next
        // single one goes to the one with the fewest, ties at random.
        private static readonly Dictionary<string, int> DeathLinkHits = new();

        // During Fire: the one player it hits, or null for everyone.
        private static PlayerCharacter _onlyPlayer;
        private static float _nextFire;

        // The queue is kept in the save too, as a count per effect, so traps still waiting when
        // the game closes come back next session (the item cursor has already counted them).
        private const string PendingPrefix = "ap_trap_pending_";

        // For this many seconds after the world is ready, counted again each time a player joins,
        // a Big Trip or Big Meeting is dropped, not played (player, 2026-10-06): no teleport while
        // players are still arriving, and none held back to land on them later. Every other
        // effect goes as usual.
        private const float JoinGrace = 20f;
        private static float _graceUntil;
        private static int _playersSeen;

        // The host's world is ready: the queue as the save has it, and the grace begins.
        internal static void OnWorldReady()
        {
            _graceUntil = Time.unscaledTime + JoinGrace;
            _playersSeen = PlayerCount();
            if (!ModConfig.ArchipelagoEnabled.Value)
                return;

            Pending.Clear();
            PendingSingle.Clear();
            foreach (var key in TrapEffects.Known)
            {
                var count = SaveManager.GetIntValue(PendingPrefix + key, 0, false);
                for (var i = 0; i < count && Pending.Count < MaxPending; i++)
                {
                    Pending.Enqueue(key);
                    PendingSingle.Enqueue(false);
                }
            }

            if (Pending.Count > 0)
                Plugin.Log.LogInfo($"{Tag} {Pending.Count} effect(s) still waiting from the last session.");
        }

        // A save bound to another seed: the effects that seed's items queued are not this one's.
        internal static void ClearLedger()
        {
            Pending.Clear();
            PendingSingle.Clear();
            foreach (var key in TrapEffects.Known)
                SaveManager.SetIntValue(PendingPrefix + key, 0);
        }

        private static int PlayerCount()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            return players != null ? players.Count : 0;
        }

        private static void CountPending(string key, int change)
        {
            if (!ModConfig.ArchipelagoEnabled.Value || !NetworkServer.active)
                return;

            var name = PendingPrefix + key;
            SaveManager.SetIntValue(name, Math.Max(0, SaveManager.GetIntValue(name, 0, false) + change));
        }

        internal static bool Enqueue(string key, string why, bool single = false)
        {
            if (Array.IndexOf(TrapEffects.Known, key) < 0)
            {
                Plugin.Log.LogWarning($"{Tag} '{key}' ({why}) is not an effect this build plays; ignored.");
                return false;
            }

            if (Pending.Count >= MaxPending)
            {
                Plugin.Log.LogInfo($"{Tag} {TrapEffects.DisplayName(key)} ({why}) let go: {MaxPending} already waiting.");
                return false;
            }

            Pending.Enqueue(key);
            PendingSingle.Enqueue(single);
            CountPending(key, +1);
            Plugin.Log.LogInfo($"{Tag} {TrapEffects.DisplayName(key)} queued ({why}); {Pending.Count} waiting.");
            return true;
        }

        internal static void OnDeathLinkReceived(string cause)
        {
            if (!ReceivesDeathLink)
                return;

            ApplyDeathEffect($"received ({cause})", $"DeathLink from {cause}");
        }

        // What a death does here (`death_link_effect`, on `death_link_target`): when a DeathLink
        // arrives, and, with `death_link_trap_on_send`, when this world sends one. Not a trap of the
        // pool: never sent on as a TrapLink.
        private static void ApplyDeathEffect(string journal, string why)
        {
            var single = _deathLinkOnePlayer;
            var key = _deathLinkEffect switch
            {
                "roulette" => Roulette(single ? SessionWide : null),
                "drop" => TrapEffects.Drop,
                _ => TrapEffects.Knockout,
            };
            SessionJournal.Write("deathlink", $"{journal} -> {TrapEffects.DisplayName(key)}");
            Enqueue(key, why + (single ? ", one player" : string.Empty), single);
        }

        // The traps that cannot hit one player: Big Meeting gathers everyone around one, and Big
        // Night's clock is the whole session's (player, 2026-10-09). Left out of the roulette of a
        // DeathLink that hits one player, and played on everyone should one come through anyway.
        private static readonly string[] SessionWide = { TrapEffects.Meeting, TrapEffects.Night };

        private static string Roulette(string[] except = null)
        {
            var total = 0;
            foreach (var pair in _roulette)
            {
                if (except == null || Array.IndexOf(except, pair.Key) < 0)
                    total += pair.Value;
            }
            if (total <= 0)
                return TrapEffects.Drop;

            var roll = UnityEngine.Random.Range(0, total);
            foreach (var pair in _roulette)
            {
                if (except != null && Array.IndexOf(except, pair.Key) >= 0)
                    continue;
                roll -= pair.Value;
                if (roll < 0)
                    return pair.Key;
            }

            return TrapEffects.Drop;
        }

        // ------------------------------------------------------------------
        // TrapLink (`trap_link`)
        // ------------------------------------------------------------------

        // The traps of the pool, as opposed to the bonuses and the DeathLink knock-out.
        private static readonly string[] PoolTraps =
        {
            TrapEffects.Drop, TrapEffects.Throw, TrapEffects.Trip, TrapEffects.Meeting, TrapEffects.Night,
            TrapEffects.Sleep, TrapEffects.Load, TrapEffects.Mask, TrapEffects.Flare,
        };

        // A trap of the pool that has just reached this world, for the other games. Only the
        // host talks to the server, and only items count: a trap a DeathLink or a TrapLink made
        // is not sent, so none loops.
        internal static void SendTrapLink(string key, string itemName)
        {
            if (!_trapLink || !NetworkServer.active || Array.IndexOf(PoolTraps, key) < 0)
                return;
            if (ApRuntime.SendTrapLink(itemName))
                Plugin.Log.LogInfo($"{Tag} TrapLink sent: {itemName}.");
        }

        // The words of other games' traps, by the Big Walk trap nearest in what it does: one that
        // is not among them, or whose Big Walk trap is not in the DeathLink roulette (a hard one
        // with `death_link_roulette_hard_traps` off), is drawn from the roulette.
        private static readonly (string key, string[] words)[] TrapLinkWords =
        {
            (TrapEffects.Sleep, new[] { "freeze", "ice", "paraly", "stun", "sleep", "slow", "stop" }),
            (TrapEffects.Drop, new[] { "drop", "fumble", "butterfinger", "disarm", "empty", "damage", "bonk" }),
            (TrapEffects.Throw, new[] { "throw", "launch", "spring", "bounce", "banana", "slip" }),
            (TrapEffects.Trip, new[] { "teleport", "warp", "trip", "tp", "reverse", "random" }),
            (TrapEffects.Night, new[] { "dark", "night", "fog", "blackout", "blind" }),
            (TrapEffects.Mask, new[] { "mask", "screen", "blur", "tiny", "zoom" }),
            (TrapEffects.Flare, new[] { "flare", "flash", "bomb", "explo", "fire", "light" }),
            (TrapEffects.Load, new[] { "strip", "unequip", "lose", "inventory", "rupee", "item" }),
        };

        internal static void OnTrapLinkReceived(string source, string trapName)
        {
            if (!_trapLink || !NetworkServer.active)
                return;

            var name = trapName.ToLowerInvariant();
            string key = null;
            foreach (var trap in PoolTraps)
            {
                if (name == "big " + trap)
                    key = trap;
            }

            if (key == null)
            {
                foreach (var (candidate, words) in TrapLinkWords)
                {
                    if (Array.Exists(words, word => name.Contains(word)))
                    {
                        key = candidate;
                        break;
                    }
                }
            }

            if (key == null || !_roulette.TryGetValue(key, out var weight) || weight <= 0)
                key = Roulette();

            SessionJournal.Write("traplink", $"received ({source}: {trapName}) -> {TrapEffects.DisplayName(key)}");
            Enqueue(key, $"TrapLink from {source}: {trapName}");
        }

        // Host, every frame. `settled`: the session's startup burst is through (ApRuntime).
        internal static void TickHost(bool settled)
        {
            if (TomatoStarts.Count > 0)
                StartDueTomatoes();
            if (Tomatoes.Count > 0)
                ClearTomatoes();
            if (Helmets.Count > 0)
                TickHelmets();
            if (RetiringHelmets.Count > 0)
                RetireHelmets();

            var players = PlayerCount();
            if (players > _playersSeen)
            {
                _graceUntil = Time.unscaledTime + JoinGrace;
                Plugin.Log.LogInfo($"{Tag} A player joined: any Big Trip or Big Meeting in the next {JoinGrace:0}s is dropped.");
            }

            _playersSeen = players;

            if (Pending.Count == 0 || !settled || !WorldManager.isReadyForEffects || Time.unscaledTime < _nextFire)
                return;

            if (Debug.DebugPlayerLookup.FindLocalPlayer() == null)
                return;

            var key = Pending.Dequeue();
            var single = PendingSingle.Count > 0 && PendingSingle.Dequeue();
            CountPending(key, -1);
            if (Time.unscaledTime < _graceUntil && (key == TrapEffects.Trip || key == TrapEffects.Meeting))
            {
                Plugin.Log.LogInfo($"{Tag} {TrapEffects.DisplayName(key)} dropped: players are still arriving.");
                return;
            }

            _nextFire = Time.unscaledTime + Spacing;
            if (single && Array.IndexOf(SessionWide, key) < 0)
            {
                _onlyPlayer = LeastHit();
                try
                {
                    Fire(key);
                }
                finally
                {
                    _onlyPlayer = null;
                }
            }
            else
            {
                Fire(key);
            }
        }

        // The player the fewest DeathLinks have hit this session, ties at random; counted as hit.
        private static PlayerCharacter LeastHit()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null || players.Count == 0)
                return null;

            PlayerCharacter chosen = null;
            var fewest = int.MaxValue;
            var ties = 0;
            foreach (var player in players)
            {
                if (player == null)
                    continue;
                var hits = DeathLinkHits.TryGetValue(player.gameObject.name, out var h) ? h : 0;
                if (hits < fewest)
                {
                    fewest = hits;
                    chosen = player;
                    ties = 1;
                }
                else if (hits == fewest && UnityEngine.Random.Range(0, ++ties) == 0)
                {
                    chosen = player;
                }
            }

            if (chosen != null)
            {
                DeathLinkHits[chosen.gameObject.name] = fewest + 1;
                Plugin.Log.LogInfo($"{Tag} This DeathLink hits {chosen.gameObject.name} only ({fewest + 1} so far).");
            }
            return chosen;
        }

        internal static void Fire(string key)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogInfo($"{Tag} Only the host fires an effect.");
                return;
            }

            SessionJournal.Write("trap", TrapEffects.DisplayName(key));
            switch (key)
            {
                case TrapEffects.Trip:
                    FireTrip(TrapEffects.Trip);
                    break;
                case TrapEffects.Meeting:
                    FireMeeting();
                    break;
                case TrapEffects.Load:
                    FireLoad();
                    break;
                case TrapEffects.Mask:
                    FireMask();
                    break;
                case TrapEffects.Flare:
                {
                    // Every machine readies its colours (or its gentle clearing), then the host's
                    // shots follow a moment later (Core/BigFlare).
                    var walkers = Walkers();
                    foreach (var walker in walkers)
                        Send(walker, key, null, 0f);
                    BigFlare.FireOn(walkers.ConvertAll(w => w.Player));
                    break;
                }
                case TrapEffects.Knockout:
                    // The packs off on the host, then each machine drops and dazes its player. The
                    // falls that follow are the mod's: none of them sends a DeathLink back.
                    _knockoutUntil = Time.unscaledTime + KnockoutQuiet;
                    FireLoad();
                    foreach (var walker in Walkers())
                        Send(walker, key, null, 0f);
                    break;
                default:
                    foreach (var walker in Walkers())
                        Send(walker, key, null, 0f);
                    if (Array.IndexOf(Lasting, key) >= 0 && _onlyPlayer == null)
                        ActiveUntil[key] = Time.unscaledTime + _duration;
                    break;
            }
        }

        // The effects that last, and until when, so that a player who joins while one runs gets it
        // too, for the time left (player, 2026-10-05). The others happen once and are over.
        private static readonly string[] Lasting = { TrapEffects.Speed, TrapEffects.Jump, TrapEffects.Sleep };
        private static readonly Dictionary<string, float> ActiveUntil = new();

        // From the ModChannel, when a guest says hello.
        internal static void OnGuestJoined(NetworkConnection connection)
        {
            foreach (var pair in ActiveUntil)
            {
                var left = Mathf.CeilToInt(pair.Value - Time.unscaledTime);
                if (left < 1)
                    continue;

                Plugin.Log.LogInfo($"{Tag} Guest #{connection.connectionId} joins during {TrapEffects.DisplayName(pair.Key)}: {left}s of it.");
                ModChannel.SendEffect(connection, pair.Key, left, null, 0f);
            }
        }

        // ------------------------------------------------------------------
        // Who is playing, and where
        // ------------------------------------------------------------------

        private sealed class Walker
        {
            internal PlayerCharacter Player;
            internal NetworkConnectionToClient Connection;
            internal bool Local;
            internal Vector3 Position;
            internal float Yaw;
        }

        // Every player of the session with the connection it plays through: the host's own is
        // connection 0, the local one. Read from the server's connections, which are what a
        // message can be sent down.
        private static List<Walker> Walkers()
        {
            var all = AllWalkers();
            if (_onlyPlayer != null)
                all.RemoveAll(walker => walker.Player != _onlyPlayer);
            return all;
        }

        private static List<Walker> AllWalkers()
        {
            var walkers = new List<Walker>();
            foreach (var connection in NetworkServer.connections.Values)
            {
                var identity = connection?.identity;
                var player = identity != null ? identity.GetComponent<PlayerCharacter>() : null;
                if (player == null)
                    continue;

                walkers.Add(new Walker
                {
                    Player = player,
                    Connection = connection,
                    Local = connection.connectionId == NetworkConnection.LocalConnectionId,
                    Position = player.transform.position,
                    Yaw = player.transform.eulerAngles.y,
                });
            }

            return walkers;
        }

        // Big Trip and Big Meeting: a timed tomato for each player about to be moved, set going at
        // once; it pops when they go (player, 2026-10-06). Taken back out of the world a moment
        // after it pops.
        // At the pop the tomato leaves the hands; it leaves the world this much later, once its
        // pop has been heard (the sound is on the tomato, and dies with it).
        private const float TomatoLinger = 0.1f;
        private const float TomatoSoundSeconds = 1.5f;
        private static readonly List<KeyValuePair<Prop, float>> Tomatoes = new();
        private const float TomatoStartDelay = 0.3f;
        private static readonly List<(Prop tomato, PlayerCharacter player, float at)> TomatoStarts = new();

        private static void StartDueTomatoes()
        {
            for (var i = TomatoStarts.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < TomatoStarts[i].at)
                    continue;

                var start = TomatoStarts[i];
                TomatoStarts.RemoveAt(i);
                if (start.tomato != null && start.tomato.gameObject != null)
                    StartTomato(start.tomato, start.player);
            }
        }

        private static void GiveTomato(Walker walker)
        {
            var tomato = GadgetItemSpawner.SpawnFor(GadgetKind.Pomodoro, walker.Player);
            if (tomato == null)
                return;

            // Set going once it is in the hands, as a player's own tomato is (its tick is a sound
            // on the tomato, which the hand-over a frame after the spawn would otherwise cut).
            var startAt = Time.unscaledTime + TomatoStartDelay;
            TomatoStarts.Add((tomato, walker.Player, startAt));
            Tomatoes.Add(new KeyValuePair<Prop, float>(tomato, startAt + GadgetItemSpawner.TomatoSeconds + TomatoLinger));
        }

        // The tomato's own switch, as a player setting it going: its state goes up on the host and
        // reaches every machine, whose timer then runs. With the tomato as the context's prop, as
        // when a player picks one up: its tick and pop play at "the context's prop"
        // (PeckTransformReference.ContextProp), and with an empty context they had nowhere to play
        // (compared with a real tomato's verbose log, 2026-10-06).
        private static void StartTomato(Prop tomato, PlayerCharacter player)
        {
            foreach (var peckSwitch in tomato.GetComponentsInChildren<PeckSwitch>(true))
            {
                if (peckSwitch == null || peckSwitch.gameObject.name != "OnSetSwitch")
                    continue;

                var context = new PeckContext(0)
                {
                    propIdentity = tomato.GetComponent<NetworkIdentity>(),
                    playerIdentity = player != null ? player.netIdentity : null,
                };
                peckSwitch.Peck(context);
                Plugin.Log.LogInfo($"{Tag} Tomato set going for {GadgetItemSpawner.TomatoSeconds:0}s.");
                return;
            }

            Plugin.Log.LogWarning($"{Tag} The tomato has no OnSetSwitch; it will not pop.");
        }

        // Host, every frame: the tomatoes that have popped, out of hands and out of the world.
        private static readonly HashSet<int> Released = new();

        private static void ClearTomatoes()
        {
            for (var i = Tomatoes.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < Tomatoes[i].Value)
                    continue;

                var tomato = Tomatoes[i].Key;
                Tomatoes.RemoveAt(i);
                try
                {
                    if (tomato == null || tomato.gameObject == null)
                        continue;

                    // First time due: out of the hands, back in the list for when its pop is over.
                    if (!Released.Contains(tomato.GetInstanceID()))
                    {
                        Released.Add(tomato.GetInstanceID());
                        ReceivedItemSpawner.ReleaseFromHands(tomato);
                        Tomatoes.Add(new KeyValuePair<Prop, float>(tomato, Time.unscaledTime + TomatoSoundSeconds));
                        continue;
                    }

                    Released.Remove(tomato.GetInstanceID());
                    NetworkServer.Destroy(tomato.gameObject);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not take a tomato away: {ex.Message}");
                }
            }
        }

        private static void Send(Walker walker, string key, Vector3? destination, float yaw)
        {
            if (destination != null && (key == TrapEffects.Trip || key == TrapEffects.Meeting))
                GiveTomato(walker);

            if (walker.Local)
                TrapEffects.Play(key, _duration, destination, yaw);
            else
                ModChannel.SendEffect(walker.Connection, key, _duration, destination, yaw);
        }

        // Guests say whether a puzzle's mask is on their screen (ModChannel, KindStatus).
        private static readonly Dictionary<int, bool> GuestMasked = new();

        internal static void NoteGuestMasked(int connectionId, bool masked)
        {
            if (GuestMasked.TryGetValue(connectionId, out var before) && before == masked)
                return;

            GuestMasked[connectionId] = masked;
            Plugin.Log.LogInfo($"{Tag} Guest #{connectionId}: screen mask {(masked ? "on" : "off")}.");
        }

        private static bool IsMasked(Walker walker)
        {
            return walker.Local
                ? TrapEffects.LocalMasked
                : GuestMasked.TryGetValue(walker.Connection.connectionId, out var masked) && masked;
        }

        // Whether Big Trip and Big Meeting leave this player where they are.
        private static bool IsSpared(Walker walker, out string why)
        {
            why = null;
            if (IsMasked(walker))
                why = "a puzzle's mask is on their screen";
            else if (_spareGauntlet && Gauntlet.Contains(walker.Position))
                why = "in the Silent Gauntlet";
            return why != null;
        }

        // ------------------------------------------------------------------
        // Big Trip and Big Meeting
        // ------------------------------------------------------------------

        private static void FireTrip(string key)
        {
            var walkers = Walkers();
            var spots = TripSpots.Candidates();
            if (spots.Count == 0)
            {
                Plugin.Log.LogInfo($"{Tag} Big Trip: no place to send anyone yet.");
                return;
            }

            // A different place for each player, none of them where that player already is.
            var shuffled = spots.OrderBy(_ => UnityEngine.Random.value).ToList();
            var used = new HashSet<int>();
            foreach (var walker in walkers)
            {
                if (IsSpared(walker, out var why))
                {
                    Plugin.Log.LogInfo($"{Tag} Big Trip spares {walker.Player.gameObject.name}: {why}.");
                    continue;
                }

                var pick = -1;
                for (var i = 0; i < shuffled.Count && pick < 0; i++)
                {
                    if (!used.Contains(i) && Vector3.Distance(shuffled[i].Point, walker.Position) > 40f)
                        pick = i;
                }

                for (var i = 0; i < shuffled.Count && pick < 0; i++)
                {
                    if (!used.Contains(i))
                        pick = i;
                }

                if (pick < 0)
                    pick = UnityEngine.Random.Range(0, shuffled.Count);

                used.Add(pick);
                Plugin.Log.LogInfo($"{Tag} Big Trip sends {walker.Player.gameObject.name} to {shuffled[pick].Label}.");
                Send(walker, key, shuffled[pick].Point, walker.Yaw);
            }
        }

        private static void FireMeeting()
        {
            var walkers = Walkers();
            var free = walkers.Where(w => !IsSpared(w, out _)).ToList();
            if (walkers.Count < 2 || free.Count < 2)
            {
                Plugin.Log.LogInfo($"{Tag} Big Meeting: {walkers.Count} player(s), {free.Count} free to move; a Big Trip instead.");
                FireTrip(TrapEffects.Trip);
                return;
            }

            var target = free[UnityEngine.Random.Range(0, free.Count)];
            var movers = free.Where(w => w != target).ToList();
            Plugin.Log.LogInfo($"{Tag} Big Meeting around {target.Player.gameObject.name}, {movers.Count} player(s) gathered.");

            var taken = new List<Vector3>();
            for (var i = 0; i < movers.Count; i++)
            {
                var angle = target.Yaw + 360f * (i + 1) / (movers.Count + 1);

                // Nowhere to stand beside them: on them, which the game sorts out by pushing apart.
                var landing = SafeLanding(target.Position, MeetingRadius, angle, 2f, taken)
                              ?? target.Position + Vector3.up * 0.8f;

                // Facing the player they were gathered around.
                var look = target.Position - landing;
                var yaw = new Vector2(look.x, look.z).sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(new Vector3(look.x, 0f, look.z)).eulerAngles.y
                    : target.Yaw;
                Send(movers[i], TrapEffects.Meeting, landing, yaw);
            }
        }

        // A place a player can stand beside `center` (player, 2026-10-05: never in a wall): seen
        // from the centre with nothing in between, its floor within `maxRise` of the centre's
        // height, room for a standing player, and not where another was just put. Twelve
        // directions from `angle`, at the distance then further and nearer. Null if none.
        internal static Vector3? SafeLanding(Vector3 center, float radius, float angle, float maxRise, List<Vector3> taken)
        {
            var eye = center + Vector3.up * 1.2f;
            foreach (var distance in new[] { radius, radius * 1.6f, radius * 0.6f })
            {
                for (var i = 0; i < 12; i++)
                {
                    var point = center + Quaternion.Euler(0f, angle + 30f * i, 0f) * Vector3.forward * distance;
                    if (Physics.Linecast(eye, point + Vector3.up * 1.2f, out _, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    if (!Physics.Raycast(point + Vector3.up * 2.5f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    if (Mathf.Abs(hit.point.y - center.y) > maxRise)
                        continue;

                    var feet = hit.point;
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    if (taken != null && taken.Exists(other => Vector3.Distance(other, feet) < 1f))
                        continue;

                    taken?.Add(feet);
                    return feet + Vector3.up * 0.8f;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Big Load
        // ------------------------------------------------------------------

        private static readonly string[] PackNames = { "BackpackProp", "HolsterProp", "GourdCartonProp" };

        // Every pack a player wears (a backpack, a belt, a gourd carton pinned in one of the
        // player's own homes) comes off and falls behind them, and whatever it holds falls out
        // of it first (player, 2026-10-05). Host only, through the pins it owns, as the key
        // custody frees a key from its stone (Prop.ServerSetUnpinned).
        // ------------------------------------------------------------------
        // Big Mask
        // ------------------------------------------------------------------

        // The blindfold puzzles' helmet (BlindfoldProp) pinned into each player's head home
        // (`blindfoldPropHome`, on the head bone), as a player puts it on another: its own
        // PeckEffectMask darkens the wearer's screen and the others see the helmet (measured
        // 2026-10-07). A clone, locked to the head on every machine (blockRemovingFromHomes,
        // GadgetItemSpawner), taken off and destroyed when the trap ends. A player whose screen
        // already has a puzzle's mask, or whose head home holds something, is spared (player).
        private static readonly List<(Prop helmet, PropHome home, float until)> Helmets = new();

        // A helmet taken off its head waits here before it leaves the world: the screens that wear
        // it need to see it come off to lift its mask, and a guest's stayed blurred when it was
        // destroyed in the same frame (player, 2026-10-09).
        private const float HelmetLinger = 1.5f;
        private static readonly List<(Prop helmet, float at)> RetiringHelmets = new();

        private static void FireMask()
        {
            foreach (var walker in Walkers())
            {
                if (IsMasked(walker))
                {
                    Plugin.Log.LogInfo($"{Tag} Big Mask spares {walker.Player.gameObject.name}: a mask is already on their screen.");
                    continue;
                }

                PropHome home = null;
                foreach (var candidate in walker.Player.GetComponentsInChildren<PropHome>(true))
                {
                    if (candidate != null && candidate.gameObject.name == "blindfoldPropHome")
                    {
                        home = candidate;
                        break;
                    }
                }

                if (home == null || home.pinnedProp != null)
                {
                    Plugin.Log.LogInfo($"{Tag} Big Mask spares {walker.Player.gameObject.name}: {(home == null ? "no head home" : "something is on their head")}.");
                    continue;
                }

                var helmet = GadgetItemSpawner.SpawnFor(GadgetKind.Blindfold, walker.Player, toHands: false);
                if (helmet == null)
                    continue;

                try
                {
                    helmet.ServerSetPinned(home);
                    Helmets.Add((helmet, home, Time.unscaledTime + _duration));
                    Plugin.Log.LogInfo($"{Tag} Big Mask on {walker.Player.gameObject.name} for {_duration}s.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not put the helmet on {walker.Player.gameObject.name}: {ex.Message}");
                    NetworkServer.Destroy(helmet.gameObject);
                }
            }
        }

        // Host, every frame: a helmet knocked off its head goes back on; one whose time is up comes
        // off and leaves the world.
        private static void TickHelmets()
        {
            for (var i = Helmets.Count - 1; i >= 0; i--)
            {
                var (helmet, home, until) = Helmets[i];
                try
                {
                    if (helmet == null || helmet.gameObject == null || home == null)
                    {
                        Helmets.RemoveAt(i);
                        continue;
                    }

                    if (Time.unscaledTime >= until)
                    {
                        Helmets.RemoveAt(i);
                        helmet.blockRemovingFromHomes = false;
                        helmet.ServerSetUnpinned();
                        RetiringHelmets.Add((helmet, Time.unscaledTime + HelmetLinger));
                        Plugin.Log.LogInfo($"{Tag} Big Mask over for one player.");
                        continue;
                    }

                    if (helmet.currentHome != home)
                        helmet.ServerSetPinned(home);
                }
                catch (Exception ex)
                {
                    Helmets.RemoveAt(i);
                    Plugin.Log.LogWarning($"{Tag} A Big Mask helmet went wrong: {ex.Message}");
                }
            }
        }

        private static void RetireHelmets()
        {
            for (var i = RetiringHelmets.Count - 1; i >= 0; i--)
            {
                var (helmet, at) = RetiringHelmets[i];
                if (Time.unscaledTime < at)
                    continue;
                RetiringHelmets.RemoveAt(i);
                try
                {
                    if (helmet != null && helmet.gameObject != null)
                        NetworkServer.Destroy(helmet.gameObject);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not take a Big Mask helmet out of the world: {ex.Message}");
                }
            }
        }

        private static void FireLoad()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return;

            List<Prop> contents = null;
            foreach (var player in players)
            {
                if (player == null || _onlyPlayer != null && player != _onlyPlayer)
                    continue;

                var behind = player.transform.position - player.transform.forward * 0.8f + Vector3.up * 0.6f;
                foreach (var home in player.GetComponentsInChildren<PropHome>(true))
                {
                    var pack = home != null ? home.pinnedProp : null;
                    if (pack == null || !Array.Exists(PackNames, n => pack.gameObject.name.StartsWith(n, StringComparison.Ordinal)))
                        continue;

                    // What the pack holds: every prop kept in a home of that pack. The home's own
                    // parentProp names the pack; its place in the hierarchy does not, since a worn
                    // pack's Kernal is moved under the player's bones (measured 2026-10-06).
                    var spilled = 0;
                    foreach (var content in contents ??= AllProps())
                    {
                        var at = content != null ? content.currentHome : null;
                        if (at == null || content == pack || at.parentProp != pack)
                            continue;
                        if (Unpin(content, behind + UnityEngine.Random.insideUnitSphere * 0.4f))
                            spilled++;
                    }

                    if (Unpin(pack, behind))
                        Plugin.Log.LogInfo($"{Tag} Big Load: {player.gameObject.name} lost {pack.gameObject.name}, {spilled} thing(s) spilled.");
                }
            }
        }

        private static List<Prop> AllProps()
        {
            return new List<Prop>(UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        private static bool Unpin(Prop prop, Vector3 at)
        {
            try
            {
                prop.ServerSetUnpinned();
                prop.SetLoose();
                prop.transform.position = at;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Load could not take {prop.gameObject.name} off: {ex.Message}");
                return false;
            }
        }

        // The floor under a point, looked for from a little above it.
        internal static Vector3 Ground(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 2.5f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.8f;
            return point + Vector3.up * 0.5f;
        }

        // ------------------------------------------------------------------
        // DeathLink out
        // ------------------------------------------------------------------

        // A failed puzzle calls Trigger (Core/PuzzleFailures). A big fall and falling asleep were
        // built and dropped (player, 2026-10-05).
        private static int _missed;
        private static float _lastSent = -100f;

        // A fall that knocks a player down (`big_fall`, players 2026-10-07; built first on
        // 2026-10-05, 9d2ec1e). The game runs PlayerFaller.TriggerFall on the machine of the player
        // who fell and ProcessRemoteFall on the others (decompiled 2026-10-05), so the host sees its
        // own falls through the first and its guests' through the second (Patches/TrapPatches).
        // A knock-out the mod gave is not a fall: none counts for a few seconds after one.
        private const float FallCooldown = 5f;
        private const float KnockoutQuiet = 8f;
        private static readonly Dictionary<int, float> LastFall = new();
        private static float _knockoutUntil = -100f;

        internal static void OnFall(PlayerCharacter player)
        {
            if (!NetworkServer.active || player == null || Time.unscaledTime < _knockoutUntil)
                return;

            var id = player.GetInstanceID();
            if (LastFall.TryGetValue(id, out var at) && Time.unscaledTime - at < FallCooldown)
                return;

            LastFall[id] = Time.unscaledTime;
            Plugin.Log.LogInfo($"{Tag} {player.gameObject.name} took a big fall.");
            Trigger("big_fall", "took a big fall");
        }

        internal static void Trigger(string trigger, string what)
        {
            if (!_sendsDeathLink || !DeathLinkOn || !_triggers.Contains(trigger))
                return;

            if (_missed < Amnesty)
            {
                _missed++;
                Plugin.Log.LogInfo($"{Tag} DeathLink forgiven ({trigger}): {_missed}/{Amnesty} of the amnesty.");
                return;
            }

            if (Time.unscaledTime - _lastSent < DeathLinkCooldown)
                return;

            _missed = 0;
            _lastSent = Time.unscaledTime;
            ApRuntime.SendDeathLink(what);

            // `death_link_trap_on_send`: the players who sent it take the effect of a death too.
            if (_trapOnSend)
                ApplyDeathEffect("sent", "own DeathLink sent");
        }

        internal static void ForgetWorld()
        {
            Gauntlet.Forget();
            PuzzleFailures.ForgetWorld();
            Tomatoes.Clear();
            Helmets.Clear();
            RetiringHelmets.Clear();
            TomatoStarts.Clear();
            Released.Clear();
            ActiveUntil.Clear();
        }

        internal static void ForgetGuests()
        {
            GuestMasked.Clear();
        }

        internal static string Describe()
        {
            var lines = new List<string>
            {
                $"{Pending.Count} waiting, duration {_duration}s, spare gauntlet {_spareGauntlet}, "
                + $"deathlink on={DeathLinkOn} send={_sendsDeathLink} [{string.Join(",", _triggers)}] {_missed}/{Amnesty} receive={_receivesDeathLink} ({_deathLinkEffect})",
            };

            if (NetworkServer.active)
            {
                foreach (var walker in Walkers())
                {
                    IsSpared(walker, out var why);
                    lines.Add($"  {walker.Player.gameObject.name}{(walker.Local ? " (host)" : string.Empty)} at {walker.Position:F0}"
                              + $", in gauntlet={Gauntlet.Contains(walker.Position)}, masked={IsMasked(walker)}"
                              + (why != null ? $" -> spared: {why}" : string.Empty));
                }

                foreach (var spot in TripSpots.Candidates())
                    lines.Add($"  trip spot: {spot.Label} at {spot.Point:F1}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    // The Silent Gauntlet's extent, from what its stages are built of: the box around every
    // renderer under the `SilentGauntlet <n>Player` object, found once per world.
    internal static class Gauntlet
    {
        private const string RootPrefix = "SilentGauntlet";
        private const float Margin = 6f;

        private static Bounds? _bounds;
        private static bool _looked;

        internal static bool Contains(Vector3 point)
        {
            if (!_looked)
                Find();
            return _bounds != null && _bounds.Value.Contains(point);
        }

        internal static void Forget()
        {
            _bounds = null;
            _looked = false;
        }

        private static void Find()
        {
            if (!WorldManager.isReadyForEffects)
                return;

            _looked = true;
            try
            {
                Transform root = null;
                foreach (var state in UnityEngine.Object.FindObjectsByType<TrackedPeckState>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    for (var t = state != null ? state.transform : null; t != null; t = t.parent)
                    {
                        if (t.name.StartsWith(RootPrefix, StringComparison.Ordinal))
                        {
                            root = t;
                            break;
                        }
                    }

                    if (root != null)
                        break;
                }

                if (root == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(Gauntlet)}] No '{RootPrefix}' object in this world.");
                    return;
                }

                Bounds? bounds = null;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                        continue;
                    if (bounds == null)
                        bounds = renderer.bounds;
                    else
                    {
                        var b = bounds.Value;
                        b.Encapsulate(renderer.bounds);
                        bounds = b;
                    }
                }

                if (bounds != null)
                {
                    var b = bounds.Value;
                    b.Expand(Margin * 2f);
                    _bounds = b;
                }

                Plugin.Log.LogInfo($"[{nameof(Gauntlet)}] '{root.name}': {(_bounds != null ? $"from {_bounds.Value.min:F0} to {_bounds.Value.max:F0}" : "no renderer")}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(Gauntlet)}] Could not measure the Gauntlet: {ex.Message}");
            }
        }
    }

    // Where Big Trip may send a player: only places the players have already reached, so the
    // trip never skips a lock the logic counts on (a place reached is a place unlocked): the hub,
    // the towers whose door has been opened (the teleport buttons' own record), the radio stations
    // switched on, the puzzles whose check is made (beside their gourd's home, never a puzzle that
    // puts a mask on the screen), the firework launchers fired and the packs picked up (player,
    // 2026-10-05). Never the Gauntlet nor the chapel.
    internal static class TripSpots
    {
        internal struct Spot
        {
            internal string Label;
            internal Vector3 Point;
        }

        // How far from a radio station (a launcher, a puzzle's gourd) a player lands, looked for
        // around it.
        private const float StationDistance = 3.5f;

        // A puzzle with a screen mask this close to its gourd's home is a mask puzzle.
        private const float MaskPuzzleRadius = 60f;

        internal static List<Spot> Candidates()
        {
            var spots = new List<Spot>();
            try
            {
                spots.AddRange(TeleportButtons.TripLandings()
                    .Select(landing => new Spot { Label = landing.Key, Point = landing.Value }));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TripSpots)}] Tower landings: {ex.Message}");
            }

            try
            {
                foreach (var station in UnityEngine.Object.FindObjectsByType<BroadcastStation>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!RadioStations.TryGetSystem(station, out var system))
                        continue;
                    if (SaveManager.GetIntValue(system.ToString(), 0, false) == 0)
                        continue;

                    var landing = AroundStation(station.transform.position);
                    if (landing != null)
                        spots.Add(new Spot { Label = $"radio {system}", Point = landing.Value });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TripSpots)}] Radio stations: {ex.Message}");
            }

            try
            {
                AddPuzzles(spots);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TripSpots)}] Puzzles: {ex.Message}");
            }

            try
            {
                foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (transform == null || !transform.name.StartsWith("FireworkLauncher", StringComparison.Ordinal))
                        continue;
                    var key = PackChecks.LandmarkOf(transform);
                    if (key == null || !PackChecks.LauncherFired(key))
                        continue;
                    var landing = AroundStation(transform.position);
                    if (landing != null)
                        spots.Add(new Spot { Label = $"fireworks {key}", Point = landing.Value });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TripSpots)}] Fireworks: {ex.Message}");
            }

            try
            {
                foreach (var pair in GadgetItemSpawner.VanillaPacksByGuid())
                {
                    if (!CheckTracker.IsReported(ApLocationIds.PickupPrefix + pair.Key))
                        continue;
                    var landing = AroundStation(pair.Value.transform.position);
                    if (landing != null)
                        spots.Add(new Spot { Label = $"pack {pair.Value.gameObject.name} {pair.Key}", Point = landing.Value });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TripSpots)}] Packs: {ex.Message}");
            }

            spots.RemoveAll(spot => Gauntlet.Contains(spot.Point));
            return spots;
        }

        // Every puzzle whose check this save has made, beside its gourd's home (where the gourd
        // sits once solved), but those with a screen mask near: the player could land in the
        // puzzle, and a mask puzzle is one Big Trip must not drop anyone into.
        private static void AddPuzzles(List<Spot> spots)
        {
            var masks = new List<Vector3>();
            foreach (var mask in UnityEngine.Object.FindObjectsByType<PeckEffectMask>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mask != null)
                    masks.Add(mask.transform.position);
            }

            foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out var location)
                    || !CheckTracker.IsReported(location))
                    continue;

                var home = GourdRegistry.TryGetHomeFor(prop.saveablePropName);
                if (home == null)
                    continue;

                var at = home.transform.position;
                if (masks.Exists(mask => Vector3.Distance(mask, at) < MaskPuzzleRadius))
                    continue;

                var landing = AroundStation(at);
                if (landing != null)
                    spots.Add(new Spot { Label = $"puzzle {prop.saveablePropName}", Point = landing.Value });
            }
        }

        // A patch of floor beside the station (the launcher, the gourd's home), near its own
        // height, in the open and with room to stand (Traps.SafeLanding).
        private static Vector3? AroundStation(Vector3 station)
        {
            return Traps.SafeLanding(station, StationDistance, 0f, 2.5f, null);
        }
    }

    // Plays the effects' clocks on every machine, fires the host's queue, watches for DeathLink
    // triggers, and has guests tell the host whether a puzzle's mask is on their screen.
    internal sealed class TrapRunner : MonoBehaviour
    {
        public TrapRunner(IntPtr ptr) : base(ptr)
        {
        }

        private static bool _worldWasReady;
        private static float _nextStatus;
        private static bool _lastMaskedSent;

        private void Update()
        {
            try
            {
                var ready = WorldManager.isReadyForEffects;
                if (!ready && _worldWasReady)
                {
                    TrapEffects.EndAll();
                    Traps.ForgetWorld();
                }

                if (ready && !_worldWasReady && NetworkServer.active)
                    Traps.OnWorldReady();

                _worldWasReady = ready;
                TrapEffects.Tick();
                PackChecks.Tick();
                ColourPainter.Tick();
                BigFlare.Tick();
                Patches.SettingsMenuArchipelagoRows.KeepPlaced();

                if (NetworkServer.active)
                {
                    Traps.TickHost(ApRuntime.SessionSettled);
                }
                else
                {
                    Traps.ForgetGuests();
                    if (NetworkClient.active)
                        TellHostAboutMask();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TrapRunner)}] {ex.Message}");
            }
        }

        // On a change, and again every few seconds in case one was missed.
        private static void TellHostAboutMask()
        {
            var masked = TrapEffects.LocalMasked;
            if (masked == _lastMaskedSent && Time.unscaledTime < _nextStatus)
                return;

            if (ModChannel.SendStatus(masked))
            {
                _lastMaskedSent = masked;
                _nextStatus = Time.unscaledTime + 5f;
            }
        }
    }
}
