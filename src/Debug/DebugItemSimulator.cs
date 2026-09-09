using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Simule la réception d'un item Archipelago à distance, sans connexion
    // réseau Archipelago réelle : exerce exactement le même chemin de code
    // (Core.ItemApplier) qu'un vrai client appellera plus tard. Cible le Prop
    // savable non-collecté le plus proche du joueur.
    //
    // Passe par DebugGourdLookup.FindNearestUncollectedProp (Prop.allProps),
    // pas par RewardGourd : confirmé en session de test le 2026-09-07, les big
    // keys n'ont pas de composant RewardGourd (donc pas de gourdState), au
    // contraire de l'hypothèse initiale des notes — FindNearestLocked (qui,
    // lui, filtre sur RewardGourd.gourdState == Locked) ne les trouve jamais.
    // ItemApplier n'a de toute façon jamais eu besoin de RewardGourd : seul
    // saveablePropName compte.
    internal static class DebugItemSimulator
    {
        internal static void SimulateReceiveNext()
        {
            var prop = DebugGourdLookup.FindNearestUncollectedProp();
            if (prop == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugItemSimulator)}] Plus aucun gourd/big key non-collecté dans la zone actuelle.");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugItemSimulator)}] Simulation de réception d'item : {prop.saveablePropName}");
            ItemApplier.ApplyGourdItem(prop.saveablePropName);
        }

        // Cible directement une SaveablePropName précise, sans passer par
        // FindNearestUncollectedProp : utile pour tester une big key qui n'est
        // pas dans la zone actuellement chargée (téléphérique/train, cf.
        // big-walk-archipelago-notes.md "À tester" du 2026-09-09), ou dont on
        // veut forcer l'application peu importe la proximité du joueur.
        internal static void SimulateReceive(SaveablePropName propName)
        {
            Plugin.Log.LogInfo(
                $"[{nameof(DebugItemSimulator)}] Simulation de réception d'item (ciblée) : {propName}");
            ItemApplier.ApplyGourdItem(propName);
        }
    }
}
