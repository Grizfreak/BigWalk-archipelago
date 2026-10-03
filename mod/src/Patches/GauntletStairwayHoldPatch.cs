using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Keeps a Silent Gauntlet stairway shut while `gauntlet_mode: locked_stages`
    // holds it and its item has not arrived (0.1.3).
    //
    // At the state, like ArchDoorHoldPatch: the stairway's buttons, the
    // combinator behind them and the debug keys all end in SetState on the
    // gate's own TrackedPeckState, so refusing it going up covers every way of
    // opening it. Only a raise is refused; the state going back down is always
    // let through.
    //
    // The first test is the cheap one, because this runs on every state change
    // in the game: it is a flag, then a lookup by instance id.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class GauntletStairwayHoldPatch
    {
        private static bool Prefix(TrackedPeckState __instance, PeckContext __0)
        {
            if (!GauntletStairways.Enabled || __0 == null || __0.compressedState < 1)
                return true;

            if (!GauntletStairways.IsHeldShut(__instance, out var level))
                return true;

            Plugin.Log.LogInfo(
                $"[{nameof(GauntletStairwayHoldPatch)}] Stage {level + 1}'s {__instance.transform.parent?.name} stays shut: its item has not arrived.");
            return false;
        }
    }
}
