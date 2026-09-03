using System.Collections.Generic;

namespace BigWalkArchipelago.Core
{
    // Dédoublonnage partagé entre tous les Patches/ qui peuvent détecter le
    // même check par des chemins de code différents. Confirmé nécessaire en
    // session de test le 2026-09-03 : RewardGourd.ServerSetGourdState(Loose)
    // ET PeckEffectSavableHome.Peck() (sur le "vice launch switch") écrivent
    // tous les deux dans SaveManager.SetIntValue pour le même gourd — sans ce
    // tracker partagé, GourdStatePatch et SaveValuePatch reporteraient chacun
    // leur propre check pour le même événement.
    internal static class CheckTracker
    {
        private static readonly HashSet<string> Reported = new();

        internal static bool TryMarkReported(string locationId) => Reported.Add(locationId);
    }
}
