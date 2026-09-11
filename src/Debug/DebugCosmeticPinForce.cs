using BigWalkArchipelago.Core;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Outil d'action (pas un diagnostic). Épingler un gourd cosmétique dans
    // un vrai PropHome passe normalement par la même interaction manuelle
    // qu'un vrai gourd (peck/maintien sur le "vice launch switch" du
    // monument, cf. big-walk-archipelago-notes.md) — même piège que les
    // cloches "N-hold" déjà rencontré (DebugPeckCombinatorForce) : fastidieux
    // à tester seul, surtout en alternant avec la lecture des logs. Ce tool
    // court-circuite l'interaction : trouve le gourd cosmétique le plus
    // proche (Core.ReceivedItemSpawner.IsCosmeticClone) et le PropHome vide
    // le plus proche, et appelle Prop.ServerSetPinned directement dessus —
    // déclenche PropHome.onAnyChangeServer exactement comme un vrai pin,
    // donc CosmeticMonumentFillTracker persiste normalement, sans traitement
    // spécial nécessaire.
    internal static class DebugCosmeticPinForce
    {
        internal static void ForceNearby(float radius)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] Ignoré : pas host.");
                return;
            }

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;

            var cosmeticProp = FindNearestCosmeticProp(origin, radius);
            if (cosmeticProp == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] Aucun gourd cosmétique à moins de {radius}m (en spawner un avec P d'abord ?).");
                return;
            }

            var propHome = FindNearestEmptyPropHome(origin, radius);
            if (propHome == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugCosmeticPinForce)}] Aucun PropHome vide à moins de {radius}m.");
                return;
            }

            var rewardGourd = cosmeticProp.GetComponent<RewardGourd>();
            if (rewardGourd != null)
                rewardGourd.ServerSetGourdState(GourdFlag.GourdState.Stashed);

            cosmeticProp.ServerSetPinned(propHome);

            Plugin.Log.LogInfo(
                $"[{nameof(DebugCosmeticPinForce)}] {cosmeticProp.gameObject.name} épinglé dans {propHome.saveableHomeName}.");
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
                if (home == null || home.pinnedProp != null)
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
