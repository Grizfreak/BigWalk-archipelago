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
        private static readonly HashSet<GadgetKind> KeptInWorld = new()
        {
            GadgetKind.Lamp,
        };

        // A block of our own, clear of ReceivedItemSpawner.CosmeticAssetId
        // (0xB16_9A00) and of anything Mirror or the game might register —
        // the value only has to be agreed on by every machine, which a
        // shared constant guarantees. One per GadgetKind: the SpawnDelegate
        // signature Mirror accepts is (Vector3, uint) only (no room for a
        // payload saying which gadget), so the assetId itself is what tells
        // a client what to build — same reasoning as CosmeticAssetId's own
        // comment.
        internal static readonly Dictionary<GadgetKind, uint> AssetIds = new()
        {
            { GadgetKind.Megaphone, 0xB16_9B01 },
            { GadgetKind.WalkieTalkie, 0xB16_9B02 },
            { GadgetKind.Backpack, 0xB16_9B03 },
            { GadgetKind.Belt, 0xB16_9B04 },
            { GadgetKind.FlareGun, 0xB16_9B05 },
            { GadgetKind.Laser, 0xB16_9B06 },
            { GadgetKind.Binoculars, 0xB16_9B07 },
            { GadgetKind.Compass, 0xB16_9B08 },
            { GadgetKind.FoldingMap, 0xB16_9B09 },
            { GadgetKind.Radio, 0xB16_9B0A },
            { GadgetKind.GourdCarton, 0xB16_9B0B },
            { GadgetKind.Torch, 0xB16_9B0C },
            { GadgetKind.Lamp, 0xB16_9B0D },
            { GadgetKind.XrayGoggles, 0xB16_9B0E },
            { GadgetKind.FlareGunBlue, 0xB16_9B0F },
            { GadgetKind.FlareGunGreen, 0xB16_9B10 },
            { GadgetKind.FlareGunYellow, 0xB16_9B11 },
        };

        // Marks both a captured template and a spawned pickup as "ours",
        // the same way ReceivedItemSpawner.CosmeticNameSuffix does for a
        // gourd clone. A template is never spawned over the network itself
        // (only clones built FROM it are), so it needs a suffix of its own
        // rather than reusing that one.
        private const string TemplateNameSuffix = "(AP template)";
        private const string PlaceholderNameSuffix = "(AP placeholder)";

        private static readonly Dictionary<GadgetKind, Prop> _templates = new();

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

            foreach (var candidate in all)
            {
                if (candidate == null || candidate.gameObject == null)
                    continue;

                if (!NameMatches(candidate.gameObject.name, prefabName) || IsOurs(candidate.gameObject.name))
                    continue;

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

                var previous = _templates.TryGetValue(kind, out var existing) ? existing : null;
                _templates[kind] = clone.GetComponent<Prop>();

                // Re-armed on every world load (VanillaGadgetRemover), so a
                // stale template from the previous world would otherwise
                // leak for the life of the process.
                if (previous != null && previous.gameObject != null)
                    UnityEngine.Object.Destroy(previous.gameObject);

                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] {kind} template captured from '{candidate.gameObject.name}'.");
                return;
            }

            Plugin.Log.LogWarning(
                $"[{nameof(GadgetItemSpawner)}] No {kind} ({prefabName}) found in the scene to capture as a template; a received one will have nothing to clone from.");
        }

        private static Prop GetTemplate(GadgetKind kind)
        {
            return _templates.TryGetValue(kind, out var template) && template != null ? template : null;
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
        internal static int DestroyLooseCosmeticGadgets()
        {
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

                if (prop.GetComponent<RewardGourd>() != null)
                    continue;

                seen++;

                var home = prop.currentHome;
                if (home == null)
                {
                    candidates.Add(prop);
                    continue;
                }

                placed++;

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
                var resolved = ReceivedItemSpawner.ResolveSpawnPosition(toPlayer);
                if (resolved == null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(GadgetItemSpawner)}] Nowhere to put a {kind} yet (no player and no InventorySpawn loaded), cosmetic spawn skipped.");
                    return null;
                }

                var prop = BuildNeutralizedClone(kind, resolved.Value, Quaternion.identity);
                if (prop == null)
                    return null;

                NetworkServer.Spawn(prop.gameObject, AssetIds[kind]);

                // Without this the clone stays "fixed" as in its original
                // resting pose — same reasoning as the gourd version.
                prop.SetLoose();

                if (toPlayer && ModConfig.PutGourdInHands.Value)
                    ReceivedItemSpawner.QueueHandover(prop);

                var identity = prop.GetComponent<NetworkIdentity>();
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] Cosmetic {kind} spawned at {prop.transform.position} (netId {(identity != null ? identity.netId : 0)}).");
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
        internal static Prop BuildNeutralizedClone(GadgetKind kind, Vector3 position, Quaternion rotation)
        {
            var template = GetTemplate(kind);
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
                template = GetTemplate(kind);
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

            return prop;
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
