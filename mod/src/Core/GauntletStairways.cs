using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The Silent Gauntlet's stairways as Archipelago items (`gauntlet_mode:
    // locked_stages`, 0.1.3).
    //
    // What the game does, measured on 2026-10-03 (the Keypad 5 dump). Each of
    // the seven stages, `Level0..6` under `SilentGauntlet 2Player/Positioner`,
    // has two doors. Solving the stage's puzzle writes the system
    // `GauntletChamber<N>` and opens a wall inside the stage; that write is
    // the stage's check, reported like a radio station's (SaveValuePatch). The
    // stairway out of the stage, `GourdTower_Stairs/GourdTower_Gate All Hold`,
    // opens when its buttons are held together: the TrackedPeckState
    // `GourdTowerGateNetworking` goes 0 to 1. Nothing saves it.
    //
    // So, while the slot says `locked_stages`:
    //
    //   - a stairway's state going up is refused until its item has arrived
    //     (Patches/GauntletStairwayHoldPatch), whatever asks, and so is the
    //     wall inside the stage that the puzzle opens;
    //   - its item sets the state to 1 and is remembered in the save
    //     (`ap_gauntlet_<N>`), because items already applied are not replayed;
    //   - the state is not saved by the game, so every granted stairway is
    //     opened again whenever a world is up (GauntletStairwayEnforcer).
    //
    // Host only, like every item: a guest's stairway is the server's, and its
    // state reaches them over the network.
    internal static class GauntletStairways
    {
        private const string KeyPrefix = "ap_gauntlet_";
        private const string GateName = "GourdTowerGateNetworking";
        private const string GatePath = "GourdTower_Gate All Hold";
        private const string WallPath = "GourdTower_Gate Challenge Complete";
        private const string GauntletPath = "SilentGauntlet";

        // Stage N's puzzle system, in stage order: the level a system opens is
        // its position here.
        internal static readonly SavableSystem[] Chambers =
        {
            SavableSystem.GauntletChamber0,
            SavableSystem.GauntletChamber1,
            SavableSystem.GauntletChamber2,
            SavableSystem.GauntletChamber3,
            SavableSystem.GauntletChamber4,
            SavableSystem.GauntletChamber5,
            SavableSystem.GauntletChamber6,
        };

        // The two gates of each stage, found in the scene by name and path.
        // GateByLevel is the stairway, which the buttons opened in the game.
        // WallByLevel is the wall inside the stage, which the puzzle opens: it
        // is held shut like the stairway, because a wall that opened while the
        // way on stayed closed would look like progress that is not. Its item
        // opens it once the puzzle is solved, or at once when the puzzles are
        // not required.
        private static readonly TrackedPeckState[] GateByLevel = new TrackedPeckState[7];
        private static readonly TrackedPeckState[] WallByLevel = new TrackedPeckState[7];

        // Instance id of every gate or wall found, and the level it belongs to.
        private static readonly Dictionary<int, int> LevelByInstance = new();

        // Instance ids already looked at and found not to be a gate, so the
        // patch does not read the same state's name twice.
        private static readonly HashSet<int> NotAGate = new();

        // From slot_data (`gauntlet_puzzles_required`): whether the puzzle is
        // still needed to open a stage's wall.
        private static bool _puzzlesRequired = true;

        // Looking for the gates reads every saved state of the world, so a gate
        // that cannot be found is looked for again only now and then.
        private const float DiscoverIntervalSeconds = 10f;
        private static float _nextDiscoverAt;

        // The pseudo-need PuzzleNeedHider files the stairway buttons under. It
        // is not one of PuzzleNeeds' own, so no item, ledger or overlay line
        // belongs to it: whether it is hidden is ButtonsHidden.
        internal const string ButtonsNeed = "gauntlet_stairs";

        private static bool _mirrored;
        private static bool _partsLocked;
        private static bool _partsMirrored;

        // False until slot_data has said so: nothing is held on the strength
        // of a list that has not arrived, and a guest never holds anything.
        internal static bool Enabled { get; private set; }

        // The stairway buttons are gone wherever the stages are locked: on the
        // host from slot_data, on a guest because its host said so.
        internal static bool ButtonsHidden => Enabled || _mirrored;

        // The parts of the stages' puzzles are hidden until their item arrives
        // (PuzzleNeedHider) wherever the slot counts on them, in either Gauntlet
        // mode (`lock_gauntlet_needs`).
        internal static bool PartsHidden => _partsLocked || _partsMirrored;

        // Guest: the host's snapshot says whether the stages, and their parts,
        // are locked.
        internal static void ApplyFromHost(bool lockedStages, bool partsLocked)
        {
            _partsMirrored = partsLocked;

            if (!lockedStages && !_mirrored)
                return;

            _mirrored = lockedStages;
        }

        // The guest left the host: whatever it was told stops being true.
        internal static void ForgetMirror()
        {
            _mirrored = false;
            _partsMirrored = false;
        }

        internal static bool IsChamber(SavableSystem system)
        {
            return Array.IndexOf(Chambers, system) >= 0;
        }

        internal static void Configure(bool lockedStages, bool puzzlesRequired, bool partsLocked)
        {
            Enabled = lockedStages;
            _puzzlesRequired = puzzlesRequired;
            _partsLocked = partsLocked;
            Plugin.Log.LogInfo(
                $"[{nameof(GauntletStairways)}] "
                + (Enabled
                    ? "locked stages: each stairway stays shut until its item arrives"
                      + (_puzzlesRequired ? "; a stage's puzzle opens its wall." : "; a stage's item opens its wall too.")
                    : "vanilla stairways."));

            Enforce();
        }

        // The world went away: its gates went with it. The slot's setting did
        // not, because the Archipelago connection outlives a return to the
        // menu, and hosting the same save again does not say it a second time.
        internal static void ForgetGates()
        {
            LevelByInstance.Clear();
            NotAGate.Clear();
            Array.Clear(GateByLevel, 0, GateByLevel.Length);
            Array.Clear(WallByLevel, 0, WallByLevel.Length);
            _nextDiscoverAt = 0f;
        }

        // A save hosted with Archipelago switched off is vanilla.
        internal static void Forget()
        {
            Enabled = false;
            _partsLocked = false;
            ForgetGates();
        }

        internal static bool IsGranted(int level)
        {
            return SaveManager.GetIntValue(KeyPrefix + level, 0, false) != 0;
        }

        // Whether this stage's puzzle has been solved: the system its solving
        // writes, which is also the stage's check.
        private static bool PuzzleDone(int level)
        {
            return SaveManager.GetIntValue(Chambers[level].ToString(), 0, false) != 0;
        }

        // The patch's question, asked on every state change of the game, so it
        // is a set lookup and nothing else. It covers a stage's stairway and its
        // wall alike.
        //
        // A gate is recognised here on its first state change and not only when
        // Discover finds it: the game saves a solved puzzle and opens that
        // stage's wall itself while a world loads, which is before anything has
        // looked for the gates (found 2026-10-03: a wall open after a reload
        // whose item had not arrived).
        internal static bool IsHeldShut(TrackedPeckState state, out int level)
        {
            level = -1;
            if (!Enabled || state == null)
                return false;

            var id = state.GetInstanceID();
            if (!LevelByInstance.TryGetValue(id, out level))
            {
                if (NotAGate.Contains(id))
                    return false;

                if (!TryRegister(state, out level))
                {
                    NotAGate.Add(id);
                    return false;
                }
            }

            return !IsGranted(level);
        }

        // The lowest stage whose stairway item has not arrived, for a progressive door; -1 when all
        // seven have.
        internal static int LowestShut()
        {
            for (var level = 0; level < Chambers.Length; level++)
            {
                if (!IsGranted(level))
                    return level;
            }

            return -1;
        }

        internal static bool Grant(int level)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"[{nameof(GauntletStairways)}] Ignored: stage {level + 1} stairway received while not host.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + level, 1);
            Plugin.Log.LogInfo($"[{nameof(GauntletStairways)}] Stage {level + 1} stairway granted.");
            Open(level);
            return true;
        }

        // A save rebound to another seed replays every item from scratch, so
        // stairways granted under the old binding go with it.
        internal static void ClearLedger()
        {
            for (var level = 0; level < Chambers.Length; level++)
                SaveManager.SetIntValue(KeyPrefix + level, 0);
        }

        // Brings every stage to what the slot says: opened where its item has
        // arrived, shut where it has not. Cheap enough to run every couple of
        // seconds, which is how a gate whose object loads late still follows,
        // and how a wall the game opened on its own while the world loaded is
        // put back when its item is missing. Does nothing when the slot does
        // not lock the stages.
        internal static void Enforce()
        {
            if (!Enabled || !NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            Discover();
            for (var level = 0; level < Chambers.Length; level++)
            {
                if (IsGranted(level))
                    Open(level);
                else
                    Shut(level);
            }
        }

        // Only a gate that is open is touched, and going down is always let
        // through by the hold patch.
        private static void Shut(int level)
        {
            ShutGate(GateByLevel[level], $"Stage {level + 1} stairway");
            ShutGate(WallByLevel[level], $"Stage {level + 1} wall");
        }

        private static void ShutGate(TrackedPeckState gate, string what)
        {
            if (gate == null || gate.currentPeckContext.state < 1)
                return;

            gate.SetState(0);
            Plugin.Log.LogInfo($"[{nameof(GauntletStairways)}] {what} shut again: its item has not arrived.");
        }

        private static void Open(int level)
        {
            OpenGate(GateByLevel[level], $"Stage {level + 1} stairway");

            // With the puzzles required, the wall waits for its puzzle: solved
            // before the item came, it opens now; solved after, the game opens
            // it itself, the hold being lifted.
            if (!_puzzlesRequired || PuzzleDone(level))
                OpenGate(WallByLevel[level], $"Stage {level + 1} wall");
        }

        private static void OpenGate(TrackedPeckState gate, string what)
        {
            if (gate == null || gate.currentPeckContext.state >= 1)
                return;

            gate.SetState(1);
            Plugin.Log.LogInfo($"[{nameof(GauntletStairways)}] {what} opened.");
        }

        // Finds the gates, once per world and again when one is gone.
        private static void Discover()
        {
            var missing = false;
            foreach (var gate in GateByLevel)
                missing |= gate == null;
            foreach (var wall in WallByLevel)
                missing |= wall == null;

            if (!missing || Time.unscaledTime < _nextDiscoverAt)
                return;

            _nextDiscoverAt = Time.unscaledTime + DiscoverIntervalSeconds;

            var loaded = UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (loaded == null)
                return;

            foreach (var state in loaded)
            {
                try
                {
                    if (state != null)
                        TryRegister(state, out _);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(GauntletStairways)}] A gate was skipped: {ex.Message}");
                }
            }
        }

        // Whether this state is one of the stages' gates, and which stage's.
        // Both kinds are held and both are remembered by level.
        private static bool TryRegister(TrackedPeckState state, out int level)
        {
            level = -1;
            if (state.gameObject.name != GateName)
                return false;

            var path = PathOf(state.transform);
            if (!path.Contains(GauntletPath) || !TryLevelOf(path, out level))
                return false;

            if (path.Contains(GatePath))
                GateByLevel[level] = state;
            else if (path.Contains(WallPath))
                WallByLevel[level] = state;
            else
                return false;

            LevelByInstance[state.GetInstanceID()] = level;
            return true;
        }

        private static bool TryLevelOf(string path, out int level)
        {
            level = -1;
            foreach (var part in path.Split('/'))
            {
                if (part.Length == 6 && part.StartsWith("Level", StringComparison.Ordinal)
                    && int.TryParse(part.Substring(5), out level) && level >= 0 && level < Chambers.Length)
                    return true;
            }

            return false;
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
