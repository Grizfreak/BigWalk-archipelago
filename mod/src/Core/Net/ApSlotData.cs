using System;
using System.Collections;
using System.Collections.Generic;

namespace BigWalkArchipelago.Core.Net
{
    // Typed view over the slot_data dictionary the server sends on every
    // connection. Field by field this mirrors apworld/protocol.md §2 —
    // change one and change the other.
    //
    // Values come out of Newtonsoft as plain CLR scalars for primitives and
    // as JArray for lists. Nothing here references Newtonsoft types: JArray
    // is read through IEnumerable and its elements through IConvertible, so
    // the mod keeps compiling whichever JSON stack the client library ships
    // with tomorrow.
    internal sealed class ApSlotData
    {
        internal string WorldVersion { get; private set; } = "unknown";
        internal string Goal { get; private set; } = "gauntlet";
        internal int DepositGoalAmount { get; private set; }
        internal bool RadioStationChecks { get; private set; } = true;

        // Defaults to FALSE where the others default to their common value,
        // and deliberately: this one decides whether the mod takes something
        // away from the player. An apworld too old to send the field is one
        // with no Broadcast items in its pool, so suppressing the radio for it
        // would make seven stations permanently unreachable.
        internal bool RadioStationItems { get; private set; }

        // FALSE by default for the same reason, and it is the more important
        // of the two. While this is on, placing a big key no longer opens its
        // door — the feature arrives as its own item instead (see
        // Core/KeyFeatures.cs). An apworld too old to send the field has no
        // feature items in its pool, so suppressing the doors for it would
        // make every one of them permanently shut: not a rough edge, an
        // unfinishable seed.
        internal bool BigKeyFeatures { get; private set; }

        // FALSE by default, for the mirror-image reason. While this is on
        // the mod holds every big key locked in its stone until the matching
        // item arrives. An apworld too old to send the field puts no keys in
        // its pool, so locking them would leave all seven unobtainable and
        // the seed unfinishable.
        internal bool BigKeyItems { get; private set; }
        internal int TotalMonumentSlots { get; private set; }
        internal int[] DepositLocationAmounts { get; private set; } = Array.Empty<int>();

        internal long LocationIdBase { get; private set; } = ApLocationIds.DefaultBase;
        internal long RadioIdOffset { get; private set; } = ApLocationIds.DefaultRadioOffset;
        internal long DepositIdOffset { get; private set; } = ApLocationIds.DefaultDepositOffset;
        internal long CutIdOffset { get; private set; } = ApLocationIds.DefaultCutOffset;
        internal long KeyItemIdOffset { get; private set; } = ApLocationIds.DefaultKeyItemOffset;
        internal long GourdItemId { get; private set; } = ApLocationIds.DefaultGourdItemId;

        internal static ApSlotData From(Dictionary<string, object> raw)
        {
            var data = new ApSlotData();
            if (raw == null)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApSlotData)}] No slot_data received; falling back to built-in defaults.");
                return data;
            }

            data.WorldVersion = GetString(raw, "world_version", data.WorldVersion);
            data.Goal = GetString(raw, "goal", data.Goal);
            data.DepositGoalAmount = GetInt(raw, "deposit_goal_amount", data.DepositGoalAmount);
            data.RadioStationChecks = GetBool(raw, "radio_station_checks", data.RadioStationChecks);
            data.RadioStationItems = GetBool(raw, "radio_station_items", data.RadioStationItems);
            data.BigKeyFeatures = GetBool(raw, "big_key_features", data.BigKeyFeatures);
            data.BigKeyItems = GetBool(raw, "big_key_items", data.BigKeyItems);
            data.TotalMonumentSlots = GetInt(raw, "total_monument_slots", data.TotalMonumentSlots);
            data.DepositLocationAmounts = GetIntArray(raw, "deposit_location_amounts");

            data.LocationIdBase = GetInt(raw, "location_id_base", (int)data.LocationIdBase);
            data.RadioIdOffset = GetInt(raw, "radio_id_offset", (int)data.RadioIdOffset);
            data.DepositIdOffset = GetInt(raw, "deposit_id_offset", (int)data.DepositIdOffset);
            data.CutIdOffset = GetInt(raw, "cut_id_offset", (int)data.CutIdOffset);
            data.KeyItemIdOffset = GetInt(raw, "key_item_id_offset", (int)data.KeyItemIdOffset);
            data.GourdItemId = GetInt(raw, "gourd_item_id", (int)data.GourdItemId);

            return data;
        }

        internal string Describe()
        {
            return $"apworld v{WorldVersion}, goal={Goal}"
                   + (Goal == "deposits" ? $" ({DepositGoalAmount} deposits)" : string.Empty)
                   + $", {TotalMonumentSlots} monument slots"
                   + $", {DepositLocationAmounts.Length} deposit check(s)"
                   + $", radio checks {(RadioStationChecks ? "on" : "off")}"
                   + $", radio items {(RadioStationItems ? "on" : "off")}"
                   + $", big key features {(BigKeyFeatures ? "on" : "off")}"
                   + $", big keys as items {(BigKeyItems ? "on" : "off")}";
        }

        private static string GetString(Dictionary<string, object> raw, string key, string fallback)
        {
            return raw.TryGetValue(key, out var value) && value != null ? value.ToString() : fallback;
        }

        private static int GetInt(Dictionary<string, object> raw, string key, int fallback)
        {
            if (!raw.TryGetValue(key, out var value) || value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApSlotData)}] slot_data['{key}'] is not a number ({ex.Message}); using {fallback}.");
                return fallback;
            }
        }

        private static bool GetBool(Dictionary<string, object> raw, string key, bool fallback)
        {
            if (!raw.TryGetValue(key, out var value) || value == null)
                return fallback;

            try
            {
                return Convert.ToBoolean(value);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApSlotData)}] slot_data['{key}'] is not a boolean ({ex.Message}); using {fallback}.");
                return fallback;
            }
        }

        private static int[] GetIntArray(Dictionary<string, object> raw, string key)
        {
            if (!raw.TryGetValue(key, out var value) || value is not IEnumerable sequence || value is string)
                return Array.Empty<int>();

            var result = new List<int>();
            foreach (var element in sequence)
            {
                try
                {
                    result.Add(Convert.ToInt32(element));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(ApSlotData)}] slot_data['{key}'] holds a non-numeric entry, skipped ({ex.Message}).");
                }
            }

            result.Sort();
            return result.ToArray();
        }
    }
}
