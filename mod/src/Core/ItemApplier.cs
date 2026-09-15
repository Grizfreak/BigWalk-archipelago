using Mirror;

namespace BigWalkArchipelago.Core
{
    // Single entry point for materializing a remotely received Archipelago
    // item (today: simulated by Debug/DebugItemSimulator; tomorrow: the
    // real Archipelago client will call the same method). Design settled in
    // session (cf. big-walk-archipelago-notes.md): writes directly to
    // SaveManager, never touching the live RewardGourd/Prop in the scene —
    // Prop.Start() will do the real pin by itself on the next load of the
    // relevant zone. This keeps the physical puzzle intact and solvable.
    internal static class ItemApplier
    {
        // Generic gourd item received over the AP network (cf. FIX
        // 2026-09-11, big-walk-archipelago-notes.md): pure progression
        // currency for monuments, entirely decoupled from puzzle
        // resolution. Must NEVER write to SaveManager under a specific
        // gourdXxx name nor drive TryApplyLiveEffect — unlike the 1:1 model
        // still used for big keys (ApplyBigKeyItem below). Its own location
        // check continues to be reported only by an actual in-game physical
        // resolution (GourdStatePatch/SaveValuePatch, never touched here).
        internal static bool ApplyGourdItem()
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ItemApplier)}] Ignored: gourd received while not host.");
                return false;
            }

            // The return value matters, and used to be discarded here.
            // SpawnCosmeticPickup gives back null when it cannot do the job
            // — most often because no InventorySpawn is loaded yet, which is
            // exactly the situation right after a save loads. Reporting that
            // as success made Core/Net/ApRuntime record the gourd as
            // materialized and stop trying, so a gourd owed to the player
            // silently evaporated. A caller that knows it failed can retry.
            if (ReceivedItemSpawner.SpawnCosmeticPickup() == null)
                return false;

            Plugin.Log.LogInfo($"[{nameof(ItemApplier)}] Generic gourd applied (cosmetic spawn only).");
            return true;
        }

        internal static bool ApplyBigKeyItem(SaveablePropName propName)
        {
            // Every save write must come from the host (authority model
            // already established for the rest of the mod, cf. notes.md).
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ItemApplier)}] Ignored: {propName} received while not host.");
                return false;
            }

            if (!GourdRegistry.TryGetHomeName(propName, out var homeName))
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ItemApplier)}] No known home for {propName}, item ignored.");
                return false;
            }

            // Marks the location as already settled BEFORE writing. Just
            // keeping a flag during the single synchronous call below is
            // not enough: confirmed in testing on 2026-09-07, Prop.Start()
            // rewrites the same key via the game's real
            // SaveManager.SetIntValue on the next zone load (to re-pin the
            // prop), and SaveValuePatch — which cannot distinguish this
            // deferred rewrite from a genuine resolution — would then
            // report a phantom check. Marking here covers both cases (the
            // immediate write below AND Prop.Start()'s deferred one).
            //
            // Fixed on 2026-09-07 (spotted in testing, cf. notes.md
            // discussion "locations/items logic to rethink"): TryMarkReported
            // alone isn't enough, the check must also be reported. Before
            // this fix, an item received BEFORE any local resolution of
            // this location never reported the check anywhere (ItemApplier
            // silently marked it without calling
            // Plugin.Reporter.ReportCheck) — the location then stayed
            // permanently blocked (CheckTracker prevents any future
            // report), so its own randomized item was never sent to
            // whoever was waiting for it in the multiworld. Same pattern as
            // GourdStatePatch/SaveValuePatch: whichever comes first (real
            // resolution OR received item) reports the check once, no
            // matter which of the two it is.
            if (GourdRegistry.TryGetLocationId(propName, out var locationId)
                && CheckTracker.TryMarkReported(locationId))
                Plugin.Reporter.ReportCheck(locationId);

            SaveManager.SetIntValue(propName.ToString(), (int)homeName);

            TryApplyLiveEffect(propName, homeName);

            // NO cosmetic spawn here. There used to be one, as a "you just
            // received something" notification for any item (2026-09-11) —
            // but later the same day cosmetic gourds stopped being decoration
            // and became the ONLY way to fill a monument. That turned this
            // line into a bug that survived unnoticed until the first real
            // in-game session (2026-09-15): receiving a big key visibly
            // dropped a GOURD at the hub, and every key handed the player a
            // free unit of the one currency the Archipelago world balances
            // exactly (the pool holds one Gourd per monument slot and no
            // more, see apworld/protocol.md §4).
            //
            // A big key needs no notification anyway: it materializes in its
            // own plinth and whatever it opens opens on the spot.

            Plugin.Log.LogInfo($"[{nameof(ItemApplier)}] Item applied: {propName} -> {homeName}.");
            return true;
        }

        // Instant effect, on top of the SaveManager write above — ONLY for
        // props without a RewardGourd component (big keys, cf.
        // big-walk-archipelago-notes.md, discovered during a test session
        // on 2026-09-07). The two reasons that forbid a live pin for a
        // gourd don't apply to these props: no GourdState/map icon to
        // desync (there isn't one), and no local puzzle to keep solvable
        // (a big key's "check" is the peck toward its plinth, not a
        // repeatable resolution). Does nothing if the zone isn't loaded
        // (prop not found) or if a RewardGourd is present: Prop.Start()
        // will then take over on the next load, exactly as before in all
        // cases.
        private static void TryApplyLiveEffect(SaveablePropName propName, SaveableHomeName homeName)
        {
            Prop targetProp = null;
            foreach (var prop in Prop.allProps)
            {
                if (prop != null && prop.saveablePropName == propName)
                {
                    targetProp = prop;
                    break;
                }
            }

            if (targetProp == null)
                return;

            if (targetProp.GetComponent<RewardGourd>() != null)
                return;

            var propHome = PropHome.GetSaveableHome(homeName);
            if (propHome == null)
                return;

            targetProp.ServerSetPinned(propHome);
            Plugin.Log.LogInfo(
                $"[{nameof(ItemApplier)}] Immediate effect applied (no RewardGourd): {propName} pinned live to {homeName}.");
        }
    }
}
