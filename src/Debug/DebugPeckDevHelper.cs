using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // PeckDevHelper.Trigger(UnlockRules) est une méthode statique de triche
    // déjà intégrée au jeu (probablement utilisée par House House en interne
    // pour tester rapidement des états avancés) : chaque switch "DevUnlock"
    // en scène porte un PeckDevHelper avec un UnlockRules assigné (unlocks,
    // lights, chairlift, train, bell, tunnel, map, gourd) et se déclenche
    // quand Trigger() est appelé avec des règles qui matchent (UnlockRules.Matches).
    // Trouvé en jeu (F9) près d'un "HubGate" : un GameObject "DevUnlock" avec
    // PeckDevHelper + PeckSwitch, à 6-9m d'un GateMainSystem (TrackedPeckState
    // + PeckRelay) — cf. big-walk-archipelago-notes.md, session du 2026-09-09,
    // discussion "Arch doors". Corps de Trigger() non décompilé (RVA absente
    // du dump il2cpp.cs) : on l'utilise en boîte noire, en observant l'effet
    // en jeu plutôt qu'en re-décompilant via Ghidra.
    internal static class DebugPeckDevHelper
    {
        internal static void DumpNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckDevHelper)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckDevHelper>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckDevHelper)}] Aucun PeckDevHelper trouvé dans la zone actuelle.");
                return;
            }

            var entries = new List<(PeckDevHelper helper, float sqrDistance)>();
            foreach (var helper in all)
            {
                if (helper == null)
                    continue;

                var sqrDistance = (helper.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                    continue;

                entries.Add((helper, sqrDistance));
            }

            entries.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            Plugin.Log.LogInfo($"[{nameof(DebugPeckDevHelper)}] {entries.Count} PeckDevHelper dans un rayon de {radius}m :");
            foreach (var (helper, sqrDistance) in entries)
            {
                var distance = Math.Sqrt(sqrDistance).ToString("F1");
                var rules = helper.unlockRules;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckDevHelper)}]   {helper.gameObject.name} — distance={distance}m — " +
                    $"unlockRules(unlocks={rules.unlocks}, lights={rules.lights}, chairlift={rules.chairlift}, " +
                    $"train={rules.train}, bell={rules.bell}, tunnel={rules.tunnel}, map={rules.map}, gourd={rules.gourd}) — " +
                    $"fireWithUnlocks={helper.fireWithUnlocks}, fireWithLights={helper.fireWithLights}, fireWithTrain={helper.fireWithTrain}");
            }
        }

        internal static void Trigger(PeckDevHelper.UnlockRules rules)
        {
            Plugin.Log.LogInfo(
                $"[{nameof(DebugPeckDevHelper)}] Trigger : unlocks={rules.unlocks}, lights={rules.lights}, " +
                $"chairlift={rules.chairlift}, train={rules.train}, bell={rules.bell}, tunnel={rules.tunnel}, " +
                $"map={rules.map}, gourd={rules.gourd}");
            PeckDevHelper.Trigger(rules);
        }
    }
}
