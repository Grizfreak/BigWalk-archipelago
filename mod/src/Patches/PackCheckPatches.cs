using System;
using BigWalkArchipelago.Core;
using HarmonyLib;
using Mirror;

namespace BigWalkArchipelago.Patches
{
    // A pack picked up, for its check (Core/PackChecks). After the server half of the game's
    // pick-up Command, and only when the pick-up went through: the player's held information then
    // names the prop asked for. Every player's pick-up passes here on the host.
    [HarmonyPatch(typeof(PlayerNetworking), "UserCode_CmdPickUp__PlayerHeldInformation")]
    internal static class PackPickUpPatch
    {
        private static void Postfix(PlayerNetworking __instance, PlayerHeldInformation heldInformation)
        {
            try
            {
                if (!NetworkServer.active || heldInformation == null
                    || heldInformation.heldType != PlayerHeldInformation.HeldType.Prop || heldInformation.identity == null)
                    return;

                var held = __instance.playerHeldInformation;
                if (held == null || held.identity == null || held.identity.netId != heldInformation.identity.netId)
                    return;

                PackChecks.OnPickedUp(heldInformation.identity.GetComponent<Prop>());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(PackPickUpPatch)}] {ex.Message}");
            }
        }
    }
}
