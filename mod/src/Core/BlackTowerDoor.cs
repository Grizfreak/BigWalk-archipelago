using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The Black Tower's door at its foot, open from the start when the slot asks
    // (`open_black_tower`, player's wish 2026-10-05).
    //
    // In the game the door opens once the five monuments of the hub and the four towers are full:
    // a combinator (`BlackTower <n>Player/Positioner/DoorLogic`, rule min5@1 over the five
    // `LiveSignal_EndingGate` indicators) drives the door's state (`BasicDoor/BasicDoorPeckLogic`,
    // saved nowhere), measured with the wiring dump of 2026-10-05. The host sets that state to open
    // and Patches/BlackTowerDoorHoldPatch refuses it going back down, since the combinator writes
    // it again whenever one of its indicators changes. The state is networked, so guests follow.
    //
    // The button at the top of the tower (its inner door) is left to the players.
    internal static class BlackTowerDoor
    {
        internal static bool Enabled { get; private set; }

        private static TrackedPeckState _door;

        internal static void Configure(bool open)
        {
            if (Enabled != open)
                Plugin.Log.LogInfo($"[{nameof(BlackTowerDoor)}] The Black Tower's door is {(open ? "open from the start" : "as in the game")}.");

            Enabled = open;
        }

        internal static bool IsTheDoor(TrackedPeckState state) => _door != null && state == _door;

        // Every couple of seconds (TeleportButtonRunner): finds the door once its world is
        // loaded and opens it.
        internal static void Enforce()
        {
            if (!Enabled || !NetworkServer.active)
                return;

            var door = FindDoor();
            if (door == null || door.currentPeckContext.state >= 1)
                return;

            door.SetState(1);
            Plugin.Log.LogInfo($"[{nameof(BlackTowerDoor)}] The Black Tower's door opened.");
        }

        private static TrackedPeckState FindDoor()
        {
            if (_door != null)
                return _door;

            foreach (var state in UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (state == null || state.name != "BasicDoorPeckLogic")
                    continue;

                var door = state.transform.parent;
                var positioner = door != null ? door.parent : null;
                var tower = positioner != null ? positioner.parent : null;
                if (door == null || door.name != "BasicDoor" || positioner == null || positioner.name != "Positioner"
                    || tower == null || !tower.name.StartsWith("BlackTower "))
                    continue;

                _door = state;
                Plugin.Log.LogInfo($"[{nameof(BlackTowerDoor)}] Door found under '{tower.name}'.");
                return state;
            }

            return null;
        }
    }
}
