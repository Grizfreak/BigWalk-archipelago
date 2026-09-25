using System;
using System.Collections.Generic;

namespace BigWalkArchipelago.Core
{
    // Deduplication shared by every code path that can detect the same
    // check. Confirmed necessary during a test session on 2026-09-03:
    // RewardGourd.ServerSetGourdState(Loose) AND
    // PeckEffectSavableHome.Peck() (on the "vice launch switch") both write
    // to SaveManager.SetIntValue for the same gourd.
    //
    // Persisted in SaveManager (not a simple in-memory HashSet): confirmed
    // in testing on 2026-09-07, an in-memory HashSet only protects WITHIN
    // the current session. On zone load, the game re-asserts the state of
    // an already-resolved gourd via (at least) two independent paths —
    // Prop.Start() re-pinning the prop, AND a repeat call to
    // RewardGourd.ServerSetGourdState(Loose) — each one re-triggering
    // GourdStatePatch/SaveValuePatch on every game restart if the
    // deduplication doesn't survive the session. The "ap_reported_" prefix
    // avoids any collision with the game's normal
    // SaveablePropName/SaveableHomeName keys.
    internal static class CheckTracker
    {
        private const string KeyPrefix = "ap_reported_";

        // NOT IN A SAVE PLAYED WITHOUT ARCHIPELAGO (2026-09-25). Loading a
        // vanilla save with the switch off wrote 60 `ap_reported_*` keys into
        // it — one per puzzle, key and radio already done — for checks that
        // were only ever logged. With the switch off nothing needs to survive
        // a restart, so deduplication stays in memory and the save is left as
        // the player had it.
        private static readonly HashSet<string> ReportedThisSessionOnly = new();

        internal static bool TryMarkReported(string locationId)
        {
            if (!ModConfig.ArchipelagoEnabled.Value)
                return ReportedThisSessionOnly.Add(locationId);

            var key = KeyPrefix + locationId;
            if (SaveManager.GetIntValue(key, 0, false) != 0)
                return false;

            SaveManager.SetIntValue(key, 1);
            return true;
        }

        // Every location this save has already reported, by the game's own
        // identifier (the same string TryMarkReported was given).
        //
        // Needed because this tracker is deliberately one-way: once a
        // location is marked, nothing reports it again, not even across
        // restarts. That is correct for the game side, but it means a check
        // validated while the Archipelago client happened to be offline
        // would be lost forever. Core/Net/ApRuntime replays this whole set
        // on every connection so the server can catch up.
        internal static IEnumerable<string> GetReportedLocationNames()
        {
            var data = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (data == null || data.entries == null)
                yield break;

            // Indexed rather than foreach: SaveData.entries is an IL2CPP
            // List<SaveEntry> of structs, and this runs while the game may
            // add entries of its own.
            var entries = data.entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.value == 0 || entry.key == null)
                    continue;

                if (entry.key.StartsWith(KeyPrefix, StringComparison.Ordinal))
                    yield return entry.key.Substring(KeyPrefix.Length);
            }
        }
    }
}
