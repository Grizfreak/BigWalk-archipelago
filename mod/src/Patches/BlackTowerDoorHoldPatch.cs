using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Keeps the Black Tower's door at its foot open while `open_black_tower` asks for it, or while
    // the Black Tower takes gourds (Core/BlackTowerDoor): its combinator writes it shut again whenever one of the five
    // monuments' indicators changes. Only a lowering of that one state is refused.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class BlackTowerDoorHoldPatch
    {
        private static bool Prefix(TrackedPeckState __instance, PeckContext __0)
        {
            if (__0 == null || __0.compressedState >= 1 || !BlackTowerDoor.IsTheDoor(__instance))
                return true;

            return !BlackTowerDoor.Held;
        }
    }
}
