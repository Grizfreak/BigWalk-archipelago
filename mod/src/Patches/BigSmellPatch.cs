using System;
using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Big Smell (Core/TrapEffects): the push away from the other players added to the velocity the controls ask for
    // this fixed step, so the game's movement, walls included, carries it. LocalFixedUpdate runs for the local player only.
    [HarmonyPatch(typeof(PlayerMover), nameof(PlayerMover.LocalFixedUpdate))]
    internal static class BigSmellPatch
    {
        private static void Postfix(PlayerMover __instance)
        {
            try
            {
                var push = TrapEffects.SmellVelocity;
                if (push.sqrMagnitude > 0f)
                    __instance.localControlsVelocity += push;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(BigSmellPatch)}] {ex.Message}");
            }
        }
    }
}
