using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Tells Core/WorldButtons when one of its native buttons is pressed. A postfix on SetState,
    // the one place every press ends, and the first test is a flag so the game's many other
    // state changes cost nothing while the mod has put no button into the world.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class WorldButtonPatch
    {
        private static void Postfix(TrackedPeckState __instance, PeckContext __0)
        {
            if (Debug.DebugStateLog.On)
                Debug.DebugStateLog.ObserveState(__instance, __0.state);

            PackChecks.OnState(__instance, __0.state);
            PuzzleFailures.OnState(__instance, __0.state);

            if (!WorldButtons.HasNative || __instance == null)
                return;

            WorldButtons.OnState(__instance, __0);
        }
    }
}
