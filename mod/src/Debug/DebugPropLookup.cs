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

        // A lamp is plausibly pure scenery — no guid, no useHeldSwitch, not a
        // walkie-talkie — which the candidate filter below would otherwise
        // treat as indistinguishable from the hundreds of PegTile/foldingChair
        // entries that really are just scenery. Named substrings widen the
        // filter for exactly this case (player request, 2026-09-22: find a
        // lamp and a "ray-x gun" that two full-radius dumps never turned up).
        private static readonly string[] ExtraNameHints = { "lamp", "ray", "torch", "light" };

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
                var nameHints = ContainsAnyHint(prop.name);

                // A prop that does nothing, has no identity, is not a
                // walkie-talkie and doesn't even look the part by name is
                // scenery. There are hundreds of those.
                if (!isWalkieTalkie && !hasGuid && !isUsable && !nameHints)
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

        private static bool ContainsAnyHint(string name)
        {
            foreach (var hint in ExtraNameHints)
            {
                if (name.Contains(hint, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // Added 2026-09-22: Ctrl+H's "what am I holding" dump came back with
        // 0 renderers on BOTH a broken and a working gadget clone — while
        // held, a prop's visible mesh apparently lives on a different
        // transform than prop.gameObject entirely, so that test was pointed
        // at the wrong object the whole time. This instead finds the actual
        // ground clones (the ones the screenshot showed pink) directly, by
        // the same name suffix ReceivedItemSpawner/GadgetItemSpawner tag
        // every cosmetic spawn with, and dumps their renderers for real.
        // WHY THIS EXISTS (2026-09-23). A cloned backpack accepts nothing
        // stowed into it, while a vanilla one accepts everything — measured
        // in co-op. The suspect was named by a warning the log had been
        // repeating at every single gadget spawn without anyone reading it:
        //
        //     Failed to add ticket 41356 to ticketOffice. Duplicate ticket
        //
        // `PropHome` implements `ITicketed`, and `TicketOffice` is a
        // `Dictionary<ushort, ITicketed>` — so a ticket is the stable,
        // shared identity by which a home is referred to. A clone inherits
        // its template's ticket, the office refuses the duplicate, and the
        // clone's homes end up addressable by nobody.
        //
        // That is a hypothesis with a good motive, and it stays a hypothesis
        // until this prints. It dumps each clone's homes beside the hidden
        // VANILLA instance of the same prefab, which is still in the scene
        // since the removal became a local hide — the two lists side by side
        // should differ in exactly one column if the story above is right.
        //
        // It is deliberately not a fix. Handing out fresh tickets means
        // inventing a shared identity that every machine must agree on, and
        // getting that wrong does not fail quietly: a ticket that collides
        // with a real one points the game at the wrong switch. Measure, then
        // build the scheme.
        private static void DumpHomeTickets(Prop prop, string label)
        {
            var homes = prop.gameObject.GetComponentsInChildren<PropHome>(true);
            if (homes == null || homes.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]     {label}: no PropHome at all.");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]     {label}: {homes.Length} home(s)");
            foreach (var home in homes)
            {
                if (home == null)
                    continue;

                var ticket = home.ticket;
                var known = "no";
                try
                {
                    var office = LobbyNetworking.TicketOffice.instance;
                    if (office != null && office.tickets != null && office.tickets.ContainsKey(ticket))
                        known = office.tickets[ticket] == null
                            ? "yes (null)"
                            : (office.tickets[ticket].Pointer == home.Pointer ? "yes, US" : "yes, SOMEBODY ELSE");
                }
                catch (Exception ex)
                {
                    known = $"unreadable ({ex.Message})";
                }

                var wornBy = home.parentCharacter != null ? home.parentCharacter.gameObject.name : "<nobody>";
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPropLookup)}]       '{home.gameObject.name}' ticket={ticket} inOffice={known} "
                    + $"| saveableHomeName={home.saveableHomeName} isInventory={home.isInventory} "
                    + $"blockPlacing={home.blockPlacing} blockGrabbing={home.blockGrabbing} "
                    + $"pinGroup={home.pinGroup} "
                    + $"parentCharacter={wornBy}");
            }
        }

        // The hidden vanilla instance of the same prefab, for the comparison
        // the dump above is for. Inactive since the hiding pass, hence the
        // Include.
        private static void DumpVanillaCounterpart(string cosmeticName)
        {
            var prefabName = cosmeticName.Replace(
                BigWalkArchipelago.Core.ReceivedItemSpawner.CosmeticNameSuffix, string.Empty).Trim();

            var all = UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return;

            foreach (var candidate in all)
            {
                if (candidate == null || candidate.gameObject == null)
                    continue;

                var name = candidate.gameObject.name;
                if (name.Contains(BigWalkArchipelago.Core.ReceivedItemSpawner.CosmeticNameSuffix, System.StringComparison.Ordinal)
                    || name.Contains("(AP template)", System.StringComparison.Ordinal)
                    || !name.StartsWith(prefabName, System.StringComparison.Ordinal))
                    continue;

                DumpHomeTickets(candidate, $"VANILLA '{name}' (active={candidate.gameObject.activeInHierarchy})");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]     no vanilla '{prefabName}' left in the scene to compare against.");
        }

        // WHY THIS EXISTS (2026-09-25). A received lamp is built from a
        // buoy light of the island, and since the same day the buoy is drawn
        // at random, on the premise that the island's buoys come in the
        // game's two colours. A player sent five and got five red ones: the
        // premise is wrong somewhere, and three explanations fit equally
        // well — the green ones are a different prefab the capture never
        // matches, they lie beyond the 32 it keeps, or the colour is not the
        // buoy's own at all and is applied at run time. This tells them apart
        // in one keypress instead of a fourth guess: every prop whose name
        // mentions a buoy or a lamp, grouped by name, material and light
        // colour, with the captured templates counted apart.
        private static void DumpLampCensus()
        {
            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] === lamp census ===");

            var all = UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return;

            var groups = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            var total = 0;

            foreach (var prop in all)
            {
                if (prop == null || prop.gameObject == null)
                    continue;

                var name = prop.gameObject.name;
                if (name.IndexOf("Buoy", System.StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("Lamp", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                total++;

                var role = name.Contains("(AP template)", System.StringComparison.Ordinal) ? "TEMPLATE"
                    : name.Contains(BigWalkArchipelago.Core.ReceivedItemSpawner.CosmeticNameSuffix, System.StringComparison.Ordinal) ? "CLONE"
                    : "vanilla";

                var baseName = name;
                var paren = baseName.IndexOf(" (", System.StringComparison.Ordinal);
                if (paren > 0)
                    baseName = baseName.Substring(0, paren);

                var key = $"{role,-8} {baseName} | active={prop.gameObject.activeInHierarchy} | {DescribeLook(prop.gameObject)}";
                groups[key] = groups.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]   {total} prop(s) mention a buoy or a lamp, in {groups.Count} group(s):");
            foreach (var pair in groups)
                Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]   {pair.Value.ToString().PadLeft(3)} x {pair.Key}");
        }

        // What decides a lamp's colour, whichever it turns out to be: the
        // emitted light, the first renderer's material, and any colour held in
        // that renderer's property block or material under the usual names.
        private static string DescribeLook(GameObject root)
        {
            var parts = new List<string>();

            try
            {
                var light = root.GetComponentInChildren<Light>(true);
                parts.Add(light != null ? $"light=#{ColorUtility.ToHtmlStringRGB(light.color)}" : "light=<none>");
            }
            catch (System.Exception ex)
            {
                parts.Add($"light=<{ex.Message}>");
            }

            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                {
                    parts.Add("renderer=<none>");
                    return string.Join(" | ", parts);
                }

                var renderer = renderers[0];
                var material = renderer.sharedMaterial;
                parts.Add($"material={(material != null ? material.name : "<none>")}");

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                foreach (var property in new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor", "_RColor" })
                {
                    if (!block.isEmpty && block.HasColor(property))
                        parts.Add($"block{property}=#{ColorUtility.ToHtmlStringRGB(block.GetColor(property))}");
                    else if (material != null && material.HasProperty(property))
                        parts.Add($"mat{property}=#{ColorUtility.ToHtmlStringRGB(material.GetColor(property))}");
                }
            }
            catch (System.Exception ex)
            {
                parts.Add($"renderer=<{ex.Message}>");
            }

            return string.Join(" | ", parts);
        }

        internal static void DumpCosmeticGadgets()
        {
            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] === cosmetic gadget clones ===");

            var all = UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] No props loaded.");
                return;
            }

            var found = 0;
            foreach (var prop in all)
            {
                if (prop == null || prop.gameObject == null
                    || !prop.gameObject.name.Contains(BigWalkArchipelago.Core.ReceivedItemSpawner.CosmeticNameSuffix, System.StringComparison.Ordinal))
                    continue;

                found++;
                Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]   '{prop.gameObject.name}' | active={prop.gameObject.activeInHierarchy}");

                DumpHomeTickets(prop, "CLONE");
                DumpVanillaCounterpart(prop.gameObject.name);

                var renderers = prop.gameObject.GetComponentsInChildren<Renderer>(true);
                Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}]     {renderers.Length} renderer(s):");
                foreach (var renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    var sharedMaterial = renderer.sharedMaterial;
                    var materialName = sharedMaterial != null ? sharedMaterial.name : "<null>";
                    var shaderName = sharedMaterial != null && sharedMaterial.shader != null
                        ? sharedMaterial.shader.name
                        : "<no shader>";

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);

                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPropLookup)}]       '{renderer.gameObject.name}' | material={materialName} | shader={shaderName}"
                        + $" | propertyBlockEmpty={block.isEmpty} | lightmapIndex={renderer.lightmapIndex} | enabled={renderer.enabled}");
                }
            }

            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] {found} cosmetic clone(s) found.");

            DumpLampCensus();
            Plugin.Log.LogInfo($"[{nameof(DebugPropLookup)}] === end of cosmetic gadget clones ===");
        }
    }
}
