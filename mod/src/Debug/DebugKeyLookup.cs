using System;
using System.Collections.Generic;
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
            DumpKeyCustody();
            DumpCuttingStations();

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

                var home = GourdRegistry.TryGetHome(homeName);
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

                DumpPlinthWiring(home);
            }
        }

        // What the plinth is actually made of.
        //
        // Added after the 2026-09-21 measurement refuted the first answer.
        // `Prop.SetPinDirectControlSystem` drives three states on a pin; the
        // key's own two are empty for every big key, so the HOME's
        // `pinDirectControlSystem` — on a GameObject called
        // `BigKeyPlinthNetworking` — is all that is left of that function.
        //
        // A TrackedPeckState does nothing by itself: something has to be
        // registered on it. `systemRefences` is that list, and the effects in
        // this game are components sitting on the same object or next to it
        // (the pattern DebugComponentLookup found around the hub sphere:
        // TrackedPeckState + PeckSystemBlock + PeckEffectToggle + PeckEffectAudio
        // on one object). So this prints the count AND the component list of
        // the object, its parent and its children — which is the question
        // "what is this thing", asked of the only object still in the frame.
        private static void DumpPlinthWiring(PropHome home)
        {
            var state = home.pinDirectControlSystem;
            if (state == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       pinDirectControlSystem is null — this plinth drives "
                    + "nothing at all on a pin.");
                return;
            }

            try
            {
                var refs = state.systemRefences;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       pin state '{state.name}': "
                    + (refs == null
                        ? "systemRefences null — NOTHING listens, so driving it cannot open anything."
                        : $"{refs.Count} effect(s) registered."));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       pin state '{state.name}': systemRefences unreadable "
                    + $"({ex.Message}).");
            }

            var go = state.gameObject;
            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}]       path {DebugComponentLookup.DescribePath(go.transform)}/{go.name}");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}]       on it: {DebugComponentLookup.DescribeComponents(go)}");

            var parent = go.transform.parent;
            if (parent != null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       parent '{parent.name}': "
                    + DebugComponentLookup.DescribeComponents(parent.gameObject));
            }

            // The siblings matter as much as the children: the effect that
            // opens a door may well hang beside the state rather than under it.
            var scope = parent != null ? parent : go.transform;
            for (var i = 0; i < scope.childCount; i++)
            {
                var child = scope.GetChild(i);
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]         '{child.name}': "
                    + DebugComponentLookup.DescribeComponents(child.gameObject));
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
        //
        // Extended 2026-09-21 with `taggedPinSystems`, which is where the
        // door turned out to live. `Prop.SetPinDirectControlSystem(home,
        // pinned)` — decompiled rather than guessed — drives three things in
        // order: the HOME's `pinDirectControlSystem`, the PROP's own, and
        // then every entry of the prop's `taggedPinSystems` whose
        // `propGroup` equals `home.pinGroup`. That last loop is the feature:
        // the wiring hangs off the KEY, not off the plinth, which is exactly
        // why all three earlier candidates came back empty — no
        // `PropHomeBlock` on a plinth, no `onPin` PeckSwitch, no switch in
        // the game keyed on a big key. Nobody was looking at the key itself.
        //
        // So for each key this prints every (propGroup -> TrackedPeckState)
        // pair it carries, and marks with `<== MATCHES PLINTH` the one whose
        // group is the plinth's `pinGroup`. That marked line is, by
        // construction, both what has to stop firing when the player places
        // the key and what the Archipelago feature item has to drive
        // instead. Its `savableSystem` also settles whether a mod-side
        // ledger is needed at all: anything other than NotSavable and the
        // game persists the unlock by itself.
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

                DumpTaggedPinSystems(prop);
            }
        }

        // The door, per key. Printed under the key that owns it.
        private static void DumpTaggedPinSystems(Prop prop)
        {
            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}]       own pinDirectControlSystem {Describe(prop.pinDirectControlSystem)}");

            // The group to match against. Null when the tower is not loaded,
            // in which case every pair is printed unmarked rather than
            // nothing at all — a key's wiring is worth seeing even when its
            // plinth is elsewhere.
            var pinGroup = (PropGroup?)null;
            var keyHome = GourdRegistry.TryGetHomeFor(prop.saveablePropName);
            if (keyHome != null)
                pinGroup = keyHome.pinGroup;

            var tagged = prop.taggedPinSystems;
            if (tagged == null || tagged.Length == 0)
            {
                // Said out loud, because an empty array here would refute the
                // whole mechanism rather than merely fail to confirm it.
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       taggedPinSystems: NONE — this key drives no feature on pin, "
                    + "and the door is something else entirely.");
                return;
            }

            for (var i = 0; i < tagged.Length; i++)
            {
                var pair = tagged[i];
                var matches = pinGroup.HasValue && pair.propGroup == pinGroup.Value;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       taggedPinSystems[{i}] on {pair.propGroup} -> "
                    + $"{Describe(pair.peckSystem)}{(matches ? "   <== MATCHES PLINTH" : string.Empty)}");
            }

            if (pinGroup.HasValue)
                return;

            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}]       (plinth not loaded, so no pinGroup to match against — "
                + "walk to the tower to see which pair is the door.)");
        }

        // WHAT HOLDS A KEY BACK — the question that decides how Archipelago
        // can hand one over (design settled 2026-09-21: the keys become items
        // that spawn like gourds, instead of being released by filling a
        // monument).
        //
        // The hypothesis, taken from the binary rather than from intuition:
        // `PeckEffectPropHomeSettings` carries a `propHome`/`propHomeBlock`, a
        // driving `TrackedPeckState`, and a `settingsPerState[]` of
        // `{ blockPlacingMask, blockPlacingValue, blockGrabbingMask,
        // blockGrabbingValue }`. That is precisely the shape of "this monument
        // filled up, so that key may now be picked up", and
        // `PropHome.blockGrabbing` is the field it writes.
        //
        // If it holds, the mod needs no Harmony patch for either half — which
        // matters, because `OnPeck` and `Apply` both have NO code address in
        // the export while `Awake` does, so they are inlined and a patch on
        // them would bind to nothing, exactly like `ServerCutSegment`.
        // Releasing a key is then `blockGrabbing = false` on its home, and
        // suppressing the vanilla release is re-asserting `true` — the same
        // "re-apply rather than intercept" shape Core/RadioStations.cs and
        // Core/ArchDoorUnlocker.cs already have.
        //
        // Printed even when nothing matches, because "no PeckEffectPropHomeSettings
        // targets this home" is the result that refutes the hypothesis, and a
        // tool that says nothing when it finds nothing costs more than it saves.
        private static void DumpKeyCustody()
        {
            var effects = UnityEngine.Object.FindObjectsByType<PeckEffectPropHomeSettings>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}] key custody \u2014 {effects.Length} PeckEffectPropHomeSettings loaded:");

            foreach (var prop in Prop.allProps)
            {
                if (prop == null || !GourdRegistry.IsBigKey(prop.saveablePropName))
                    continue;

                var home = prop.currentHome;
                if (home == null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugKeyLookup)}]   {prop.saveablePropName}: in no home at all \u2014 loose "
                        + "already, so nothing is holding it.");
                    continue;
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]   {prop.saveablePropName} sits in '{home.name}' "
                    + $"({home.saveableHomeName}) | blockGrabbing={home.blockGrabbing} "
                    + $"blockPlacing={home.blockPlacing} | pinGroup {home.pinGroup}");

                // BOTH ways an effect can name a home, and the second was
                // missing on the first pass: `PeckEffectPropHomeSettings` has
                // a `propHome` AND a `propHomeBlock`, and six of the seven keys
                // came back "nothing targets this home" purely because only the
                // first was compared. An absence that was never checked is a
                // worse diagnostic than none at all.
                var found = 0;
                foreach (var effect in effects)
                {
                    if (effect == null)
                        continue;

                    var via = (string)null;
                    if (effect.propHome != null && effect.propHome.Pointer == home.Pointer)
                        via = "propHome";
                    else if (BlockOwns(effect.propHomeBlock, home))
                        via = $"propHomeBlock '{effect.propHomeBlock.name}'";

                    if (via == null)
                        continue;

                    found++;
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugKeyLookup)}]       released by '{effect.name}' (via {via}) driven from "
                        + $"{Describe(effect.systemReference.peckSystem)} | "
                        + DescribeSettings(effect.settingsPerState));
                }

                if (found == 0)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugKeyLookup)}]       no PeckEffectPropHomeSettings names this home, by "
                        + "propHome or by propHomeBlock. Something else frees this one.");
                }

                DumpKeyAppearance(prop);
            }
        }

        private static bool BlockOwns(PropHomeBlock block, PropHome home)
        {
            if (block == null || block.homes == null)
                return false;

            for (var i = 0; i < block.homes.Length; i++)
            {
                if (block.homes[i] != null && block.homes[i].Pointer == home.Pointer)
                    return true;
            }

            return false;
        }

        // Can a key be recoloured, and with what?
        //
        // Asked because the keys are all the same yellow across the bridge and
        // the four coloured towers — only the Black Monolith's and the Green
        // Dome's look different — so once they arrive from Archipelago and pile
        // up at the hub there is nothing to tell them apart.
        //
        // The mechanism the mod uses for gourds does NOT transfer:
        // `ReceivedItemSpawner.ApplyCosmeticColor` sets
        // `RewardGourd.isVariantChallenge` + `variantChallengeColor`, and a big
        // key has no RewardGourd at all — that is established, and it is why
        // ItemApplier has a separate path for keys in the first place.
        //
        // What might transfer is the layer underneath it. `PropertyBlockHelper`
        // is a plain MonoBehaviour — `targetRenderer`, `additonalRenderers`,
        // `colorSettings[] { propertyName, color }` and a public `Refresh()` —
        // with nothing gourd-specific about it. If a key carries one, colouring
        // is the same two lines. If it does not, this prints the renderers and
        // materials instead, so the next attempt can target a shader property
        // by name rather than guess one.
        private static void DumpKeyAppearance(Prop prop)
        {
            var helper = prop.GetComponentInChildren<PropertyBlockHelper>(true);
            if (helper == null)
            {
                var renderers = prop.GetComponentsInChildren<Renderer>(true);
                var names = new List<string>();
                foreach (var renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    var material = renderer.sharedMaterial;
                    var materialName = material != null ? material.name : "<no material>";
                    names.Add($"'{renderer.name}'({materialName})");
                }

                var joined = string.Join(", ", names);
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       colour: no PropertyBlockHelper. "
                    + $"{renderers.Length} renderer(s): {joined}");
                return;
            }

            var settings = helper.colorSettings;
            if (settings == null || settings.Length == 0)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       colour: PropertyBlockHelper on '{helper.name}' but it "
                    + "exposes no colorSettings.");
                return;
            }

            for (var i = 0; i < settings.Length; i++)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]       colour: '{helper.name}'.colorSettings[{i}] "
                    + $"'{settings[i].propertyName}' = {settings[i].color}");
            }
        }

        // One line per state the effect can be driven to: which of the two
        // flags it writes (the mask) and what it writes (the value).
        private static string DescribeSettings(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<PeckEffectPropHomeSettings.PropHomeSetting> settings)
        {
            if (settings == null || settings.Length == 0)
                return "settingsPerState is empty";

            var parts = new List<string>();
            for (var i = 0; i < settings.Length; i++)
            {
                var setting = settings[i];
                var grab = setting.blockGrabbingMask ? $"blockGrabbing:={setting.blockGrabbingValue}" : "grab untouched";
                var place = setting.blockPlacingMask ? $"blockPlacing:={setting.blockPlacingValue}" : "place untouched";
                parts.Add($"state {i} -> {grab}, {place}");
            }

            return string.Join(" | ", parts);
        }

        // Where the 25 cut segments physically are, sorted by distance.
        //
        // Added 2026-09-21, and not for the design: for the logic. The
        // forage locations take the same rule as their tower's deposit, on
        // the reasoning that cutting a key needs that tower's monument full.
        // That reasoning says nothing about WHERE the cutting happens, and
        // this world has already shipped one seed-breaking bug of exactly
        // that shape — the Green Cup Key could be placed past the chairlift
        // its own key opens, because the region graph claimed the island was
        // open. If a cutting station for a key sits behind another key's
        // door, the same trap is back.
        //
        // Answered the same way that one was: stand in the gated zone and
        // read the distances, then stand somewhere plainly open and read them
        // again. Presence proves nothing — the game instantiates this kind of
        // object everywhere — so distance is the only signal, and it is
        // printed rather than the mere fact that a station exists.
        //
        // `UnlockTrail.finalTarget` is printed too: if it names the plinth,
        // it is the trail-to-tower mapping, which nothing else in the game
        // exposes.
        private static void DumpCuttingStations()
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? origin = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            var trails = UnityEngine.Object.FindObjectsByType<UnlockTrail>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Plugin.Log.LogInfo(
                $"[{nameof(DebugKeyLookup)}] {trails.Length} UnlockTrail(s) loaded"
                + (origin.HasValue ? ", stations sorted by distance:" : " (no local player, so no distances):"));

            foreach (var trail in trails)
            {
                if (trail == null)
                    continue;

                var stations = trail.stations;
                var finalTarget = trail.finalTarget != null ? trail.finalTarget.name : "<none>";

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugKeyLookup)}]   trail '{trail.name}' | {(stations != null ? stations.Count : 0)} station(s) "
                    + $"| finalTarget '{finalTarget}'");

                if (stations == null)
                    continue;

                var rows = new List<(float distance, string line)>();
                for (var i = 0; i < stations.Count; i++)
                {
                    var station = stations[i];
                    if (station == null)
                        continue;

                    var distance = origin.HasValue
                        ? Vector3.Distance(origin.Value, station.transform.position)
                        : float.NaN;

                    var home = station.cuttingHome;
                    var homeName = home != null ? $"{home.saveableHomeName} (accepts {home.pinGroup})" : "<no cuttingHome>";

                    rows.Add((float.IsNaN(distance) ? float.MaxValue : distance,
                        $"      station index {station.stationIndex} '{station.name}' "
                        + $"| {(float.IsNaN(distance) ? "<no player>" : $"{distance:F1}m")} "
                        + $"| cuttingHome {homeName} | cutSystem {Describe(station.cutSystem.peckSystem)}"));
                }

                rows.Sort((a, b) => a.distance.CompareTo(b.distance));
                foreach (var row in rows)
                    Plugin.Log.LogInfo($"[{nameof(DebugKeyLookup)}] {row.line}");
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

        // A TrackedPeckState is worth naming by its label, its savableSystem
        // AND what the save currently holds for it. The expectation is that
        // these feature switches are NotSavable, which is precisely what
        // would force a mod-side ledger like Core/RadioStations.cs keeps —
        // but a savableSystem here would mean the game persists the unlock
        // by itself and no ledger is needed. The value settles which of the
        // two the implementation has to be, so it is printed rather than
        // assumed. Falls back to the SaveIdentity guid, which is the key
        // TrackedPeckState.SetState writes when there is no savableSystem.
        private static string Describe(TrackedPeckState state)
        {
            if (state == null)
                return "<none>";

            var saveKey = state.savableSystem != SavableSystem.NotSavable
                ? state.savableSystem.ToString()
                : (state.saveIdentity != null ? state.saveIdentity.saveGuid : null);

            var saved = string.IsNullOrEmpty(saveKey)
                ? "<nothing to save under>"
                : $"SaveManager['{saveKey}']={SaveManager.GetIntValue(saveKey, -12345, false)}";

            return $"'{state.label}' on '{state.name}' (savableSystem {state.savableSystem}, {saved})";
        }
    }
}
