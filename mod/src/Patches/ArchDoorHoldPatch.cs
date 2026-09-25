using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Keeps an arch door shut while `lock_arch_doors` holds it and its item
    // has not arrived (2026-09-25).
    //
    // At the state, not at the switch, and on purpose: the question of which
    // button opens which door in vanilla never needs answering. Everything
    // that opens a door ends in TrackedPeckState.SetState on the door's own
    // SavableSystem — the one-way switch behind it, the save re-asserting a
    // door an older build of this mod opened (TrackedPeckState.Initialize
    // calls SetState with the saved value, decompiled the same day), and
    // this mod itself. Refusing the state going up while the door is held
    // covers all of them; the state going down is always let through, which
    // is how ArchDoors closes a door that was already open.
    //
    // Nothing is held on a machine that has not been told the list: a guest,
    // or a game with Archipelago off. The server's door is the guests' door.
    [HarmonyPatch(typeof(TrackedPeckState), nameof(TrackedPeckState.SetState), new[] { typeof(PeckContext) })]
    internal static class ArchDoorHoldPatch
    {
        private static bool Prefix(TrackedPeckState __instance, PeckContext __0)
        {
            if (__instance == null || __0 == null || __0.compressedState < 1)
                return true;

            var system = __instance.savableSystem;
            if (!ArchDoors.IsArchDoor(system) || !ArchDoors.IsHeldClosed(system))
                return true;

            Plugin.Log.LogInfo($"[{nameof(ArchDoorHoldPatch)}] {system} stays closed: its item has not arrived.");
            return false;
        }
    }
}
