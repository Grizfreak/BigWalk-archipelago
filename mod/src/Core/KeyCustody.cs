using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The seven big keys themselves, as Archipelago items.
    //
    // WHY THIS EXISTS. The first pass of the 2026-09-21 design left the keys
    // exactly as the vanilla game hands them out — fill a tower's monument
    // and its key is released — and made only the FEATURE an item. The player
    // corrected that: a key should arrive from the multiworld and spawn like
    // a gourd, not be earned by depositing gourds. So the monument now buys
    // nothing but its own deposit checks, and a key is something another
    // player sends you.
    //
    // What a key is FOR is unchanged, and that is the point of keeping both
    // halves: it is a check carrier. Cutting its five segments and placing it
    // in its receptacle are six checks, and nothing else. The door is still
    // its own item (Core/KeyFeatures.cs), so receiving a key never opens
    // anything by itself.
    //
    // WHAT HOLDS A KEY BACK (measured in game 2026-09-21, Ctrl+K). Every one
    // of the seven sits in a `PropHome` — six in a `KeyStoneHome`, and each
    // reports `blockGrabbing = true`. That single bool is the lock, and the
    // game's own release is a `PeckEffectPropHomeSettings` whose
    // `settingsPerState[2]` writes `blockGrabbing := false`, driven by a
    // TrackedPeckState whose label spells the mechanism out:
    // `0 - locked, 1 - animating, 2 - grabbable`, on `KeyScrewLogic`.
    //
    // So both halves of this class write the same field, in opposite
    // directions, and neither needs a Harmony patch — which matters, because
    // `PeckEffectPropHomeSettings.Apply` and `OnPeck` have NO code address in
    // the export while `Awake` does. They are inlined, so a patch on them
    // would bind to nothing and suppress nothing, in silence: the trap
    // `ServerCutSegment` and `BroadcastStation.Unlock` have already sprung
    // twice on this project.
    //
    // Re-asserting rather than intercepting also means this is robust to not
    // knowing what frees the other six keys. Only `bigKeyOverflow`'s releaser
    // was found in the dump — the rest are most likely streamed out at 300m
    // to 900m — and it does not matter: whatever flips the bool, this flips
    // it back until the item arrives.
    internal static class KeyCustody
    {
        // Same reasoning as CheckTracker's "ap_reported_", RadioStations'
        // "ap_radio_" and KeyFeatures' "ap_feature_": a prefix the game's own
        // enum names can never produce, so SaveValuePatch never mistakes one
        // of these writes for a key being placed.
        private const string KeyPrefix = "ap_key_";

        // Set from slot_data on connection. While false the game is left
        // completely alone — monuments release their keys exactly as in the
        // vanilla game. That covers an apworld too old to send the field, and
        // a session with Archipelago disabled entirely; in both, locking the
        // keys away would leave them unobtainable, since nothing would ever
        // send one.
        internal static bool KeysAreItems { get; private set; }

        // Keys granted but not yet delivered to the player — a world that is
        // still loading has no spawn point and no player to put one near.
        private static readonly HashSet<SaveablePropName> Pending = new();

        private static bool _pendingBlockedLogged;

        // A key delivered to the spawn point, and where it was put, kept until
        // it has been looked at again a couple of seconds later.
        //
        // Observed on 2026-09-21: the key appeared at the hub and vanished
        // instantly. `ServerSetUnpinned` did not throw and the move reported
        // success, so what happened is something taking it BACK — and from in
        // front of the screen, "deactivated", "re-pinned to its stone",
        // "unspawned by Mirror" and "culled by the game's own region system"
        // all look exactly alike. The same symptom, with the same ambiguity,
        // already cost this project a day over the cosmetic gourd, which is why
        // DebugHotkeys has a delayed recheck of its own; this is that pattern,
        // moved next to the thing it is diagnosing.
        private static readonly List<(SaveablePropName key, Vector3 target, float at, int corrections)> Delivered = new();

        private const float RecheckDelaySeconds = 2f;

        // Far enough that it cannot be the key simply falling to the ground —
        // the drop from the spawn point measured 1.3m — and near enough to
        // catch a launch long before it is out of sight.
        private const float StrayMetres = 5f;

        internal static void Configure(bool keysAreItems)
        {
            if (KeysAreItems == keysAreItems)
                return;

            KeysAreItems = keysAreItems;
            Plugin.Log.LogInfo(
                $"[{nameof(KeyCustody)}] Big keys are "
                + (keysAreItems
                    ? "Archipelago items: filling a monument no longer releases one."
                    : "left vanilla: a full monument releases its own key."));
        }

        internal static bool IsGranted(SaveablePropName propName)
        {
            return SaveManager.GetIntValue(KeyPrefix + propName, 0, false) != 0;
        }

        // A key item arrived. Persisted first, delivered second: the ledger is
        // what survives the world reload, and the key lying at the hub is only
        // this session's copy of it.
        internal static bool Grant(SaveablePropName propName)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyCustody)}] Ignored: {propName} received while not host.");
                return false;
            }

            if (!GourdRegistry.IsBigKey(propName))
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyCustody)}] {propName} is not a big key; item ignored.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + propName, 1);
            Pending.Add(propName);

            var delivered = TickPending(toPlayer: true);
            Plugin.Log.LogInfo(
                $"[{nameof(KeyCustody)}] {propName} granted"
                + (delivered ? " and delivered." : "; it will be delivered once the world is ready."));

            return true;
        }

        // A new (seed, slot) binding replays every item from scratch, so the
        // keys this save was granted under the old one go with it. Mirrors
        // RadioStations and KeyFeatures.
        //
        // What this cannot undo is a key already lying in the world. Tick()
        // re-locks its home, so it cannot be taken again after a reload, and
        // the honest repair for one already in a player's hands is the same
        // as for a gourd: Ctrl+R.
        internal static void ClearLedger()
        {
            var cleared = 0;
            foreach (var propName in GourdRegistry.BigKeys)
            {
                if (!IsGranted(propName))
                    continue;

                SaveManager.SetIntValue(KeyPrefix + propName, 0);
                cleared++;
            }

            Pending.Clear();
            _pendingBlockedLogged = false;

            if (cleared > 0)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(KeyCustody)}] {cleared} key grant(s) cleared: this save is being replayed against "
                    + "a different seed or slot.");
            }
        }

        // Queues every key this save was granted, for delivery into a world
        // that has just appeared. Unlike the radio and the doors, a key that
        // is already lying about does NOT need re-delivering — the game
        // persists a prop's position and its home by itself. So this only
        // queues the ones still sitting in their stone.
        internal static void RearmFromLedger()
        {
            Pending.Clear();
            _pendingBlockedLogged = false;

            // A new world rebuilds every renderer, so the keys need painting
            // again. Done here rather than in a world-load handler of its own
            // because this already runs on exactly the two occasions that
            // matter: a world becoming ready, and a connection into one that
            // is already loaded.
            KeyColours.Rearm();

            if (!KeysAreItems)
                return;

            foreach (var propName in GourdRegistry.BigKeys)
            {
                if (IsGranted(propName) && IsStillInItsStone(propName))
                    Pending.Add(propName);
            }

            if (Pending.Count > 0)
                TickPending(toPlayer: false);
        }

        // Called from ApRuntime's poll tick. Two jobs, and the first one runs
        // every tick for as long as a session lasts: hold the lock on every
        // key this slot has not been given.
        internal static void Tick()
        {
            if (!KeysAreItems || !NetworkServer.active)
                return;

            EnforceCustody();
            TickPending(toPlayer: false);
            KeyColours.PaintOnce();
        }

        // The suppression, and the release, in one pass — they are the same
        // field written both ways.
        //
        // Deliberately keyed on `startHome`, never on `currentHome`. A key's
        // current home becomes its PLINTH once the players carry it there,
        // and writing `blockGrabbing` on a plinth would lock a key into its
        // socket, or free something that should not be free. `startHome` is
        // the stone it came out of and it does not move.
        private static void EnforceCustody()
        {
            foreach (var prop in GourdRegistry.LoadedBigKeys())
            {
                var stone = prop.startHome;
                if (stone == null)
                    continue;

                // Belt and braces: a plinth is a PropHome like any other, and
                // if `startHome` ever turned out to be one, this would be the
                // line that stopped the damage.
                if (GourdRegistry.IsBigKeyPlinth(stone.saveableHomeName))
                    continue;

                var shouldBeLocked = !IsGranted(prop.saveablePropName);
                if (stone.blockGrabbing != shouldBeLocked)
                    stone.blockGrabbing = shouldBeLocked;
            }
        }

        private static bool IsStillInItsStone(SaveablePropName propName)
        {
            var prop = FindKey(propName);
            if (prop == null)
                return false;

            var home = prop.currentHome;
            return home != null && prop.startHome != null && home.Pointer == prop.startHome.Pointer;
        }

        // Returns true when nothing is left waiting.
        private static bool TickPending(bool toPlayer)
        {
            if (Pending.Count == 0)
                return true;

            // Copied before iterating: delivery runs the game's own code, and
            // nothing guarantees it leaves this set alone.
            var attempts = new List<SaveablePropName>(Pending);
            foreach (var propName in attempts)
            {
                if (!TryDeliver(propName, toPlayer))
                    continue;

                Pending.Remove(propName);
                Plugin.Log.LogInfo($"[{nameof(KeyCustody)}] {propName} is now at the spawn point.");
            }

            if (Pending.Count > 0)
            {
                LogBlockedOnce();
                return false;
            }

            _pendingBlockedLogged = false;
            return true;
        }

        private static void LogBlockedOnce()
        {
            if (_pendingBlockedLogged)
                return;

            _pendingBlockedLogged = true;
            Plugin.Log.LogInfo(
                $"[{nameof(KeyCustody)}] {Pending.Count} key(s) waiting on a world that can take them. Still trying.");
        }

        // Takes a key out of its stone and drops it where a gourd would land.
        //
        // THE UNCERTAIN STEP IS `ServerSetUnpinned`, and it is guarded on
        // purpose. The method exists by name in the 1.48 interop assembly but
        // has NO code address in the il2cpp export, while its sibling
        // `ServerSetPinned` has one — the signature of a method inlined into
        // its callers, which is how `ServerCutSegment` and
        // `BroadcastStation.Unlock` both fooled this project before. If the
        // call throws, the key is still released and still obtainable: it
        // simply stays in its stone at the tower, grabbable, which is where
        // the vanilla game would have left it anyway. That is a worse
        // experience, not a broken one, and the log says which happened.
        private static bool TryDeliver(SaveablePropName propName, bool toPlayer)
        {
            var prop = FindKey(propName);
            if (prop == null)
                return false;

            var destination = ReceivedItemSpawner.ResolveSpawnPosition(toPlayer);
            if (destination == null)
                return false;

            // Unlocked first and unconditionally, because this is the half
            // that decides whether the key can be obtained at all.
            var stone = prop.startHome;
            if (stone != null && !GourdRegistry.IsBigKeyPlinth(stone.saveableHomeName))
                stone.blockGrabbing = false;

            try
            {
                prop.ServerSetUnpinned();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyCustody)}] {propName}: Prop.ServerSetUnpinned threw ({ex.Message}). The key is "
                    + "unlocked but stays in its stone at the tower — go and fetch it there.");
                return true;
            }

            try
            {
                prop.SetLoose();
                Teleport(prop, destination.Value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyCustody)}] {propName}: could not be moved to the spawn point ({ex.Message}). "
                    + "It is unpinned and grabbable wherever it currently is.");
            }

            Delivered.Add((propName, destination.Value, Time.time + RecheckDelaySeconds, 0));
            return true;
        }

        // Ctrl+R for keys: every key this slot owns and that is not already in
        // its plinth goes back to the spawn point. Same promise as the gourd
        // resync — nothing that counts for Archipelago can be lost by pressing
        // it, because a key already placed is a check already sent and is left
        // exactly where it is.
        internal static int ResyncToSpawn()
        {
            if (!KeysAreItems || !NetworkServer.active)
                return 0;

            var moved = 0;
            foreach (var propName in GourdRegistry.BigKeys)
            {
                if (!IsGranted(propName))
                    continue;

                var prop = FindKey(propName);
                if (prop == null)
                    continue;

                var home = prop.currentHome;
                if (home != null && GourdRegistry.IsBigKeyPlinth(home.saveableHomeName))
                    continue;

                Pending.Add(propName);
                moved++;
            }

            if (moved > 0)
                TickPending(toPlayer: false);

            return moved;
        }

        // Moving a key across the island.
        //
        // Deliberately just the transform, after two wrong diagnoses. The key
        // turned up 201m away and 60m higher two seconds after delivery, still
        // active, visible and loose, which read like a Rigidbody launched by a
        // teleport — and is not: the player watched it go back to its ORIGINAL
        // spot at its tower, out of its socket. The game is fetching it home,
        // and `UnityEngine.Rigidbody` is not even referenced by this project.
        //
        // What fetches it is the open question. `PropHome.propShepherd` and
        // `PropHome.CheckShepherd(Prop)` are the named candidates.
        private static void Teleport(Prop prop, Vector3 destination)
        {
            prop.transform.position = destination;
        }

        // Holds a freshly delivered key where it was put, EVERY FRAME for a
        // couple of seconds, and counts how often it had to.
        //
        // That count is the experiment. The key returns to its original spot
        // at its tower, out of its socket — the player watched it happen —
        // so something in the game fetches it home. `PropHome.CheckShepherd`
        // calling `PropShepherd.DoShepherd(prop)` is the named candidate, and
        // Ghidra will not say who calls it: IL2CPP dispatches indirectly and
        // resolves almost no static call edges.
        //
        // ONE correction means the fetch fires once, on the unpin, and
        // putting the key back afterwards is the whole fix. MANY means
        // something holds it there continuously, and then this is a tug of
        // war that has to be won at the cause instead. The log says which,
        // and the two are indistinguishable from in front of the screen.
        internal static void SettleDelivered()
        {
            if (Delivered.Count == 0)
                return;

            for (var i = Delivered.Count - 1; i >= 0; i--)
            {
                var entry = Delivered[i];
                var prop = FindKey(entry.key);

                if (prop != null && Time.time < entry.at
                    && Vector3.Distance(prop.transform.position, entry.target) > StrayMetres)
                {
                    Teleport(prop, entry.target);
                    Delivered[i] = (entry.key, entry.target, entry.at, entry.corrections + 1);
                    continue;
                }

                if (Time.time < entry.at)
                    continue;

                Delivered.RemoveAt(i);
                Plugin.Log.LogInfo(
                    $"[{nameof(KeyCustody)}] {entry.key}: put back {entry.corrections} time(s) in "
                    + $"{RecheckDelaySeconds}s. "
                    + (entry.corrections == 0
                        ? "Nothing fetched it home."
                        : entry.corrections <= 3
                            ? "A few corrections: the game fetches it home once or twice after the "
                              + "unpin, and putting it back settles it."
                            : "Continuous: something holds it at its tower, and this is a tug of war "
                              + "that has to be won at the cause."));
                Report(entry.key, entry.target);
            }
        }

        private static void Report(SaveablePropName propName, Vector3 target)
        {
            var prop = FindKey(propName);
            if (prop == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(KeyCustody)}] {propName} {RecheckDelaySeconds}s later: GONE from Prop.allProps "
                    + "entirely \u2014 destroyed, not merely hidden.");
                return;
            }

            var go = prop.gameObject;
            var home = prop.currentHome;
            var homeName = home != null ? $"'{home.name}' ({home.saveableHomeName})" : "<none, still loose>";

            var netId = 0u;
            try
            {
                netId = prop.netId;
            }
            catch (Exception)
            {
                // A prop whose NetworkIdentity has gone is exactly one of the
                // outcomes being told apart here, so this is data, not noise.
            }

            var renderer = go.GetComponentInChildren<Renderer>(true);
            var visible = renderer != null
                ? $"renderer.enabled={renderer.enabled} isVisible={renderer.isVisible}"
                : "no renderer";


            Plugin.Log.LogInfo(
                $"[{nameof(KeyCustody)}] {propName} {RecheckDelaySeconds}s later: "
                + $"activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} netId={netId} | "
                + $"home {homeName} | put at {target}, now at {prop.transform.position} "
                + $"({Vector3.Distance(target, prop.transform.position):F1}m away) | {visible}");
        }

        private static Prop FindKey(SaveablePropName propName)
        {
            // Through GourdRegistry, because `Prop.allProps` throws when there
            // is no world — which is the normal state at connection time, and
            // where this was caught coming out of RearmFromLedger.
            return GourdRegistry.FindProp(propName);
        }
    }
}
