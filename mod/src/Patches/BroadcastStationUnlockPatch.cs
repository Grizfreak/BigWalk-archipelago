using BigWalkArchipelago.Core;
using HarmonyLib;
using Mirror;

namespace BigWalkArchipelago.Patches
{
    // Stops a radio station from granting itself.
    //
    // `BroadcastStation.Unlock` is the effect subscribed to the station's peck
    // system, and it is the ONLY thing between switching a station on and its
    // music playing — an inlined copy of FmRadioManager.Unlock that writes
    // nothing to the save (decompiled 2026-09-21, see Core/RadioStations.cs
    // for the three facts this rests on). Skipping it therefore:
    //
    //   - leaves the check intact. The save write happens one level up, in the
    //     station's TrackedPeckState, which is where SaveValuePatch reads it.
    //     The player switches the station on, the check goes out, the music
    //     does not start.
    //   - is the only way to suppress it at all. There is no relock: writing
    //     the FmStation* key back to 0 does nothing, because FmRadioManager
    //     never reads it.
    //
    // It also runs on the restore path. A TrackedPeckState re-asserts its
    // saved state when its zone loads, which re-fires this effect — so
    // without the prefix every station already switched on would come back
    // for free on the next load, item or no item.
    //
    // Only the host suppresses. Nobody else in the session runs an
    // Archipelago client, so a guest has no way of knowing which stations the
    // slot has received; leaving their radio vanilla means they hear a
    // station as soon as it is switched on, while the host waits for the
    // item. A cosmetic difference between two players' speakers, and the
    // alternative — a guest whose radio can never play anything — is worse.
    // Stated in the apworld option so nobody discovers it in play.
    //
    // `nameof` rather than the literal "Unlock" that a private method usually
    // forces: Il2CppInterop re-emits the game's private methods as public, so
    // the compiler can check this one for us. A Harmony target that has been
    // renamed in a game update then fails the build instead of throwing
    // during PatchAll and taking the whole plugin down with it.
    //
    // The argument is taken by index (`__0`) and not by name. Parameter names
    // survive into the interop assembly only by convention, and a mismatch
    // there is the same load-time throw with no compiler warning.
    [HarmonyPatch(typeof(BroadcastStation), nameof(BroadcastStation.Unlock))]
    internal static class BroadcastStationUnlockPatch
    {
        private static bool Prefix(BroadcastStation __instance, PeckContext __0)
        {
            if (!ModConfig.ArchipelagoEnabled.Value || !RadioStations.ItemsInPlay || !NetworkServer.active)
                return true;

            // The original's own first line: anything below 1 is the switch
            // going off or idling, and unlocks nothing. Let it through rather
            // than claim in the log that a station was switched on.
            if (__0 == null || __0.compressedState < 1)
                return true;

            if (!RadioStations.TryGetSystem(__instance, out var system))
            {
                // One of FmStation7/8/9, or a station whose peck system is not
                // wired the way every other one is. Not ours to hold back: the
                // apworld has no item for it, so suppressing it would take
                // music away that nothing could ever give back.
                Plugin.Log.LogInfo(
                    $"[{nameof(BroadcastStationUnlockPatch)}] '{__instance.name}' has no station this world knows "
                    + "about; leaving its unlock alone.");
                return true;
            }

            if (RadioStations.IsGranted(system))
                return true;

            Plugin.Log.LogInfo(
                $"[{nameof(BroadcastStationUnlockPatch)}] {system} switched on; its music waits for the Archipelago "
                + "item.");
            return false;
        }
    }
}
