using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // An inventory of the objects lying around the island, for the question
    // "which of these could become a real Archipelago item instead of the
    // inert Postcard / Souvenir Pebble / Novelty Keychain?"
    //
    // It has to be an in-game dump, and that is not laziness: `il2cpp.cs` has
    // no class for any of them. There is no FlareGun and no WalkieTalkie —
    // they are plain `Prop` prefabs told apart by their prefab and by what
    // their `useHeldSwitch` is wired to, none of which exists in the binary.
    // Only the running game can list them.
    //
    // Three columns decide whether a prop is a candidate:
    //
    //   - **`savablePropGuid`** — the game's own per-prop identity, used with
    //     `SaveManager.GetIsInInventory(guid)` and `SaveData.inventory`. A
    //     prop that has one can be told apart from its siblings across
    //     sessions, which is what a *location* would need. A prop without one
    //     can still be an *item* (spawn a copy) but can never be a check.
    //   - **`radioVoiceAssigner`** — a first-class field on Prop, so any prop
    //     carrying one is a walkie-talkie. The one gadget the code names.
    //   - **`useHeldSwitch`** — the prop does something when used while held,
    //     which is the difference between a tool and a paperweight.
    //
    // Counts first, then detail, because `Prop.allProps` holds every prop in
    // the loaded world and an undifferentiated list of it is unreadable.
    internal static class DebugPropLookup
    {
        // Props named the same are the same prefab; only a couple of each is
        // worth printing in full.
        private const int DetailPerName = 2;

        internal static void Dump()
        {
            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] === prop inventory ===");

            var props = Prop.allProps;
            if (props == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] Prop.allProps is null — no world loaded.");
                return;
            }

            var counts = new Dictionary<string, int>();
            var printed = new Dictionary<string, int>();
            var candidates = 0;

            // Pass one: how many of each, so the summary is honest about what
            // the detail below is a sample of.
            for (var i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (prop == null)
                    continue;

                var name = prop.name;
                counts[name] = counts.TryGetValue(name, out var n) ? n + 1 : 1;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugPropLookup)}] {props.Count} prop(s) loaded, {counts.Count} distinct name(s).");

            // Pass two: the detail, for the props that could plausibly become
            // something. A gourd or a big key is neither — the world already
            // has those, and printing them would bury the answer.
            for (var i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (prop == null || prop.saveablePropName != SaveablePropName.notSavable)
                    continue;

                var isWalkieTalkie = prop.radioVoiceAssigner != null;
                var hasGuid = !string.IsNullOrEmpty(prop.savablePropGuid);
                var isUsable = prop.useHeldSwitch != null;

                // A prop that does nothing, has no identity and is not a
                // walkie-talkie is scenery. There are hundreds of those.
                if (!isWalkieTalkie && !hasGuid && !isUsable)
                    continue;

                candidates++;

                var name = prop.name;
                var shown = printed.TryGetValue(name, out var s) ? s : 0;
                if (shown >= DetailPerName)
                {
                    printed[name] = shown + 1;
                    continue;
                }

                printed[name] = shown + 1;

                var groups = "<none>";
                var list = prop.propGroups;
                if (list != null && list.Count > 0)
                {
                    var parts = new string[list.Count];
                    for (var j = 0; j < list.Count; j++)
                        parts[j] = list[j].ToString();
                    groups = string.Join(", ", parts);
                }

                var inInventory = hasGuid && SaveManager.GetIsInInventory(prop.savablePropGuid);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPropLookup)}]   '{name}' (x{counts[name]}) | guid="
                    + (hasGuid ? prop.savablePropGuid : "<none>")
                    + $" | inInventory={inInventory} | walkieTalkie={isWalkieTalkie} | usableHeld={isUsable}"
                    + $" | saveType={prop.propSaveType} | groups [{groups}]");
            }

            // Said even at zero, and said with the number: "no candidates" and
            // "the tool did not run" must never look alike.
            Plugin.Log.LogInfo(
                $"[{nameof(DebugPropLookup)}] {candidates} candidate prop(s) — carrying a guid, a walkie-talkie or a"
                + " use-while-held switch. Anything else loaded is scenery."
                + (candidates == 0 ? " Stand somewhere with objects around and press again." : string.Empty));

            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] === end of prop inventory ===");
        }
    }
}
