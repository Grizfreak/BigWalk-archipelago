using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;

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

        private ArchipelagoSession _session;
        private volatile ConnectionStatus _status = ConnectionStatus.Idle;

        // Written by the connect task, read by the main thread once Status
        // has flipped to Connected/Failed — that flip is the handoff, so no
        // extra locking is needed around them.
        private HashSet<long> _slotLocations = new();

        internal ConnectionStatus Status => _status;
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

            Task.Run(() => ConnectBlocking(endpoint));
        }

        private void ConnectBlocking(ApEndpoint endpoint)
        {
            try
            {
                var session = ArchipelagoSessionFactory.CreateSession(endpoint.Host, endpoint.Port);

                var queue = new ConcurrentQueue<ItemInfo>();
                _incomingItems = queue;

                // Subscribed BEFORE logging in, on purpose: the server sends
                // every item the slot has ever received as soon as the
                // connection is established, and a handler attached after
                // that would miss the entire backlog.
                session.Items.ItemReceived += (ReceivedItemsHelper helper) => OnItemReceived(helper, queue);
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

                if (result is not LoginSuccessful success)
                {
                    LastError = result is LoginFailure failure && failure.Errors is { Length: > 0 }
                        ? string.Join(" / ", failure.Errors)
                        : "unknown login failure";
                    _status = ConnectionStatus.Failed;
                    return;
                }

                _session = session;
                SlotData = ApSlotData.From(success.SlotData);
                SeedName = session.RoomState?.Seed ?? string.Empty;
                _slotLocations = new HashSet<long>(session.Locations.AllLocations);

                _status = ConnectionStatus.Connected;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _status = ConnectionStatus.Failed;
            }
        }

        private static void OnItemReceived(ReceivedItemsHelper helper, ConcurrentQueue<ItemInfo> queue)
        {
            // Managed types only — see the threading note at the top.
            try
            {
                queue.Enqueue(helper.DequeueItem());
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
            Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Socket error: {message} ({exception?.Message}).");
        }

        internal bool TryDequeueItem(out ItemInfo item)
        {
            return _incomingItems.TryDequeue(out item);
        }

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

        internal void SendGoal()
        {
            if (_status != ConnectionStatus.Connected)
                return;

            try
            {
                _session.SetGoalAchieved();
                Plugin.Log.LogInfo($"[{nameof(ApConnection)}] Goal reported to the server.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApConnection)}] Failed to report the goal: {ex.Message}");
            }
        }

        internal void Reset()
        {
            _session = null;
            _status = ConnectionStatus.Idle;
        }
    }
}
