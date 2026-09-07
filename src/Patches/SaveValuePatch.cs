using System;
using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Filet de sécurité : SaveManager.SetIntValue est le point d'écriture bas
    // niveau commun à tous les chemins de code qui persistent un gourd. On ne
    // connaît pas tous ces chemins (cf. big-walk-archipelago-notes.md) —
    // confirmé en session de test le 2026-09-03 : en plus de RewardGourd.
    // ServerSetGourdState, PeckEffectSavableHome.Peck() (sur un "vice launch
    // switch") appelle Prop.SavePropHome indépendamment, sans jamais passer
    // par RewardGourd. Ce patch capte génériquement toute écriture, au prix de
    // ne pas savoir quel état exact du gourd l'a déclenchée.
    //
    // CheckTracker.TryMarkReported (persisté, cf. Core/CheckTracker.cs)
    // garantit qu'un même gourd déjà signalé — par ce patch, par
    // GourdStatePatch, ou lors d'une session précédente — n'est jamais
    // reporté deux fois.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetIntValue))]
    internal static class SaveValuePatch
    {
        private static void Postfix(string key, int value)
        {
            // value == 0 signifie "non pinné" (cf. Prop.SavePropHome dans les
            // notes) : ce n'est jamais un check, potentiellement un retrait.
            if (value == 0)
                return;

            if (!Enum.TryParse<SaveablePropName>(key, out var propName))
                return;

            if (!GourdRegistry.TryGetLocationId(propName, out var locationId))
                return;

            if (!CheckTracker.TryMarkReported(locationId))
                return;

            Plugin.Reporter.ReportCheck(locationId);
        }
    }
}
