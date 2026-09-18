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
            Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}] Pinning the 7 big keys...");

            foreach (var bigKey in BigKeys)
            {
                var applied = ItemApplier.ApplyBigKeyItem(bigKey);
                Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}]   {bigKey} -> {(applied ? "OK" : "failed (see warning above)")}");
            }

            // The two goal flags are NO LONGER written here, and the name of
            // this tool is now a little wrong.
            //
            // It wrote them to test the 2026-09-10 hub-sphere hypothesis,
            // which has long since been answered. What it kept doing was
            // latching an Archipelago goal by accident: GauntletComplete is
            // absent from a fresh save, so it has no load baseline, and the
            // first write during play is taken — correctly — for the real
            // thing (cf. ApGoalFlags). A player pressing this key to get the
            // seven big keys was silently completing their own gauntlet
            // goal; it happened on 2026-09-18.
            //
            // Pinning the big keys is the part that is actually useful, and
            // it goes through ItemApplier like a genuine item, so nothing
            // here fakes progress any more.
            Plugin.Log.LogInfo($"[{nameof(DebugForceEndingFlags)}] Done. The goal flags are deliberately left alone — reach them in-game.");
        }
    }
}
