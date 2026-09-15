using BigWalkArchipelago.Core;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Action tool (not a diagnostic). Pinning a cosmetic gourd into a real
    // PropHome normally goes through the same manual interaction as a real
    // gourd (peck/hold on the monument's "vice launch switch", cf.
    // big-walk-archipelago-notes.md) — same pitfall as the "N-hold" bells
    // already encountered (DebugPeckCombinatorForce): tedious to test alone,
    // especially while alternating with reading the logs. This tool
    // short-circuits the interaction: it finds the nearest cosmetic gourd
    // (Core.ReceivedItemSpawner.IsCosmeticClone) and the nearest empty
    // PropHome, and calls Prop.ServerSetPinned directly on it — this fires
    // PropHome.onAnyChangeServer exactly like a real pin, so
    // CosmeticMonumentFillTracker persists normally, with no special
    // handling required.
    internal static class DebugCosmeticPinForce
    {
        internal static void ForceNearby(float radius)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] Skipped: not host.");
                return;
            }

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] Local player not found.");
                return;
            }

            var origin = localPlayer.transform.position;

            var cosmeticProp = FindNearestCosmeticProp(origin, radius);
            if (cosmeticProp == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] No cosmetic gourd within {radius}m (spawn one with P first?).");
                return;
            }

            var propHome = FindNearestEmptyPropHome(origin, radius);
            if (propHome == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] No empty PropHome within {radius}m.");
                return;
            }

            var rewardGourd = cosmeticProp.GetComponent<RewardGourd>();
            if (rewardGourd != null)
                rewardGourd.ServerSetGourdState(GourdFlag.GourdState.Stashed);

            cosmeticProp.ServerSetPinned(propHome);

            Plugin.Log.LogInfo(
                $"[{nameof(DebugCosmeticPinForce)}] {cosmeticProp.gameObject.name} pinned into {propHome.saveableHomeName}.");
        }

        private static Prop FindNearestCosmeticProp(Vector3 origin, float radius)
        {
            var sqrRadius = radius * radius;
            Prop closest = null;
            var closestSqrDistance = float.MaxValue;

            foreach (var prop in Prop.allProps)
            {
                if (!ReceivedItemSpawner.IsCosmeticClone(prop))
                    continue;

                var sqrDistance = (prop.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius || sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closest = prop;
            }

            return closest;
        }

        private static PropHome FindNearestEmptyPropHome(Vector3 origin, float radius)
        {
            var sqrRadius = radius * radius;
            PropHome closest = null;
            var closestSqrDistance = float.MaxValue;

            foreach (var home in PropHome.allPropHomes)
            {
                if (home == null || home.pinnedProp != null || !ReceivedItemSpawner.IsMonumentHome(home))
                    continue;

                var sqrDistance = (home.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius || sqrDistance >= closestSqrDistance)
                    continue;

                closestSqrDistance = sqrDistance;
                closest = home;
            }

            return closest;
        }
    }
}
