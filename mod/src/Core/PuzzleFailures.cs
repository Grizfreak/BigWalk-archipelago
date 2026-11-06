using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // A failed puzzle, as a DeathLink trigger (`puzzle_failed`, Core/Traps). Three signs, read with
    // Ghidra and the state log on 2026-10-05:
    //
    //   A validator's screen goes to Failure. ValidatorDisplay shows Off 0, Blank 1, Success 2,
    //   Failure 3, Thinking 4 through a TrackedPeckState of its own; the tile validators
    //   (PegTileValidator.AfterDrumRoll) and the timers (TimerDialController: the timed tomato's
    //   dial among them) set it. Their own `onFailure` switch is often not wired (Indoor
    //   Semaphore's is not), the screen always is.
    //
    //   A failure switch fires: PressInOrder and CountingController have one (PeckSwitch.Peck).
    //   Not PressInOrder's reset (player on Discord, 2026-10: opening the door of Pointer's Paradise killed
    //   everyone, "as a button pressed in the wrong order"). Read with Ghidra on 2026-10-20: a
    //   wrong press (OnInput) pecks the failure switch with the presser's context, and SetOff,
    //   when the puzzle goes inactive with `resetWhenInactive`, pecks it too, with an empty one
    //   (no player, action -1): the game's way of putting the lights out.
    //
    //   A timed tomato runs out with its puzzle not done (state log, 2026-10-05): the Pomodoro's own
    //   state goes 1 when it starts and back to 0 when its time is up. Only the tomatoes that sit
    //   in a PomodoroDispenser when the world loads (player: a tomato given some other way, by a
    //   trap one day, is no puzzle's); its puzzle is the one nearest that dispenser. Solved in
    //   the meantime (its check made), it was no failure.
    //
    //   A synchronised button lets go with its puzzle not done (player, 2026-10-05): the buttons of a
    //   SimPressController spring back on their own after a while, and one springing back while
    //   the controller has not succeeded means the press was missed. Once its success switch has
    //   fired, a controller's buttons no longer count. (Not the buttons two players hold together:
    //   those are another class.)
    //
    // Everything is listed once per world, by instance, with the nearest puzzle for the log. Only
    // the host acts: it holds the connection and the puzzles' states are its own.
    internal static class PuzzleFailures
    {
        private const string Tag = "[" + nameof(PuzzleFailures) + "]";

        // One failure per puzzle part per few seconds, and none in the first seconds of a world,
        // when every state re-asserts itself.
        private const float Cooldown = 3f;
        private const float WorldSettleSeconds = 8f;
        private const int ScreenFailure = 3;

        // A clamp opens slowly; the check goes once the gourd is loose (Tyrope, Window Labyrinth).
        private const float TomatoGraceSeconds = 15f;

        // A door closing pecks the failure switch and turns the puzzle off, in either order.
        private const int ResetFrames = 3;

        private const string ChamberPrefix = "GauntletChamber";

        private sealed class World
        {
            internal readonly Dictionary<int, string> FailureSwitches = new();

            // The failure switches of the PressInOrder puzzles, whose reset pecks them too.
            internal readonly HashSet<int> ResetBySetOff = new();

            // A PressInOrder's active state, by instance: its failure switch.
            internal readonly Dictionary<int, int> ActiveOf = new();

            // A failure peck waiting a few frames for the door that may turn the puzzle off.
            internal readonly Dictionary<int, int> PendingFrame = new();

            // When a PressInOrder last went off, by its failure switch.
            internal readonly Dictionary<int, int> OffFrame = new();

            // The Gauntlet stage of each id that Fail can receive.
            internal readonly Dictionary<int, int> StageOf = new();
            internal readonly Dictionary<int, string> Screens = new();

            // A synchronised button's state, by instance: the controller it belongs to.
            internal readonly Dictionary<int, int> SyncButtons = new();
            internal readonly Dictionary<int, string> SyncControllers = new();

            // A controller's success switch, by instance: the controller.
            internal readonly Dictionary<int, int> SyncSuccess = new();
            internal readonly HashSet<int> SyncDone = new();
            internal readonly Dictionary<int, int> LastState = new();

            // A tomato's state, by instance: its puzzle's name and location (the check name).
            internal readonly Dictionary<int, KeyValuePair<string, string>> Tomatoes = new();

            // A tomato that ran out, by instance: when to look whether its check went after all.
            internal readonly Dictionary<int, float> TomatoesOut = new();
            internal float ReadyAt;

            // One line per thing watched, for BepInEx/puzzle-failures.tsv.
            internal readonly List<string> Rows = new();
        }

        private static World _world;
        private static readonly Dictionary<int, float> LastFired = new();

        private static World Current
        {
            get
            {
                if (_world == null && WorldManager.isReadyForEffects)
                    _world = Build();
                return _world;
            }
        }

        // From the PeckSwitch.Peck patches; `byNobody` when the context names no player and no action.
        internal static void OnPeck(PeckSwitch peckSwitch, bool byNobody)
        {
            if (peckSwitch == null || !NetworkServer.active)
                return;

            var world = Current;
            if (world == null)
                return;

            var id = peckSwitch.GetInstanceID();
            if (world.SyncSuccess.TryGetValue(id, out var controller))
            {
                if (world.SyncDone.Add(controller))
                    Plugin.Log.LogInfo($"{Tag} Done: {world.SyncControllers[controller]}; its buttons no longer count.");
                return;
            }

            if (!world.FailureSwitches.TryGetValue(id, out var puzzle))
                return;

            if (byNobody && world.ResetBySetOff.Contains(id))
            {
                Plugin.Log.LogInfo($"{Tag} Reset, not failed: {puzzle} went inactive (no player pressed anything).");
                return;
            }

            // Pointer's Paradise (2026-11-01): the door's peck carries the closer's context.
            if (world.ResetBySetOff.Contains(id))
            {
                if (world.OffFrame.TryGetValue(id, out var off) && Time.frameCount - off <= ResetFrames)
                {
                    Plugin.Log.LogInfo($"{Tag} Reset, not failed: {puzzle} had just gone inactive.");
                    return;
                }

                world.PendingFrame[id] = Time.frameCount;
                return;
            }

            Fail(id, puzzle, world);
        }

        // From the TrackedPeckState.SetState patch (Patches/WorldButtonPatch), every state change.
        internal static void OnState(TrackedPeckState state, int value)
        {
            if (state == null || !NetworkServer.active)
                return;

            var world = Current;
            if (world == null)
                return;

            var id = state.GetInstanceID();
            if (world.ActiveOf.TryGetValue(id, out var failureSwitch))
            {
                if (value == 0)
                    world.OffFrame[failureSwitch] = Time.frameCount;
                if (value == 0 && world.PendingFrame.TryGetValue(failureSwitch, out var frame) && Time.frameCount - frame <= ResetFrames)
                {
                    world.PendingFrame.Remove(failureSwitch);
                    Plugin.Log.LogInfo($"{Tag} Reset, not failed: {world.FailureSwitches[failureSwitch]} was closed by a player.");
                }
                return;
            }
            if (world.Screens.TryGetValue(id, out var puzzle))
            {
                if (value == ScreenFailure)
                    Fail(id, puzzle, world);
                return;
            }

            if (world.Tomatoes.TryGetValue(id, out var tomato))
            {
                var started = world.LastState.TryGetValue(id, out var tomatoWas) && tomatoWas != 0;
                world.LastState[id] = value;
                if (started && value == 0 && !Solved(tomato.Value))
                    world.TomatoesOut[id] = Time.unscaledTime + TomatoGraceSeconds;
                else if (value != 0)
                    world.TomatoesOut.Remove(id);
                return;
            }

            if (!world.SyncButtons.TryGetValue(id, out var controller))
                return;

            var before = world.LastState.TryGetValue(id, out var was) ? was : 0;
            world.LastState[id] = value;
            if (before != 0 && value == 0 && !world.SyncDone.Contains(controller))
                Fail(id, world.SyncControllers[controller] + " (a synchronised button let go)", world);
        }

        // Every frame (ApRuntime): a wrong press waits a few frames for a door.
        internal static void TickPresses()
        {
            var world = _world;
            if (world == null || world.PendingFrame.Count == 0)
                return;

            foreach (var id in new List<int>(world.PendingFrame.Keys))
            {
                if (Time.frameCount - world.PendingFrame[id] <= ResetFrames)
                    continue;

                world.PendingFrame.Remove(id);
                Fail(id, world.FailureSwitches[id], world);
            }
        }

        // Every couple of seconds: a tomato that ran out fails if still unsolved after its grace.
        internal static void Tick()
        {
            var world = _world;
            if (world == null || world.TomatoesOut.Count == 0 || !NetworkServer.active)
                return;

            foreach (var id in new List<int>(world.TomatoesOut.Keys))
            {
                if (Time.unscaledTime < world.TomatoesOut[id])
                    continue;

                world.TomatoesOut.Remove(id);
                var tomato = world.Tomatoes[id];
                if (Solved(tomato.Value))
                    Plugin.Log.LogInfo($"{Tag} Solved in time: {tomato.Key} (its check went after the tomato ran out).");
                else
                    Fail(id, tomato.Key + " (the timed tomato ran out)", world);
            }
        }

        // A Gauntlet stage may be no location of this seed, so its chamber is read from the save.
        private static bool Solved(string location)
        {
            if (location.StartsWith(ChamberPrefix, StringComparison.Ordinal)
                && int.TryParse(location.Substring(ChamberPrefix.Length), out var level))
                return GauntletStairways.IsStageSolved(level);

            return !string.IsNullOrEmpty(location) && CheckTracker.IsReported(location);
        }

        private static void Fail(int id, string puzzle, World world)
        {
            if (Time.unscaledTime < world.ReadyAt)
                return;
            if (world.StageOf.TryGetValue(id, out var stage) && GauntletStairways.IsStageSolved(stage))
            {
                Plugin.Log.LogInfo($"{Tag} Not failed: {puzzle}, its Gauntlet stage is solved already.");
                return;
            }
            if (LastFired.TryGetValue(id, out var at) && Time.unscaledTime - at < Cooldown)
                return;
            LastFired[id] = Time.unscaledTime;

            Plugin.Log.LogInfo($"{Tag} Failed: {puzzle}.");
            SessionJournal.Write("puzzle failed", puzzle);
            Traps.Trigger("puzzle_failed", Describe(puzzle));
        }

        // How a failure reads for the other games (player, 2026-10-06): the slot's name goes in
        // front (ApRuntime.SendDeathLink), then the puzzle by its location name as the server
        // knows it, and how it was failed. `puzzle` is "gourdX (Kind)" or "gourdX (what happened)".
        private static readonly Dictionary<string, string> Hows = new()
        {
            { "PressInOrder", "buttons pressed out of order" },
            { "CountingController", "a wrong count" },
            { "ValidatorDisplay", "the validator screen went red" },
        };

        internal static string Describe(string puzzle)
        {
            var space = puzzle.IndexOf(' ');
            var key = space > 0 ? puzzle.Substring(0, space) : puzzle;
            var open = puzzle.LastIndexOf('(');
            var how = open > 0 ? puzzle.Substring(open + 1).TrimEnd(')') : null;
            if (how != null && Hows.TryGetValue(how, out var said))
                how = said;

            var name = key;
            if (Core.Net.ApLocationIds.TryResolveLocation(key, out var locationId))
                name = Core.Net.ApRuntime.LocationName(locationId);
            // The server has no name for a stage that is no location of this seed.
            if (key.StartsWith(ChamberPrefix, StringComparison.Ordinal) && (name == key || name == locationId.ToString())
                && int.TryParse(key.Substring(ChamberPrefix.Length), out var level))
                name = $"Silent Gauntlet's stage {level + 1}";

            return how == null ? $"failed the {name}" : $"failed the {name} ({how})";
        }

        private static bool InDispenser(Transform transform)
        {
            for (var t = transform; t != null; t = t.parent)
            {
                if (t.name.StartsWith("PomodoroDispenser", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        internal static void ForgetWorld()
        {
            _world = null;
            LastFired.Clear();
        }

        private static World Build()
        {
            var world = new World { ReadyAt = Time.unscaledTime + WorldSettleSeconds };
            try
            {
                // Each puzzle by its gourd: its name, its check, where it is.
                var gourds = new List<(string name, string location, Vector3 at)>();
                foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var prop = gourd != null ? gourd.prop : null;
                    if (prop != null && GourdRegistry.TryGetLocationId(prop.saveablePropName, out var location))
                        gourds.Add((prop.saveablePropName.ToString(), location, prop.transform.position));
                }

                (string name, string location) PuzzleOf(Transform at, int id)
                {
                    if (GauntletStairways.TryStageOf(at, out var stage))
                    {
                        world.StageOf[id] = stage;
                        var chamber = ChamberPrefix + stage;
                        return (chamber, chamber);
                    }

                    return Nearest(at.position);
                }

                (string name, string location) Nearest(Vector3 at)
                {
                    var nearest = ("a puzzle", string.Empty);
                    var best = float.MaxValue;
                    foreach (var gourd in gourds)
                    {
                        var distance = Vector3.Distance(gourd.at, at);
                        if (distance < best)
                        {
                            best = distance;
                            nearest = (gourd.name, gourd.location);
                        }
                    }

                    return nearest;
                }

                // `id` is the one Fail will get, so a solved stage is let off.
                string Name(Component owner, int id)
                {
                    var puzzle = PuzzleOf(owner.transform, id).name;
                    world.Rows.Add($"{owner.GetIl2CppType().Name}	{puzzle}	{PathOf(owner.transform)}");
                    return $"{puzzle} ({owner.GetIl2CppType().Name})";
                }

                foreach (var screen in UnityEngine.Object.FindObjectsByType<ValidatorDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var state = screen != null ? screen.peckSystemReference.peckSystem : null;
                    if (state != null)
                        world.Screens[state.GetInstanceID()] = Name(screen, state.GetInstanceID());
                }

                foreach (var p in UnityEngine.Object.FindObjectsByType<PressInOrder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (p != null && p.failureSwitch != null)
                    {
                        world.FailureSwitches[p.failureSwitch.GetInstanceID()] = Name(p, p.failureSwitch.GetInstanceID());
                        world.ResetBySetOff.Add(p.failureSwitch.GetInstanceID());
                        if (p.isActiveSystem.peckSystem != null)
                            world.ActiveOf[p.isActiveSystem.peckSystem.GetInstanceID()] = p.failureSwitch.GetInstanceID();
                    }
                }

                foreach (var c in UnityEngine.Object.FindObjectsByType<CountingController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (c != null && c.failureSwitch != null)
                        world.FailureSwitches[c.failureSwitch.GetInstanceID()] = Name(c, c.failureSwitch.GetInstanceID());
                }

                foreach (var sim in UnityEngine.Object.FindObjectsByType<SimPressController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (sim == null || sim.switches == null)
                        continue;

                    var controller = sim.GetInstanceID();
                    world.SyncControllers[controller] = Name(sim, controller);
                    if (sim.onSucess != null)
                        world.SyncSuccess[sim.onSucess.GetInstanceID()] = controller;
                    foreach (var button in sim.switches)
                    {
                        var state = button != null ? button.stateSystem : null;
                        if (state != null)
                        {
                            world.SyncButtons[state.GetInstanceID()] = controller;
                            if (world.StageOf.TryGetValue(controller, out var stage))
                                world.StageOf[state.GetInstanceID()] = stage;
                        }
                    }
                }

                // The tomatoes: the prop's own state, where its dispenser stands.
                foreach (var state in UnityEngine.Object.FindObjectsByType<TrackedPeckState>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (state == null || state.gameObject.name != "Pomodoro" || !InDispenser(state.transform))
                        continue;

                    var puzzle = PuzzleOf(state.transform, state.GetInstanceID());
                    world.Tomatoes[state.GetInstanceID()] = new KeyValuePair<string, string>(puzzle.name, puzzle.location);
                    world.Rows.Add($"Pomodoro	{puzzle.name}	{PathOf(state.transform)}");
                }

                Plugin.Log.LogInfo(
                    $"{Tag} Watching {world.Screens.Count} validator screen(s), {world.FailureSwitches.Count} failure switch(es), "
                    + $"{world.SyncButtons.Count} synchronised button(s) of {world.SyncControllers.Count} puzzle(s), "
                    + $"{world.Tomatoes.Count} timed tomato(es).");

                var rows = new List<string>(world.Rows);
                rows.Sort((a, b) => string.CompareOrdinal(a.Split('	')[1], b.Split('	')[1]));
                rows.Insert(0, "kind	nearest puzzle	path");
                System.IO.File.WriteAllLines(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "puzzle-failures.tsv"), rows);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not list what a failure looks like: {ex.Message}");
            }

            return world;
        }
    }
}
