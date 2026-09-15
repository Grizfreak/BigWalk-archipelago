using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core.Net
{
    // The Archipelago client's main loop, and the only place in the mod where
    // network state meets the game.
    //
    // Everything here runs on Unity's main thread, by construction: ApConnection
    // parks incoming items in a concurrent queue and flips a status flag, and
    // this Update() is what actually reads them and calls into IL2CPP. The
    // split is not stylistic — touching a game object from one of the client
    // library's network threads is a native crash with no managed stack.
    //
    // Host-only, like every write in this mod: Big Walk's save belongs to the
    // host (Mirror [Server] authority), so a non-host client has nothing
    // truthful to report and nothing it is allowed to apply.
    internal class ApRuntime : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public ApRuntime(IntPtr ptr) : base(ptr)
        {
        }

        private const float RetryDelaySeconds = 10f;
        private const float PollIntervalSeconds = 1f;

        // Applying an item can spawn a physical prop, and a reconnection
        // replays every item the slot ever received — on the new-save
        // recovery path that is the entire run at once. Spread over frames
        // so a reconnect never lands as a multi-second freeze. (The
        // session-start gourd restore paces itself separately, one gourd per
        // GourdRestoreInterval — see RestoreLooseGourds.)
        private const int MaxItemsPerFrame = 4;

        // How long to let the server's replay arrive before trusting an
        // empty item queue to mean "that was all of it".
        private const float ReplayGraceSeconds = 3f;

        // How long a connection attempt may go unanswered before it is
        // written off. The client library gives no timeout of its own
        // that can be relied on here.
        private const float ConnectTimeoutSeconds = 15f;

        // ALL state below is static, and that is not laziness. This is a type
        // injected into IL2CPP: the managed wrapper around the component is
        // not guaranteed to be the same object for the component's whole
        // life, so per-instance fields can silently come back reset. Most of
        // the mod gets away with that (ArchDoorUnlocker's one "done" bool);
        // here it would be a data-loss bug, because losing _seenItemCount
        // mid-session makes the next replay re-apply items already applied —
        // duplicating every gourd the slot ever received. Same reasoning as
        // CosmeticMonumentFillTracker, which is static throughout for the
        // same reason.

        // Fed by ApReporter from the detection patches, drained below. A
        // queue rather than a direct send so that a check reported while
        // disconnected is never dropped on the floor mid-frame.
        private static readonly ConcurrentQueue<string> PendingLocationNames = new();

        private static readonly ApConnection Connection = new();
        private static readonly List<long> SendBuffer = new();

        // Position in the slot's ordered received-items list. _seen counts
        // what this session has pulled off the queue; _applied is what this
        // SAVE has already materialized (ApItemCursor). The gap between them
        // is exactly the replay the server sends on every connection.
        private static int _seenItemCount;
        private static int _appliedItemCount;

        // Loose-gourd reconciliation, per connection (see RestoreLooseGourds).
        private static bool _worldWasReady;
        private static bool _looseGourdsRestored;
        private static bool _looseRestoreBlockedLogged;
        private static bool _reconciliationLogged;
        private static int _looseGourdsRestoredCount;
        private static int _gourdsSeenThisSession;
        private static int _gourdsSpawnedThisSession;
        private static float _restoreTimer;
        private static float _connectedAt;

        private static int _lastReportedDepositCount = -1;
        private static bool _goalSent;
        private static float _retryTimer;
        private static float _connectingSince;
        private static float _pollTimer;
        private static bool _disabledNoticeLogged;
        private static string _lastConnectProblem = string.Empty;

        // What Core/Net/ApStatusOverlay puts on screen, or null when there
        // is nothing worth saying. Decided here rather than in the overlay
        // so OnGUI — which Unity calls several times per frame — does no
        // work in the common case, and so "is this worth interrupting the
        // player for" stays one decision in one place.
        internal static string StatusMessage { get; private set; }
        internal static bool StatusIsWarning { get; private set; }

        private const float ConnectedNoticeSeconds = 6f;

        internal static void QueueLocation(string locationName)
        {
            // Dropped outright on a non-host. Its detection patches can
            // still fire, but it never connects and so never drains this
            // queue — every check it saw would sit there for the whole
            // session, growing without bound and reported by nobody. The
            // host's own client is what reports these.
            if (string.IsNullOrEmpty(locationName) || !NetworkServer.active)
                return;

            PendingLocationNames.Enqueue(locationName);
        }

        private static void RefreshStatusMessage()
        {
            // Nothing to say when Archipelago is switched off, and nothing
            // to say to a player who is not the host: their client never
            // connects by design, so an "disconnected" warning would be a
            // lie.
            if (!ModConfig.ArchipelagoEnabled.Value || !NetworkServer.active)
            {
                StatusMessage = null;
                return;
            }

            switch (Connection.Status)
            {
                case ApConnection.ConnectionStatus.Connecting:
                    StatusMessage = "Archipelago: connecting...";
                    StatusIsWarning = false;
                    break;

                case ApConnection.ConnectionStatus.Connected:
                    // Shown briefly, then out of the way: the point is to
                    // confirm the details were right, not to sit there for
                    // the rest of the run.
                    StatusMessage = Time.unscaledTime - _connectedAt < ConnectedNoticeSeconds
                        ? "Archipelago: connected"
                        : null;
                    StatusIsWarning = false;
                    break;

                default:
                    StatusMessage = string.IsNullOrEmpty(Connection.LastError)
                        ? "Archipelago: not connected"
                        : $"Archipelago: disconnected - {Connection.LastError} (retrying)";
                    StatusIsWarning = true;
                    break;
            }
        }

        private void Update()
        {
            RefreshStatusMessage();

            if (!ModConfig.ArchipelagoEnabled.Value)
            {
                if (!_disabledNoticeLogged)
                {
                    _disabledNoticeLogged = true;
                    Plugin.Log.LogInfo(
                        $"[{nameof(ApRuntime)}] Archipelago disabled in the config; checks are logged locally only.");
                }

                return;
            }

            // Loose gourds do not survive a world reload, and a world reload
            // does not require the process to restart: going back to the
            // main menu and hosting again destroys them just the same, while
            // the Archipelago socket stays open and every static here keeps
            // its value. Arming the reconciliation on "a connection was
            // established" therefore missed that case entirely — it has to
            // be "a world became ready".
            var worldReady = NetworkServer.active && WorldManager.isReadyForEffects;
            if (worldReady && !_worldWasReady)
                OnWorldBecameReady();

            _worldWasReady = worldReady;

            // Not hosting: nothing to connect, and anything queued stays
            // queued. This also covers the main menu, where there is no save
            // to read a slot name out of yet.
            if (!NetworkServer.active)
                return;

            switch (Connection.Status)
            {
                case ApConnection.ConnectionStatus.Idle:
                case ApConnection.ConnectionStatus.Failed:
                    TickReconnect();
                    return;

                case ApConnection.ConnectionStatus.Connecting:
                    // An attempt that never comes back would otherwise wedge
                    // the client here for good: TryConnectAndLogin does not
                    // reliably return against a dead port (proven in-game,
                    // 2026-09-15 — one failed retry then silence for
                    // minutes). Past this, the attempt is written off and the
                    // retry loop takes over.
                    if (Time.unscaledTime - _connectingSince > ConnectTimeoutSeconds)
                    {
                        Plugin.Log.LogWarning(
                            $"[{nameof(ApRuntime)}] Connection attempt gave no answer after {ConnectTimeoutSeconds:0}s; abandoning it and retrying.");
                        Connection.AbandonAttempt($"no answer after {ConnectTimeoutSeconds:0}s");
                        _retryTimer = 0f;
                    }

                    return;
            }

            if (_appliedItemCount < 0)
                OnJustConnected();

            // Quitting to the menu and hosting a *different* save leaves this
            // session open and connected. Carrying on would apply that
            // session's items into the new save using the old save's cursor,
            // so the session is torn down and rebuilt from the new save.
            if (HasSaveChanged())
                return;

            FlushPendingChecks();

            // Applying an item spawns props and writes the save, so it waits
            // for the same "safe to touch the world" signal the rest of the
            // mod uses. Items simply stay queued until then.
            if (WorldManager.isReadyForEffects)
            {
                DrainIncomingItems();

                // Every frame, because it paces itself: it drops one gourd
                // per GourdRestoreInterval and returns immediately the rest
                // of the time.
                RestoreLooseGourds();
            }

            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f)
                return;

            _pollTimer = PollIntervalSeconds;

            if (WorldManager.isReadyForEffects)
            {
                ReportDeposits();
                CheckGoal();
            }
        }

        // A freshly loaded world contains none of the loose gourds the
        // previous one did, so everything tracking "what exists right now"
        // starts over. The ledger itself (ap_gourds_received) is untouched:
        // it counts what the player was given, which a reload does not
        // change.
        private static void OnWorldBecameReady()
        {
            _looseGourdsRestored = false;
            _looseRestoreBlockedLogged = false;
            _reconciliationLogged = false;
            _looseGourdsRestoredCount = 0;
            _gourdsSpawnedThisSession = 0;
            _restoreTimer = 0f;
        }

        private static bool HasSaveChanged()
        {
            var save = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (save == null)
                return false;

            var uuid = !string.IsNullOrWhiteSpace(save.filenameUid) ? save.filenameUid : save.slotName;
            if (string.IsNullOrEmpty(uuid) || uuid == Connection.SaveUuid)
                return false;

            Plugin.Log.LogInfo(
                $"[{nameof(ApRuntime)}] A different save is now loaded; dropping the Archipelago session and reconnecting for it.");

            Connection.Reset();
            _appliedItemCount = -1;
            _retryTimer = 0f;
            return true;
        }

        private static void TickReconnect()
        {
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer > 0f)
                return;

            _retryTimer = RetryDelaySeconds;

            if (Connection.Status == ApConnection.ConnectionStatus.Failed)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApRuntime)}] Archipelago connection lost or refused ({Connection.LastError}); retrying in {RetryDelaySeconds:0}s.");
                Connection.Reset();
            }

            if (!ApEndpoint.TryResolve(out var endpoint, out var problem))
            {
                // Logged once per distinct cause rather than every retry: the
                // usual case is simply "the player has not filled the address
                // in yet", which would otherwise spam the log forever.
                if (problem != _lastConnectProblem)
                {
                    _lastConnectProblem = problem;
                    Plugin.Log.LogInfo($"[{nameof(ApRuntime)}] Not connecting: {problem}.");
                }

                return;
            }

            _lastConnectProblem = string.Empty;
            _connectingSince = Time.unscaledTime;
            Plugin.Log.LogInfo(
                $"[{nameof(ApRuntime)}] Connecting to {endpoint.Host}:{endpoint.Port} as '{endpoint.SlotName}'...");

            // Marks the session as "not yet initialised" so the first frame
            // after a successful login runs OnJustConnected.
            _appliedItemCount = -1;
            Connection.BeginConnect(endpoint);
        }

        private static void OnJustConnected()
        {
            ApLocationIds.Configure(Connection.SlotData);

            _seenItemCount = 0;
            _appliedItemCount = ApItemCursor.SyncTo(Connection.SeedName, Connection.SlotName);
            _lastReportedDepositCount = -1;
            // Deliberately does NOT reset the loose-gourd state. That
            // describes the world — what is currently lying on the ground —
            // and a reconnection does not touch the world. Resetting it here
            // is what made reconnecting spawn a second full set of gourds
            // (observed in-game, 2026-09-15: 28 restored, connection
            // dropped and recovered, 28 more on top). Only OnWorldBecameReady
            // may clear it, because only a world reload actually destroys
            // them.

            // Connection-scoped, unlike the above: the replay and its timing
            // belong to this session, not to whichever world is loaded.
            _gourdsSeenThisSession = 0;
            _connectedAt = Time.unscaledTime;

            // A goal string this build cannot detect would otherwise leave
            // the player unable to ever finish, with nothing in the log to
            // say why. Refuse it loudly here, once, rather than silently
            // polling for something that will never be true.
            var goal = Connection.SlotData.Goal;
            _goalSent = goal is not ("gauntlet" or "ending" or "deposits");
            if (_goalSent)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApRuntime)}] This slot's goal is '{goal}', which this version of the mod cannot detect. "
                    + "Goal completion will NOT be reported automatically — update the mod to match the apworld.");
            }

            Plugin.Log.LogInfo(
                $"[{nameof(ApRuntime)}] Connected. {Connection.SlotData.Describe()}. "
                + $"{_appliedItemCount} item(s) already applied to this save.");

            ResendKnownChecks();
        }

        // Every check this save has ever reported is resent on connection.
        //
        // This is what makes a disconnection harmless. CheckTracker marks a
        // location as reported permanently, in the save — so a check
        // validated while the client was offline would otherwise never be
        // sent again, and its item would stay lost to whoever was waiting for
        // it in the multiworld. Resending the whole set costs one packet and
        // the server deduplicates.
        private static void ResendKnownChecks()
        {
            SendBuffer.Clear();

            foreach (var locationName in CheckTracker.GetReportedLocationNames())
            {
                if (ApLocationIds.TryResolveLocation(locationName, out var id)
                    && Connection.BelongsToSlot(id))
                    SendBuffer.Add(id);
            }

            if (SendBuffer.Count == 0)
                return;

            Plugin.Log.LogInfo($"[{nameof(ApRuntime)}] Resending {SendBuffer.Count} check(s) already validated on this save.");
            Connection.SendChecks(SendBuffer.ToArray());
        }

        private static void FlushPendingChecks()
        {
            SendBuffer.Clear();

            while (PendingLocationNames.TryDequeue(out var locationName))
            {
                if (!ApLocationIds.TryResolveLocation(locationName, out var id))
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(ApRuntime)}] No Archipelago id for '{locationName}', check dropped.");
                    continue;
                }

                // The slot decides what exists, not the game: radio stations
                // can be off, the Green Dome can be excluded. Sending an id
                // this slot does not have is a protocol error, so it is
                // filtered here rather than hoping the server is forgiving.
                if (!Connection.BelongsToSlot(id))
                    continue;

                SendBuffer.Add(id);
            }

            if (SendBuffer.Count > 0)
                Connection.SendChecks(SendBuffer.ToArray());
        }

        // Puts back the gourds this save owns but no longer physically has.
        //
        // Only a gourd pinned in a monument survives a restart: a loose or
        // carried one has no save identity, by design, and is gone. So at
        // every session start the world is made to match the ledger again:
        //
        //     loose gourds to spawn = gourds ever received
        //                             - gourds deposited in monuments
        //                             - gourds already spawned this session
        //
        // Nothing tries to remember *which* gourd was where, because gourds
        // are interchangeable — the same property that makes the global
        // deposit model softlock-proof. Quit holding five gourds, find five
        // at the hub.
        //
        // The ledger is rebuilt from the server rather than merely trusted:
        // the replay names every item this slot ever received, so counting
        // the gourds in it is authoritative and repairs a save written by a
        // version of the mod that did not keep the count at all. Hence
        // waiting for the replay to arrive and go quiet before acting —
        // reconciling against a ledger of zero would "restore" nothing and
        // then latch.
        private static void RestoreLooseGourds()
        {
            if (_looseGourdsRestored || Connection.HasPendingItems)
                return;

            // An empty queue right after connecting usually means the replay
            // has not landed yet, not that there is nothing to replay.
            if (Time.unscaledTime - _connectedAt < ReplayGraceSeconds)
                return;

            if (_gourdsSeenThisSession > ApItemCursor.GourdsReceived)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(ApRuntime)}] Gourd ledger rebuilt from the server: {_gourdsSeenThisSession} received "
                    + $"(this save recorded {ApItemCursor.GourdsReceived}).");
                ApItemCursor.SetGourdsReceived(_gourdsSeenThisSession);
            }

            var deposited = CosmeticMonumentFillTracker.GetFilledMonumentCount();
            var owed = ApItemCursor.GourdsReceived - deposited - _gourdsSpawnedThisSession;

            if (!_reconciliationLogged)
            {
                _reconciliationLogged = true;
                Plugin.Log.LogInfo(
                    $"[{nameof(ApRuntime)}] Gourd reconciliation: {ApItemCursor.GourdsReceived} received, "
                    + $"{deposited} deposited, {_gourdsSpawnedThisSession} already spawned -> {owed} to restore.");
            }

            if (owed <= 0)
            {
                _looseGourdsRestored = true;
                if (_looseGourdsRestoredCount > 0)
                    Plugin.Log.LogInfo(
                        $"[{nameof(ApRuntime)}] Restored {_looseGourdsRestoredCount} gourd(s) received but never deposited.");
                return;
            }

            // One at a time, spaced out, each dropped on the game's own
            // spawn point. Two earlier attempts placed them by computing
            // offsets — a spiral, then that spiral raycast onto the ground —
            // and both lost gourds outside the playable area (11 of 28, then
            // 8 of 28). The mistake was mine to make twice: deciding whether
            // a position is valid is the game's job, and
            // InventorySpawn.GetNextSpawnPosition already answers it.
            //
            // Landing them one by one is what replaces the spreading: each
            // gourd falls onto ones that have already settled and rolls off,
            // instead of dozens being born interpenetrating — which is what
            // made the game stutter to begin with.
            _restoreTimer -= Time.unscaledDeltaTime;
            if (_restoreTimer > 0f)
                return;

            _restoreTimer = Mathf.Max(0f, ModConfig.CosmeticGourdRestoreInterval.Value);

            // A failed spawn is NOT counted. The usual cause is that the
            // hub's InventorySpawn is not loaded yet, which resolves itself
            // a moment later, so this returns and tries again rather than
            // recording a gourd the player never got.
            if (!ItemApplier.ApplyGourdItem())
            {
                if (!_looseRestoreBlockedLogged)
                {
                    _looseRestoreBlockedLogged = true;
                    Plugin.Log.LogInfo(
                        $"[{nameof(ApRuntime)}] Cannot spawn the {owed} owed gourd(s) yet (the hub's spawn point is "
                        + "probably not loaded); still trying.");
                }

                return;
            }

            _gourdsSpawnedThisSession++;
            _looseGourdsRestoredCount++;
        }

        private static void DrainIncomingItems()
        {
            var appliedThisFrame = 0;

            while (appliedThisFrame < MaxItemsPerFrame && Connection.TryDequeueItem(out var item))
            {
                _seenItemCount++;

                // Counted even for items about to be skipped: this tally is
                // what rebuilds the gourd ledger in RestoreLooseGourds, and
                // for that it has to reflect the slot's whole history, not
                // just what is new to this save.
                var isGourd = item.ItemId == ApLocationIds.GourdItemId;
                if (isGourd)
                    _gourdsSeenThisSession++;

                // Already materialized into this save by an earlier session:
                // this is the server's replay, not a new item. Skipping it is
                // the entire reason ApItemCursor exists. Skipped items are
                // free, so they do not count against the per-frame budget.
                if (_seenItemCount <= _appliedItemCount)
                    continue;

                // Raised before the item is applied, deliberately: a gourd
                // that was granted is owed to the player whether or not the
                // prop actually spawned. Next session's reconciliation makes
                // good on it.
                if (isGourd)
                    ApItemCursor.CountGourdReceived();

                try
                {
                    var materialized = Apply(item);
                    if (isGourd && materialized)
                        _gourdsSpawnedThisSession++;
                    else if (isGourd)
                        // The prop did not appear (hub not loaded yet, most
                        // likely). Re-arm the reconciler so it puts this
                        // gourd back rather than leaving the player short.
                        _looseGourdsRestored = false;
                }
                catch (Exception ex)
                {
                    // The item is already off the queue, so the cursor still
                    // advances: stalling here would wedge every later item
                    // behind this one forever. Recovering a genuinely lost
                    // item is what the new-save replay path is for.
                    Plugin.Log.LogError(
                        $"[{nameof(ApRuntime)}] Failed to apply item {item.ItemId}: {ex}");
                }

                appliedThisFrame++;
                _appliedItemCount = _seenItemCount;
                ApItemCursor.Set(_appliedItemCount);
            }
        }

        // Returns whether the item actually materialized in the world, which
        // is what the gourd reconciliation counts.
        private static bool Apply(Archipelago.MultiClient.Net.Models.ItemInfo item)
        {
            var itemId = item.ItemId;

            if (itemId == ApLocationIds.GourdItemId)
                return ItemApplier.ApplyGourdItem();

            if (ApLocationIds.TryResolveBigKeyItem(itemId, out var propName))
                return ItemApplier.ApplyBigKeyItem(propName);

            // Filler, traps and anything a newer apworld invents: no effect,
            // by design. Logged rather than silent so a genuinely unhandled
            // item is visible in the log instead of looking like a bug in the
            // multiworld.
            Plugin.Log.LogInfo($"[{nameof(ApRuntime)}] Received '{item.ItemName}' ({itemId}): no in-game effect.");
            return false;
        }

        // Monument deposits are counted across every monument together, never
        // per tower — that global count is what makes it impossible for a bad
        // distribution of gourds to lock a seed (Option A, see
        // apworld/design-decisions.md).
        //
        // Reports thresholds crossed, and never un-reports: the count CAN go
        // down in principle, and a check that has been sent stays sent.
        private static void ReportDeposits()
        {
            var amounts = Connection.SlotData.DepositLocationAmounts;
            if (amounts.Length == 0)
                return;

            var filled = CosmeticMonumentFillTracker.GetFilledMonumentCount();
            if (filled <= _lastReportedDepositCount)
                return;

            _lastReportedDepositCount = filled;
            SendBuffer.Clear();

            foreach (var amount in amounts)
            {
                if (amount > filled)
                    break;

                var id = ApLocationIds.DepositLocationId(amount);
                if (Connection.BelongsToSlot(id))
                    SendBuffer.Add(id);
            }

            if (SendBuffer.Count > 0)
                Connection.SendChecks(SendBuffer.ToArray());
        }

        private static void CheckGoal()
        {
            if (_goalSent || !IsGoalReached())
                return;

            _goalSent = true;
            Connection.SendGoal();
        }

        private static bool IsGoalReached()
        {
            switch (Connection.SlotData.Goal)
            {
                case "gauntlet":
                    return ApGoalFlags.IsLatched(SavableSystem.GauntletComplete);

                case "ending":
                    return ApGoalFlags.IsLatched(SavableSystem.EndingGate);

                case "deposits":
                    var target = Connection.SlotData.DepositGoalAmount;
                    return target > 0 && CosmeticMonumentFillTracker.GetFilledMonumentCount() >= target;

                default:
                    // Unreachable: OnJustConnected already refused to arm
                    // goal detection for a goal this build does not know.
                    return false;
            }
        }
    }
}
