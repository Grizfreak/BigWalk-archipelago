using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // What each player holds, in the session journal (`hands` lines), on every machine.
    //
    // Written for a gourd the host could not pick up after a restart (players, 2026-10-15): the
    // received gourd had been handed to a guest before the host quit, and nothing said whether it
    // was still held at that moment, nor by whom. So:
    //   - a line whenever a player takes or lets go of something, read every half second;
    //   - at `world gone`, what each player held last (from the cache: the props may be gone);
    //   - at `world ready`, what each player holds, and on the host the mod's gourds in the world,
    //     each with its netId, its place and whether someone holds it.
    // Held is read both ways the game knows it, as CosmeticGourdSpawnHandler.FindHolderName does:
    // the hands (this machine's view) and playerHeldInformation (the synced one); a line says when
    // the two disagree. Log only: it changes nothing in the game.
    internal static class HandsJournal
    {
        private const float Period = 0.5f;

        private static float _next;

        // Per player (its netId, or its name before it has one): the last description written.
        private static readonly Dictionary<string, string> _last = new();
        private static readonly Dictionary<string, string> _names = new();

        // Every frame, from PuzzleNeedHider.Update while a world is ready.
        internal static void Tick()
        {
            if (Time.unscaledTime < _next)
                return;

            _next = Time.unscaledTime + Period;
            try
            {
                foreach (var pc in Players())
                {
                    var who = Id(pc);
                    var now = Holding(pc);
                    _last.TryGetValue(who, out var before);
                    _names[who] = pc.gameObject.name;
                    if (now == before)
                        continue;

                    _last[who] = now;
                    if (before == null && now == Nothing)
                        continue;

                    SessionJournal.Write("hands", now == Nothing
                        ? $"{pc.gameObject.name} lets go of {before}"
                        : $"{pc.gameObject.name} takes {now}");
                }
            }
            catch (Exception)
            {
            }
        }

        internal static void WorldGone()
        {
            try
            {
                foreach (var pair in _last)
                {
                    if (pair.Value != Nothing)
                        SessionJournal.Write("hands", $"{Name(pair.Key)} held {pair.Value} when the world went", withPosition: false);
                }
            }
            catch (Exception)
            {
            }

            _last.Clear();
            _names.Clear();
        }

        internal static void WorldReady()
        {
            try
            {
                foreach (var pc in Players())
                {
                    var now = Holding(pc);
                    _last[Id(pc)] = now;
                    _names[Id(pc)] = pc.gameObject.name;
                    SessionJournal.Write("hands", $"{pc.gameObject.name} holds {now} at world ready");
                }

                if (!NetworkServer.active)
                    return;

                var count = 0;
                foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (gourd == null || !ReceivedItemSpawner.IsCosmeticClone(gourd.prop))
                        continue;

                    count++;
                    var prop = gourd.prop;
                    var at = prop.transform.position;
                    var holder = CosmeticGourdSpawnHandler.FindHolderName(prop) ?? "nobody";
                    var home = prop.currentHome != null ? prop.currentHome.name : "no home";
                    SessionJournal.Write("hands", string.Format(CultureInfo.InvariantCulture,
                        "AP gourd netId {0} at ({1:F1}, {2:F1}, {3:F1}), {4}, {5}, held by {6}",
                        NetIdOf(prop), at.x, at.y, at.z, prop.gameObject.activeInHierarchy ? "active" : "inactive", home, holder),
                        withPosition: false);
                }

                SessionJournal.Write("hands", $"{count} AP gourd(s) in the world at world ready", withPosition: false);
            }
            catch (Exception)
            {
            }
        }

        private const string Nothing = "nothing";

        private static IEnumerable<PlayerCharacter> Players()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                yield break;

            foreach (var pc in players)
            {
                if (pc != null && pc.gameObject != null)
                    yield return pc;
            }
        }

        private static string Id(PlayerCharacter pc)
        {
            var identity = pc.GetComponent<NetworkIdentity>();
            return identity != null && identity.netId != 0 ? identity.netId.ToString(CultureInfo.InvariantCulture) : pc.gameObject.name;
        }

        private static string Name(string id) => _names.TryGetValue(id, out var name) ? name : id;

        // "<name> netId <n> <saveablePropName> (AP cosmetic)", or "nothing"; both readings when they disagree.
        private static string Holding(PlayerCharacter pc)
        {
            var inHands = pc.hands != null ? pc.hands.heldProp : null;

            NetworkIdentity synced = null;
            try
            {
                if (pc.playerNetworking != null)
                    synced = pc.playerNetworking.playerHeldInformation.identity;
            }
            catch (Exception)
            {
            }

            var hands = inHands != null ? Describe(inHands) : Nothing;
            var sync = synced != null ? Describe(synced.GetComponent<Prop>(), synced) : Nothing;
            return hands == sync ? hands : $"{hands} (hands) / {sync} (synced)";
        }

        private static string Describe(Prop prop, NetworkIdentity identity = null)
        {
            if (prop == null)
                return identity != null ? $"netId {identity.netId} (no prop)" : Nothing;

            var ours = ReceivedItemSpawner.IsCosmeticClone(prop) || prop.gameObject.name.Contains("(AP", StringComparison.Ordinal)
                ? " (AP cosmetic)"
                : string.Empty;
            return $"{prop.gameObject.name} netId {NetIdOf(prop)} {prop.saveablePropName}{ours}";
        }

        private static uint NetIdOf(Prop prop)
        {
            var identity = prop != null ? prop.GetComponent<NetworkIdentity>() : null;
            return identity != null ? identity.netId : 0;
        }
    }
}
