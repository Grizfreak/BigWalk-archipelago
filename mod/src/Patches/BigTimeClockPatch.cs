using System;
using BigWalkArchipelago.Core;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Patches
{
    // Big Time's clocks (Core/Traps): the game's LobbyClock turns a clock's two hands on the island's time, fast.
    // A clock the mod made tells the real time of this machine's own computer instead (player, 2026-10-13):
    // the black hand the hours, the red one the minutes (positive angles turn clockwise on this model, seen 2026-10-13). Each machine draws its own clocks, so each player
    // reads their own time.
    [HarmonyPatch(typeof(LobbyClock), nameof(LobbyClock.Update))]
    internal static class BigTimeClockPatch
    {
        private static bool Prefix(LobbyClock __instance)
        {
            try
            {
                if (__instance == null || !__instance.gameObject.name.Contains(ReceivedItemSpawner.CosmeticNameSuffix, StringComparison.Ordinal))
                    return true;

                var now = DateTime.Now;
                var hours = (now.Hour % 12 + now.Minute / 60f) * 30f;
                var minutes = (now.Minute + now.Second / 60f) * 6f;
                if (__instance.hourHand != null)
                    __instance.hourHand.localEulerAngles = new Vector3(0f, 0f, hours);
                if (__instance.secondHand != null)
                    __instance.secondHand.localEulerAngles = new Vector3(0f, 0f, minutes);
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
