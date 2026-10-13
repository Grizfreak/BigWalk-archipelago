using System;
using System.Collections.Generic;
using System.Linq;

namespace BigWalkArchipelago.Core.Net
{
    // What a seed asks for, one by one (ROADMAP G5, 0.5). The apworld's `objectives` list: every one
    // is required, in any order, and at least one of them is an ending (an ending plays once per
    // save, so the order cannot matter and each one is latched when it happens).
    //
    //   big_wall        the chapel bell broken by two players (EndingGate); an ending, the short one
    //   big_goodbye     the Gauntlet's bell (GauntletComplete); an ending
    //   big_game        the secret ending behind the Spawn Secret Door; an ending
    //   big_collection  deposit_goal_amount gourds in the towers' slots
    //
    // A seed with no list keeps the single `goal` and every line of ApRuntime that reads it:
    // Active is false and nothing here is asked.
    internal static class ApObjectives
    {
        private static readonly string[] Known =
        {
            "big_wall", "big_goodbye", "big_game", "big_collection", "big_keys", "big_radio", "big_towers",
            "big_climb", "big_fiesta", "big_lights", "big_objects", "big_body",
        };
        private static readonly string[] Endings = { "big_goodbye", "big_game", "big_wall" };

        // Set by the debug key (DebugApFeed.NextObjectives), to see the board and the overlay without a server.
        internal static string[] DebugKeys;

        internal static IReadOnlyList<string> Keys =>
            DebugKeys ?? (ApRuntime.SlotData != null ? ApRuntime.SlotData.Objectives : Array.Empty<string>());

        internal static bool Active => Keys.Count > 0;

        // Every listed objective is one this build can detect: a list with a name it does not know
        // would never be finished, and is refused the way an unknown goal is.
        internal static bool Detectable =>
            Active && Keys.All(key => Array.IndexOf(Known, key) >= 0 && (!ObjectiveSpots.IsSpot(key) || ObjectiveSpots.Known(key)));

        // The checks the keys and the stations are: counted from what this save has reported.
        // What the tracker keeps is the game's own name: a big key's prop (its placement) and a station's
        // SavableSystem.
        internal static int KeysPlaced() =>
            CheckTracker.GetReportedLocationNames().Count(name =>
                Enum.TryParse<SaveablePropName>(name, out var prop) && GourdRegistry.IsBigKey(prop));

        internal static int StationsOn() =>
            CheckTracker.GetReportedLocationNames().Count(name =>
                Enum.TryParse<SavableSystem>(name, out var system) && RadioStations.IsRealStation(system));

        // The counts the seed asks for; a made-up few when the fake list of the debug key is on and no
        // server has said anything.
        private static int GourdTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.DepositGoalAmount : DebugKeys != null ? 10 : 0;
        private static int TowersTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.TowersRequired : 0;
        private static int KeysTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.KeysRequired : DebugKeys != null ? 3 : int.MaxValue;
        private static int StationsTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.RadioStationsRequired : DebugKeys != null ? 3 : int.MaxValue;

        private static int ObjectsTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.ObjectsRequiredCount : DebugKeys != null ? 5 : 0;
        private static int BodyTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.BodyRequiredCount : DebugKeys != null ? 5 : 0;

        // Object Sanity's checks this save has sent: each object (every) or each kind (first_pickup).
        internal static int ObjectsPicked() =>
            CheckTracker.GetReportedLocationNames().Count(name =>
                name.StartsWith(ApLocationIds.ObjectPrefix, StringComparison.Ordinal)
                || name.StartsWith(ApLocationIds.ObjectKindPrefix, StringComparison.Ordinal));

        private static int LightsTarget => ApRuntime.SlotData != null ? ApRuntime.SlotData.LightsRequiredCount : DebugKeys != null ? 5 : 0;

        internal static bool IsEnding(string key) => Array.IndexOf(Endings, key) >= 0;

        internal static bool IsDone(string key)
        {
            switch (key)
            {
                case "big_wall":
                    return ApGoalFlags.IsLatched(SavableSystem.EndingGate);
                case "big_goodbye":
                    return ApGoalFlags.IsLatched(SavableSystem.GauntletComplete);
                case "big_game":
                    return ApSecondEnding.Reached;
                case "big_collection":
                    return GourdTarget > 0 && ApRuntime.DepositCount >= GourdTarget;
                case "big_towers":
                    return MonumentOrder.TowersFull(TowersTarget);
                case "big_climb":
                case "big_fiesta":
                    return ObjectiveSpots.IsDone(key);
                case "big_keys":
                    return KeysPlaced() >= KeysTarget;
                case "big_radio":
                    return StationsOn() >= StationsTarget;
                case "big_lights":
                    return LightsTarget > 0 && PackChecks.LightsOn() >= LightsTarget;
                case "big_objects":
                    return ObjectsTarget > 0 && ObjectsPicked() >= ObjectsTarget;
                case "big_body":
                    return BodyTarget > 0 && AbilityLocks.ItemsReceived >= BodyTarget;
                default:
                    return false;
            }
        }

        internal static bool AllDone => Active && Keys.All(IsDone);

        // The objectives other than this one: what an ending waits for with `lock_endings`.
        internal static bool OthersDone(string key) => Keys.Where(other => other != key).All(IsDone);

        internal static int DoneCount => Keys.Count(IsDone);

        internal static string Label(string key)
        {
            switch (key)
            {
                case "big_climb":
                    return "Big Climb (highest point)";
                case "big_fiesta":
                    return "Big Fiesta (the party)";
                case "big_towers":
                    return $"Big Towers ({MonumentOrder.FullCount()}/{TowersTarget} full)";
                case "big_wall":
                    return "Big Wall (break the bell)";
                case "big_keys":
                    return $"Big Keys ({KeysPlaced()}/{KeysTarget} placed)";
                case "big_radio":
                    return $"Big Radio ({StationsOn()}/{StationsTarget} stations)";
                case "big_lights":
                    return $"Big Lights ({PackChecks.LightsOn()}/{LightsTarget} lit)";
                case "big_objects":
                    return $"Big Objects ({ObjectsPicked()}/{ObjectsTarget} picked up)";
                case "big_body":
                    return $"Big Body ({AbilityLocks.ItemsReceived}/{BodyTarget} abilities)";
                case "big_goodbye":
                    return "Big Goodbye (the Gauntlet)";
                case "big_game":
                    return "Big Game (the secret ending)";
                case "big_collection":
                    return $"Big Collection ({ApRuntime.DepositCount}/{GourdTarget} {GourdNames.Plural})";
                default:
                    return key;
            }
        }

        // For the overlay, one line each (it splits on '\n'): "Objectives 1/3", then "[x]  Big Wall" and so on.
        internal static string Summary() =>
            $"Objectives {DoneCount}/{Keys.Count}"
            + string.Concat(Keys.Select(key => $"\n   [{(IsDone(key) ? "x" : "  ")}]  {(IsEnding(key) ? $"<b>{Label(key)}</b>" : Label(key))}"));
    }
}
