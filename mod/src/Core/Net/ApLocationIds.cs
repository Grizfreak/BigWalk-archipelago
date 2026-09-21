using System;
using BigWalkArchipelago.Core;

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
        internal const long DefaultCutOffset = 3_000;

        // The KEY items, which are a different thing from the FEATURE items
        // even though both are one-per-tower. A feature id is `B + propName`
        // and opens a door; a key id is `B + 4000 + propName` and is the
        // physical key a player carries, cuts and places. They had to stop
        // sharing a number the moment the keys became items of their own.
        internal const long DefaultKeyItemOffset = 4_000;
        internal const long DefaultGourdItemId = DefaultBase + 1;

        private static long _base = DefaultBase;
        private static long _radioOffset = DefaultRadioOffset;
        private static long _depositOffset = DefaultDepositOffset;
        private static long _cutOffset = DefaultCutOffset;
        private static long _keyItemOffset = DefaultKeyItemOffset;
        private static long _gourdItemId = DefaultGourdItemId;

        // A cut segment has no identifier of the game's own — `cuts` is an
        // anonymous Mirror SyncList of bools — so one is made here out of the
        // two things that do identify it: the key it belongs to and its index
        // in that list. The separator is a character no C# enum name can
        // contain, which is what lets TryResolveLocation tell a cut from the
        // plain `SaveablePropName` of the key's own placement location.
        private const string CutSeparator = "#cut";

        // Room for ten segments per key, which is twice what the widest one
        // has. Spacing them by the key's own enum value keeps the arithmetic
        // to one line on the Python side too (apworld/protocol.md §3).
        private const long CutStride = 10;

        // Which SavableSystem values are real stations lives in
        // Core/RadioStations.cs, next to the code that unlocks them —
        // SavableSystem also declares FmStation7/8/9, which the apworld leaves
        // out (no names, never seen written), so an id resolving to one of
        // those would be a check nothing listens for and an item nothing can
        // grant. One list, asked twice.

        internal static long GourdItemId => _gourdItemId;

        internal static void Configure(ApSlotData slotData)
        {
            if (slotData == null)
                return;

            _base = slotData.LocationIdBase;
            _radioOffset = slotData.RadioIdOffset;
            _depositOffset = slotData.DepositIdOffset;
            _cutOffset = slotData.CutIdOffset;
            _keyItemOffset = slotData.KeyItemIdOffset;
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
                && RadioStations.IsRealStation(system))
            {
                locationId = _base + _radioOffset + (long)system;
                return true;
            }

            if (TryResolveCut(locationName, out locationId))
                return true;

            locationId = 0;
            return false;
        }

        // The name a cut segment is reported and remembered under. Kept here
        // rather than in the patch that produces it, next to the parsing that
        // has to undo it: the two are one format, and CheckTracker replays
        // these strings from the save on every connection, so a change to one
        // without the other silently loses every cut this save ever made.
        internal static string CutLocationName(SaveablePropName propName, int index)
        {
            return $"{propName}{CutSeparator}{index}";
        }

        private static bool TryResolveCut(string locationName, out long locationId)
        {
            locationId = 0;

            var separator = locationName.IndexOf(CutSeparator, StringComparison.Ordinal);
            if (separator <= 0)
                return false;

            if (!int.TryParse(locationName.Substring(separator + CutSeparator.Length), out var index)
                || index < 0 || index >= CutStride)
                return false;

            if (!Enum.TryParse<SaveablePropName>(locationName.Substring(0, separator), out var propName)
                || !GourdRegistry.IsBigKey(propName))
                return false;

            locationId = _base + _cutOffset + (long)propName * CutStride + index;
            return true;
        }

        internal static long DepositLocationId(int depositedCount)
        {
            return _base + _depositOffset + depositedCount;
        }

        // A Broadcast item carries the station's SavableSystem value in the
        // same slot its location id does — item and location ids are separate
        // namespaces in Archipelago, so the apworld reuses the number rather
        // than inventing a second rule (see apworld/protocol.md §4). Anything
        // that does not land on one of the seven real stations is not ours.
        internal static bool TryResolveRadioItem(long itemId, out SavableSystem system)
        {
            var value = itemId - _base - _radioOffset;
            system = SavableSystem.NotSavable;

            if (value < 0 || value > int.MaxValue)
                return false;

            if (!Enum.IsDefined(typeof(SavableSystem), (int)value))
                return false;

            var candidate = (SavableSystem)(int)value;
            if (!RadioStations.IsRealStation(candidate))
                return false;

            system = candidate;
            return true;
        }

        // The physical key for a tower, as opposed to the door it fits.
        // Same shape as TryResolveBigKeyItem below, one offset apart, and
        // deliberately a separate method rather than a flag: a caller that
        // confused the two would hand a door to KeyCustody or a key to
        // KeyFeatures, and both would look like nothing happening.
        internal static bool TryResolveKeyItem(long itemId, out SaveablePropName propName)
        {
            return TryResolveProp(itemId - _base - _keyItemOffset, out propName);
        }

        // Received big keys are 1:1 with a SaveablePropName, so the item id
        // carries the enum value directly and this is just the inverse of
        // ApLocationIds' own arithmetic. Anything that does not land on a
        // real big key is not ours to apply (filler, traps, another game's
        // item echoed back) and the caller ignores it.
        internal static bool TryResolveBigKeyItem(long itemId, out SaveablePropName propName)
        {
            return TryResolveProp(itemId - _base, out propName);
        }

        private static bool TryResolveProp(long value, out SaveablePropName propName)
        {
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
