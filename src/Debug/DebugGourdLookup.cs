using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Lookup partagé entre les outils de debug (DebugGourdUnlocker, DebugItemSimulator).
    internal static class DebugGourdLookup
    {
        internal static RewardGourd FindNearestLocked()
        {
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;

            // Préfère le plus proche du joueur local (plutôt qu'un ordre
            // arbitraire) pour que l'effet soit visible/testable immédiatement.
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            RewardGourd closest = null;
            float closestSqrDistance = float.MaxValue;

            foreach (var gourd in all)
            {
                if (gourd == null || gourd.gourdState != GourdFlag.GourdState.Locked)
                    continue;

                if (playerPosition == null)
                    return gourd;

                float sqrDistance = (gourd.transform.position - playerPosition.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = gourd;
                }
            }

            return closest;
        }
    }
}
