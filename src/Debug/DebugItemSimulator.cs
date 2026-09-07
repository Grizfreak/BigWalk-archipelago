using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Simule la réception d'un item Archipelago à distance, sans connexion
    // réseau Archipelago réelle : exerce exactement le même chemin de code
    // (Core.ItemApplier) qu'un vrai client appellera plus tard. Cible le
    // gourd verrouillé le plus proche du joueur (même UX que
    // DebugGourdUnlocker/F3) pour rester facile à observer/tester.
    internal static class DebugItemSimulator
    {
        internal static void SimulateReceiveNext()
        {
            var closest = DebugGourdLookup.FindNearestLocked();
            if (closest == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugItemSimulator)}] Plus aucun gourd/big key verrouillé dans la zone actuelle.");
                return;
            }

            var prop = closest.prop;
            if (prop == null)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(DebugItemSimulator)}] RewardGourd sans prop associé, simulation ignorée.");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugItemSimulator)}] Simulation de réception d'item : {prop.saveablePropName}");
            ItemApplier.ApplyGourdItem(prop.saveablePropName);
        }
    }
}
