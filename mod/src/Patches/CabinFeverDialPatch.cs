using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // R6b: the Cabin Fever dial's animation (Core/CabinFeverDial), written after the game's own text.
    [HarmonyPatch(typeof(TimerTextController), nameof(TimerTextController.Update))]
    internal static class CabinFeverDialPatch
    {
        private static void Postfix(TimerTextController __instance)
        {
            CabinFeverDial.Apply(__instance);
        }
    }
}
