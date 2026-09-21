using System;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Everything needed to decide the "big keys as forage checks" design, in
    // one dump. Written 2026-09-21, before any of it exists, so the design
    // rests on what the game actually contains rather than on a third-party
    // document and a decompilation.
    //
    // It answers three questions, in the order the design needs them:
    //
    //   1. **Which towers have a KeyBlank, and how many segments?** The
    //      third-party doc says "5 cutters per tower, 25 across the first
    //      five"; the player remembers the drawbridge plus the four coloured
    //      towers, four or five holes each. `cuts` is sized at runtime from
    //      scene data, so only the game can say. That count is exactly how
    //      many new locations the apworld would gain.
    //
    //   2. **What does a plinth accept, and what does a key carry?** The
    //      suppression plan is a prefix on `KeyBlank.RefreshPropGroup` that
    //      keeps `finishedPropGroup` out of `prop.propGroups`. The PropGroup
    //      enum suggests that is enough — an uncut key is `BigKey` (32) and
    //      the finished one `BigKeyComplete` (36) — but only the plinth's own
    //      `pinGroup` proves the socket refuses the uncut key.
    //
    //   3. **Is there a feature switch separate from the key?** The plan the
    //      player asked for is for Archipelago to grant the unlock itself
    //      (map room, chairlift, train, tunnels) instead of pinning a key.
    //      `PropHome.pinDirectControlSystem` and
    //      `PropHomeBlock.isFullDirectControlSystem` are the two candidate
    //      channels. Note that neither is expected to carry a SavableSystem
    //      of its own — that enum has no entry for these features — which is
    //      what would force a mod-side ledger, exactly like the radio's.
    //
    // NOT to be confused with PeckDevHelper.Trigger(UnlockRules{map:true}).
    // That is the game's own cheat and it is a sledgehammer: ArchDoorUnlocker
    // documents Trigger(unlocks) also writing EndingGate and the seven
    // FmStation* and four LookoutLight* values, confirmed by inspecting save
    // files. The targeted TrackedPeckState is the channel to use.
    internal static class DebugKeyLookup
    {
        internal static void Dump()
        {
            Plugin.Log.LogInfo($"[{nameof(DebugKeyLookup)}] === big key dump ===");

            DumpKeyBlanks();
            DumpPlinths();
            DumpKeyedSwitches();
            DumpKeyProps();

            Plugin.Log.LogInfo($"[{nameof(DebugKeyLookup)}] === end of big key dump ===");
        }

        // Question 1.
        private static void DumpKeyBlanks()
        {
            var blanks = UnityEngine.Object.FindObjectsByType<KeyBlank>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Said even when it is zero: a tool silent on an empty result is
            // indistinguishable from a key that never registered.
            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}] KeyBlank instances loaded: {blanks.Length}"
                + (blanks.Length == 0
                    ? " (they stream in with their tower; walk nearer one, or fly with F2)"
                    : string.Empty));

            foreach (var blank in blanks)
            {
                if (blank == null)
                    continue;

                var prop = blank.prop;
                var propName = prop != null ? prop.saveablePropName.ToString() : "<no prop>";

                var segments = "<null>";
                var cut = 0;
                var cuts = blank.cuts;
                if (cuts != null)
                {
                    var total = cuts.Count;
                    var flags = new char[total];
                    for (var i = 0; i < total; i++)
                    {
                        flags[i] = cuts[i] ? 'X' : '.';
                        if (cuts[i])
                            cut++;
                    }

                    segments = total > 0 ? new string(flags) : "<empty>";
                }

                var finished = blank.finishedPropGroup;
                var alreadyFinished = prop != null && HasGroup(prop, finished);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]   '{blank.name}' | key {propName} "
                    + $"| segments [{segments}] ({cut} cut) | finishedPropGroup {finished} "
                    + $"| already in propGroups={alreadyFinished} | startFinished={blank.startFinished} "
                    + $"| covers={(blank.covers != null ? blank.covers.Length : 0)}");
            }
        }

        // Questions 2 and 3.
        private static void DumpPlinths()
        {
            var blocks = UnityEngine.Object.FindObjectsByType<PropHomeBlock>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (SaveablePropName propName in Enum.GetValues(typeof(SaveablePropName)))
            {
                if (!GourdRegistry.IsBigKey(propName)
                    || !GourdRegistry.TryGetHomeName(propName, out var homeName))
                    continue;

                var home = PropHome.GetSaveableHome(homeName);
                if (home == null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugKeyLookup)}]   plinth {homeName} ({propName}): not loaded.");
                    continue;
                }

                // The group the socket accepts. If this is the *Complete
                // variant, skipping RefreshPropGroup is enough to keep the
                // key out of it and no further suppression is needed.
                var pinSystem = Describe(home.pinDirectControlSystem);

                // The block that watches this home, and the switch it drives
                // when the home fills — the candidate "feature unlock"
                // channel that would let Archipelago open the door without
                // ever touching the key.
                var blockSystem = "<no PropHomeBlock found>";
                foreach (var block in blocks)
                {
                    if (block == null || block.homes == null)
                        continue;

                    var owns = false;
                    for (var i = 0; i < block.homes.Length && !owns; i++)
                        owns = block.homes[i] != null && block.homes[i].Pointer == home.Pointer;

                    if (!owns)
                        continue;

                    blockSystem = $"{Describe(block.isFullDirectControlSystem)} (block '{block.name}', "
                                  + $"{block.homes.Length} home(s))";
                    break;
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]   plinth {homeName} ({propName}) | pinGroup {home.pinGroup} "
                    + $"| pinDirectControlSystem {pinSystem} | isFull -> {blockSystem}");

                // The switches the plinth fires when a key goes in or comes
                // out. With the key made inert on placement, whatever these
                // drive is precisely what has to stop happening — and what
                // the Archipelago item has to drive instead.
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       onPin {DescribeSwitch(home.onPin)} "
                    + $"| onUnpin {DescribeSwitch(home.onUnpin)}");
            }
        }

        // Every switch in the world that demands a key to operate.
        //
        // `PeckSwitch.needsKey` + `keyType` (a PropGroup) is the game's own
        // "this cannot be used without the right key" mechanism, and it is a
        // far better candidate for the door than the plinth: a switch keyed on
        // BigKeyComplete is, by construction, the thing a finished key opens.
        // If the doors turn out to live here rather than behind the plinth's
        // pin, both halves of the design — making the key inert and letting
        // Archipelago open the door — point at this one switch.
        private static void DumpKeyedSwitches()
        {
            var switches = UnityEngine.Object.FindObjectsByType<PeckSwitch>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var found = 0;
            foreach (var peckSwitch in switches)
            {
                if (peckSwitch == null || !peckSwitch.needsKey)
                    continue;

                found++;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]   keyed switch '{peckSwitch.name}' | keyType {peckSwitch.keyType} "
                    + $"| drives {Describe(peckSwitch.trackedStateSystem)} "
                    + $"| mode {peckSwitch.stateMode} specific={peckSwitch.specificState}");
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}] {found} switch(es) requiring a key, out of {switches.Length} loaded."
                + (found == 0 ? " None here — the doors may be plinth-driven after all." : string.Empty));
        }

        private static string DescribeSwitch(PeckSwitch peckSwitch)
        {
            if (peckSwitch == null)
                return "<none>";

            return $"'{peckSwitch.name}' -> {Describe(peckSwitch.trackedStateSystem)}";
        }

        // What each key prop carries right now, and what the save says about
        // it — the before/after the suppression has to change.
        private static void DumpKeyProps()
        {
            foreach (var prop in Prop.allProps)
            {
                if (prop == null || !GourdRegistry.IsBigKey(prop.saveablePropName))
                    continue;

                var groups = "<none>";
                var list = prop.propGroups;
                if (list != null && list.Count > 0)
                {
                    var parts = new string[list.Count];
                    for (var i = 0; i < list.Count; i++)
                        parts[i] = list[i].ToString();
                    groups = string.Join(", ", parts);
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]   key prop '{prop.name}' ({prop.saveablePropName}) "
                    + $"| propGroups [{groups}] "
                    + $"| SaveManager value={SaveManager.GetIntValue(prop.saveablePropName.ToString(), 0, false)}");
            }
        }

        private static bool HasGroup(Prop prop, PropGroup group)
        {
            var list = prop.propGroups;
            if (list == null)
                return false;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == group)
                    return true;
            }

            return false;
        }

        // A TrackedPeckState is worth naming by both its label and its
        // savableSystem: the expectation is that these feature switches have
        // NotSavable, which is precisely what would force a mod-side ledger.
        private static string Describe(TrackedPeckState state)
        {
            if (state == null)
                return "<none>";

            return $"'{state.label}' (savableSystem {state.savableSystem})";
        }
    }
}
