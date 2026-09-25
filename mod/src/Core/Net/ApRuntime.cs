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

        // Loose-gadget reconciliation (see RestoreLooseGadgets) — same
        // shape as the gourd fields just above, minus a deposit sink: a
        // filler gadget has nowhere to be "spent", so what is owed is
        // simply "received minus spawned this session" per kind, forever,
        // never reduced by anything the player does with the ones already
        // out there. Player request, 2026-09-22: "you should keep them,
        // and spawn them if restarting a room, as other gourds and keys
        // stuff."
        private static readonly Dictionary<GadgetKind, int> _gadgetsSpawnedThisSession = new();
        private static bool _looseGadgetsRestored;
        private static bool _looseGadgetsRestoreBlockedLogged;
        private static float _gadgetRestoreTimer;

        private static int _lastReportedDepositCount = -1;

        // Deposits counted on the last poll tick, kept because
        // CosmeticMonumentFillTracker.GetFilledMonumentCount scans every
        // entry in the save and three callers now want the number — the
        // deposit checks, the goal, and the line on screen, which OnGUI
        // would otherwise ask for several times a frame.
        private static int _depositCount;

        private static bool _goalSent;

        // Whether this build knows how to detect the slot's goal at all.
        // Split out from _goalSent, which used to carry both meanings by
        // being pre-set to true for an unknown goal: harmless while nothing
        // read it, a lie the moment the overlay started saying "reached".
        private static bool _goalDetectable;
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

        // What this slot is playing for, under the connection line. Player
        // request (2026-09-22), in two steps: first the deposit count, which
        // is the one goal with no moment of arrival to announce and so has
        // to say where you are all of the time — then every other goal too,
        // because "what am I even trying to do here" is a question a seed
        // handed to a co-op group two weeks ago cannot answer by itself.
        internal static string GoalLine { get; private set; }

        // True only for a goal this build cannot detect, which is worth the
        // warning colour: the seed is playable and its goal will never be
        // reported. It was a log warning nobody reads until now.
        internal static bool GoalLineIsWarning { get; private set; }

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

        // Puts the player's gourds back within reach, without quitting.
        //
        // Sweeps away every cosmetic gourd that is not in a monument and
        // re-arms the reconciliation, which then restocks the hub with
        // exactly what the ledger says is owed. Monument deposits are
        // untouched, so nothing the Archipelago logic counts can be lost
        // by pressing this.
        //
        // Exists because a gourd can become unreachable while the world
        // stays loaded — the case the player raised: received inside a
        // sealed puzzle room, which the game will not let you carry it out
        // of. A world reload already fixes that; this makes the same fix
        // available without one, and covers any future placement mishap
        // the same way.
        // Ctrl+R also brings back a stranded key, at the player's request:
        // same promise as for a gourd, since a key already in its plinth is
        // a check already sent and is left exactly where it is.
        internal static void ResyncKeys()
        {
            var moved = KeyCustody.ResyncToSpawn();
            if (moved <= 0)
                return;

            Plugin.Log.LogInfo($"[{nameof(ApRuntime)}] {moved} big key(s) sent back to the spawn point.");
            ApNotices.Post($"Resync: {moved} key(s) sent back to the spawn point");
        }

        internal static void ResyncGourds()
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogInfo($"[{nameof(ApRuntime)}] Gourd resync ignored: only the host can do this.");
                return;
            }

            // Refused while disconnected, and this is not caution for its
            // own sake: the rebuild only runs on a live connection, so
            // sweeping the gourds away now would take them and give nothing
            // back until the server returned.
            if (Connection.Status != ApConnection.ConnectionStatus.Connected)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApRuntime)}] Gourd resync refused: not connected to Archipelago, so the gourds could not be put back. Try again once reconnected.");
                ApNotices.Post("Resync refused: not connected to Archipelago", warning: true);
                return;
            }

            var removed = ReceivedItemSpawner.DestroyLooseCosmeticGourds(out var keptGourds);

            _looseGourdsRestored = false;
            _looseRestoreBlockedLogged = false;
            _reconciliationLogged = false;
            _looseGourdsRestoredCount = 0;

            // The gourds left in backpacks, cartons and belts still exist, so
            // they are already spawned as far as the ledger is concerned.
            // Zero here would restock every one of them a second time.
            _gourdsSpawnedThisSession = keptGourds;
            _restoreTimer = 0f;

            Plugin.Log.LogInfo(
                $"[{nameof(ApRuntime)}] Gourd resync: {removed} loose gourd(s) cleared and {keptGourds} left where "
                + "they were stowed, restocking the hub from the ledger.");
            ApNotices.Post($"Resync: {removed} gourd(s) cleared, restocking the hub");
        }

        // The same sweep for the filler gadgets, at the player's request
        // (2026-09-22): "it should also reset the inventories and the
        // tracked objects — that avoids duplication and stuck objects too."
        //
        // It has to reset the per-session tally as well as destroy the
        // props, because that tally is half of what RestoreLooseGadgets owes
        // ("received minus spawned this session"). Sweeping without
        // resetting would put nothing back; resetting without sweeping would
        // spawn a second copy of everything already lying around. The two
        // belong in one action, which is why this is not two methods.
        //
        // Held gadgets are dropped by the sweep rather than left in someone's
        // hands: a prop destroyed out of a pair of hands leaves those hands
        // believing they still hold it, which is exactly the stuck-object
        // case this is meant to end.
        internal static void ResyncGadgets()
        {
            if (!NetworkServer.active || Connection.Status != ApConnection.ConnectionStatus.Connected)
                return;

            var removed = GadgetItemSpawner.DestroyLooseCosmeticGadgets(out var keptGadgets);

            // Same reasoning as the gourds: what the sweep spared is still in
            // the world, and the tally is half of what the restock owes.
            _gadgetsSpawnedThisSession.Clear();
            foreach (var pair in keptGadgets)
                _gadgetsSpawnedThisSession[pair.Key] = pair.Value;
            _looseGadgetsRestored = false;
            _looseGadgetsRestoreBlockedLogged = false;
            _gadgetRestoreTimer = 0f;

            Plugin.Log.LogInfo(
                $"[{nameof(ApRuntime)}] Gadget resync: {removed} loose gadget(s) cleared, restocking from the ledger.");
            ApNotices.Post($"Resync: {removed} item(s) cleared, restocking from the ledger");
        }

        private static void RefreshStatusMessage()
        {
            // Nothing to say when Archipelago is switched off, and nothing
            // to say to a player who is not the host: their client never
            // connects by design, so an "disconnected" warning would be a
            // lie.
            // Nothing to say to a player who is not the host: their client
            // never connects by design, so any of this would be a lie.
            if (!NetworkServer.active)
            {
                StatusMessage = null;
                GoalLine = null;
                return;
            }

            RefreshGoalLine();

            // Switched off says so, rather than showing nothing. Silence and
            // "connected" used to look identical, and the switch now lives on
            // the hosting screen where it is easy to leave off — and it
            // persists into the next launch, so a whole session can be played
            // disconnected with no sign of it anywhere but the log. Seen
            // happening on 2026-09-22.
            if (!ModConfig.ArchipelagoEnabled.Value)
            {
                StatusMessage = "Archipelago: off";
                StatusIsWarning = false;
                return;
            }

            switch (Connection.Status)
            {
                case ApConnection.ConnectionStatus.Connecting:
                    StatusMessage = "Archipelago: connecting...";
                    StatusIsWarning = false;
                    break;

                case ApConnection.ConnectionStatus.Connected:
                    // Shown for the whole session, not just for a few
                    // seconds after connecting — player request, 2026-09-22.
                    // A connection that heals itself silently is exactly the
                    // one worth being able to confirm at a glance, and the
                    // line is where the feed and the resync hint hang from.
                    StatusMessage = "Archipelago: connected";
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

        // Reads the count cached on the poll tick, never the tracker itself:
        // this runs once a frame and the tracker walks the whole save.
        private static void RefreshGoalLine()
        {
            // SlotData IS NULL until a login has succeeded, and this runs from
            // the frame the player starts hosting — unlike every other reader
            // of it, all of which are downstream of a connection. Hosting
            // before the room answers therefore threw here on every frame,
            // which is how it was found (in game, 2026-09-22): the overlay
            // never drew and the log filled with trampoline exceptions.
            var slotData = Connection.SlotData;
            if (slotData == null || !ModConfig.ArchipelagoEnabled.Value)
            {
                GoalLine = null;
                return;
            }

            string goal;
            switch (slotData.Goal)
            {
                case "gauntlet":
                    goal = "Big Goodbye, complete the Silent Gauntlet behind the Big Wall";
                    break;

                case "ending":
                    goal = "Big Wall, break the bell inside the Big Wall";
                    break;

                case "second_ending":
                    goal = "Big Game, reach the secret ending behind the Spawn Secret Door";
                    break;

                case "deposits":
                    var target = slotData.DepositGoalAmount;
                    goal = $"Big Collection, place {target} gourds in the towers' slots ({_depositCount}/{target})";
                    break;

                default:
                    // Said out loud rather than left in the log. The seed is
                    // playable and every check works; it is only the goal
                    // that will never be reported, which is exactly the kind
                    // of thing nobody notices until the end of a run.
                    GoalLine = $"Goal: {slotData.Goal} - this build cannot detect it";
                    GoalLineIsWarning = true;
                    return;
            }

            // "reached" means REPORTED, not merely met. For the deposit goal
            // the count already says whether it is met, and for the other
            // three there is nothing else on screen to confirm the server was
            // ever told — which is the half that can fail on its own.
            GoalLine = _goalSent ? $"Goal: {goal} - reached" : $"Goal: {goal}";
            GoalLineIsWarning = false;
        }

        private void Update()
        {
            RefreshStatusMessage();

            // Once a frame, here rather than in the overlay: OnGUI runs
            // several times per frame and stays pure presentation.
            ApNotices.Prune();

            // Every frame, and ahead of the early returns: a key that has just
            // been delivered is being pulled back to its tower by the game,
            // and a poll once a second is far too coarse to hold it. Returns
            // immediately when nothing has just been delivered, which is all
            // of the time.
            KeyCustody.SettleDelivered();

            // Read before the early returns below so the key always gets
            // an answer in the log, even when the mod is in a state where
            // it will decline to act.
            if (ModConfig.ResyncGourdsKey.Value.IsDown())
            {
                ResyncGourds();
                ResyncKeys();
                ResyncGadgets();
            }

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

            // The goal flags baseline themselves from the writes the game
            // makes WHILE a world loads, so the moment to forget the old
            // world's baseline is when it goes away, not when the next one
            // is ready — by then the writes that matter have already been
            // seen (cf. ApGoalFlags).
            if (!worldReady && _worldWasReady)
            {
                ApGoalFlags.OnWorldUnloaded();

                // The feed reports on the world that produced it. Carrying
                // "Check: Cabin Fever" back to the main menu would be
                // reporting on a game that is no longer running.
                ApNotices.Clear();
            }

            _worldWasReady = worldReady;

            // A gourd spawned last tick and meant for the player's hands is
            // handed over now, a frame after its spawn went out on the wire
            // (cf. ReceivedItemSpawner.DrainPendingHandover).
            ReceivedItemSpawner.DrainPendingHandover();

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
                RestoreLooseGadgets();
            }

            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f)
                return;

            _pollTimer = PollIntervalSeconds;

            if (WorldManager.isReadyForEffects)
            {
                // Once per tick, for the three things that want it.
                _depositCount = CosmeticMonumentFillTracker.GetFilledMonumentCount();

                ReportDeposits();

                // Both return immediately once nothing is pending, which is
                // the case for all but the first seconds of a world.
                RadioStations.TickPending();
                KeyFeatures.TickPending();

                // Not "pending" like the other two: this one also holds the
                // lock on every key the slot has not been given, so it has
                // work to do for as long as the session lasts.
                KeyCustody.Tick();
            }

            // Outside the world gate, unlike everything above it: every goal
            // here is decided from latched state and a cached count, and an
            // ending tears its world down as it plays. The patch on it
            // reports the goal itself (Patches/EndingStartPatch.cs) — this is
            // the second chance, for the frames where the world is already
            // gone and the socket is not.
            CheckGoal();
        }

        // A freshly loaded world contains none of the loose gourds the
        // previous one did, so everything tracking "what exists right now"
        // starts over. The ledger itself (ap_gourds_received) is untouched:
        // it counts what the player was given, which a reload does not
        // change.
        private static void OnWorldBecameReady()
        {
            // The radio unlock lives in RAM only (see Core/RadioStations.cs),
            // so a granted station has to be put back into every new world —
            // exactly like the loose gourds below, and for the same reason.
            RadioStations.RearmFromLedger();

            // And the doors, for a reason that is worth stating because it is
            // not the same one: the game's persistence for these features IS
            // the key sitting in its plinth, which this world stops happening.
            // With the key inert, nothing else carries a door across a reload
            // (see Core/KeyFeatures.cs).
            KeyFeatures.RearmFromLedger();
            KeyCustody.RearmFromLedger();

            _looseGourdsRestored = false;
            _looseRestoreBlockedLogged = false;
            _reconciliationLogged = false;
            _looseGourdsRestoredCount = 0;
            _gourdsSpawnedThisSession = 0;
            _restoreTimer = 0f;

            // Same reasoning, for the filler gadgets: none of the ones
            // spawned in the previous world survive this one, so the
            // reconciliation has to run again from a clean "0 spawned this
            // session" for every kind.
            _gadgetsSpawnedThisSession.Clear();
            _looseGadgetsRestored = false;
            _looseGadgetsRestoreBlockedLogged = false;
            _gadgetRestoreTimer = 0f;
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

            // Read before the cursor below, because the patch that suppresses
            // the game's own radio unlock consults it on every peck and a
            // world can already be loaded when this runs.
            RadioStations.Configure(Connection.SlotData.RadioStationItems);

            // Read here for the same reason, and it matters more: the patch
            // that stops a key opening its door consults this on every pin.
            KeyFeatures.Configure(Connection.SlotData.BigKeyFeatures);
            KeyCustody.Configure(Connection.SlotData.BigKeyItems);

            _seenItemCount = 0;
            _appliedItemCount = ApItemCursor.SyncTo(Connection.SeedName, Connection.SlotName);

            // A cursor back at zero means this save has just been bound to a
            // different (seed, slot) and everything is about to replay. The
            // stations it was granted under the old binding go with it —
            // otherwise the save keeps music the new seed has not given it.
            if (_appliedItemCount == 0)
            {
                RadioStations.ClearLedger();
                KeyFeatures.ClearLedger();
                KeyCustody.ClearLedger();
            }

            _lastReportedDepositCount = -1;
            _depositCount = 0;
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
            _goalDetectable = goal is "gauntlet" or "ending" or "deposits" or "second_ending";
            _goalSent = !_goalDetectable;
            if (!_goalDetectable)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApRuntime)}] This slot's goal is '{goal}', which this version of the mod cannot detect. "
                    + "Goal completion will NOT be reported automatically — update the mod to match the apworld.");
            }

            Plugin.Log.LogInfo(
                $"[{nameof(ApRuntime)}] Connected. {Connection.SlotData.Describe()}. "
                + $"{_appliedItemCount} item(s) already applied to this save.");

            // ResendKnownChecks FIRST, and that order is not cosmetic. On
            // 2026-09-21 RearmFromLedger threw out of here — FmRadioManager's
            // instance property raises rather than returning null when no
            // world is loaded, which is the normal state at connection time —
            // and the exception crossed the IL2CPP trampoline, taking the rest
            // of this method with it. The checks this save had already
            // validated were never resent, silently. The throw is fixed at
            // source, and the resend is no longer behind anything that could
            // reintroduce it.
            ResendKnownChecks();

            // Covers connecting into a world that is already loaded: the
            // stations and doors already applied to this save are replayed by
            // the server but skipped by the cursor, so this is the only thing
            // that would ever start their music, or open them, again.
            RadioStations.RearmFromLedger();
            KeyFeatures.RearmFromLedger();
            KeyCustody.RearmFromLedger();
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
                // can be off, deposit checks thinned out. Sending an id
                // this slot does not have is a protocol error, so it is
                // filtered here rather than hoping the server is forgiving.
                if (!Connection.BelongsToSlot(id))
                    continue;

                SendBuffer.Add(id);
            }

            if (SendBuffer.Count == 0)
                return;

            Connection.SendChecks(SendBuffer.ToArray());

            // Only the checks flushed here, never the set ResendKnownChecks
            // pushes on every connect: those are already-known checks being
            // repeated for the server's benefit, and announcing them would
            // replay the player's whole run at them on each reconnection.
            foreach (var id in SendBuffer)
                ApNotices.Post($"Check: {Connection.LocationName(id)}");
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
            // toPlayer: false — this is the session-start restock, which
            // belongs at the hub rather than raining on whoever just
            // loaded in, wherever they happen to be standing.
            if (!ItemApplier.ApplyGourdItem(toPlayer: false))
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

        // Same reconciliation as RestoreLooseGourds, minus the deposit
        // subtraction: a filler gadget has nowhere to be spent, so "owed"
        // is simply the received ledger minus what this session has already
        // spawned, per kind. Player request, 2026-09-22 ("keep them, and
        // spawn them if restarting a room, as other gourds and keys
        // stuff"). Paced the same way and reusing the same interval config
        // — this is cosmetic filler, not worth a setting of its own.
        private static void RestoreLooseGadgets()
        {
            if (_looseGadgetsRestored || Connection.HasPendingItems)
                return;

            if (Time.unscaledTime - _connectedAt < ReplayGraceSeconds)
                return;

            _gadgetRestoreTimer -= Time.unscaledDeltaTime;
            if (_gadgetRestoreTimer > 0f)
                return;

            GadgetKind? next = null;
            var allSettled = true;

            foreach (GadgetKind kind in Enum.GetValues(typeof(GadgetKind)))
            {
                var spawnedThisSession = _gadgetsSpawnedThisSession.TryGetValue(kind, out var n) ? n : 0;
                var owed = ApItemCursor.GadgetsReceived(kind) - spawnedThisSession;
                if (owed <= 0)
                    continue;

                allSettled = false;
                next = kind;
                break;
            }

            if (allSettled)
            {
                _looseGadgetsRestored = true;
                return;
            }

            _gadgetRestoreTimer = Mathf.Max(0f, ModConfig.CosmeticGourdRestoreInterval.Value);

            // toPlayer: false, same reasoning as the gourd restock — the
            // session-start catch-up belongs at the hub, not raining on
            // whoever just loaded in wherever they happen to be standing.
            if (!ItemApplier.ApplyGadgetItem(next.Value, toPlayer: false))
            {
                if (!_looseGadgetsRestoreBlockedLogged)
                {
                    _looseGadgetsRestoreBlockedLogged = true;
                    Plugin.Log.LogInfo(
                        $"[{nameof(ApRuntime)}] Cannot restore the owed {next.Value} yet (the hub's spawn point is "
                        + "probably not loaded); still trying.");
                }

                return;
            }

            _gadgetsSpawnedThisSession[next.Value] =
                (_gadgetsSpawnedThisSession.TryGetValue(next.Value, out var count) ? count : 0) + 1;
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

                // Same "what is this item" question as isGourd above, for
                // the filler gadgets — resolved here too (not just inside
                // Apply) because the received ledger has to be counted
                // exactly once per item, on the same "not a replay" gate
                // the gourd counter uses.
                var isGadget = ApLocationIds.TryResolveGadgetItem(item.ItemId, out var gadgetKind);

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
                else if (isGadget)
                    ApItemCursor.CountGadgetReceived(gadgetKind);

                // Past the replay gate, so this only ever announces an item
                // that is new to this save. A fresh connection to a slot
                // already owed forty gourds still posts forty of them in one
                // frame, which is why ApNotices collapses repeats.
                ApNotices.Post($"Received: {item.ItemName}");

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
                    else if (isGadget && materialized)
                        _gadgetsSpawnedThisSession[gadgetKind] =
                            (_gadgetsSpawnedThisSession.TryGetValue(gadgetKind, out var count) ? count : 0) + 1;
                    else if (isGadget)
                        // Same reasoning as the gourd branch: the item is
                        // owed regardless of whether the spawn landed, so
                        // re-arm the reconciler to make good on it.
                        _looseGadgetsRestored = false;
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
                // Into the hands only once the session has settled. The
                // startup burst — everything the server replays on connect,
                // plus anything earned while this save was offline — belongs
                // at the spawn like the reconciliation's own restock: the
                // players walk through there on every launch anyway, and
                // filling someone's hands during a loading screen is both
                // startling and, with two players, a good way to have one of
                // them holding something before they have control.
                //
                // _looseGourdsRestored is precisely that line: the
                // reconciliation refuses to run while Connection.HasPendingItems,
                // so it only latches once the whole burst is through.
                // Anything applied after it is genuinely live.
                return ItemApplier.ApplyGourdItem(toPlayer: _looseGourdsRestored);

            // The KEY before the DOOR, because the two ranges are one offset
            // apart and a key id would otherwise never be reached. Same
            // settled flag as the gourd above: the two arrive in the same
            // startup burst and belong at the same place during it.
            if (ApLocationIds.TryResolveKeyItem(itemId, out var keyName))
                return KeyCustody.Grant(keyName, toPlayer: _looseGourdsRestored);

            if (ApLocationIds.TryResolveBigKeyItem(itemId, out var propName))
                return ItemApplier.ApplyBigKeyItem(propName);

            // Checked before the big key would ever see it: the two ranges do
            // not overlap, but a Broadcast is the one item whose effect lives
            // entirely outside SaveManager's prop keys, so it gets its own
            // resolver rather than a special case inside ItemApplier.
            if (ApLocationIds.TryResolveRadioItem(itemId, out var station))
                return RadioStations.Grant(station);

            // A filler gadget item (megaphone, walkie-talkie, backpack,
            // belt, flare gun) — same toPlayer split as the gourd above,
            // and for the same reason: the startup replay belongs at the
            // hub, a live arrival belongs with the player. Its own settled
            // flag, not the gourd one: the two reconciliations run
            // independently and can finish at different times.
            if (ApLocationIds.TryResolveGadgetItem(itemId, out var gadgetKind))
                return ItemApplier.ApplyGadgetItem(gadgetKind, toPlayer: _looseGadgetsRestored);

            // Traps and anything a newer apworld invents: no effect, by
            // design. Logged rather than silent so a genuinely unhandled
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

            var filled = _depositCount;
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

        // Called from Patches/GourdStatePatch, on the frame the second
        // ending's gourd comes loose. Everything the goals read is latched or
        // cached, so this needs no world and no tick — only a socket, and
        // SendGoal already survives not having one.
        internal static void ReportGoalIfReached()
        {
            CheckGoal();
        }

        private static void CheckGoal()
        {
            if (_goalSent || !IsGoalReached())
                return;

            // Marked sent only once the socket took it. A goal can now be
            // reached on a frame where the connection is down — an ending
            // transition reports itself while the world dies around it — and
            // every goal is latched or cached, so leaving it armed costs
            // nothing and the next tick or the next connection reports it.
            if (Connection.SendGoal())
                _goalSent = true;
        }

        private static bool IsGoalReached()
        {
            // Guarded for the same reason RefreshGoalProgress is, and for one
            // more: ReportGoalIfReached is called from a Harmony patch on the
            // game's ending, which fires whether or not this session ever
            // connected to anything.
            if (Connection.SlotData == null)
                return false;

            switch (Connection.SlotData.Goal)
            {
                case "gauntlet":
                    return ApGoalFlags.IsLatched(SavableSystem.GauntletComplete);

                case "ending":
                    return ApGoalFlags.IsLatched(SavableSystem.EndingGate);

                case "deposits":
                    var target = Connection.SlotData.DepositGoalAmount;
                    return target > 0 && _depositCount >= target;

                case "second_ending":
                    return ApSecondEnding.Reached;

                default:
                    // Unreachable: OnJustConnected already refused to arm
                    // goal detection for a goal this build does not know.
                    return false;
            }
        }
    }
}
