using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The hub's three arch doors as Archipelago items (`lock_arch_doors`,
    // 2026-09-25).
    //
    // Each door is a TrackedPeckState keyed on its own SavableSystem —
    // SpawnHubGate (the first, the tutorial's way back to the hub),
    // HubShortcutToSportsCreek (left) and HubTunnel (right) — and SetState(1)
    // on the live object opens it on the spot (see ArchDoorUnlocker). So:
    //
    //   - a door the slot holds closed is kept closed by refusing its state
    //     going up (Patches/ArchDoorHoldPatch), whatever asks: the game's own
    //     switch, or the save re-asserting a door an older build opened;
    //   - its item opens it, once, and is remembered in the save
    //     (`ap_archdoor_<system>`), because items already applied are not
    //     replayed on the next connection;
    //   - every other door is opened as the mod always has.
    //
    // Host only, like everything that answers to slot_data. A guest's door
    // is the server's door: its state reaches them over the network.
    internal static class ArchDoors
    {
        private const string KeyPrefix = "ap_archdoor_";

        internal static readonly SavableSystem[] All =
        {
            SavableSystem.SpawnHubGate,
            SavableSystem.HubShortcutToSportsCreek,
            SavableSystem.HubTunnel,
        };

        private static readonly HashSet<SavableSystem> Locked = new();

        // False until slot_data has said which doors are locked, so nothing is
        // opened on the strength of a list that has not arrived yet.
        internal static bool Configured { get; private set; }

        internal static bool IsArchDoor(SavableSystem system)
        {
            return Array.IndexOf(All, system) >= 0;
        }

        internal static void Configure(IEnumerable<string> lockedSystemNames)
        {
            Locked.Clear();
            foreach (var name in lockedSystemNames ?? Array.Empty<string>())
            {
                if (Enum.TryParse<SavableSystem>(name, out var system) && IsArchDoor(system))
                    Locked.Add(system);
                else
                    Plugin.Log.LogWarning($"[{nameof(ArchDoors)}] '{name}' is not an arch door this build knows; ignored.");
            }

            Configured = true;
            Plugin.Log.LogInfo(
                $"[{nameof(ArchDoors)}] Held closed until their item arrives: "
                + (Locked.Count == 0 ? "none." : string.Join(", ", Locked) + "."));

            Enforce();
        }

        // Without Archipelago there is no list to wait for: every door opens.
        internal static void ConfigureWithoutArchipelago()
        {
            Locked.Clear();
            Configured = true;
        }

        internal static void Forget()
        {
            Locked.Clear();
            Configured = false;
        }

        internal static bool IsHeldClosed(SavableSystem system)
        {
            return Locked.Contains(system) && !IsGranted(system);
        }

        private static bool IsGranted(SavableSystem system)
        {
            return SaveManager.GetIntValue(KeyPrefix + system, 0, false) != 0;
        }

        internal static bool Grant(SavableSystem system)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"[{nameof(ArchDoors)}] Ignored: {system} received while not host.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + system, 1);
            Plugin.Log.LogInfo($"[{nameof(ArchDoors)}] {system} granted.");
            SetDoor(system, open: true);
            return true;
        }

        // A save rebound to another seed replays every item from scratch, so
        // doors granted under the old binding go with it.
        internal static void ClearLedger()
        {
            foreach (var system in All)
                SaveManager.SetIntValue(KeyPrefix + system, 0);
        }

        // Brings every door to what the slot says it should be: held closed,
        // or open. Run when slot_data arrives and whenever a world becomes
        // ready, since either can come first.
        internal static void Enforce()
        {
            if (!Configured || !NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            foreach (var system in All)
                SetDoor(system, open: !IsHeldClosed(system));
        }

        // Live when the door's object is loaded, which is what makes it swing
        // there and then; the save alone otherwise, which the door reads back
        // on its next load. Closing goes through the same call: the hold patch
        // only ever refuses a state going UP.
        private static void SetDoor(SavableSystem system, bool open)
        {
            var target = FindState(system);
            var wanted = open ? 1 : 0;

            if (target != null)
            {
                if (target.currentPeckContext.state == wanted)
                    return;

                target.SetState(wanted);
                Plugin.Log.LogInfo($"[{nameof(ArchDoors)}] {system} {(open ? "opened" : "closed")}.");
                return;
            }

            if (SaveManager.GetIntValue(system.ToString(), 0, false) != wanted)
                SaveManager.SetIntValue(system.ToString(), wanted);
        }

        private static TrackedPeckState FindState(SavableSystem system)
        {
            var loaded = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsSortMode.None);
            if (loaded == null)
                return null;

            foreach (var state in loaded)
            {
                if (state != null && state.savableSystem == system)
                    return state;
            }

            return null;
        }
    }
}
