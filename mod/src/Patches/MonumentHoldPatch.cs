using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // `tower_mode` (ROADMAP G4, 0.5): a monument that is not the next one refuses a gourd, and the button that
    // opens its tower does not move. Both are asked on the game's own calls, so a flag comes first.
    //
    // A gourd is placed where the player stands, so the refusal is on every machine
    // (MonumentOrder.Tick also sets PropHome.blockPlacing there). The host decides which monuments are
    // open and tells the guests.
    [HarmonyPatch(typeof(PropHome), nameof(PropHome.IsSafeToPlace))]
    internal static class MonumentPlacePatch
    {
        private static void Postfix(PropHome __instance, ref bool __result)
        {
            if (!__result || !MonumentOrder.Active)
                return;

            if (MonumentOrder.IsClosed(__instance))
            {
                __result = false;
                ApNoticesLine.MonumentClosed();
            }
        }
    }

    // The pin itself, on the host: the game never asked IsSafeToPlace for the gourd put into the Yellow tower's monument
    // (2026-10-12, Ghidra: no caller of it found), so the operation that really pins a prop is refused as well. A
    // monument that already holds a gourd is never closed (MonumentOrder.IsClosed), so what a save restores goes through.
    [HarmonyPatch(typeof(PropHome), nameof(PropHome.SetPinned))]
    internal static class MonumentPinPatch
    {
        private static bool Prefix(PropHome __instance, Prop __0)
        {
            if (__0 == null || !MonumentOrder.Active || !Mirror.NetworkServer.active)
                return true;

            if (!MonumentOrder.IsClosed(__instance))
                return true;

            Plugin.Log.LogInfo(
                $"[{nameof(MonumentPinPatch)}] {__0.name} refused in {__instance.saveableHomeName}: that monument is not open.");
            ApNoticesLine.MonumentClosed();
            return false;
        }
    }

    // What the game really calls on the host when a player places a prop in a home: the command
    // `PlayerNetworking.CmdPlaceInHome`, then `Prop.ServerSetPinned(home)` (the first patch above is on
    // `PropHome.SetPinned`, which it does not go through: a gourd was put into a closed monument, 2026-10-12).
    [HarmonyPatch(typeof(Prop), nameof(Prop.ServerSetPinned))]
    internal static class MonumentServerPinPatch
    {
        private static bool Prefix(Prop __instance, PropHome __0)
        {
            if (__0 == null || !MonumentOrder.Active || !Mirror.NetworkServer.active)
                return true;

            if (!MonumentOrder.IsClosed(__0))
                return true;

            Plugin.Log.LogInfo(
                $"[{nameof(MonumentServerPinPatch)}] {__instance.name} refused in {__0.saveableHomeName}: that monument is not open.");
            ApNoticesLine.MonumentClosed();
            return false;
        }
    }

    // The towers' buttons: a state raise on a closed tower's is refused, like a Gauntlet stairway's
    // (GauntletStairwayHoldPatch), which is the same call.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class TowerButtonHoldPatch
    {
        private static bool Prefix(TrackedPeckState __instance, PeckContext __0)
        {
            if (!MonumentOrder.Active || __0 == null || __0.compressedState < 1)
                return true;

            if (!MonumentOrder.HoldsButton(__instance))
                return true;

            Plugin.Log.LogInfo(
                $"[{nameof(TowerButtonHoldPatch)}] {__instance.savableSystem} stays still: that tower's monument is not open.");
            ApNoticesLine.MonumentClosed();
            return false;
        }
    }

    // `lock_endings_until_objectives` with the goal big_wall (the player, 2026-10-12): the chapel's bell, the game's
    // `EndingGate` (0 off, 1 on, 2 animating; F9 at the bell, 2026-10-12), does not ring while the other objectives are not
    // done. The same call as the towers' buttons; the host decides, and a guest follows what the host said is shut.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class ChapelBellHoldPatch
    {
        private static bool Prefix(TrackedPeckState __instance, PeckContext __0)
        {
            // Not while the world loads: that is the save's own state coming back, not a ring (2026-10-12: refusing it
            // there left the goal flags with no baseline).
            if (__0 == null || __0.compressedState < 1 || __instance.savableSystem != SavableSystem.EndingGate
                || !WorldManager.isReadyForEffects)
                return true;

            if (!BigWalkArchipelago.Core.ObjectiveBoard.HoldsTheLastStairway || !BigWalkArchipelago.Core.ObjectiveBoard.Locked("big_wall"))
                return true;

            Plugin.Log.LogInfo($"[{nameof(ChapelBellHoldPatch)}] The bell stays still: the objectives are not done.");
            BigWalkArchipelago.Core.ObjectiveBoard.SayEndingIsShut("big_wall");
            return false;
        }
    }

    // One line on the screen when something is refused, at most one every few seconds.
    internal static class ApNoticesLine
    {
        private static float _saidAt = -100f;

        internal static void MonumentClosed()
        {
            if (UnityEngine.Time.unscaledTime - _saidAt < 6f)
                return;

            _saidAt = UnityEngine.Time.unscaledTime;
            BigWalkArchipelago.Core.Net.ApNotices.Post("That monument is not open yet: they open one after the other", warning: true);
        }
    }
}
