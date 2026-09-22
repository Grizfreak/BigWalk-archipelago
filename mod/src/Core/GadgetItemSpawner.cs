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
    // Unlike a gourd, the VANILLA instances of these props are also removed
    // from the map (RemoveVanillaInstances, driven by VanillaGadgetRemover)
    // — player decision, 2026-09-22: a filler item must not also be
    // findable lying around for free. That makes the template a one-shot
    // capture taken locally on each machine BEFORE removal
    // (CaptureTemplates), rather than "whatever is loaded right now" like
    // ReceivedItemSpawner.FindTemplate — once the real instances are gone
    // there is nothing left in the world to clone from.
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

        private static readonly Dictionary<GadgetKind, Prop> _templates = new();

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
            var all = UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsSortMode.None);
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

        // Host only, called right after CaptureTemplates by
        // VanillaGadgetRemover. A plain GameObject.SetActive(false) is not
        // something Mirror replicates, so making these props disappear for
        // every player needs the same networked call the mod already uses
        // to clean up its own cosmetic clones (NetworkServer.Destroy)
        // instead of hiding them locally. The template captured just above
        // is inactive and therefore excluded by the default (active-only)
        // FindObjectsByType scan below, so this never destroys its own
        // template.
        internal static int RemoveVanillaInstances()
        {
            var removed = 0;
            var all = UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsSortMode.None);
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
                    NetworkServer.Destroy(prop.gameObject);
                    removed++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(GadgetItemSpawner)}] Could not remove a vanilla gadget prop: {ex.Message}");
                }
            }

            if (removed > 0)
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetItemSpawner)}] Removed {removed} vanilla gadget prop(s) from the map.");

            return removed;
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
    }
}
