using System;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Logs the player-count screen's Play button and nothing else. It exists
    // because a start that stuck on that button (2026-09-25) could only be
    // explained by the order in which Continue and Play reach the hosting
    // screen's own start, and that order had never been written down. With
    // this line beside HostMenuConfirmStartPatch's own, the next log says it.
    //
    // Applied on its own rather than through PatchAll, and inside a
    // try/catch: a patch that fails to apply there throws out of Load and
    // takes every other feature of the mod with it, and a line of
    // diagnostics is not worth that risk.
    internal static class PlayerCountProceedLog
    {
        internal static void TryApply(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(PlayerCountMenu), nameof(PlayerCountMenu.ActionProceed));
                var prefix = new HarmonyMethod(typeof(PlayerCountProceedLog), nameof(Prefix));
                harmony.Patch(target, prefix: prefix);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"[{nameof(PlayerCountProceedLog)}] Not applied on this build: {ex.Message}");
            }
        }

        private static void Prefix()
        {
            Plugin.Log.LogInfo($"[{nameof(PlayerCountProceedLog)}] Play pressed on the player-count screen.");
        }
    }
}
