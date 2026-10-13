using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Big Ghost (Core/TrapEffects): after the game has set a player's whiteness for the frame (the Silent Gauntlet's
    // speechlessness, 0 outside it), the full white while the trap lasts. Only the looks: the voice is untouched.
    [HarmonyPatch(typeof(PlayerSpeechless), nameof(PlayerSpeechless.Update))]
    internal static class BigGhostPatch
    {
        private static void Postfix(PlayerSpeechless __instance)
        {
            if (!TrapEffects.GhostActive)
                return;
            var player = __instance.playerCharacter;
            if (player != null && player.looks != null)
                player.looks.SetSpeechlessness(1f);
        }
    }
}
