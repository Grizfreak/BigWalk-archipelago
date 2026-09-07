using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Lookup partagé entre les outils de debug (DebugGourdUnlocker, DebugItemSimulator).
    internal static class DebugGourdLookup
    {
        // Diagnostic pur (aucune écriture). Scanne Prop.allProps (pas
        // RewardGourd) : confirmé en session de test le 2026-09-07, les big
        // keys n'ont PAS de composant RewardGourd du tout (0 trouvée via
        // FindObjectsByType<RewardGourd>, même en incluant les inactifs, alors
        // que le joueur se tenait devant une clé) — donc contrairement à
        // l'hypothèse initiale des notes ("même pipeline RewardGourd/Prop que
        // les gourds"), une big key est un Prop nu (probablement avec un
        // PeckSwitch onUseAsKey / PropGroup.BigKey, cf. big-walk-archipelago-notes.md),
        // pas un RewardGourd avec un GourdState. Prop.allProps est le registre
        // qui les couvre tous, gourds et big keys confondus.
        internal static void LogNearby(int count)
        {
            var all = Prop.allProps;
            if (all == null || all.Count == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] Aucun Prop trouvé dans la zone actuelle.");
                return;
            }

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            // Prop.allProps est un Il2CppSystem.Collections.Generic.List<Prop>
            // (pas un System.Collections.Generic.List<T>) : pas d'IEnumerable<T>
            // côté interop, donc pas de LINQ directement dessus — on matérialise
            // dans une vraie List<> .NET avant de trier/logger.
            var entries = new List<(Prop prop, RewardGourd gourdComponent, bool active, bool saved, float sqrDistance)>();
            foreach (var prop in all)
            {
                if (prop == null || prop.saveablePropName == SaveablePropName.notSavable)
                    continue;

                var sqrDistance = playerPosition.HasValue
                    ? (prop.transform.position - playerPosition.Value).sqrMagnitude
                    : 0f;
                entries.Add((
                    prop,
                    prop.GetComponent<RewardGourd>(),
                    prop.gameObject.activeInHierarchy,
                    SaveManager.GetIntValue(prop.saveablePropName.ToString(), 0, false) != 0,
                    sqrDistance));
            }

            entries.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            var playerLabel = playerPosition.HasValue ? playerPosition.Value.ToString() : "<joueur local introuvable>";
            Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] Position joueur : {playerLabel}");
            Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}] {entries.Count} Prop savable dans la zone, les {Math.Min(count, entries.Count)} plus proches :");
            for (var i = 0; i < entries.Count && i < count; i++)
            {
                var entry = entries[i];
                var distance = playerPosition.HasValue ? Math.Sqrt(entry.sqrDistance).ToString("F1") : "?";
                var gourdState = entry.gourdComponent != null ? entry.gourdComponent.gourdState.ToString() : "n/a (pas de RewardGourd)";
                Plugin.Log.LogInfo($"[{nameof(DebugGourdLookup)}]   {entry.prop.saveablePropName} — gourdState={gourdState} — déjà sauvegardé={entry.saved} — actif={entry.active} — distance={distance}m");
            }
        }

        // Lookup générique successeur de FindNearestLocked : couvre gourds ET
        // big keys en se basant sur le signal canonique "pas encore écrit dans
        // SaveManager" (le même que ItemApplier/CheckTracker utilisent déjà)
        // plutôt que sur RewardGourd.gourdState, qui n'existe pas pour les big
        // keys (cf. LogNearby ci-dessus).
        internal static Prop FindNearestUncollectedProp()
        {
            var all = Prop.allProps;
            if (all == null || all.Count == 0)
                return null;

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;

            Prop closest = null;
            float closestSqrDistance = float.MaxValue;

            foreach (var prop in all)
            {
                if (prop == null || prop.saveablePropName == SaveablePropName.notSavable)
                    continue;

                if (SaveManager.GetIntValue(prop.saveablePropName.ToString(), 0, false) != 0)
                    continue;

                if (playerPosition == null)
                    return prop;

                float sqrDistance = (prop.transform.position - playerPosition.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = prop;
                }
            }

            return closest;
        }

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
