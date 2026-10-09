using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // A fall that knocks a player down, as a DeathLink trigger (`big_fall`, Core/Traps.OnFall).
    // The host sees its own falls through TriggerFall and its guests' through ProcessRemoteFall.
    [HarmonyPatch(typeof(PlayerFaller), nameof(PlayerFaller.TriggerFall))]
    internal static class PlayerFallPatch
    {
        private static void Postfix(PlayerFaller __instance)
        {
            if (Mirror.NetworkServer.active && __instance != null)
                Traps.OnFall(__instance.playerCharacter);
        }
    }

    [HarmonyPatch(typeof(PlayerFaller), nameof(PlayerFaller.ProcessRemoteFall))]
    internal static class RemoteFallPatch
    {
        private static void Postfix(PlayerFaller __instance)
        {
            if (Mirror.NetworkServer.active && __instance != null)
                Traps.OnFall(__instance.playerCharacter);
        }
    }

    // A failed puzzle, for DeathLink (Core/PuzzleFailures): the failure switches of PressInOrder and
    // CountingController, and the success switch of a synchronised-button puzzle, go through
    // PeckSwitch.Peck. Both overloads; the cooldown there keeps one failure one.
    [HarmonyPatch(typeof(PeckSwitch), nameof(PeckSwitch.Peck), new System.Type[] { typeof(PeckContext) })]
    internal static class PuzzleFailurePatch
    {
        private static void Postfix(PeckSwitch __instance)
        {
            PuzzleFailures.OnPeck(__instance);
        }
    }

    [HarmonyPatch(typeof(PeckSwitch), nameof(PeckSwitch.Peck), new System.Type[0])]
    internal static class PuzzleFailurePlainPatch
    {
        private static void Postfix(PeckSwitch __instance)
        {
            PuzzleFailures.OnPeck(__instance);
        }
    }

    // Asleep under Big Sleep, the walker does not crouch either (player, 2026-10-05): the local
    // crouch input is not read, so the posture stays as it was when sleep came.
    [HarmonyPatch(typeof(PlayerCroucher), nameof(PlayerCroucher.UpdateLocal))]
    internal static class SleepCrouchPatch
    {
        private static bool Prefix()
        {
            return !TrapEffects.Asleep;
        }
    }

    // The screen masks some puzzles put on (binoculars, telescope, blindfold): Big Trip and Big
    // Meeting leave a masked player where they are (Core/TrapEffects.LocalMasked).
    [HarmonyPatch(typeof(PeckEffectMask), nameof(PeckEffectMask.SetMask))]
    internal static class ScreenMaskPatch
    {
        private static void Postfix(PeckEffectMask __instance, bool __0)
        {
            TrapEffects.NoteMask(__instance, __0);
        }
    }
}
