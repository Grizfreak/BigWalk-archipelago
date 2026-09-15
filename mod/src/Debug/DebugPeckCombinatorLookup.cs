using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Diagnostic pur (aucune écriture). Suite de l'investigation des "cloches"
    // de la zone de fin (cf. big-walk-archipelago-notes.md, section "bells") :
    // PeckCombinator est le système générique qui combine plusieurs
    // TrackedPeckState (ex. deux boutons pressés simultanément par 2 joueurs)
    // via des PeckRule (systems[] + minimumMatches), et déclenche en sortie
    // soit un TrackedPeckState direct (directControlSystem), soit deux
    // PeckSwitch (onConditionMet/onConditionStop). Repéré en jeu (F9) près de
    // GoodbyeChapel : un GameObject "NHoldLogic" porte justement un
    // PeckSystemBlock + PeckCombinator, candidat naturel pour le mécanisme
    // "deux boutons simultanés" des cloches. Ce tool inspecte directement ces
    // références pour trouver la vraie clé SaveManager sans deviner.
    //
    // Durci le 2026-09-10 après un freeze (Windows "Application Hang", pas de
    // crash/exception loggée) déclenché par un F7 près de VictoryLogic, dans
    // une zone (Gauntlet) au câblage Peck plus dense que les salles de cloche
    // déjà testées avec succès. Chaque combinator/référence est maintenant
    // logué et enveloppé individuellement (try/catch) pour qu'une référence
    // problématique interrompe seulement son propre bloc plutôt que tout le
    // dump — filet de sécurité pour une vraie exception .NET (ex. accès à un
    // objet Unity semi-détruit) ; n'aide pas contre un plantage natif dur,
    // mais réduit le risque en journalisant chaque étape avant d'y toucher.
    internal static class DebugPeckCombinatorLookup
    {
        internal static void DumpNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckCombinator>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] Aucun PeckCombinator trouvé dans la zone actuelle.");
                return;
            }

            foreach (var combinator in all)
            {
                if (combinator == null)
                    continue;

                float sqrDistance;
                try
                {
                    sqrDistance = (combinator.transform.position - origin).sqrMagnitude;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] PeckCombinator illisible (position) : {ex.Message}");
                    continue;
                }

                if (sqrDistance > sqrRadius)
                    continue;

                // Logué avant toute lecture des champs : si le reste plante,
                // on sait au moins quel GameObject était en cours d'inspection.
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {combinator.gameObject.name} (PeckCombinator) — début inspection.");

                try
                {
                    DescribeCombinator(combinator);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(DebugPeckCombinatorLookup)}] {combinator.gameObject.name} — exception pendant l'inspection, bloc ignoré : {ex}");
                }
            }
        }

        private static void DescribeCombinator(PeckCombinator combinator)
        {
            DescribeTrackedPeckState("  directControlSystem", combinator.directControlSystem);
            DescribeSwitch("  onConditionMet", combinator.onConditionMet);
            DescribeSwitch("  onConditionStop", combinator.onConditionStop);

            var rules = combinator.rules;
            if (rules == null || rules.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}]   (aucune règle)");
                return;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                try
                {
                    var rule = rules[i];
                    var maxDesc = rule.hasMaximum ? rule.maximumMatches.ToString() : "illimité";
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPeckCombinatorLookup)}]   règle[{i}] — minimumMatches={rule.minimumMatches}, maximumMatches={maxDesc}, desiredState={rule.desiredState}");

                    var systems = rule.systems;
                    if (systems != null)
                    {
                        for (var s = 0; s < systems.Length; s++)
                            DescribeTrackedPeckState($"    systems[{s}]", systems[s]);
                    }

                    // rule.block regroupe des TrackedPeckState par PeckSystemBlock
                    // plutôt que directement dans systems[] — c'est le cas des
                    // règles "N-hold" (ex. NHoldLogic 2/EndingGate) dont
                    // systems[] est vide mais qui référencent quand même un
                    // groupe de boutons (ex. "ButtonNHoldTwo") via ce champ.
                    DescribeBlock("    rule.block", rule.block);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}]   règle[{i}] — exception, ignorée : {ex.Message}");
                }
            }
        }

        private static void DescribeTrackedPeckState(string label, TrackedPeckState state)
        {
            try
            {
                if (state == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}=<null>");
                    return;
                }

                var saveIdentity = state.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<pas de SaveIdentity>";
                var savableSystem = state.savableSystem;
                var key = savableSystem != SavableSystem.NotSavable ? savableSystem.ToString() : saveGuid;
                var currentValue = SaveManager.GetIntValue(key, -12345, false);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckCombinatorLookup)}] {label}={state.gameObject.name} — savableSystem={savableSystem} — " +
                    $"saveGuid={saveGuid} — clé réelle='{key}' — SaveManager[{key}]={currentValue}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] {label} — exception pendant la lecture, ignorée : {ex.Message}");
            }
        }

        private static void DescribeBlock(string label, PeckSystemBlock block)
        {
            try
            {
                if (block == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}=<null>");
                    return;
                }

                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}={block.gameObject.name} (PeckSystemBlock)");

                var systems = block.systems;
                if (systems == null || systems.Length == 0)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label} — (aucun system dans le block)");
                    return;
                }

                for (var s = 0; s < systems.Length; s++)
                    DescribeTrackedPeckState($"{label}.systems[{s}]", systems[s]);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] {label} — exception pendant la lecture, ignorée : {ex.Message}");
            }
        }

        private static void DescribeSwitch(string label, PeckSwitch sw)
        {
            try
            {
                if (sw == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorLookup)}] {label}=<null>");
                    return;
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugPeckCombinatorLookup)}] {label}={sw.gameObject.name} (PeckSwitch, specificState={sw.specificState})");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorLookup)}] {label} — exception pendant la lecture, ignorée : {ex.Message}");
                return;
            }

            DescribeTrackedPeckState($"{label}.trackedStateSystem", sw.trackedStateSystem);
        }
    }
}
