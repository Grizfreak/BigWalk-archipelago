using System;

namespace BigWalkArchipelago.Debug
{
    // Diagnostic pur (aucune écriture). Pour les mécanismes dont la chaîne
    // Peck ne mène à aucun TrackedPeckState/savableSystem exploitable (ex. le
    // Gauntlet, cf. big-walk-archipelago-notes.md — NHoldSucess est
    // NotSavable et rien en aval n'est visible depuis un PeckCombinator/
    // PeckSwitch), la seule façon fiable de trouver la vraie clé est de
    // comparer un dump complet de SaveManager avant/après l'action en jeu
    // (ex. casser la cloche) plutôt que de deviner via le graphe d'objets.
    internal static class DebugSaveDump
    {
        internal static void DumpAll()
        {
            var data = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (data == null || data.entries == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}] Aucune SaveData chargée.");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}]   skipAidsActive={data.skipAidsActive}");

            var entries = data.entries;
            Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}] --- dump complet ({entries.Count} entrées int) ---");

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}]   {entry.key}={entry.value}");
            }

            Plugin.Log.LogInfo($"[{nameof(DebugSaveDump)}] --- fin du dump ---");
        }
    }
}
