using System;
using System.Collections.Generic;

namespace BigWalkArchipelago.Core.Net
{
    // Translation between the game's own identifiers and Archipelago's
    // numeric ids. The apworld derives its ids from the same game enums on
    // purpose (see apworld/protocol.md §3), precisely so this side can do
    // arithmetic instead of shipping a copy of the Python tables that would
    // silently drift the first time either side changed.
    //
    // The offsets are not hardcoded as the only source: they arrive in
    // slot_data on every connection (Configure below), with the constants
    // here as the values to fall back on. That way a future apworld can move
    // its id block without this file needing a matching release.
    internal static class ApLocationIds
    {
        internal const long DefaultBase = 8_600_000;
        internal const long DefaultRadioOffset = 1_000;
        internal const long DefaultDepositOffset = 2_000;
        internal const long DefaultGourdItemId = DefaultBase + 1;

        private static long _base = DefaultBase;
        private static long _radioOffset = DefaultRadioOffset;
        private static long _depositOffset = DefaultDepositOffset;
        private static long _gourdItemId = DefaultGourdItemId;

        // The seven radio stations that exist in the real game. SavableSystem
        // also declares FmStation7/8/9, but only seven were ever observed
        // being written together by the game's own dev helper and only these
        // seven have real names — the apworld leaves the other three out, so
        // reporting them here would just produce ids nothing listens for.
        private static readonly HashSet<SavableSystem> RadioStations = new()
        {
            SavableSystem.FmStationSleuthFm,
            SavableSystem.FmStationKosmische,
            SavableSystem.FmStationDanceFm,
            SavableSystem.FmStationBreathwork,
            SavableSystem.FmStationJourneyBeat,
            SavableSystem.FmStationAFJ,
            SavableSystem.FmStationFourthSpace,
        };

        internal static long GourdItemId => _gourdItemId;

        internal static void Configure(ApSlotData slotData)
        {
            if (slotData == null)
                return;

            _base = slotData.LocationIdBase;
            _radioOffset = slotData.RadioIdOffset;
            _depositOffset = slotData.DepositIdOffset;
            _gourdItemId = slotData.GourdItemId;
        }

        // Resolves what ICheckReporter.ReportCheck already receives: the
        // game's own enum name, as written into SaveManager. Both detection
        // paths funnel through here — SaveablePropName for puzzles and big
        // keys (GourdStatePatch/SaveValuePatch), SavableSystem for radio
        // stations (SaveValuePatch).
        internal static bool TryResolveLocation(string locationName, out long locationId)
        {
            if (Enum.TryParse<SaveablePropName>(locationName, out var propName)
                && propName != SaveablePropName.notSavable)
            {
                locationId = _base + (long)propName;
                return true;
            }

            if (Enum.TryParse<SavableSystem>(locationName, out var system)
                && RadioStations.Contains(system))
            {
                locationId = _base + _radioOffset + (long)system;
                return true;
            }

            locationId = 0;
            return false;
        }

        internal static long DepositLocationId(int depositedCount)
        {
            return _base + _depositOffset + depositedCount;
        }

        // Received big keys are 1:1 with a SaveablePropName, so the item id
        // carries the enum value directly and this is just the inverse of
        // ApLocationIds' own arithmetic. Anything that does not land on a
        // real big key is not ours to apply (filler, traps, another game's
        // item echoed back) and the caller ignores it.
        internal static bool TryResolveBigKeyItem(long itemId, out SaveablePropName propName)
        {
            var value = itemId - _base;
            propName = SaveablePropName.notSavable;

            if (value < 0 || value > int.MaxValue)
                return false;

            if (!Enum.IsDefined(typeof(SaveablePropName), (int)value))
                return false;

            var candidate = (SaveablePropName)(int)value;
            if (!GourdRegistry.IsBigKey(candidate))
                return false;

            propName = candidate;
            return true;
        }
    }
}
