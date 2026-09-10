using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Outil d'action (pas un diagnostic) : force directement l'état des
    // TrackedPeckState qu'un PeckCombinator proche surveille (rule.systems[]
    // ET rule.block.systems[], cf. DebugPeckCombinatorLookup/F7) à leur
    // desiredState, au lieu de simuler des pressions de boutons.
    //
    // Raison d'être (cloche du sommet du Gauntlet, cf. big-walk-archipelago-
    // notes.md) : les boutons "N-hold" semblent nécessiter un maintien
    // continu à 2 joueurs, pas un simple tap — DebugPeckFire.Peck() (un
    // événement discret) ne suffit pas et, pire, re-pecker le SEUL bouton
    // trouvé à portée (le sien) le fait basculer plutôt que d'simuler le
    // second joueur. TrackedPeckState.SetState(int) écrit l'état directement
    // et court-circuite tout le mécanisme d'appui/maintien.
    internal static class DebugPeckCombinatorForce
    {
        internal static void ForceNearby(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var all = UnityEngine.Object.FindObjectsByType<PeckCombinator>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}] Aucun PeckCombinator trouvé dans la zone actuelle.");
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
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorForce)}] PeckCombinator illisible (position) : {ex.Message}");
                    continue;
                }

                if (sqrDistance > sqrRadius)
                    continue;

                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}] {combinator.gameObject.name} (PeckCombinator) — début forçage.");

                try
                {
                    ForceCombinator(combinator);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(DebugPeckCombinatorForce)}] {combinator.gameObject.name} — exception pendant le forçage, bloc ignoré : {ex}");
                }
            }
        }

        private static void ForceCombinator(PeckCombinator combinator)
        {
            var rules = combinator.rules;
            if (rules == null || rules.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}]   (aucune règle)");
                return;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                try
                {
                    var rule = rules[i];
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}]   règle[{i}] — desiredState cible={rule.desiredState}");

                    ForceState(rule.systems, rule.desiredState);

                    var block = rule.block;
                    if (block != null)
                        ForceState(block.systems, rule.desiredState);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorForce)}]   règle[{i}] — exception, ignorée : {ex.Message}");
                }
            }
        }

        private static void ForceState(TrackedPeckState[] systems, int desiredState)
        {
            if (systems == null)
                return;

            for (var s = 0; s < systems.Length; s++)
            {
                var state = systems[s];
                if (state == null)
                    continue;

                try
                {
                    state.SetState(desiredState);
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckCombinatorForce)}]     {state.gameObject.name}.SetState({desiredState}) appelé.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckCombinatorForce)}]     SetState exception, ignorée : {ex.Message}");
                }
            }
        }
    }
}
