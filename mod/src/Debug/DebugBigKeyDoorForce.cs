using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Opens a big-key door without the key, to settle the one question the
    // "big keys as forage checks" design still rested on: what actually
    // opens a door.
    //
    // Five candidates are now dead, and this tool exists to kill or confirm
    // the sixth without a guess reaching production code.
    //
    // Dead: no `PropHomeBlock` watches a plinth; no plinth fires a
    // `PeckSwitch` on pin; no switch in the game is keyed on a big key;
    // `Prop.taggedPinSystems` is EMPTY on all seven keys; and the key's own
    // `pinDirectControlSystem` is null on all seven. The first three were
    // measured before 2026-09-21, the last two on it — and the last two
    // refuted a decompiled reading of
    // `Prop.SetPinDirectControlSystem` that had looked conclusive.
    //
    // Alive: the HOME's `pinDirectControlSystem`, the only one of the three
    // states that function touches which is non-null for a big key. This
    // drives it and prints how many effects are registered on it first,
    // because "nothing listens" and "the mechanism is wrong" look identical
    // from in front of a door that stays shut.
    //
    // If it opens a door, that is the feature working, one call short of
    // Core/KeyFeatures.cs being able to do it on an item. If it does not,
    // `PropHome.onPinServer`/`onChangeServer` are what is left.
    //
    // Host only, like every other write in this mod. `SetState` is the same
    // call Core/ArchDoorUnlocker.cs already uses for the hub shortcuts, for
    // the same reason: a raw SaveManager write changes nothing on screen
    // until the next load, while SetState performs the identical write AND
    // runs the visual side.
    internal static class DebugBigKeyDoorForce
    {
        internal static void ForceOne(string nameFilter)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(DebugBigKeyDoorForce)}] Not the host: nothing done. Every write in this mod is host-side.");
                return;
            }

            var candidates = Collect();
            if (candidates.Count == 0)
            {
                // Never silent on an empty result: "the tool found nothing"
                // and "the key never registered" are indistinguishable from
                // the outside, and this project has paid for that confusion
                // twice.
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugBigKeyDoorForce)}] No big key prop found at all. Nothing to drive.");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugBigKeyDoorForce)}] {candidates.Count} big key(s), nearest plinth first:");
            foreach (var candidate in candidates)
                Plugin.Log.LogInfo($"[{nameof(DebugBigKeyDoorForce)}]   {candidate.Describe()}");

            var chosen = Choose(candidates, nameFilter);
            if (chosen == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugBigKeyDoorForce)}] Nothing driven: "
                    + (string.IsNullOrEmpty(nameFilter)
                        ? "not one of them has both its plinth loaded and a matching taggedPinSystems entry. "
                          + "Walk to a tower and press again."
                        : $"no big key matches '{nameFilter}' with its plinth loaded. Clear "
                          + $"{nameof(ModConfig.BigKeyDoorName)} to take the nearest one instead."));
                return;
            }

            Drive(chosen);
        }

        // The KEY item, as opposed to the door. Hands one over exactly as a
        // real Archipelago item would — Core/KeyCustody.Grant — so what this
        // exercises is the shipping path and not an imitation of it.
        //
        // Exists because the keys are shuffled into the multiworld like
        // everything else now, so in a solo seed they sit in this world's own
        // locations and no longer turn up simply by connecting.
        internal static void GrantOne(string nameFilter)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(DebugBigKeyDoorForce)}] Not the host: nothing done.");
                return;
            }

            if (!KeyCustody.KeysAreItems)
            {
                // Said rather than done silently: with the flag off the keys
                // are vanilla and granting one would be meaningless, which is
                // indistinguishable from a key that failed to arrive.
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugBigKeyDoorForce)}] This slot does not have big keys as items "
                    + "(slot_data big_key_items is off), so there is nothing to grant.");
                return;
            }

            var candidates = Collect();
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(nameFilter)
                    && !candidate.PropName.ToString().Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugBigKeyDoorForce)}] Granting the KEY {candidate.PropName} "
                    + $"(plinth {candidate.HomeName}); its door is a separate item.");
                KeyCustody.Grant(candidate.PropName);
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugBigKeyDoorForce)}] No big key matches '{nameFilter}'. "
                + $"Clear {nameof(ModConfig.BigKeyDoorName)} to take the nearest one.");
        }

        // Every big key, with whatever of its wiring is currently resolvable.
        // Deliberately collected even when incomplete: a key whose plinth is
        // not loaded still belongs in the printed table, because the table is
        // how the operator sees WHY nothing was driven.
        private static List<Candidate> Collect()
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? origin = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            var candidates = new List<Candidate>();

            foreach (var prop in Prop.allProps)
            {
                if (prop == null || !GourdRegistry.IsBigKey(prop.saveablePropName))
                    continue;

                var candidate = new Candidate { Prop = prop, PropName = prop.saveablePropName };

                if (GourdRegistry.TryGetHomeName(candidate.PropName, out var homeName))
                {
                    candidate.HomeName = homeName;
                    candidate.Home = GourdRegistry.TryGetHome(homeName);
                }

                if (candidate.Home != null)
                {
                    candidate.Door = FindTaggedSystem(prop, candidate.Home.pinGroup);
                    candidate.Channel = "taggedPinSystems";

                    // MEASURED 2026-09-21, and it refuted the first answer:
                    // every big key reports `taggedPinSystems: NONE` and a
                    // null `pinDirectControlSystem` of its own. So of the
                    // three states `Prop.SetPinDirectControlSystem` drives on
                    // a pin, two are empty for these props and only the
                    // HOME's is left — a TrackedPeckState living on a
                    // GameObject called `BigKeyPlinthNetworking`, non-null on
                    // all seven plinths.
                    //
                    // It is therefore either the door or the end of this
                    // function as a lead, and driving it is how to tell. If
                    // nothing opens, what is left is
                    // `PropHome.onPinServer`/`onChangeServer` — the plain C#
                    // delegates fired by `PropHome.FireServerEvents`, which no
                    // dump has ever enumerated.
                    if (candidate.Door == null)
                    {
                        candidate.Door = candidate.Home.pinDirectControlSystem;
                        candidate.Channel = "home.pinDirectControlSystem (fallback)";
                    }
                }

                // Distance to the PLINTH, not to the key: the plinth is where
                // the feature is, and the operator is standing next to the
                // door they want opened. Keys are a poor ruler anyway — the
                // lesson of 2026-09-21 is that the game instantiates this
                // kind of object everywhere, so two dumps taken in two zones
                // came back byte-identical. Both distances are printed so
                // that if the plinths turn out to be replicated the same way,
                // the table says so instead of quietly lying.
                candidate.PlinthDistance = Distance(origin, candidate.Home);
                candidate.KeyDistance = Distance(origin, prop);

                candidates.Add(candidate);
            }

            candidates.Sort((a, b) => Sortable(a.PlinthDistance).CompareTo(Sortable(b.PlinthDistance)));
            return candidates;
        }

        private static Candidate Choose(List<Candidate> candidates, string nameFilter)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Door == null)
                    continue;

                if (!string.IsNullOrEmpty(nameFilter)
                    && !candidate.PropName.ToString().Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                return candidate;
            }

            return null;
        }

        // The door for this key: the tagged pin system whose group is the one
        // its plinth accepts. Exactly the test the game's own loop makes.
        private static TrackedPeckState FindTaggedSystem(Prop prop, PropGroup pinGroup)
        {
            var tagged = prop.taggedPinSystems;
            if (tagged == null)
                return null;

            for (var i = 0; i < tagged.Length; i++)
            {
                var pair = tagged[i];
                if (pair.propGroup == pinGroup && pair.peckSystem != null)
                    return pair.peckSystem;
            }

            return null;
        }

        private static void Drive(Candidate candidate)
        {
            var door = candidate.Door;
            var saveKey = SaveKeyOf(door);

            // Bracketed before and after, like the combinator and radio keys.
            // A state that was already at 1 and a call that did nothing look
            // the same on screen, and the difference is the whole result.
            Plugin.Log.LogInfo(
                $"[{nameof(DebugBigKeyDoorForce)}] Driving {candidate.PropName} via {candidate.Channel} -> "
                + $"'{door.label}' on '{door.name}' (savableSystem {door.savableSystem}); "
                + $"before: {ReadSaved(saveKey)}. {DescribeListeners(door)}");

            try
            {
                door.SetState(1);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[{nameof(DebugBigKeyDoorForce)}] SetState(1) threw: {ex}");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugBigKeyDoorForce)}] SetState(1) returned; after: {ReadSaved(saveKey)}. "
                + $"Now look at what {candidate.HomeName} is supposed to open.");

            // The half of the answer that decides whether Core/ needs a
            // ledger of its own. NotSavable means the unlock lives in RAM and
            // nowhere else, so it would have to be re-applied to every world
            // that loads — the shape Core/RadioStations.cs already has.
            if (door.savableSystem == SavableSystem.NotSavable && string.IsNullOrEmpty(saveKey))
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugBigKeyDoorForce)}] This state persists nothing: if the door opened, the mod "
                    + "will have to keep its own ledger and re-apply on every world load.");
            }
        }

        // How many effects are registered on this state. Zero means driving
        // it cannot possibly do anything visible, which is worth knowing
        // BEFORE concluding that the mechanism is wrong — the two look
        // identical from in front of a door that stays shut.
        private static string DescribeListeners(TrackedPeckState state)
        {
            try
            {
                var refs = state.systemRefences;
                return refs == null
                    ? "systemRefences is null — nothing listens."
                    : $"{refs.Count} effect(s) registered on it.";
            }
            catch (Exception ex)
            {
                return $"could not read systemRefences ({ex.Message}).";
            }
        }

        private static string SaveKeyOf(TrackedPeckState state)
        {
            if (state.savableSystem != SavableSystem.NotSavable)
                return state.savableSystem.ToString();

            return state.saveIdentity != null ? state.saveIdentity.saveGuid : null;
        }

        private static string ReadSaved(string saveKey)
        {
            return string.IsNullOrEmpty(saveKey)
                ? "nothing saved under any key"
                : $"SaveManager['{saveKey}']={SaveManager.GetIntValue(saveKey, -12345, false)}";
        }

        private static float Distance(Vector3? origin, UnityEngine.Component component)
        {
            if (!origin.HasValue || component == null)
                return float.NaN;

            return Vector3.Distance(origin.Value, component.transform.position);
        }

        // NaN sorts last rather than unpredictably: an unloaded plinth is the
        // least interesting row, not a random one.
        private static float Sortable(float distance)
        {
            return float.IsNaN(distance) ? float.MaxValue : distance;
        }

        private sealed class Candidate
        {
            internal Prop Prop;
            internal SaveablePropName PropName;
            internal SaveableHomeName HomeName;
            internal PropHome Home;
            internal TrackedPeckState Door;
            internal string Channel;
            internal float PlinthDistance;
            internal float KeyDistance;

            internal string Describe()
            {
                var plinth = Home != null
                    ? $"{HomeName} at {Format(PlinthDistance)} (accepts {Home.pinGroup})"
                    : $"{HomeName} NOT LOADED";

                var door = Door != null
                    ? $"door '{Door.label}' on '{Door.name}' via {Channel}"
                    : "nothing to drive: no taggedPinSystems entry AND no pinDirectControlSystem";

                return $"{PropName} | plinth {plinth} | key at {Format(KeyDistance)} | {door}";
            }

            private static string Format(float distance)
            {
                return float.IsNaN(distance) ? "<no player>" : $"{distance:F1}m";
            }
        }
    }
}
