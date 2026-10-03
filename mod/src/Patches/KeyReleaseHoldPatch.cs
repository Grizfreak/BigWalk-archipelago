using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Keeps a big key in its stone while its item has not arrived (2026-10-03).
    //
    // KeyCustody already stops the key being picked up. This stops the stone letting
    // it go in the first place: the game launches a key out of its stone when a
    // monument fills, and a key lying free is out of reach of the lock on its home.
    // Every release ends in SetState on the stone's own TrackedPeckState
    // (`KeyStoneLogic`, or `KeyScrewLogic` for the Green Dome), so refusing a state
    // above 0 there covers whatever asks, and the state going back down is let through.
    //
    // The first test is a flag, because this runs on every state change in the game.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class KeyReleaseHoldPatch
    {
        private static bool Prefix(TrackedPeckState __instance, PeckContext __0)
        {
            if (!KeyCustody.KeysAreItems || __0 == null || __0.compressedState < 1)
                return true;

            if (!KeyCustody.IsReleaseHeld(__instance, out var propName))
                return true;

            Plugin.Log.LogInfo(
                $"[{nameof(KeyReleaseHoldPatch)}] {propName} stays in its stone: its item has not arrived.");
            return false;
        }
    }
}
