using System;
using BigWalkArchipelago.Core;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Patches
{
    // Big Slide (Core/TrapEffects): after the game has set the velocity the controls ask for this fixed step, the
    // local player only drifts towards it, from the velocity it had. LocalFixedUpdate runs for the local player only.
    [HarmonyPatch(typeof(PlayerMover), nameof(PlayerMover.LocalFixedUpdate))]
    internal static class BigSlidePatch
    {
        private static Vector3 _drift;
        private static bool _drifting;

        private static void Postfix(PlayerMover __instance)
        {
            try
            {
                if (!TrapEffects.SlideActive)
                {
                    _drifting = false;
                    return;
                }

                var asked = __instance.localControlsVelocity;
                if (!_drifting)
                {
                    _drift = asked;
                    _drifting = true;
                    return;
                }

                _drift = Vector3.Lerp(_drift, asked, TrapEffects.SlideGrip);
                _drift.y = asked.y;
                __instance.localControlsVelocity = _drift;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(BigSlidePatch)}] {ex.Message}");
            }
        }
    }
}
