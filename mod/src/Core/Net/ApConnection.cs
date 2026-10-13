using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;

namespace BigWalkArchipelago.Core.Net
{
    // The only file in the mod that touches the Archipelago client library.
    //
    // THREADING, and it is not a detail here: this library raises its events
    // on its own network threads, while everything the mod does to the game
    // (SaveManager, Prop, NetworkServer, spawning a gourd) is IL2CPP and must
    // happen on Unity's main thread — calling into IL2CPP from a thread the
    // runtime never attached is a native crash, not a catchable exception.
    // So this class is deliberately inert: its callbacks only ever touch
    // managed types and concurrent queues, and ApRuntime drains them from
    // Update(). Nothing here may ever reach for a game object, however
    // tempting it looks.
    //
    // TryConnectAndLogin is also blocking, which is why connecting runs on a
    // Task and the caller polls Status instead of waiting: doing it inline
    // would freeze the game for the length of the handshake, or for the whole
    // timeout when the address is wrong.
    internal sealed class ApConnection
    {
        internal enum ConnectionStatus
        {
            Idle,
            Connecting,
            Connected,
            Failed,
        }

        // Matches required_client_version in the apworld's world.py.
        private static readonly Version ClientVersion = new(0, 6, 0);

        // Recreated per connection attempt, and the handler below captures
        // the instance it was registered with rather than reading this field.
        // That is what keeps a dying session from dropping a stale item into
        // the queue the new session is filling — which would shift the whole
        // received-items count by one and make ApItemCursor skip a real item
        // forever.
        private volatile ConcurrentQueue<ItemInfo> _incomingItems = new();

        // R7: how many of each item this slot has received, by name, for the logic of the next-check board
        // (Core/ApLogic). Counted on the socket's thread as items come in, under its own lock; a new one per connection,
        // like the queue.
        private volatile Dictionary<string, int> _receivedByName = new();

        // R7: the hints the server knows that concern this slot, as it last sent them (the hint board).
        private volatile Hint[] _hints = Array.Empty<Hint>();

        // R7: the locations of this slot that hold a progression item, for anyone (scouted once at login, no hint
        // made): the next-check board puts them first.
        private volatile HashSet<long> _progressionHere = new();

        private ArchipelagoSession _session;

        // The DeathLink service of the current session, made with it, and what it has received:
        // "source: cause", for ApRuntime to drain on the main thread (threading note above).
        private DeathLinkService _deathLink;
        private readonly ConcurrentQueue<string> _deathLinksReceived = new();
        private volatile ConnectionStatus _status = ConnectionStatus.Idle;

        // Bumped for every attempt, so a stale one can be fenced off.
        private volatile int _generation;

        // Written by the connect task, read by the main thread once Status
        // has flipped to Connected/Failed — that flip is the handoff, so no
        // extra locking is needed around them.
        private HashSet<long> _slotLocations = new();

        // How many monument slots this slot has filled, as the server last
        // knew it (DataStorage, key below, per slot). Read once on the connect
        // task, with a short wait: 0 when the key is absent, the read fails or
        // times out, which is exactly the behaviour without this feature, a new
        // save re-depositing by hand. Never read on the main thread.
        internal int StoredDeposits { get; private set; }

        private const string DepositsKey = "bigwalk_deposits";
        private const int StorageWaitMilliseconds = 4000;

        // Asks the socket itself rather than trusting an event to have told
        // us. Proven necessary in-game (2026-09-15): killing the server
        // mid-session raised ErrorReceived and never SocketClosed, so the
        // status stayed Connected, nothing retried, and the mod happily
        // considered a dead connection alive — no warning, no reconnection,
        // checks quietly going nowhere. Which event a WebSocket stack
        // chooses to raise on an abrupt drop is not something to depend on;
        // `Connected` is the answer to the actual question.
        internal ConnectionStatus Status
        {
            get
            {
                if (_status == ConnectionStatus.Connected && !IsSocketAlive())
                {
                    LastError = string.IsNullOrEmpty(LastError) ? "connection dropped" : LastError;
                    _status = ConnectionStatus.Failed;
                }

                return _status;
            }
        }

        private bool IsSocketAlive()
        {
            try
            {
                return _session?.Socket != null && _session.Socket.Connected;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }
        // NULL until a login has succeeded. Everything downstream of a
        // connection can read it freely; anything reached from a
        // MonoBehaviour Update or a Harmony patch cannot, because those run
        // whether or not this session ever connected. Cost an in-game
        // session on 2026-09-22, where the status overlay dereferenced it
        // every frame from the moment the player started hosting — and since
        // that ran at the top of ApRuntime.Update, it killed the pump that
        // would have done the connecting.
        internal ApSlotData SlotData { get; private set; }
        internal string SeedName { get; private set; } = string.Empty;
        internal string SlotName { get; private set; } = string.Empty;

        // Which save file this session was opened for. ApRuntime compares it
        // against the loaded save every frame: quitting to the menu and
        // hosting a different save leaves the socket open, and applying that
        // session's items to the new save with the old cursor would be a
        // genuine corruption, not just an oddity.
        internal string SaveUuid { get; private set; } = string.Empty;
        internal string LastError { get; private set; } = string.Empty;

        internal void BeginConnect(ApEndpoint endpoint)
        {
            if (_status == ConnectionStatus.Connecting || _status == ConnectionStatus.Connected)
                return;

            _status = ConnectionStatus.Connecting;
            SlotName = endpoint.SlotName;
            SaveUuid = endpoint.Uuid;
            LastError = string.Empty;

            var generation = ++_generation;
            Task.Run(() => ConnectBlocking(endpoint, generation));
        }

        // Gives up on an attempt that never came back, so the retry loop can
        // start a fresh one.
        //
        // Needed because TryConnectAndLogin does not reliably return when
        // the port is dead: proven in-game (2026-09-15) by killing the
        // server and watching the client log one failed attempt and then
        // nothing at all for minutes — stuck in Connecting, which ApRuntime
        // treats as "wait", so it waited forever.
        //
        // The abandoned attempt is not cancellable, so it is fenced off
        // instead: bumping the generation makes every write it might still
        // perform a no-op, and a later reply from a session nobody is
        // waiting for can no longer resurrect a stale connection.
        internal void AbandonAttempt(string reason)
        {
            if (_status != ConnectionStatus.Connecting)
                return;

            _generation++;
            LastError = reason;
            _status = ConnectionStatus.Failed;
        }

        private void ConnectBlocking(ApEndpoint endpoint, int generation)
        {
            try
            {
                var session = ArchipelagoSessionFactory.CreateSession(endpoint.Host, endpoint.Port);

                var queue = new ConcurrentQueue<ItemInfo>();
                _incomingItems = queue;
                var received = new Dictionary<string, int>();
                _receivedByName = received;
                _hints = Array.Empty<Hint>();
                _progressionHere = new HashSet<long>();

                // Subscribed BEFORE logging in, on purpose: the server sends
                // every item the slot has ever received as soon as the
                // connection is established, and a handler attached after
                // that would miss the entire backlog.
                session.Items.ItemReceived += (ReceivedItemsHelper helper) => OnItemReceived(helper, queue, received);
                session.Socket.SocketClosed += OnSocketClosed;
                session.Socket.ErrorReceived += OnErrorReceived;

                var result = session.TryConnectAndLogin(
                    "Big Walk",
                    endpoint.SlotName,
                    ItemsHandlingFlags.AllItems,
                    ClientVersion,
                    Array.Empty<string>(),
                    endpoint.Uuid,
                    endpoint.Password,
                    true);

                // Everything past this point writes shared state, so it
                // only counts if this attempt is still the current one. An
                // attempt abandoned for taking too long must not come back
                // later and declare itself connected.
                if (generation != _generation)
                    return;

                if (result is not LoginSuccessful success)
                {
                    LastError = result is LoginFailure failure && failure.Errors is { Length: > 0 }
                        ? string.Join(" / ", failure.Errors)
                        : "unknown login failure";
                    _status = ConnectionStatus.Failed;
                    return;
                }

                var storedDeposits = ReadStoredDeposits(session);

                var deathLink = session.CreateDeathLinkService();
                var slot = endpoint.SlotName;
                deathLink.OnDeathLinkReceived += received =>
                {
                    // Managed types only. One of this slot's own comes back from the server too.
                    if (received == null || received.Source == slot)
                        return;
                    _deathLinksReceived.Enqueue(
                        string.IsNullOrEmpty(received.Cause) ? received.Source : $"{received.Source}: {received.Cause}");
                };

                // A check this slot sent, as the server tells it (player, 2026-10-13): what it was and what it gave to
                // whom, a short line on the overlay. On the socket's thread: queued, shown from the main thread.
                session.MessageLog.OnMessageReceived += message =>
                {
                    try
                    {
                        // R7: the server's answer to a hint asked at the hub's sign, a line on the overlay for a short
                        // while after the question: the hint itself, or a refusal, which comes as a plain message
                        // ("Nothing found for recognized item name ...", seen 2026-10-13) or a command's result.
                        if (DateTime.UtcNow < _hintAnswerUntil
                            && (message is Archipelago.MultiClient.Net.MessageLog.Messages.HintItemSendLogMessage
                                || message is Archipelago.MultiClient.Net.MessageLog.Messages.CommandResultLogMessage
                                || message.GetType() == typeof(Archipelago.MultiClient.Net.MessageLog.Messages.LogMessage)))
                        {
                            ApNotices.PostFromSocket($"Hint: {message}");
                        }

                        // A hint is an ItemSendLogMessage too: it is no check.
                        if (message is Archipelago.MultiClient.Net.MessageLog.Messages.ItemSendLogMessage sent
                            && message is not Archipelago.MultiClient.Net.MessageLog.Messages.HintItemSendLogMessage
                            && sent.IsSenderTheActivePlayer && sent.Item != null)
                        {
                            // Always who it went to, this slot included (player, 2026-10-13).
                            ApNotices.PostFromSocket($"Check: {sent.Item.LocationName} -> {sent.Item.ItemName} ({sent.Receiver?.Name})");
                        }
                    }
                    catch (Exception)
                    {
                        // A message the library cannot describe is no reason to touch the connection.
                    }
                };

                // R7: the hints of this slot, now and as they change (the hint board reads them on the main thread).
                try
                {
                    session.DataStorage.TrackHints(hints => _hints = hints ?? Array.Empty<Hint>(), true);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not follow the hints: {ex.Message}");
                }

                // R7: what every location of this slot holds, quietly (HintCreationPolicy.None): only whether it is
                // progression is kept, for the next-check board's order.
                try
                {
                    var ids = new List<long>(session.Locations.AllLocations).ToArray();
                    session.Locations.ScoutLocationsAsync(HintCreationPolicy.None, ids).ContinueWith(OnScouted);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not scout the slot's locations: {ex.Message}");
                }

                _session = session;
                _deathLink = deathLink;
                StoredDeposits = storedDeposits;
                SlotData = ApSlotData.From(success.SlotData);
                SeedName = session.RoomState?.Seed ?? string.Empty;
                _slotLocations = new HashSet<long>(session.Locations.AllLocations);

                _status = ConnectionStatus.Connected;
            }
            catch (Exception ex)
            {
                if (generation != _generation)
                    return;

                LastError = ex.Message;
                _status = ConnectionStatus.Failed;
            }
        }

        // The deposits this slot has made, from the server's own storage. Only
        // ever goes up (see StoreDeposits), and a room is one seed, so a new
        // seed starts at nothing. Called on the connect task, where waiting is
        // allowed; any failure is a 0 and a log line, never an exception.
        private static int ReadStoredDeposits(ArchipelagoSession session)
        {
            try
            {
                var element = session.DataStorage[Scope.Slot, DepositsKey];
                element.Initialize(0);

                var read = element.GetAsync();
                if (!read.Wait(StorageWaitMilliseconds))
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(ApConnection)}] The server did not answer the deposits read in {StorageWaitMilliseconds} ms; "
                        + "no deposit will be restored this session.");
                    return 0;
                }

                var token = read.Result;
                var count = token != null && token.Type != Newtonsoft.Json.Linq.JTokenType.Null ? token.ToObject<int>() : 0;
                Plugin.Log.LogInfo($"[{nameof(ApConnection)}] The server remembers {count} deposit(s) for this slot.");
                return Math.Max(0, count);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApConnection)}] Could not read the deposits from the server ({ex.Message}); none will be restored.");
                return 0;
            }
        }

        // Fire and forget, and a MAXIMUM: the server keeps the larger of what it
        // has and `count`, so a late or repeated write can never lower it. A
        // write lost to a dead socket is covered by the next one.
        internal void StoreDeposits(int count)
        {
            if (_status != ConnectionStatus.Connected || count <= 0)
                return;

            try
            {
                _session.DataStorage[Scope.Slot, DepositsKey] += Operation.Max(count);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not store {count} deposit(s) on the server: {ex.Message}");
            }
        }

        private static void OnItemReceived(ReceivedItemsHelper helper, ConcurrentQueue<ItemInfo> queue,
            Dictionary<string, int> received)
        {
            // Managed types only — see the threading note at the top.
            try
            {
                var item = helper.DequeueItem();
                queue.Enqueue(item);
                if (item != null && !string.IsNullOrEmpty(item.ItemName))
                {
                    lock (received)
                        received[item.ItemName] = received.TryGetValue(item.ItemName, out var count) ? count + 1 : 1;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to read an incoming item: {ex.Message}");
            }
        }

        private void OnSocketClosed(string reason)
        {
            // Never demote a Failed status back to something softer, and
            // never touch the game from here: ApRuntime notices the change on
            // its next frame and decides what to do about it.
            LastError = string.IsNullOrEmpty(reason) ? "connection closed" : reason;
            _status = ConnectionStatus.Failed;
        }

        private void OnErrorReceived(Exception exception, string message)
        {
            // Recorded, not acted on: an error is not proof the connection
            // is gone, and Status asks the socket directly anyway. Keeping
            // the message means the warning the player sees names the real
            // cause instead of a generic "connection dropped".
            LastError = string.IsNullOrEmpty(message) ? exception?.Message ?? "socket error" : message;
            Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Socket error: {message} ({exception?.Message}).");
        }

        internal bool TryDequeueItem(out ItemInfo item)
        {
            return _incomingItems.TryDequeue(out item);
        }

        // Whether the server's replay is still arriving. ApRuntime waits for
        // this to go quiet before reconciling gourds, because that
        // reconciliation depends on having seen the whole item history.
        internal bool HasPendingItems => !_incomingItems.IsEmpty;

        // Whether an id belongs to this slot at all. Snapshotted once at
        // login rather than read live, both to avoid enumerating a collection
        // the network thread is mutating and because the answer cannot
        // change: which locations exist in a slot is fixed at generation.
        //
        // This is the guard apworld/protocol.md §3 insists on — it is what
        // makes it safe for the mod to report anything the game writes
        // without first knowing which options this slot was rolled with.
        internal bool BelongsToSlot(long locationId)
        {
            return _slotLocations.Contains(locationId);
        }

        // The multiworld's name for a location, for the feed on screen. The
        // mod's own identifiers are the game's enum values (`gourdHighButton`),
        // which are no use to a player: the datapackage is the only place the
        // player-facing name exists, so a lookup that fails falls back to the
        // number rather than inventing one.
        internal string LocationName(long locationId)
        {
            try
            {
                var name = _session?.Locations.GetLocationNameFromId(locationId);
                return string.IsNullOrEmpty(name) ? locationId.ToString() : name;
            }
            catch (Exception)
            {
                return locationId.ToString();
            }
        }

        // Hints for these locations of this slot (ROADMAP H3): what sits there, shown to the player and
        // free, like the hints at the start. Announced once each by the server.
        internal void ScoutAsHints(long[] locationIds)
        {
            if (_status != ConnectionStatus.Connected || locationIds.Length == 0)
                return;

            try
            {
                _session.Locations.ScoutLocationsAsync(HintCreationPolicy.CreateAndAnnounceOnce, locationIds);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to scout {locationIds.Length} location(s) as hints: {ex.Message}");
            }
        }

        internal void SendChecks(long[] locationIds)
        {
            if (_status != ConnectionStatus.Connected || locationIds.Length == 0)
                return;

            try
            {
                _session.Locations.CompleteLocationChecks(locationIds);
            }
            catch (Exception ex)
            {
                // Not lost: every reported check is persisted in the save
                // (CheckTracker) and the whole set is resent on reconnect.
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to send {locationIds.Length} check(s), will resend on reconnect: {ex.Message}");
            }
        }

        // Returns whether the server actually took it, so the caller can
        // leave the goal armed rather than marking it sent into a dead
        // socket. It matters since the second ending reports itself from the
        // frame the game tears the world down (Patches/EndingStartPatch).
        internal bool SendGoal()
        {
            if (_status != ConnectionStatus.Connected)
                return false;

            try
            {
                _session.SetGoalAchieved();
                Plugin.Log.LogInfo($"[{nameof(ApConnection)}] Goal reported to the server.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to report the goal: {ex.Message}");
                return false;
            }
        }

        // A DeathLink for the other games: a Bounce packet tagged "DeathLink", which the server
        // hands only to clients that asked for the tag. Sending needs no tag of ours; whether it
        // kills anything is the other game's business.
        internal bool SendDeathLink(string cause)
        {
            if (_status != ConnectionStatus.Connected || _deathLink == null)
                return false;

            try
            {
                _deathLink.SendDeathLink(new DeathLink(SlotName, cause));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to send a DeathLink: {ex.Message}");
                return false;
            }
        }

        // Asks the server for the DeathLinks of the multiworld (the "DeathLink" tag), with the
        // slot's `death_link` on receive or both. Sending needs no tag.
        internal void ListenForDeathLinks(bool listen)
        {
            if (_status != ConnectionStatus.Connected || _deathLink == null)
                return;

            try
            {
                if (listen)
                    _deathLink.EnableDeathLink();
                else
                    _deathLink.DisableDeathLink();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not change the DeathLink tag: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // TrapLink: Bounce packets tagged "TrapLink", {"time", "source", "trap_name"}
        // ------------------------------------------------------------------

        private readonly ConcurrentQueue<(string source, string trap)> _trapLinksReceived = new();
        private bool _trapLinkHooked;

        // Asks the server for the TrapLinks of the multiworld (the tag), or stops.
        internal void ListenForTrapLinks(bool listen)
        {
            if (_status != ConnectionStatus.Connected || _session == null)
                return;

            try
            {
                var tags = new List<string>(_session.ConnectionInfo.Tags ?? Array.Empty<string>());
                tags.Remove("TrapLink");
                if (listen)
                    tags.Add("TrapLink");
                _session.ConnectionInfo.UpdateConnectionOptions(tags.ToArray());

                if (listen && !_trapLinkHooked)
                {
                    _trapLinkHooked = true;
                    var own = SlotName;
                    _session.Socket.PacketReceived += packet =>
                    {
                        // Managed types only, off Unity's thread: queued for the main loop.
                        if (packet is not Archipelago.MultiClient.Net.Packets.BouncedPacket bounced
                            || bounced.Tags == null || !bounced.Tags.Contains("TrapLink") || bounced.Data == null)
                            return;
                        var source = bounced.Data.TryGetValue("source", out var s) ? s?.ToString() : null;
                        var trap = bounced.Data.TryGetValue("trap_name", out var t) ? t?.ToString() : null;
                        if (!string.IsNullOrEmpty(trap) && source != own)
                            _trapLinksReceived.Enqueue((source ?? "?", trap));
                    };
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not change the TrapLink tag: {ex.Message}");
            }
        }

        internal bool SendTrapLink(string trapName)
        {
            if (_status != ConnectionStatus.Connected || _session == null)
                return false;

            try
            {
                _session.Socket.SendPacket(new Archipelago.MultiClient.Net.Packets.BouncePacket
                {
                    Tags = new List<string> { "TrapLink" },
                    Data = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                    {
                        ["time"] = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                        ["source"] = SlotName,
                        ["trap_name"] = trapName,
                    },
                });
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to send a TrapLink: {ex.Message}");
                return false;
            }
        }

        internal bool TryDequeueTrapLink(out string source, out string trap)
        {
            var found = _trapLinksReceived.TryDequeue(out var pair);
            source = pair.source;
            trap = pair.trap;
            return found;
        }

        internal bool TryDequeueDeathLink(out string cause)
        {
            return _deathLinksReceived.TryDequeue(out cause);
        }

        // R7: until when an answer of the server is taken as the answer to a hint asked at the sign.
        private DateTime _hintAnswerUntil = DateTime.MinValue;

        // R7: a hint asked at the hub's sign, as the Text Client's `!hint`: it costs the slot's hint points.
        internal void AskHint(string text)
        {
            if (_session == null || _status != ConnectionStatus.Connected || string.IsNullOrWhiteSpace(text))
                return;
            try
            {
                _hintAnswerUntil = DateTime.UtcNow.AddSeconds(10);
                _session.Say("!hint " + text.Trim());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not ask for a hint: {ex.Message}");
            }
        }

        // R7: the slot's hint points and what a hint costs (-1, -1 when unknown).
        internal (int points, int cost) HintBudget()
        {
            try
            {
                return _session != null ? (_session.RoomState.HintPoints, _session.RoomState.HintCost) : (-1, -1);
            }
            catch (Exception)
            {
                return (-1, -1);
            }
        }

        // R7: what this slot has received, by item name (a copy, safe on the main thread).
        internal Dictionary<string, int> ReceivedByName()
        {
            var received = _receivedByName;
            lock (received)
                return new Dictionary<string, int>(received);
        }

        // R7: the locations of this slot the server knows as checked (a copy; empty when not connected).
        internal HashSet<long> CheckedLocations()
        {
            try
            {
                return _session != null ? new HashSet<long>(_session.Locations.AllLocationsChecked) : new HashSet<long>();
            }
            catch (Exception)
            {
                // The socket's thread was adding one: the next look will have it.
                return new HashSet<long>();
            }
        }

        // R7: the answer to the quiet scout of every location (a method, not a lambda: the interop build has no
        // NullableAttribute for one over these generic types).
        private void OnScouted(Task<Dictionary<long, ScoutedItemInfo>> task)
        {
            if (task.IsFaulted || task.Result == null)
                return;
            var progression = new HashSet<long>();
            foreach (var pair in task.Result)
            {
                if (pair.Value != null && (pair.Value.Flags & ItemFlags.Advancement) != 0)
                    progression.Add(pair.Key);
            }
            _progressionHere = progression;
            Plugin.Log.LogInfo($"[{nameof(ApConnection)}] Scouted the slot: {progression.Count} location(s) hold a progression item.");
        }

        // R7: how many of this slot's locations are not checked yet.
        internal int UncheckedCount(HashSet<long> checkedIds)
        {
            var count = 0;
            foreach (var id in _slotLocations)
            {
                if (!checkedIds.Contains(id))
                    count++;
            }
            return count;
        }

        // R7: whether a location of this slot holds a progression item (scouted at login).
        internal bool HoldsProgression(long locationId) => _progressionHere.Contains(locationId);

        // R7: the locations of this slot that open hints point to (anyone's item, not found yet).
        internal HashSet<long> HintedLocations()
        {
            var result = new HashSet<long>();
            var session = _session;
            if (session == null)
                return result;
            try
            {
                var me = session.ConnectionInfo.Slot;
                foreach (var hint in _hints)
                {
                    if (hint != null && !hint.Found && hint.FindingPlayer == me)
                        result.Add(hint.LocationId);
                }
            }
            catch (Exception)
            {
                // Read again at the next count.
            }
            return result;
        }

        // R7: the hints this slot asked for that are still open: an item for this slot, not found yet, newest last
        // (player, 2026-10-13: "nothing if the player did not ask"). Each as "item: location (finder)".
        internal List<string> OpenHintLines()
        {
            var lines = new List<string>();
            var session = _session;
            if (session == null)
                return lines;

            try
            {
                var me = session.ConnectionInfo.Slot;
                foreach (var hint in _hints)
                {
                    if (hint == null || hint.Found || hint.ReceivingPlayer != me)
                        continue;
                    var finder = session.Players.GetPlayerInfo(hint.FindingPlayer);
                    var receiver = session.Players.GetPlayerInfo(hint.ReceivingPlayer);
                    var item = session.Items.GetItemName(hint.ItemId, receiver?.Game) ?? hint.ItemId.ToString();
                    var location = session.Locations.GetLocationNameFromId(hint.LocationId, finder?.Game) ?? hint.LocationId.ToString();
                    // The slot it is in, even this one (player, 2026-10-13).
                    var where = finder == null ? location : $"{location} ({finder.Name})";
                    lines.Add($"{item}: {where}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Could not read the hints: {ex.Message}");
            }

            return lines;
        }

        internal void Reset()
        {
            _session = null;
            _deathLink = null;
            _trapLinkHooked = false;
            _status = ConnectionStatus.Idle;
        }
    }
}
