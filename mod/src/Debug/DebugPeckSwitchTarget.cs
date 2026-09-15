using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Diagnostic pur (aucune écriture). Suite de DebugTrackedPeckStateLookup :
    // le dump filtré sur savableSystem==SpawnHubGate n'a trouvé qu'un seul
    // TrackedPeckState ("GateMainSystem"), mais son savableSystem n'apparaît
    // jamais dans le fichier de save après un déclenchement confirmé en jeu —
    // donc ce n'est probablement PAS le TrackedPeckState réellement modifié.
    // PeckDevHelper.Trigger (décompilé via Ghidra, cf.
    // big-walk-archipelago-notes.md) appelle en fait PeckSwitch.Peck() sur le
    // PeckSwitch attaché au même GameObject que le PeckDevHelper (ex.
    // "DevUnlock") — et c'est le champ PeckSwitch.trackedStateSystem
    // (référence assignée dans l'éditeur Unity, pas forcément le
    // TrackedPeckState le plus proche) qui reçoit le vrai SetState(). Ce tool
    // inspecte directement cette référence pour identifier le bon
    // TrackedPeckState sans deviner.
    internal static class DebugPeckSwitchTarget
    {
        internal static void DumpNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckSwitchTarget)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckSwitchTarget)}] Aucun PeckSwitch trouvé dans la zone actuelle.");
                return;
            }

            foreach (var sw in all)
            {
                if (sw == null)
                    continue;

                var sqrDistance = (sw.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                    continue;

                var target = sw.trackedStateSystem;
                if (target == null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPeckSwitchTarget)}]   {sw.gameObject.name} (PeckSwitch) — trackedStateSystem=<null> — specificState={sw.specificState}");
                    continue;
                }

                var saveIdentity = target.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<pas de SaveIdentity>";
                var savableSystem = target.savableSystem;
                var key = savableSystem != SavableSystem.NotSavable ? savableSystem.ToString() : saveGuid;
                var currentValue = SaveManager.GetIntValue(key, -12345, false);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckSwitchTarget)}]   {sw.gameObject.name} (PeckSwitch, specificState={sw.specificState}) -> " +
                    $"trackedStateSystem={target.gameObject.name} — savableSystem={savableSystem} — saveGuid={saveGuid} — " +
                    $"clé réelle='{key}' — SaveManager[{key}]={currentValue}");
            }
        }
    }
}
