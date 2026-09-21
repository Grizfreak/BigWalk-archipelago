using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Everything the radio feature rests on, in one dump.
    //
    // It was written to settle one question the binary could not answer,
    // because the answer lives in serialized scene data: is
    // `FmRadioManager.stationTrackGroups` ordered like the FmStation* block of
    // SavableSystem? **It is not** — settled in game on 2026-09-21, six of the
    // seven disagree. The enum-order fallback was removed and replaced by a
    // dial position learned from the world and kept in the save.
    //
    // What it is for now: checking that learning. One line per dial slot with
    // the station this save has learned owns it, one line per BroadcastStation
    // found, and the ledger. A slot reading `<not learned>` while its tower is
    // loaded means LearnDialPositions is not doing its job.
    internal static class DebugRadioLookup
    {
        internal static void Dump()
        {
            Plugin.Log.LogInfo($"[{nameof(DebugRadioLookup)}] === radio dump ===");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugRadioLookup)}] Archipelago items in play: {RadioStations.ItemsInPlay}");

            DumpDial();
            DumpBroadcastStations();
            DumpLedger();

            Plugin.Log.LogInfo($"[{nameof(DebugRadioLookup)}] === end of radio dump ===");
        }

        private static void DumpDial()
        {
            // Through RadioStations, never the property directly: it throws
            // rather than returning null when there is no instance, and a
            // diagnostic tool that takes the game down with it is worse than
            // no tool at all.
            if (!RadioStations.TryGetManager(out var manager))
            {
                Plugin.Log.LogInfo($"[{nameof(DebugRadioLookup)}] No FmRadioManager in this world.");
                return;
            }

            var groups = manager.stationTrackGroups;
            if (groups == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugRadioLookup)}] FmRadioManager has no stationTrackGroups.");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugRadioLookup)}] stationTrackGroups: {groups.Length} entries.");
            for (var i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                var groupName = group != null ? group.name : "<null>";

                // Which station this save has learned sits here, if any. The
                // enum order is deliberately NOT shown any more: it was wrong
                // for six of the seven, and printing it next to the truth only
                // invited the comparison to be made again.
                var owner = "<not learned>";
                foreach (var system in RadioStations.All)
                {
                    if (RadioStations.LearnedDialPosition(system) == i)
                    {
                        owner = system.ToString();
                        break;
                    }
                }

                var unlocked = FmRadioManager.GetUnlockState(i, out var transitioning, out var transitionTime);
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugRadioLookup)}]   [{i}] '{groupName}' | learned owner {owner} "
                    + $"| unlocked={unlocked} transitioning={transitioning} ({transitionTime:0.00}s)");
            }
        }

        private static void DumpBroadcastStations()
        {
            var stations = UnityEngine.Object.FindObjectsByType<BroadcastStation>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Said explicitly, including when it is zero: a tool that prints
            // nothing when it finds nothing is indistinguishable from a tool
            // that did not run (see the lessons in NEXT-SESSION.md).
            Plugin.Log.LogInfo(
                $"[{nameof(DebugRadioLookup)}] BroadcastStation instances loaded: {stations.Length}"
                + (stations.Length == 0 ? " (the towers stream in with the world; walk nearer one)" : string.Empty));

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            Vector3? playerPosition = localPlayer != null ? localPlayer.transform.position : (Vector3?)null;
            if (!playerPosition.HasValue)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugRadioLookup)}] NO LOCAL PLAYER FOUND — the distances below are meaningless.");
            }

            foreach (var station in stations)
            {
                if (station == null)
                    continue;

                // Distance, because presence says nothing: every station is
                // loaded everywhere, so two dumps taken in two different zones
                // came back identical (2026-09-21). Which station stands in a
                // gated zone is a question about where it is.
                var distance = playerPosition.HasValue
                    ? Vector3.Distance(station.transform.position, playerPosition.Value)
                    : -1f;

                var groupName = station.musicGroup != null ? station.musicGroup.name : "<null>";
                var peckSystem = station.peckSystemReference.peckSystem;
                var systemName = peckSystem != null ? peckSystem.savableSystem.ToString() : "<no peck system>";
                var known = RadioStations.TryGetSystem(station, out var system);
                var dialIndex = known ? RadioStations.LearnedDialPosition(system) : -1;
                var saved = known ? SaveManager.GetIntValue(system.ToString(), 0, false) : 0;

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugRadioLookup)}]   {distance.ToString("0.0").PadLeft(7)}m  musicGroup '{groupName}' "
                    + $"| savableSystem {systemName} | known={known} learnedDialPos={dialIndex} "
                    + $"| SaveManager value={saved} | granted={(known && RadioStations.IsGranted(system))}");
            }
        }

        private static void DumpLedger()
        {
            foreach (var system in RadioStations.All)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugRadioLookup)}]   ledger {system}: granted={RadioStations.IsGranted(system)} "
                    + $"| game value={SaveManager.GetIntValue(system.ToString(), 0, false)}");
            }
        }
    }
}
