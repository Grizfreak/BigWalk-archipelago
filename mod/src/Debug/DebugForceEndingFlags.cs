using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Action tool (not a diagnostic). Used to test the hypothesis noted in
    // big-walk-archipelago-notes.md (hub black sphere section, 2026-09-10)
    // after Ghidra decompilation of the "end of game" chain: no function of
    // EndingTransition/PeckEffectEndingTransition writes to SaveManager, so
    // there is probably no dedicated "game finished" flag — if the hub
    // sphere checks some notion of completion, it is likely a combination of
    // the already-known flags (EndingGate, GauntletComplete, the 7 big-key
    // plinths).
    //
    // This tool forces these 9 writes all at once, remotely (like
    // ItemApplier already does for receiving an item), without having to
    // find each big key or redo both bells for real. Use it on a save that
    // has **never** seen the ending screen, so as not to skew the test —
    // otherwise there's no way to know whether the sphere reacts to this
    // forcing or was already broken beforehand.
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
            Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}] Forcing EndingGate, GauntletComplete, and the 7 big keys...");

            foreach (var bigKey in BigKeys)
            {
                var applied = ItemApplier.ApplyBigKeyItem(bigKey);
                Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}]   {bigKey} -> {(applied ? "OK" : "failed (see warning above)")}");
            }

            SaveManager.SetIntValue("EndingGate", 1);
            SaveManager.SetIntValue("GauntletComplete", 1);

            Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}] Done. SaveManager[EndingGate]={SaveManager.GetIntValue("EndingGate", -12345, false)}, SaveManager[GauntletComplete]={SaveManager.GetIntValue("GauntletComplete", -12345, false)}.");
        }
    }
}
