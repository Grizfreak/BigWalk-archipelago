using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Diagnostic pur (aucune écriture) : liste tous les TrackedPeckState
    // chargés dont savableSystem == SavableSystem.SpawnHubGate (les 3 "Arch
    // doors"/HubGate — Tutorial/Left/Right, cf. big-walk-archipelago-notes.md,
    // session du 2026-09-09) avec leur SaveIdentity.saveGuid et la valeur
    // SaveManager actuellement associée. But : confirmer empiriquement que
    // saveGuid est la vraie clé de persistance avant de l'utiliser pour
    // écrire directement (comme ItemApplier le fait pour les gourds/big
    // keys), plutôt que de continuer à passer par PeckDevHelper.Trigger, qui
    // broadcast à toute la catégorie "unlocks" et ouvre visuellement des
    // portes hors-scope (confirmé sans effet permanent, mais visuellement
    // gênant).
    internal static class DebugTrackedPeckStateLookup
    {
        internal static void DumpSpawnHubGate()
        {
            var all = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugTrackedPeckStateLookup)}] Aucun TrackedPeckState trouvé dans la zone actuelle.");
                return;
            }

            var matches = new List<TrackedPeckState> ();
            foreach (var state in all)
            {
                if (state != null && state.savableSystem == SavableSystem.SpawnHubGate)
                    matches.Add(state);
            }

            Plugin.Log.LogInfo($"[{nameof(DebugTrackedPeckStateLookup)}] {all.Length} TrackedPeckState au total, {matches.Count} avec savableSystem=SpawnHubGate :");
            foreach (var state in matches)
            {
                var saveIdentity = state.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<pas de SaveIdentity>";
                var currentValue = saveIdentity != null
                    ? SaveManager.GetIntValue(saveGuid, -12345, false).ToString()
                    : "n/a";
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugTrackedPeckStateLookup)}]   {state.gameObject.name} — label='{state.label}' — " +
                    $"hasInitialState={state.hasInitialState} — initialState={state.initialState} — " +
                    $"saveGuid={saveGuid} — SaveManager[{saveGuid}]={currentValue}");
            }
        }
    }
}
