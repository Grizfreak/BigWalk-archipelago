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
            if (GourdRegistry.TryGetLocationId(propName, out var locationId))
                CheckTracker.TryMarkReported(locationId);

            SaveManager.SetIntValue(propName.ToString(), (int)homeName);

            Plugin.Log.LogInfo($"[{nameof(ItemApplier)}] Item appliqué : {propName} -> {homeName}.");
            return true;
        }
    }
}
