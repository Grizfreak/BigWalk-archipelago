using Mirror;

namespace BigWalkArchipelago.Core
{
    // Point d'entrée unique pour matérialiser un item Archipelago reçu à
    // distance (aujourd'hui : simulé par Debug/DebugItemSimulator ; demain :
    // le vrai client Archipelago appellera la même méthode). Design tranché en
    // session (cf. big-walk-archipelago-notes.md) : écrit directement dans
    // SaveManager, sans jamais toucher le RewardGourd/Prop vivant en scène —
    // Prop.Start() fera le vrai pin tout seul au prochain chargement de la
    // zone concernée. Ça garde l'énigme physique intacte et solvable.
    internal static class ItemApplier
    {
        internal static bool ApplyGourdItem(SaveablePropName propName)
        {
            // Toute écriture de sauvegarde doit venir du host (modèle
            // d'autorité déjà établi pour le reste du mod, cf. notes.md).
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ItemApplier)}] Ignoré : {propName} reçu alors qu'on n'est pas host.");
                return false;
            }

            if (!GourdRegistry.TryGetHomeName(propName, out var homeName))
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ItemApplier)}] Aucun home connu pour {propName}, item ignoré.");
                return false;
            }

            // Marque la location comme déjà réglée AVANT d'écrire. Ne suffit
            // pas de garder un flag pendant le seul appel synchrone ci-dessous :
            // confirmé en test le 2026-09-07, Prop.Start() réécrit la même clé
            // via le vrai SaveManager.SetIntValue du jeu au prochain chargement
            // de zone (pour repinner le prop), et SaveValuePatch — qui ne peut
            // pas distinguer cette réécriture différée d'une vraie résolution —
            // reportait alors un check fantôme. Marquer ici couvre les deux cas
            // (l'écriture immédiate ci-dessous ET celle, différée, de Prop.Start()).
            //
            // Corrigé le 2026-09-07 (repéré en test, cf. discussion notes.md
            // "logique locations/items à repenser") : TryMarkReported ne
            // suffit pas seul, il faut aussi reporter le check. Avant ce fix,
            // un item reçu AVANT toute résolution locale de cette location ne
            // reportait jamais le check nulle part (ItemApplier marquait
            // silencieusement sans appeler Plugin.Reporter.ReportCheck) — la
            // location restait ensuite bloquée pour toujours (CheckTracker
            // empêche tout report futur), donc son propre item randomisé
            // n'était jamais envoyé à qui l'attend dans le multiworld. Même
            // pattern que GourdStatePatch/SaveValuePatch : premier arrivé
            // (résolution réelle OU item reçu) reporte le check une fois, peu
            // importe lequel des deux c'est.
            if (GourdRegistry.TryGetLocationId(propName, out var locationId)
                && CheckTracker.TryMarkReported(locationId))
                Plugin.Reporter.ReportCheck(locationId);

            SaveManager.SetIntValue(propName.ToString(), (int)homeName);

            TryApplyLiveEffect(propName, homeName);

            Plugin.Log.LogInfo($"[{nameof(ItemApplier)}] Item appliqué : {propName} -> {homeName}.");
            return true;
        }

        // Effet instantané, en plus de l'écriture SaveManager ci-dessus —
        // UNIQUEMENT pour les props sans composant RewardGourd (big keys,
        // cf. big-walk-archipelago-notes.md, découvert en session de test le
        // 2026-09-07). Les deux raisons qui interdisent le pin live pour un
        // gourd ne s'appliquent pas à ces props : pas de GourdState/icône
        // carte à désynchroniser (il n'y en a pas), et pas de puzzle local à
        // garder solvable (le "check" d'une big key est le peck vers sa
        // plinthe, pas une résolution répétable). Ne fait rien si la zone
        // n'est pas chargée (prop introuvable) ou si un RewardGourd est
        // présent : Prop.Start() prendra alors le relais au prochain
        // chargement, exactement comme avant pour tous les cas.
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
                $"[{nameof(ItemApplier)}] Effet immédiat appliqué (pas de RewardGourd) : {propName} pinné en direct sur {homeName}.");
        }
    }
}
