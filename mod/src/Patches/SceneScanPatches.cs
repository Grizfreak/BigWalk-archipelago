using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // A scene-wide search costs about 10 ms: these say when one is worth doing.
    [HarmonyPatch(typeof(GourdMap), nameof(GourdMap.Initialize))]
    internal static class GourdMapInitializePatch
    {
        private static void Postfix() => VariantGourdMapUnlocker.FlagsReset();
    }

    [HarmonyPatch(typeof(Prop), nameof(Prop.Start))]
    internal static class PropStartPatch
    {
        private static void Postfix() => PegTileShuffler.MarkDirty();
    }
}
