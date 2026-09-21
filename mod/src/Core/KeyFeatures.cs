using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The seven things a big key opens — the map room, the chairlift, the
    // train, the tunnels, the drawbridge, the dam and the Green Dome — as
    // Archipelago items.
    //
    // WHY THIS EXISTS. One physical act used to carry three roles at once:
    // pinning a key in its plinth was the location, the effect of the item,
    // and what opened the door. That made the key item redundant — the
    // monument released the key locally as soon as the gourds were there, so
    // the door opened whether or not Archipelago ever sent anything — and it
    // made the location one the mod had to report on the player's behalf,
    // because applying the item consumed the plinth. The three are now split
    // (see ../../../apworld/design-decisions.md, "big keys: forage checks"):
    // the 25 cut segments and the 7 placements are locations, the key is an
    // inert check carrier, and the FEATURE is the item this class grants.
    //
    // WHAT OPENS A DOOR — confirmed in game on 2026-09-21, after a first
    // answer that measurement refuted.
    //
    // `Prop.SetPinDirectControlSystem(PropHome home, bool pinned)`,
    // decompiled rather than guessed, drives three TrackedPeckStates when a
    // prop is pinned: the HOME's `pinDirectControlSystem`, the PROP's own,
    // and every entry of the prop's `taggedPinSystems` whose `propGroup`
    // equals `home.pinGroup`. The third looked like the feature and would
    // have explained every earlier negative result at once — the wiring
    // hanging off the key rather than the socket. It was wrong: both of the
    // prop's own channels are empty on all seven keys.
    //
    // **It is the HOME's state**, driven with `SetState(1)`: the tutorial
    // drawbridge opened with no key anywhere near its plinth. One unlabelled,
    // `NotSavable` TrackedPeckState per plinth, on a GameObject called
    // `BigKeyPlinthNetworking` that sits inside the feature it opens —
    // `ChairLift/Poles/ChairliftBend_green/…`,
    // `TrainSystem/ActivatorObjects/…`, `TunnelSystem/TunnelOutlet - Yellow/…`.
    // Those paths are also an independent confirmation of which key opens
    // what, which nothing else in the game exposes.
    //
    // **Do not read anything into `systemRefences`.** The intro plinth's
    // state reported ZERO effects registered and opened its door anyway; the
    // seven ranged 0 to 4 depending on what happened to be streamed in. A
    // count of zero there means nothing has registered *yet*, not that
    // driving the state is futile — a diagnostic written on 2026-09-21 said
    // otherwise and would have talked the next reader out of the right
    // answer.
    //
    // So granting a feature is one call, and it needs no key prop, no plinth
    // interaction and no save write of the game's own. It is the same call
    // `Core/ArchDoorUnlocker.cs` already makes for the hub shortcuts, for the
    // same reason: a raw SaveManager write changes nothing on screen until
    // the next load, while `SetState` performs the identical write AND runs
    // the visual side.
    //
    // WHY A LEDGER. The game's persistence for these features is not a flag
    // at all — it *is* "the key is sitting in its plinth", re-asserted by
    // `Prop.Start()` on every load, which is exactly what this world stops
    // happening (see Patches/PropPinDoorPatch.cs). The door state itself
    // carries `savableSystem = NotSavable` and no `SaveIdentity`, so it
    // writes nothing anywhere: measured, not assumed. `SavableSystem` has no
    // entry for the map room, the chairlift, the train or the tunnels, so
    // with the key inert there is nothing left to carry the unlock across a
    // reload. The mod therefore keeps its own ledger under `ap_feature_*`
    // and re-applies to every world that loads — the shape
    // Core/RadioStations.cs already has, and for the same underlying reason.
    //
    // THE CO-OP STORY IS BETTER THAN THE RADIO'S. `FmRadioManager` is local
    // to each machine and never replicated, which is why a guest hears a
    // station the host is still waiting for. A TrackedPeckState is not local:
    // peck state is networked, and `SetPinDirectControlSystem` is server-side
    // to begin with. The host grants, the guest sees the door open. No
    // wrinkle to document and nothing for the guest to run.
    internal static class KeyFeatures
    {
        // Same reasoning as CheckTracker's "ap_reported_" and RadioStations'
        // "ap_radio_": a prefix the game's own enum names can never produce,
        // so SaveValuePatch never mistakes one of these writes for a key
        // being placed.
        private const string KeyPrefix = "ap_feature_";

        // Set from slot_data on connection. While false the game is left
        // completely alone: keys open their own doors exactly as in the
        // vanilla game, which is what an older apworld, a seed with the
        // option off, and a session with Archipelago disabled all look like.
        //
        // Deliberately NOT cleared when the connection drops, for the same
        // reason the radio's flag is not: the seed still says the features
        // are shuffled, and re-opening every door on a network hiccup would
        // be a real loss. Only a new session clears it.
        internal static bool FeaturesInPlay { get; private set; }

        // Granted features still waiting for a world able to take them.
        private static readonly HashSet<SaveablePropName> Pending = new();

        private static bool _pendingBlockedLogged;

        internal static void Configure(bool featuresInPlay)
        {
            if (FeaturesInPlay == featuresInPlay)
                return;

            FeaturesInPlay = featuresInPlay;
            Plugin.Log.LogInfo(
                $"[{nameof(KeyFeatures)}] Big key features are "
                + (featuresInPlay
                    ? "Archipelago items: placing a key no longer opens its door."
                    : "left vanilla: placing a key opens its door on the spot."));
        }

        internal static bool IsGranted(SaveablePropName propName)
        {
            return SaveManager.GetIntValue(KeyPrefix + propName, 0, false) != 0;
        }

        // A feature item arrived. Persisted first, applied second: the ledger
        // is what survives the world reload, and the live unlock is only this
        // session's copy of it.
        internal static bool Grant(SaveablePropName propName)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyFeatures)}] Ignored: {propName} received while not host.");
                return false;
            }

            if (!GourdRegistry.IsBigKey(propName))
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyFeatures)}] {propName} is not a big key; item ignored.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + propName, 1);
            Pending.Add(propName);

            var opened = TickPending();
            Plugin.Log.LogInfo(
                $"[{nameof(KeyFeatures)}] {propName} granted"
                + (opened ? " and open." : "; it will open once its plinth is loaded."));

            return true;
        }

        // A new (seed, slot) binding replays every item from scratch, so the
        // features this save was granted under the old one must go with it —
        // otherwise a save rebound to another seed keeps doors that seed never
        // opened. Mirrors what ApItemCursor does to the item cursor and what
        // RadioStations does to its own ledger.
        //
        // Note what this cannot undo: a door already open in the running
        // world. There is no relock — `SetState(0)` would drive whatever
        // closing animation the feature has, on a state the game never sets
        // back itself, and the honest repair is a world reload, which the
        // rebind case already implies.
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
                    $"[{nameof(KeyFeatures)}] {cleared} feature grant(s) cleared: this save is being replayed "
                    + "against a different seed or slot. Reload the world to close the doors.");
            }
        }

        // Queues every feature this save has been granted, for TickPending to
        // apply. Called on the same two occasions as the radio's equivalent,
        // both of which leave a world that knows nothing of the ledger: a
        // world that has just become ready, and a connection established
        // while a world is ALREADY loaded, where nothing else would ever
        // re-open the doors because the server's replay of those items is
        // exactly what the item cursor skips.
        internal static void RearmFromLedger()
        {
            Pending.Clear();
            _pendingBlockedLogged = false;

            if (!FeaturesInPlay)
                return;

            foreach (var propName in GourdRegistry.BigKeys)
            {
                if (IsGranted(propName))
                    Pending.Add(propName);
            }

            if (Pending.Count > 0)
                TickPending();
        }

        // Called from ApRuntime's poll tick until nothing is left. A plinth
        // may not be loaded the instant a world becomes ready, so this simply
        // tries again a second later rather than losing the feature.
        //
        // Returns true when nothing is pending any more.
        internal static bool TickPending()
        {
            if (Pending.Count == 0)
                return true;

            // Copied before iterating: SetState runs the game's own listeners,
            // and nothing guarantees they leave this set alone.
            var attempts = new List<SaveablePropName>(Pending);
            foreach (var propName in attempts)
            {
                if (!TryOpen(propName))
                    continue;

                Pending.Remove(propName);
                Plugin.Log.LogInfo($"[{nameof(KeyFeatures)}] {propName} is now open.");
            }

            if (Pending.Count > 0)
            {
                // Silent retries every second would be indistinguishable from
                // a feature that simply does not work, which is the mistake
                // this project has already paid for twice.
                LogBlockedOnce();
                return false;
            }

            _pendingBlockedLogged = false;
            return true;
        }

        // Once per stuck spell, not once per poll tick: this runs every second
        // for as long as something is waiting.
        private static void LogBlockedOnce()
        {
            if (_pendingBlockedLogged)
                return;

            _pendingBlockedLogged = true;

            // Names the actual reason rather than guessing one. The first
            // version of this line said "waiting on a plinth that is not
            // loaded", which was one of FindDoor's four failure paths and,
            // on 2026-09-21, the wrong one: every plinth was loaded and it
            // was the key's `taggedPinSystems` that was empty. A diagnostic
            // that asserts a cause it never checked is worse than none.
            foreach (var propName in Pending)
                Plugin.Log.LogInfo($"[{nameof(KeyFeatures)}] {propName} waiting: {WhyNoDoor(propName)}");

            Plugin.Log.LogInfo(
                $"[{nameof(KeyFeatures)}] {Pending.Count} feature(s) waiting. Still trying.");
        }

        // The three ways FindDoor can come back empty, told apart. Worth the
        // lines: the first version of this diagnostic asserted a cause it had
        // never checked ("the plinth is not loaded") and was wrong about it on
        // the one day it mattered.
        private static string WhyNoDoor(SaveablePropName propName)
        {
            if (!GourdRegistry.TryGetHomeName(propName, out var homeName))
                return "no plinth is mapped to this key.";

            var home = GourdRegistry.TryGetHome(homeName);
            if (home == null)
                return $"plinth {homeName} is not loaded.";

            return $"plinth {homeName} is loaded but carries no pinDirectControlSystem, which every one of "
                   + "them had when this was measured. Run Ctrl+K.";
        }

        private static bool TryOpen(SaveablePropName propName)
        {
            var door = FindDoor(propName);
            if (door == null)
                return false;

            try
            {
                door.SetState(1);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[{nameof(KeyFeatures)}] SetState(1) failed for {propName}: {ex}");
                return false;
            }
        }

        // The door: the plinth's own `pinDirectControlSystem`, which is what
        // the game drives when a finished key goes in. Nothing else is
        // needed — not the key prop, not its PropGroup, not the key being
        // loaded or even cut. That is what makes this a grant of the feature
        // rather than an imitation of one: it is the identical call on the
        // identical object.
        //
        // The only way this comes back empty is a plinth that is not loaded,
        // which in practice does not happen once a world is ready — all seven
        // were resolvable from the tutorial spawn, 3m to 941m away. It is
        // still handled rather than assumed, because a world that has not
        // finished loading is exactly when the first item arrives.
        internal static TrackedPeckState FindDoor(SaveablePropName propName)
        {
            var home = GourdRegistry.TryGetHomeFor(propName);
            return home != null ? home.pinDirectControlSystem : null;
        }
    }
}
