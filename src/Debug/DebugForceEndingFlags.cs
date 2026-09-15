using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Outil d'action (pas un diagnostic). Sert à tester l'hypothèse notée
    // dans big-walk-archipelago-notes.md (section sphère noire du hub,
    // 2026-09-10) après décompilation Ghidra de la chaîne "fin de partie" :
    // aucune fonction de EndingTransition/PeckEffectEndingTransition
    // n'écrit dans SaveManager, donc il n'existe probablement pas de flag
    // dédié "jeu terminé" — si la sphère du hub vérifie une notion de
    // complétion, c'est vraisemblablement une combinaison des flags déjà
    // connus (EndingGate, GauntletComplete, les 7 plinthes de big key).
    //
    // Ce tool force ces 9 écritures d'un coup, à distance (comme ItemApplier
    // le fait déjà pour la réception d'un item), sans avoir à retrouver
    // chaque big key ni à refaire les deux cloches en vrai. À utiliser sur
    // une save qui n'a **jamais** vu l'écran de fin, pour ne pas fausser le
    // test — sinon on ne peut pas savoir si la sphère réagit à ce forçage ou
    // était déjà cassée avant.
    internal static class DebugForceEndingFlags
    {
        private static readonly SaveablePropName[] BigKeys =
        {
            SaveablePropName.bigKeyIntro,
            SaveablePropName.bigKeyRedZone,
            SaveablePropName.bigKeyGreenZone,
            SaveablePropName.bigKeyBlueZone,
            SaveablePropName.bigKeyYellowZone,
            SaveablePropName.bigKeyBoss,
            SaveablePropName.bigKeyOverflow,
        };

        internal static void ForceAll()
        {
            Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}] Forçage de EndingGate, GauntletComplete et des 7 big keys...");

            foreach (var bigKey in BigKeys)
            {
                var applied = ItemApplier.ApplyBigKeyItem(bigKey);
                Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}]   {bigKey} -> {(applied ? "OK" : "échec (voir warning ci-dessus)")}");
            }

            SaveManager.SetIntValue("EndingGate", 1);
            SaveManager.SetIntValue("GauntletComplete", 1);

            Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}] Terminé. SaveManager[EndingGate]={SaveManager.GetIntValue("EndingGate", -12345, false)}, SaveManager[GauntletComplete]={SaveManager.GetIntValue("GauntletComplete", -12345, false)}.");
        }
    }
}
