using System;
using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // The arm gestures of `lock_abilities` (Core/AbilityLocks). The game decides each arm's pose every
    // frame from the input: `isWaving` is an arm raised (Q / E, already blocked in Rewired when its
    // raising is locked), `isPointing` a click held. Both together is the arm extended sideways. The
    // click also picks up and drops, so it cannot be switched off: the pointing is refused here instead,
    // on the local player only (the others' arms are their own machines' business).
    [HarmonyPatch(typeof(PlayerGestures), nameof(PlayerGestures.UpdateSingleArm))]
    internal static class ArmGestureLockPatch
    {
        private static readonly System.Collections.Generic.HashSet<string> Logged = new();

        internal static void LogOnce(string what)
        {
            if (Logged.Add(what))
                Plugin.Log.LogInfo($"[{nameof(ArmGestureLockPatch)}] {what}");
        }

        private static void Prefix(PlayerGestures __instance, PlayerGestures.GestureArmParams gestureArmParams, bool isWaving, ref bool isPointing)
        {
            try
            {
                // Every player this machine draws, not only its own: the locks are the slot's, the same for everyone,
                // and a remote player's pointing reached the others whatever was blocked on the network (2026-10-13).
                if (!isPointing || !AbilityLocks.Active || __instance?.playerCharacter == null)
                    return;

                var left = gestureArmParams.layerIndex == __instance.leftArmParams.layerIndex;
                var ability = isWaving
                    ? (left ? AbilityLocks.LeftExtending : AbilityLocks.RightExtending)
                    : (left ? AbilityLocks.LeftPointing : AbilityLocks.RightPointing);
                if (AbilityLocks.IsLocked(ability))
                {
                    isPointing = false;
                    LogOnce($"refused {ability} for {(__instance.playerCharacter.isLocalPlayer ? "the local player" : "a remote player")}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ArmGestureLockPatch)}] {ex.Message}");
            }
        }
    }

    // Chair sitting: a seat is entered through PlayerPoser.EnterPose (measured 2026-10-12, a stump of the
    // spawn courtyard). Refused for furniture only, while the ability is locked.
    [HarmonyPatch(typeof(PlayerPoser), nameof(PlayerPoser.EnterPose))]
    internal static class ChairSittingLockPatch
    {
        private static bool Prefix(PlayerPose pose)
        {
            try
            {
                if (!AbilityLocks.IsLocked(AbilityLocks.ChairSitting) || !AbilityLocks.IsFurniture(pose))
                    return true;

                Plugin.Log.LogInfo($"[{nameof(ChairSittingLockPatch)}] '{pose.gameObject.name}' refused: Chair Sitting is locked.");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ChairSittingLockPatch)}] {ex.Message}");
                return true;
            }
        }
    }

    // Throwing: a throw is a drop carrying the wind-up (PlayerHeldInformation.ThrowInfo); with no wind-up it is
    // a plain drop, so the object still leaves the hand. Local, like the arms.
    [HarmonyPatch(typeof(PlayerHeldInformation), nameof(PlayerHeldInformation.ThrowInfo))]
    internal static class ThrowLockPatch
    {
        private static void Prefix(ref float windUp)
        {
            if (windUp > 0f && AbilityLocks.IsLocked(AbilityLocks.Throwing) && !TrapEffects.TrapIsThrowing)
                windUp = 0f;
        }
    }

    // And no wind-up at all while throwing is locked: no arm drawn back, no wind-up sound.
    [HarmonyPatch(typeof(PlayerDecisions), nameof(PlayerDecisions.SetWindUpStartTime))]
    internal static class WindUpLockPatch
    {
        private static bool Prefix() => !AbilityLocks.IsLocked(AbilityLocks.Throwing) || TrapEffects.TrapIsThrowing;
    }

    // Carrying another player. Refused on the host, in the server half of the game's command: the local
    // PlayerActions.ActionPickUpPlayer is not the way the game takes this (measured 2026-10-12, a carry went
    // through without it), and every player's carry passes here, guests included.
    [HarmonyPatch(typeof(PlayerNetworking), "UserCode_CmdPickUpPlayer__PlayerCharacter")]
    internal static class CarryLockPatch
    {
        private static bool Prefix()
        {
            if (!Mirror.NetworkServer.active || !AbilityLocks.IsLocked(AbilityLocks.Carrying))
                return true;

            Plugin.Log.LogInfo($"[{nameof(CarryLockPatch)}] Refused: Carrying is locked.");
            return false;
        }
    }

    // And no outline on a player to pick up while Carrying is locked (player, 2026-10-12: it still showed while
    // the carry was refused; PlayerHighlighter.SetWorldCrosshair was not it). The local aim forgets the player
    // it found (PlayerCaster.castCharacter), so nothing is outlined and no carry is even asked for.
    [HarmonyPatch(typeof(PlayerCaster), nameof(PlayerCaster.LocalUpdate))]
    internal static class CarryAimLockPatch
    {
        private static void Postfix(PlayerCaster __instance)
        {
            if (__instance != null && __instance.castCharacter != null && AbilityLocks.IsLocked(AbilityLocks.Carrying))
                __instance.castCharacter = null;
        }
    }
}
