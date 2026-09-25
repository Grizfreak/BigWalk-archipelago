using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The five hand props used as Archipelago filler items, in the order
    // data.py's FILLER_ITEMS lists them (ApLocationIds.TryResolveGadgetItem
    // depends on that order matching). Chosen from the Debug.DumpPropsKey
    // inventory (mod/reverse-engineering-notes.md, "props as items") for
    // being plain, ordinary objects nobody would miss narratively — unlike
    // the flare gun's Blue/Green/Yellow variants or a walkie-talkie, which
    // stay in the world doing whatever they already do.
    internal enum GadgetKind
    {
        Megaphone,
        WalkieTalkie,
        Backpack,
        Belt,
        FlareGun,
        Laser,
        Binoculars,
        Compass,
        FoldingMap,
        Radio,
        GourdCarton,
        Torch,
        Lamp,
        XrayGoggles,
        FlareGunBlue,
        FlareGunGreen,
        FlareGunYellow,
    }

    // Generic sibling of ReceivedItemSpawner for the island's own hand props
    // (megaphone, walkie-talkie, backpack, belt/holster, flare gun), used as
    // Archipelago filler items (player request, 2026-09-22: "the items in
    // the game, like backpacks, belts, megaphone, talkie walkie"). Same
    // clone-and-network-spawn technique as a cosmetic gourd, generalized to
    // a plain Prop instead of a RewardGourd — none of these carry a
    // gourdState or a puzzle-coloured look, so that half of the gourd
    // machinery (ApplyCosmeticColor, propsToMakeSavable) does not apply.
    //
    // Unlike a gourd, the VANILLA instances of these props are also taken
    // out of the map (HideVanillaInstances, driven by VanillaGadgetRemover)
    // — player decision, 2026-09-22: a filler item must not also be
    // findable lying around for free. That makes the template a one-shot
    // capture taken locally on each machine BEFORE the hiding
    // (CaptureTemplates), rather than "whatever is loaded right now" like
    // ReceivedItemSpawner.FindTemplate — once the real instances are out of
    // the way there is nothing left in the world to clone from.
    //
    // THAT HIDING IS LOCAL, AND IT IS THE WHOLE POINT (2026-09-22, measured
    // in co-op). It used to be a host-side NetworkServer.Destroy, chosen
    // because SetActive(false) is not replicated — and it replicated far too
    // well. A guest joins a world whose props the host destroyed on its own
    // world load, so the guest's CaptureTemplates finds nothing at all, and
    // the first gadget the host is sent arrives at a client that cannot
    // build it. The guest's log proved it by its single success: exactly one
    // template captured, the Lamp, which is the one kind KeptInWorld spares.
    // Each machine now hides its own copies for itself, which needs no
    // replication at all.
    internal static class GadgetItemSpawner
    {
        private static readonly Dictionary<GadgetKind, string> PrefabNames = new()
        {
            { GadgetKind.Megaphone, "MegaphoneProp" },
            { GadgetKind.WalkieTalkie, "WalkieTalkieProp" },
            { GadgetKind.Backpack, "BackpackProp" },
            { GadgetKind.Belt, "HolsterProp" },
            { GadgetKind.FlareGun, "FlareGunProp" },
            { GadgetKind.Laser, "LaserProp" },
            { GadgetKind.Binoculars, "BinocularsProp" },
            { GadgetKind.Compass, "CompassProp" },
            { GadgetKind.FoldingMap, "FoldingMapProp" },
            { GadgetKind.Radio, "FmRadioProp" },
            // The two-sided carton players carry gourds in (player
            // description, 2026-09-22: "the special 2 sided backpack for
            // gourds") — confirmed via Ctrl+J (DebugPropLookup): 1 instance,
            // 'GoesInBackpack'. Distinct from FmRadioManager/RadioStations,
            // which are the tower broadcast system and unrelated to this
            // portable prop.
            { GadgetKind.GourdCarton, "GourdCartonProp" },
            { GadgetKind.Torch, "TorchProp" },
            // What the player calls "lamp" (2026-09-22): the only prop a
            // widened Ctrl+J (lamp/ray/torch/light name hints) ever turned
            // up besides TorchProp. Left in the world — see RemoveFromWorld
            // below — a marine buoy light is scenery worth keeping lit.
            { GadgetKind.Lamp, "BuoyLight" },
            // The player's "ray-x gun" (2026-09-22): identified via the new
            // Ctrl+H (DebugHeldItemLookup) rather than guessed, since it
            // never showed up in any name-hint dump — "ray-x" was the
            // player's own reading of "X-ray", not a gun at all.
            { GadgetKind.XrayGoggles, "XrayGogglesProp" },
            // The three colour variants of the flare gun (player request,
            // 2026-09-22: "could we have also other flare guns in the
            // pool?") — distinct prefabs, not a colour-block variant of
            // the base FlareGunProp, per the earlier prop inventory.
            { GadgetKind.FlareGunBlue, "FlareGunPropBlue" },
            { GadgetKind.FlareGunGreen, "FlareGunPropGreen" },
            { GadgetKind.FlareGunYellow, "FlareGunPropYellow" },
        };

        // Kinds whose vanilla instances stay in the world instead of being
        // destroyed by RemoveVanillaInstances. Lamp only, by player decision
        // (2026-09-22) — every other gadget here follows the default (every
        // other filler item must not also be findable for free, see the
        // type comment above), Lamp is the one exception.
        // Other vanilla props a kind may take its look from, beside its own
        // PrefabNames entry.
        //
        // MEASURED 2026-09-25 with the Ctrl+M lamp census: the island's round
        // lamps are three prefabs, not one — 65 BuoyLight and 3 BuoyRedProp,
        // whose light is #FF9980 (red), and 44 BuoyProp, whose light is
        // #FFB766 (the yellow one). All three share one material with a white
        // tint, so the colour is baked into the mesh and the light, and a
        // lamp can only look yellow if it is cloned from a BuoyProp. The
        // capture used to match BuoyLight alone, which is why five lamps drawn
        // at random all came out red: every one of the 32 templates was.
        //
        // Only where the look is taken from. The clone is still named after
        // PrefabNames, so everything that recognises a received lamp by name
        // keeps recognising it; and the Lamp is a kind the island keeps, so
        // none of these is ever hidden.
        private static readonly Dictionary<GadgetKind, string[]> AlternatePrefabNames = new()
        {
            { GadgetKind.Lamp, new[] { "BuoyProp", "BuoyRedProp" } },
        };

        private static bool MatchesKind(GadgetKind kind, string name)
        {
            if (NameMatches(name, PrefabNames[kind]))
                return true;

            if (AlternatePrefabNames.TryGetValue(kind, out var alternates))
            {
                foreach (var alternate in alternates)
                {
                    if (NameMatches(name, alternate))
                        return true;
                }
            }

            return false;
        }

        private static readonly HashSet<GadgetKind> KeptInWorld = new()
        {
            GadgetKind.Lamp,
        };

        // A block of our own, clear of ReceivedItemSpawner.CosmeticAssetId
        // (0xB16_9A00) and of anything Mirror or the game might register —
        // the value only has to be agreed on by every machine, which a
        // shared formula guarantees. The SpawnDelegate signature Mirror
        // accepts is (Vector3, uint) only, with no room for a payload saying
        // which gadget, so the asset id itself is what tells a client what
        // to build — same reasoning as CosmeticAssetId's own comment.
        //
        // ONE PER (KIND, VANILLA INSTANCE) SINCE 2026-09-23, where it used to
        // be one per kind (0xB16_9B01 to 0xB16_9B11, retired rather than
        // reused). The instance matters because of tickets: a clone carries
        // the tickets of the vanilla prop its template was taken from, the
        // ticket office accepts one holder per ticket, and the second
        // backpack of a session — built from the same template as the first —
        // accepted nothing stowed into it (measured in co-op that day). So the
        // host builds each clone from a DIFFERENT vanilla instance whose
        // tickets are free, and the asset id is how every client learns which.
        private const uint InstanceAssetIdBase = 0xB16_9C00;

        // More than any kind has in the island (torches, the most numerous,
        // count nine) and small enough for all 17 kinds to stay inside one
        // block: 0xB16_9C00 to 0xB16_9E1F.
        internal const int MaxInstancesPerKind = 32;

        // EXTRA SLOTS (2026-09-23). An index at or past the number of vanilla
        // instances of its kind is not an instance at all: it is a clone the
        // island has no spare prop for — the gourd carton exists once, so the
        // second carton a slot receives has nothing left to borrow tickets
        // from. Such a clone is built from the first template and given
        // tickets of its own, before it is ever switched on, since PropHome
        // registers in OnEnable.
        //
        // Invented tickets were refused for a day for two reasons, and this is
        // how each is met:
        //
        //  - Every machine must invent the SAME ticket. The formula below reads
        //    nothing but the kind, the index and the component's rank inside
        //    the clone, and the index already travels inside the asset id — so
        //    host and client compute the same number without talking.
        //
        //  - An invented ticket must never land on a real one. They are taken
        //    from the very top of the range (61184 to 65535), where nothing
        //    observed so far lives (27247, 41356 and 43035 were), and the host
        //    checks each one against its own office before using a slot at
        //    all. A taken ticket makes the slot unusable, never overwritten:
        //    the office refuses duplicates rather than replacing them, so the
        //    worst a collision can do is leave that one clone empty.
        //
        // What stays unmeasured: whether the game hands out any ticket at run
        // time, after the check. Nothing suggests it does.
        private const int MaxTicketsPerClone = 8;

        private const int ExtraTicketTop = 65535;

        private static ushort ExtraTicket(GadgetKind kind, int index, int rank)
        {
            return (ushort)(ExtraTicketTop - ((((int)kind * MaxInstancesPerKind) + index) * MaxTicketsPerClone + rank));
        }

        internal static uint AssetIdFor(GadgetKind kind, int index)
        {
            return InstanceAssetIdBase + (uint)((int)kind * MaxInstancesPerKind + index);
        }

        internal static bool TryDecodeAssetId(uint assetId, out GadgetKind kind, out int index)
        {
            kind = default;
            index = 0;
            if (assetId < InstanceAssetIdBase)
                return false;

            var offset = assetId - InstanceAssetIdBase;
            var kindValue = (int)(offset / MaxInstancesPerKind);
            if (!Enum.IsDefined(typeof(GadgetKind), kindValue))
                return false;

            kind = (GadgetKind)kindValue;
            index = (int)(offset % MaxInstancesPerKind);
            return true;
        }

        internal static IEnumerable<uint> AllAssetIds()
        {
            foreach (GadgetKind kind in Enum.GetValues(typeof(GadgetKind)))
            {
                for (var index = 0; index < MaxInstancesPerKind; index++)
                    yield return AssetIdFor(kind, index);
            }
        }

        // Marks both a captured template and a spawned pickup as "ours",
        // the same way ReceivedItemSpawner.CosmeticNameSuffix does for a
        // gourd clone. A template is never spawned over the network itself
        // (only clones built FROM it are), so it needs a suffix of its own
        // rather than reusing that one.
        private const string TemplateNameSuffix = "(AP template)";
        private const string PlaceholderNameSuffix = "(AP placeholder)";

        // One template per vanilla instance of each kind, in savablePropGuid
        // order — see CaptureTemplate for why that key and no other.
        private static readonly Dictionary<GadgetKind, List<Prop>> _templates = new();

        // Whether HideVanillaInstances has run since the world last loaded.
        // See EnsureVanillaHidden.
        private static bool _hiddenThisWorld;

        // What HideVanillaInstances switched off on THIS machine, kept so
        // ReHideVanillaInstances can put back anything Mirror switches on
        // again. Around 68 objects, against every Prop in the world for a
        // full scan.
        private static readonly List<GameObject> _hidden = new();

        // Called on EVERY machine (host and every client alike) once per
        // world load, before RemoveVanillaInstances runs on the host — see
        // VanillaGadgetRemover. Purely local: each machine clones a private,
        // inactive copy of whatever it currently sees loaded, mirroring
        // CosmeticGourdSpawnHandler's "each client builds its own from its
        // own scene" rather than trusting anything to travel over the wire.
        internal static void CaptureTemplates()
        {
            foreach (var kind in PrefabNames.Keys)
                CaptureTemplate(kind);
        }

        private static void CaptureTemplate(GadgetKind kind)
        {
            var prefabName = PrefabNames[kind];

            // INACTIVE ONES COUNT, and on a guest they are the only ones
            // there (2026-09-22, second co-op round). The host hides its 68
            // copies locally, and Mirror does not send a spawn message for a
            // deactivated object — so a client's own scene copies are never
            // switched on, and the default active-only scan walked straight
            // past all of them. The guest captured exactly one template, the
            // Lamp, which is the one kind the hiding pass spares: the same
            // control that caught the networked-destroy version of this bug
            // an hour earlier, pointing one step further in.
            //
            // Cloning an inactive source is the normal path here anyway —
            // the capture below deactivates its candidate before Instantiate
            // on purpose, so that the clone's Awake() is deferred.
            var all = UnityEngine.Object.FindObjectsByType<Prop>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return;

            var candidates = new List<Prop>();
            foreach (var candidate in all)
            {
                if (candidate == null || candidate.gameObject == null)
                    continue;

                if (!MatchesKind(kind, candidate.gameObject.name) || IsOurs(candidate.gameObject.name))
                    continue;

                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] No {kind} ({prefabName}) found in the scene to capture as a template; a received one will have nothing to clone from.");
                return;
            }

            // SORTED BY savablePropGuid, and the order is the whole point.
            // The host picks a template by index and sends the index inside
            // the asset id; a client builds from ITS template at that index.
            // The two lists must therefore line up on every machine, and
            // FindObjectsByType promises no order at all. The guid is the
            // game's own per-instance identity, serialized with the scene,
            // so it reads the same everywhere — which neither a name
            // ("BackpackProp" is shared) nor a position (a prop that was
            // active on the host may have been knocked about before it was
            // hidden) can promise.
            candidates.Sort(CompareByGuid);
            if (candidates.Count > MaxInstancesPerKind)
                candidates.RemoveRange(MaxInstancesPerKind, candidates.Count - MaxInstancesPerKind);

            var templates = new List<Prop>();
            foreach (var candidate in candidates)
            {
                var template = BuildTemplate(candidate, prefabName);
                if (template != null)
                    templates.Add(template);
            }

            // Re-armed on every world load (VanillaGadgetRemover), so the
            // previous world's templates would otherwise leak for the life
            // of the process.
            if (_templates.TryGetValue(kind, out var previous) && previous != null)
            {
                foreach (var old in previous)
                {
                    if (old != null && old.gameObject != null)
                        UnityEngine.Object.Destroy(old.gameObject);
                }
            }

            _templates[kind] = templates;
            Plugin.Log.LogInfo(
                $"[{nameof(GadgetItemSpawner)}] {kind}: {templates.Count} template(s) captured, one per vanilla "
                + $"'{prefabName}'{(AlternatePrefabNames.ContainsKey(kind) ? " or its alternates" : string.Empty)}.");
        }

        private static int CompareByGuid(Prop a, Prop b)
        {
            var byGuid = string.CompareOrdinal(a.savablePropGuid ?? string.Empty, b.savablePropGuid ?? string.Empty);
            return byGuid != 0 ? byGuid : string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
        }

        private static Prop BuildTemplate(Prop candidate, string prefabName)
        {
            // Captured BEFORE Instantiate (Instantiate does not copy a
            // per-instance MaterialPropertyBlock, unlike every serialized
            // reference — cf. ReceivedItemSpawner.CapturePropertyBlocks),
            // and reapplied below: without this the template itself
            // bakes in the shader-pink/magenta look, and every clone made
            // from it inherits that broken state for the rest of the
            // session.
            var propertyBlocks = ReceivedItemSpawner.CapturePropertyBlocks(candidate.gameObject);

            // Same "disable before Instantiate" trick BuildNeutralizedClone
            // (and the gourd version before it) uses: it defers the
            // clone's Awake()/OnEnable() until SetActive(true) is called
            // explicitly, which here never happens — the template is
            // meant to stay inactive for the rest of the session.
            var templateWasActive = candidate.gameObject.activeSelf;
            GameObject clone;
            try
            {
                candidate.gameObject.SetActive(false);
                clone = UnityEngine.Object.Instantiate(candidate.gameObject);
            }
            finally
            {
                // Restored regardless: this candidate is a vanilla
                // instance RemoveVanillaInstances is about to destroy
                // anyway (on the host), and on a client it is simply the
                // real prop players still see until the host's
                // NetworkServer.Destroy message reaches them.
                if (templateWasActive)
                    candidate.gameObject.SetActive(true);
            }

            clone.name = $"{prefabName} {TemplateNameSuffix}";
            ReceivedItemSpawner.ApplyPropertyBlocks(clone, propertyBlocks);
            ReceivedItemSpawner.ClearLightmapReferences(clone);
            ReceivedItemSpawner.FixMaterialessRenderers(clone);
            ReceivedItemSpawner.RefreshPropertyBlockHelpers(clone);
            clone.SetActive(false);

            var identity = clone.GetComponent<NetworkIdentity>();
            if (identity != null)
                identity.sceneId = 0;

            return clone.GetComponent<Prop>();
        }

        private static int VanillaCount(GadgetKind kind)
        {
            return _templates.TryGetValue(kind, out var list) && list != null ? list.Count : 0;
        }

        // How many indices can borrow a vanilla instance's tickets.
        //
        // NONE, for a kind the island keeps (2026-09-25, found with a real
        // second player). The Lamp is never hidden, so every buoy light's
        // tickets stay registered to the buoy itself — a lamp clone borrowing
        // one was refused, every time. Worse, the capture had filled all 32
        // indices with buoys, which left no extra slot to invent tickets in:
        // the host's log read "All 32 Lamp slot(s) are held by live clones"
        // 55 times in one evening, every received lamp fell back to the first
        // template, its switch was registered nowhere (the desync between
        // players) and it always wore that template's colour (red, where the
        // game has two). A kept kind now goes straight to invented tickets,
        // all 32 of its indices.
        private static int VanillaUsable(GadgetKind kind)
        {
            return KeptInWorld.Contains(kind) ? 0 : VanillaCount(kind);
        }

        // The template a given index is built from, and whether that index is
        // an extra slot rather than a vanilla instance of its own.
        //
        // An extra slot walks the templates in turn rather than always taking
        // the first, so that what a kind looks like in the island — two
        // colours of buoy light — is what a player receives too. The template
        // only lends its look to an extra slot, never its tickets, and the
        // index decides which one, so every machine picks the same.
        private static Prop ResolveTemplate(GadgetKind kind, int index, out bool extra)
        {
            var count = VanillaCount(kind);
            var usable = VanillaUsable(kind);
            extra = count > 0 && index >= usable;
            return GetTemplate(kind, extra ? (index - usable) % count : index);
        }

        // RANDOM, NOT FIRST (player request, 2026-09-25: "a way to randomise
        // the colour?"). Which free slot the host takes decides what the
        // clone looks like — each slot's template is a different vanilla
        // instance, and for the lamp a different buoy, red or green — so
        // drawing the slot at random draws the look at random, in the
        // proportions the island itself has.
        //
        // Only the host ever draws. The slot it picked travels inside the
        // asset id and every client builds exactly that one, so the two
        // screens cannot disagree about a colour.
        private static readonly System.Random _draw = new();

        private static int PickAtRandom(List<int> candidates)
        {
            return candidates[_draw.Next(candidates.Count)];
        }

        private static Prop GetTemplate(GadgetKind kind, int index = 0)
        {
            if (!_templates.TryGetValue(kind, out var list) || list == null || list.Count == 0)
                return null;

            if (index < 0 || index >= list.Count)
            {
                // Only a client can get here: told to build an instance it
                // does not have. Every machine loads the same scene, so this
                // is not expected — but a clone with the wrong tickets is
                // better than no clone at all, and the log says which.
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] Asked for {kind} instance {index}, but only {list.Count} were captured here; using the first.");
                index = 0;
            }

            var template = list[index];
            return template != null ? template : null;
        }

        // Which vanilla instance the next clone of this kind is built from.
        //
        // HOST ONLY. It reads this machine's ticket office, and the answer
        // travels to every client inside the asset id, so nothing about a
        // client's own office ever matters. The first instance whose tickets
        // are all free wins: every vanilla one is hidden, so a ticket still
        // in the office belongs to a clone that is alive right now.
        private static int ChooseInstance(GadgetKind kind)
        {
            if (!_templates.TryGetValue(kind, out var list) || list == null || list.Count == 0 || list[0] == null)
                return 0;

            // Nothing ticketed in this kind means nothing to share: every
            // template is available, so any of them. Asked of every template,
            // not the first: a kind can mix prefabs (the lamp does) whose
            // components differ.
            var needed = Math.Min(TicketsOf(list[0].gameObject).Count, MaxTicketsPerClone);
            var anyTicketed = list.Exists(template => template != null && TicketsOf(template.gameObject).Count > 0);
            if (!anyTicketed)
            {
                var all = new List<int>();
                for (var index = 0; index < list.Count; index++)
                    all.Add(index);
                return PickAtRandom(all);
            }

            var usable = VanillaUsable(kind);
            var candidates = new List<int>();
            for (var index = 0; index < usable; index++)
            {
                var template = list[index];
                if (template != null && TicketsOf(template.gameObject).TrueForAll(IsTicketFree))
                    candidates.Add(index);
            }

            if (candidates.Count > 0)
                return PickAtRandom(candidates);

            // Every vanilla instance is in use, or the kind never borrows one:
            // the extra slots, which invent as many tickets as the kind has
            // ticketed components.
            for (var index = usable; index < MaxInstancesPerKind; index++)
            {
                // The tickets of the template this slot would really be built
                // from: with the lamp's three prefabs mixed in one list, the
                // first template is not a safe stand-in for all of them.
                var template = list[(index - usable) % list.Count];
                var slotNeeds = template != null
                    ? Math.Min(TicketsOf(template.gameObject).Count, MaxTicketsPerClone)
                    : needed;

                var free = true;
                for (var rank = 0; rank < slotNeeds && free; rank++)
                    free = IsTicketFree(ExtraTicket(kind, index, rank));

                if (free)
                    candidates.Add(index);
            }

            if (candidates.Count > 0)
                return PickAtRandom(candidates);

            Plugin.Log.LogWarning(
                $"[{nameof(GadgetItemSpawner)}] All {MaxInstancesPerKind} {kind} slot(s) are held by live clones; this one "
                + "will look right and hold nothing.");
            return usable < MaxInstancesPerKind ? usable : 0;
        }

        // Gives an extra-slot clone its invented tickets. Called on the clone
        // while it is still inactive, so that its components register the new
        // numbers when it is switched on rather than the template's.
        //
        // The rank is the component's position in a fixed walk — homes, then
        // switches, then tracked states, each in hierarchy order — which is
        // the same on every machine because every clone of a kind is a copy
        // of the same prefab. Components carrying ticket 0 are skipped here
        // exactly as TicketsOf skips them, so both count the same things.
        private static void AssignExtraTickets(GameObject clone, GadgetKind kind, int index)
        {
            var rank = 0;
            var skipped = 0;

            foreach (var home in clone.GetComponentsInChildren<PropHome>(true))
            {
                if (home == null || home.ticket == 0)
                    continue;

                if (rank < MaxTicketsPerClone)
                    home.ticket = ExtraTicket(kind, index, rank);
                else
                    skipped++;
                rank++;
            }

            foreach (var peck in clone.GetComponentsInChildren<PeckSwitch>(true))
            {
                if (peck == null || peck.ticket == 0)
                    continue;

                if (rank < MaxTicketsPerClone)
                    peck.ticket = ExtraTicket(kind, index, rank);
                else
                    skipped++;
                rank++;
            }

            foreach (var state in clone.GetComponentsInChildren<TrackedPeckState>(true))
            {
                if (state == null || state.ticket == 0)
                    continue;

                if (rank < MaxTicketsPerClone)
                    state.ticket = ExtraTicket(kind, index, rank);
                else
                    skipped++;
                rank++;
            }

            var why = KeptInWorld.Contains(kind)
                ? "a kind the island keeps, so it never borrows a vanilla ticket"
                : $"past the {VanillaCount(kind)} vanilla instance(s)";
            Plugin.Log.LogInfo(
                $"[{nameof(GadgetItemSpawner)}] {kind} #{index} is an extra slot ({why}): gave it "
                + $"{Math.Min(rank, MaxTicketsPerClone)} invented ticket(s) from {ExtraTicket(kind, index, 0)} down.");

            if (skipped > 0)
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] {kind} #{index} has {skipped} more ticketed component(s) than the "
                    + $"{MaxTicketsPerClone} a slot reserves; those keep the template's ticket and will not work.");
        }

        // Ticket 0 is skipped: a component carrying it has no identity to
        // protect, and counting it would make every such kind look taken.
        private static List<ushort> TicketsOf(GameObject root)
        {
            var tickets = new List<ushort>();

            foreach (var home in root.GetComponentsInChildren<PropHome>(true))
            {
                if (home != null && home.ticket != 0)
                    tickets.Add(home.ticket);
            }

            foreach (var peck in root.GetComponentsInChildren<PeckSwitch>(true))
            {
                if (peck != null && peck.ticket != 0)
                    tickets.Add(peck.ticket);
            }

            foreach (var state in root.GetComponentsInChildren<TrackedPeckState>(true))
            {
                if (state != null && state.ticket != 0)
                    tickets.Add(state.ticket);
            }

            return tickets;
        }

        private static bool IsTicketFree(ushort ticket)
        {
            try
            {
                var office = LobbyNetworking.TicketOffice.instance;
                return office == null || office.tickets == null || !office.tickets.ContainsKey(ticket);
            }
            catch (Exception)
            {
                return true;
            }
        }

        // The other half of the same measurement. On 2026-09-23 the host's
        // log read "An incoming Backpack arrived before the world-ready
        // sweep": items replayed at connection can be built before
        // VanillaGadgetRemover has hidden anything, and at that moment the
        // vanilla backpack still held its tickets. The clone was refused, and
        // PropHome only registers in OnEnable, so it never tried again even
        // once the vanilla let go. So the host hides first, whenever the
        // sweep has not happened yet in this world.
        private static void EnsureVanillaHidden()
        {
            if (_hiddenThisWorld || !ModConfig.ArchipelagoEnabled.Value)
                return;

            HideVanillaInstances();
        }

        // Called by VanillaGadgetRemover whenever no world is ready, so the
        // next world starts out not hidden.
        internal static void ForgetWorld()
        {
            _hiddenThisWorld = false;
        }

        private static float _nextLateCapture;

        private const float LateCaptureCooldownSeconds = 1f;

        // Throttled, and deliberately captures ALL the kinds rather than the
        // one asked for: a spawn wave arrives in one burst, so the first
        // gadget that finds nothing pays for one scan and the rest of the
        // burst finds its template waiting. Without the throttle a wave that
        // is genuinely too early — the scene not loaded at all — would scan
        // every Prop in the world once per spawn.
        private static void TryCaptureLate(GadgetKind kind)
        {
            if (Time.time < _nextLateCapture)
                return;

            _nextLateCapture = Time.time + LateCaptureCooldownSeconds;

            Plugin.Log.LogInfo(
                $"[{nameof(GadgetItemSpawner)}] An incoming {kind} arrived before the world-ready sweep; capturing "
                + "the templates now.");
            CaptureTemplates();
        }

        // Called right after CaptureTemplates, on EVERY machine, host and
        // guest alike, and entirely locally.
        //
        // It was host-only and used NetworkServer.Destroy until 2026-09-22,
        // on the reasoning that SetActive(false) does not replicate and
        // these props therefore had to be destroyed to leave everybody's
        // map. See the note at the top of this file for what that cost in
        // co-op the same evening.
        //
        // Hiding also beats destroying locally, which would be the obvious
        // alternative: these are networked scene objects, and tearing one
        // out of a client's spawned table while the server still believes in
        // it is the same class of failure this change exists to remove.
        // Deactivated, the identity stays alive and only this machine stops
        // drawing and colliding with it.
        //
        // The template captured just above is inactive and therefore already
        // excluded by the default (active-only) FindObjectsByType scan, so
        // this never hides its own template.
        internal static int HideVanillaInstances()
        {
            var hidden = 0;
            _hidden.Clear();

            // Inactive ones included, for the reason CaptureTemplate gives:
            // on a guest these props arrive switched off and must be kept
            // that way, so they belong in _hidden even though there is
            // nothing to switch off today. Mirror turning one on later is
            // exactly what ReHideVanillaInstances is for.
            var all = UnityEngine.Object.FindObjectsByType<Prop>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return 0;

            foreach (var prop in all)
            {
                if (prop == null || prop.gameObject == null || IsOurs(prop.gameObject.name))
                    continue;

                var matched = MatchKind(prop.gameObject.name);
                if (matched == null || KeptInWorld.Contains(matched.Value))
                    continue;

                try
                {
                    var wasActive = prop.gameObject.activeSelf;
                    if (wasActive)
                        prop.gameObject.SetActive(false);

                    _hidden.Add(prop.gameObject);
                    if (wasActive)
                        hidden++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(GadgetItemSpawner)}] Could not hide a vanilla gadget prop: {ex.Message}");
                }
            }

            Plugin.Log.LogInfo(
                $"[{nameof(GadgetItemSpawner)}] Hid {hidden} vanilla gadget prop(s) from this machine's map, and is "
                + $"keeping {_hidden.Count - hidden} that were already off.");

            // Only a sweep that found something counts: one run before the
            // world has loaded finds nothing, and must not stop the next.
            _hiddenThisWorld = _hidden.Count > 0;

            return hidden;
        }

        // Anything that came back on since the sweep. Mirror calls
        // SetActive(true) on a scene object when it spawns it, so a spawn
        // wave arriving after the sweep — a late joiner's, or interest
        // management rebuilding observers — can undo it. Walks only what was
        // hidden, not the world.
        internal static int ReHideVanillaInstances()
        {
            var again = 0;

            foreach (var hidden in _hidden)
            {
                if (hidden == null)
                    continue;

                try
                {
                    if (!hidden.activeSelf)
                        continue;

                    hidden.SetActive(false);
                    again++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(GadgetItemSpawner)}] Could not hide a vanilla gadget prop again: {ex.Message}");
                }
            }

            if (again > 0)
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] {again} vanilla gadget prop(s) came back; hidden again.");

            return again;
        }

        // What the sweep leaves standing, by kind, so the rebuild can count
        // it as already there. Without this the resync cleared its tally to
        // zero and then restocked every gadget the ledger owed — including
        // the ones it had just been careful not to touch, which came back as
        // duplicates.
        private static void CountKept(Dictionary<GadgetKind, int> kept, Prop prop)
        {
            var kind = MatchKind(prop.gameObject.name);
            if (kind == null)
                return;

            kept[kind.Value] = kept.TryGetValue(kind.Value, out var count) ? count + 1 : 1;
        }

        private static GadgetKind? MatchKind(string name)
        {
            foreach (var pair in PrefabNames)
            {
                if (NameMatches(name, pair.Value))
                    return pair.Key;
            }

            return null;
        }

        // Exact-prefix match with a boundary character, not a plain
        // StartsWith: "FlareGunProp" must not also catch "FlareGunPropBlue"
        // (a real, distinct prop this mod leaves alone), only the base name
        // itself or a Unity-appended " (1)"-style suffix.
        private static bool NameMatches(string name, string exact)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name == exact || name.StartsWith(exact + " ", StringComparison.Ordinal);
        }

        private static bool IsOurs(string name)
        {
            return name.Contains(TemplateNameSuffix, StringComparison.Ordinal)
                || name.Contains(ReceivedItemSpawner.CosmeticNameSuffix, StringComparison.Ordinal);
        }

        // The gadget half of Ctrl+R, and the reason the key press needed one
        // at all (player request, 2026-09-22). A filler gadget has no
        // deposit sink: what is owed is "received, minus spawned this
        // session", forever. So a gadget that ends up somewhere unreachable
        // stays counted as spawned and is never replaced — and a gadget
        // still in someone's hands when the world is swept is counted twice
        // by the rebuild. Clearing both, and letting RestoreLooseGadgets put
        // back exactly what the ledger owes, is the same promise the gourds
        // already make.
        //
        // Scans Prop rather than a gadget type of its own: a cosmetic clone
        // is recognized by NeutralizeProgression having made it notSavable
        // plus the name suffix, which is precisely what IsCosmeticClone
        // asks. RewardGourd carries that mark too, so gourds are handed back
        // to their own sweep instead of being destroyed twice.
        // LOOSE is the operative word, and it is a rule, not a description
        // (player request, 2026-09-22): "the backpacks and belts WORN BY
        // PLAYERS must not answer this command, and neither must the objects
        // hanging inside them."
        //
        // A worn pack is not stranded — it is exactly where its owner put it
        // — and sweeping it would take the pack, everything stowed in it, and
        // the other player's afternoon with it. Worse, it is the one case
        // where the sweep would destroy something a SECOND player is using,
        // on a key only the host can press.
        //
        // What makes it decidable is PropHome, which says who a home belongs
        // to: `parentCharacter` is a home on a player (PropGroup.PackHanger,
        // WearHolster), `parentProp` is a home on another prop (a pack's own
        // GoesInBackpack/GoesInHolster slots), and `isInventory` marks a
        // stash. So the rule is simply: a prop that is in any home is placed,
        // not lost, and this sweep leaves it alone. Only a homeless prop is
        // loose.
        //
        // The second pass exists for the last case that rule does not cover:
        // a pack lying on the ground IS homeless, so it would be swept — and
        // destroying it would strand whatever is homed inside it, which is
        // the stale-reference bug this whole sweep exists to avoid. A prop
        // that is somebody's home is therefore kept too.
        internal static int DestroyLooseCosmeticGadgets(out Dictionary<GadgetKind, int> kept)
        {
            kept = new Dictionary<GadgetKind, int>();
            var destroyed = 0;
            var seen = 0;
            var placed = 0;

            // Inactive included, same as the gourd sweep: a prop that has
            // been picked up can leave the active hierarchy, and the default
            // scan would walk past exactly the one this is meant to reclaim.
            var all = UnityEngine.Object.FindObjectsByType<Prop>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return 0;

            var candidates = new List<Prop>();
            var occupiedHosts = new HashSet<int>();

            foreach (var prop in all)
            {
                if (prop == null || !ReceivedItemSpawner.IsCosmeticClone(prop))
                    continue;

                // A gourd is not this sweep's to remove, but it can still be
                // the reason a backpack or a carton has to stay: until
                // 2026-09-23 it was skipped before being asked, so a pack
                // lying on the ground with a gourd in it went, and the gourd
                // with it.
                if (prop.GetComponent<RewardGourd>() != null)
                {
                    var gourdHome = prop.currentHome;
                    if (gourdHome != null && gourdHome.parentProp != null)
                        occupiedHosts.Add(gourdHome.parentProp.GetInstanceID());
                    continue;
                }

                seen++;

                var home = prop.currentHome;
                if (home == null)
                {
                    candidates.Add(prop);
                    continue;
                }

                placed++;
                CountKept(kept, prop);

                // This prop stays, and so must whatever is holding it: a
                // backpack whose contents survive it would leave those
                // contents homed in a destroyed object.
                if (home.parentProp != null)
                    occupiedHosts.Add(home.parentProp.GetInstanceID());
            }

            foreach (var prop in candidates)
            {
                if (occupiedHosts.Contains(prop.GetInstanceID()))
                {
                    placed++;
                    CountKept(kept, prop);
                    continue;
                }

                // Tidying the hands is best effort; removing the prop is
                // not — the split that the gourd sweep learned the hard way
                // on 2026-09-15, when a failed drop aborted the removal and
                // left behind the one object that most needed reclaiming.
                ReceivedItemSpawner.ReleaseFromHands(prop);

                try
                {
                    NetworkServer.Destroy(prop.gameObject);
                    destroyed++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(GadgetItemSpawner)}] Could not remove a cosmetic gadget: {ex.Message}");
                }
            }

            if (placed > 0)
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] {placed} cosmetic gadget(s) left alone: worn, stowed or holding "
                    + "something that is.");

            seen -= placed;

            if (destroyed != seen)
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] {seen} loose cosmetic gadget(s) found but only {destroyed} removed; "
                    + "the rebuild will put back fewer than are owed.");

            return destroyed;
        }

        // Host entry point, called from ItemApplier.ApplyGadgetItem. Same
        // toPlayer split as ReceivedItemSpawner.SpawnCosmeticPickup: true
        // for a gadget arriving during play (hands, or in front of the
        // player), false for the session-start replay, which stocks it at
        // the hub instead.
        internal static GameObject SpawnCosmeticPickup(GadgetKind kind, bool toPlayer)
        {
            if (!NetworkServer.active)
                return null;

            try
            {
                var recipient = toPlayer ? ItemRecipients.Choose() : null;
                var resolved = ReceivedItemSpawner.ResolveSpawnPosition(toPlayer, recipient);
                if (resolved == null)
                {
                    ItemRecipients.Settled(recipient);
                    Plugin.Log.LogInfo(
                        $"[{nameof(GadgetItemSpawner)}] Nowhere to put a {kind} yet (no player and no InventorySpawn loaded), cosmetic spawn skipped.");
                    return null;
                }

                EnsureVanillaHidden();
                if (GetTemplate(kind) == null)
                    TryCaptureLate(kind);

                var index = ChooseInstance(kind);
                var prop = BuildNeutralizedClone(kind, resolved.Value, Quaternion.identity, index);
                if (prop == null)
                {
                    ItemRecipients.Settled(recipient);
                    return null;
                }

                NetworkServer.Spawn(prop.gameObject, AssetIdFor(kind, index));

                // Without this the clone stays "fixed" as in its original
                // resting pose — same reasoning as the gourd version.
                prop.SetLoose();
                PublishLooseStates(prop.gameObject);

                ReceivedItemSpawner.QueueHandover(prop, recipient);

                var identity = prop.GetComponent<NetworkIdentity>();
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] Cosmetic {kind} #{index} spawned at {prop.transform.position} (netId {(identity != null ? identity.netId : 0)}).");
                return prop.gameObject;
            }
            catch (Exception ex)
            {
                // Must never make the actual item reception fail — purely
                // cosmetic, same guarantee ReceivedItemSpawner gives.
                Plugin.Log.LogWarning($"[{nameof(GadgetItemSpawner)}] Cosmetic spawn failed, ignored: {ex}");
                return null;
            }
        }

        // Builds the clone WITHOUT spawning it — split out so GadgetSpawnHandler
        // can run exactly the same construction from its spawn handler, on a
        // client that never calls SpawnCosmeticPickup itself. Mirrors
        // ReceivedItemSpawner.BuildNeutralizedClone; the differences are
        // exactly the RewardGourd-only steps that one performs and this
        // has no equivalent for (gourdState, colour, propsToMakeSavable).
        internal static Prop BuildNeutralizedClone(GadgetKind kind, Vector3 position, Quaternion rotation, int index = 0)
        {
            var template = ResolveTemplate(kind, index, out var extra);
            if (template == null)
            {
                // Capture now rather than wait for the world-ready sweep.
                //
                // A guest joining a world that already has gadgets in it gets
                // Mirror's whole spawn wave before WorldManager says the world
                // is ready, so every one of those arrived with no template and
                // landed as an empty placeholder — 38 of them in one measured
                // reconnection (2026-09-23), invisible to that player for the
                // rest of the session. The log also showed how narrow the miss
                // was: the first placeholder and the first successful capture
                // are two lines apart. The props are already in the scene when
                // the wave arrives; only the sweep had not run yet. So run it.
                TryCaptureLate(kind);
                template = ResolveTemplate(kind, index, out extra);
            }

            if (template == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] No {kind} template captured yet, cosmetic spawn skipped.");
                return null;
            }

            // Same reasoning as CaptureTemplate: Instantiate does not carry
            // a per-instance MaterialPropertyBlock over, so without this
            // every gadget spawned from an (already-correct) template would
            // itself come out shader-pink/magenta.
            var templatePropertyBlocks = ReceivedItemSpawner.CapturePropertyBlocks(template.gameObject);

            var clone = UnityEngine.Object.Instantiate(template.gameObject, position, rotation);
            clone.name = $"{PrefabNames[kind]} {ReceivedItemSpawner.CosmeticNameSuffix}";
            ReceivedItemSpawner.ApplyPropertyBlocks(clone, templatePropertyBlocks);

            // Before activation, never after: the components register their
            // ticket in OnEnable, and a clone switched on with the template's
            // numbers is refused and never asks again.
            if (extra)
                AssignExtraTickets(clone, kind, index);

            var cloneIdentity = clone.GetComponent<NetworkIdentity>();
            if (cloneIdentity != null)
            {
                cloneIdentity.sceneId = 0;
                ReceivedItemSpawner.GetPrivatePropertySetter<NetworkIdentity>("hasSpawned")
                    ?.Invoke(cloneIdentity, new object[] { false });
            }

            var prop = clone.GetComponent<Prop>();
            if (prop == null)
            {
                Plugin.Log.LogWarning($"[{nameof(GadgetItemSpawner)}] Clone without a Prop, destroying and aborting.");
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            ReceivedItemSpawner.NeutralizeProgression(prop);
            StartLooseNotHung(clone);

            clone.SetActive(true);

            if (cloneIdentity != null)
                ReceivedItemSpawner.GetPrivatePropertySetter<NetworkIdentity>(nameof(NetworkIdentity.SpawnedFromInstantiate))
                    ?.Invoke(cloneIdentity, new object[] { false });

            // Re-applied AFTER activation, not just once on the template:
            // the template itself never runs Awake() (it stays inactive for
            // the whole session), but THIS clone does the moment
            // SetActive(true) runs above — and measured in-game (2026-09-22)
            // to re-enable the same materialless renderer the template had
            // already had disabled. Whatever component does that clearly
            // runs on activation, so the fix has to run after it, not before.
            ReceivedItemSpawner.ClearLightmapReferences(clone);
            ReceivedItemSpawner.FixMaterialessRenderers(clone);
            ReceivedItemSpawner.RefreshPropertyBlockHelpers(clone);
            SettleOffTheHanger(clone);

            return prop;
        }

        // A CLONE HAS NEVER BEEN ON A HANGER, SO IT MUST NOT LOOK HUNG
        // (2026-09-25, measured in co-op after the first alpha's report of a
        // belt "attached to an unseen object" and worn twice over).
        //
        // A belt carries three versions of its mesh: `belt_hung` on its
        // pavilion hanger, `belt_closed` from its first pick-up on, and `belt`
        // when worn. The worn/closed pair is switched by the belt's own
        // IsWornEffects; hung is switched off from outside it, once, when the
        // vanilla belt first leaves its hanger — and a clone is copied from a
        // template still hanging, and never leaves a hanger. So it went
        // around hung for good: on the ground, in hand, and worn, where the
        // log read `belt=True, belt_hung=True` against vanilla's `belt=True`
        // alone — the second belt in front of the wearer and in their shadow.
        //
        // So a clone starts as a vanilla prop does after its first pick-up:
        // every `<name>_hung` that has a `<name>_closed` beside it is swapped
        // for it. By name, because the backpack's straps come in the same
        // hung/closed pair and have the same history.
        //
        // THE MESHES FOLLOW STATES, AND IT IS THE STATES THAT MUST BE RIGHT
        // (measured the same day, host and guest logs side by side). The
        // swap below held on the host and not on a guest, because each mesh
        // is switched by a PeckEffectToggle following one of the belt's own
        // TrackedPeckStates — `belt_hung` follows "placed on hanger",
        // `belt_closed` follows "backpack not worn", `belt` follows
        // "backpackWornSystem" — and a clone copies those states from a
        // template still on its hanger. The guest's clone read "placed on
        // hanger" = 1, and the next time its effects were applied the hung
        // mesh came straight back. So the states are set first, before the
        // clone is switched on, to what a belt lying loose has: not on a
        // hanger, not worn. The backpack's straps answer to the same labels.
        private static readonly Dictionary<string, int> LooseStates = new()
        {
            { "placed on hanger", 0 },
            { "backpack not worn", 1 },
            { "backpackWornSystem", 0 },
        };

        //
        // AND THE STATES MUST NOT BE THE VANILLA PROP'S (decompiled after that
        // still failed on the guest). TrackedPeckState.Initialize reads its
        // starting value from the save, under its saveIdentity's saveGuid, and
        // SetState writes back under the same key. A clone copies the saveGuid
        // of the vanilla prop it was made from, so it read that belt's
        // "placed on hanger" = 1 and undid the value set here — and wearing a
        // clone wrote the vanilla belt's saved state. The clone's states lose
        // their save identity before they are switched on: a received gadget
        // is not the island's, and must neither read nor write the island's
        // record of it.
        private static void StartLooseNotHung(GameObject clone)
        {
            foreach (var state in clone.GetComponentsInChildren<TrackedPeckState>(true))
            {
                if (state == null)
                    continue;

                state.saveIdentity = null;

                if (state.label == null || !LooseStates.TryGetValue(state.label, out var value))
                    continue;

                try
                {
                    var context = state.currentPeckContext;
                    context.state = value;
                    state.currentPeckContext = context;

                    var predicted = state.peckContextForPrediction;
                    predicted.state = value;
                    state.peckContextForPrediction = predicted;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(GadgetItemSpawner)}] Could not set '{state.label}' on a cloned {clone.name}: {ex.Message}");
                }
            }
        }

        // THE GUEST'S VALUE COMES FROM THE HOST (third measurement, same day).
        // With its save identity gone and its states set before activation, a
        // guest's clone still read "placed on hanger" = 1. What is left is the
        // network: a TrackedPeckState is synchronised by its ticket, a clone
        // borrows the ticket of a vanilla prop, and the server still holds that
        // vanilla belt's last state — hanging. Only the server can change what
        // it holds, so the host sets the loose states for real, through
        // SetState, which publishes them to every client present and to come.
        private static void PublishLooseStates(GameObject clone)
        {
            if (!NetworkServer.active)
                return;

            foreach (var state in clone.GetComponentsInChildren<TrackedPeckState>(true))
            {
                if (state == null || state.label == null || !LooseStates.TryGetValue(state.label, out var value))
                    continue;

                try
                {
                    state.SetState(value);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(GadgetItemSpawner)}] Could not publish '{state.label}' for {clone.name}: {ex.Message}");
                }
            }
        }

        private static void SettleOffTheHanger(GameObject clone)
        {
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                var hung = renderer != null ? renderer.gameObject : null;
                if (hung == null || !hung.name.EndsWith("_hung", StringComparison.Ordinal) || hung.transform.parent == null)
                    continue;

                var closedName = hung.name.Substring(0, hung.name.Length - "_hung".Length) + "_closed";
                var closed = hung.transform.parent.Find(closedName);
                if (closed == null)
                    continue;

                hung.SetActive(false);
                closed.gameObject.SetActive(true);
            }
        }

        // THE TWO FALLBACKS BELOW EXIST BECAUSE OF WHAT RETURNING NOTHING
        // DID (2026-09-22, measured in co-op — see GadgetSpawnHandler.Spawn).
        //
        // A Mirror spawn handler that hands back null leaves its netId
        // unresolved on that client. The next SyncVar pointing at it is a
        // player's PlayerHeldInformation, whose `identity` field is then
        // null and whose GetProp() dereferences it — so the exception lands
        // inside DeserializeSyncVars, which stops mid-read. Mirror reports a
        // size hash mismatch, and from that moment every state update for
        // that player throws, every frame, for the rest of the session.
        // Losing one cosmetic gadget is nothing; losing a player's network
        // state is the session.
        //
        // So the handler always builds something. First choice: another
        // captured template. It is a real Prop with real components, and the
        // Lamp in particular survives the hiding pass (KeptInWorld), so any
        // machine that loaded the world normally has one to hand.
        internal static Prop BuildStandInClone(GadgetKind wanted, Vector3 position, Quaternion rotation)
        {
            foreach (var kind in StandInOrder())
            {
                if (kind == wanted || GetTemplate(kind) == null)
                    continue;

                var prop = BuildNeutralizedClone(kind, position, rotation);
                if (prop == null)
                    continue;

                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] Nothing to build an incoming {wanted} from; stood a {kind} in "
                    + "its place so the spawn resolves. It will look wrong for this player.");
                return prop;
            }

            return null;
        }

        private static IEnumerable<GadgetKind> StandInOrder()
        {
            // The Lamp first: the one kind the hiding pass spares, so the
            // one most likely to be there.
            yield return GadgetKind.Lamp;

            foreach (var kind in PrefabNames.Keys)
                yield return kind;
        }

        // Second choice, when a machine has no template whatsoever: an
        // object carrying nothing but a NetworkIdentity. It draws nothing
        // and does nothing. Its entire job is to give Mirror a netId to
        // resolve, so that a player holding it deserializes instead of
        // throwing — GetProp() then returns null rather than dereferencing a
        // null identity. That is the difference between "this player cannot
        // see the gadget" and "this player is broken until they reconnect".
        internal static GameObject BuildResolvablePlaceholder(GadgetKind wanted, Vector3 position)
        {
            try
            {
                var placeholder = new GameObject($"{wanted} {PlaceholderNameSuffix}");
                placeholder.transform.position = position;
                placeholder.AddComponent<NetworkIdentity>();

                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] Nothing at all to build an incoming {wanted} from, not even a "
                    + "stand-in; spawned an empty placeholder so this client's network state stays readable.");
                return placeholder;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetItemSpawner)}] Could not even place a placeholder for an incoming {wanted}: {ex.Message}");
                return null;
            }
        }
    }
}
