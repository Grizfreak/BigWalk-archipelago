using System;
using HarmonyLib;
using Mirror;

namespace BigWalkArchipelago.Patches
{
    // The server half of the game's pick-up Command, refusing cleanly a prop
    // that is no longer there.
    //
    // This mod makes props vanish under players: a solved puzzle's gourd
    // (Core/PuzzleGourdRetirer), and whatever Ctrl+R reclaims. A player's
    // pick-up Command can always be in flight when that happens, and the
    // game does not expect it. Decompiled on 2026-09-25,
    // `UserCode_CmdPickUp__PlayerHeldInformation` with a Prop whose identity
    // no longer resolves on the server falls through to a
    // NullReferenceException, and one whose object has been deactivated gets
    // as far as Prop.SetHeld and throws there. Either way the Command dies
    // half-way, and the player it came from is never answered: the first
    // alpha found a guest unable to pick anything up on their own screen for
    // the rest of the session.
    //
    // The answer is the one the game already gives when IsSafeToPickUp says
    // no, in the same function: write the player's held information back
    // with THEIR action number. That number matters — the owning client's
    // OnSetHeld drops any value numbered below its own last action — and
    // "holding nothing" makes that hook let go of whatever the player's
    // machine had grabbed ahead of the server.
    [HarmonyPatch(typeof(PlayerNetworking), "UserCode_CmdPickUp__PlayerHeldInformation")]
    internal static class PickUpVanishedPropPatch
    {
        private static bool Prefix(PlayerNetworking __instance, PlayerHeldInformation heldInformation)
        {
            try
            {
                if (heldInformation == null
                    || heldInformation.heldType != PlayerHeldInformation.HeldType.Prop
                    || IsStillThere(heldInformation.identity))
                    return true;

                heldInformation.identity = null;
                heldInformation.heldType = PlayerHeldInformation.HeldType.Nothing;
                heldInformation.hasDropData = false;
                heldInformation.isResultOfSnatch = false;
                __instance.NetworkplayerHeldInformation = heldInformation;

                Plugin.Log.LogInfo(
                    $"[{nameof(PickUpVanishedPropPatch)}] {__instance.gameObject.name} reached for a prop that is gone; "
                    + "told them they hold nothing.");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(PickUpVanishedPropPatch)}] Check failed, the game handles this pick-up: {ex.Message}");
                return true;
            }
        }

        private static bool IsStillThere(NetworkIdentity identity)
        {
            return identity != null
                && identity.netId != 0
                && identity.gameObject.activeInHierarchy;
        }
    }
}
