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

        // Empty by default: an apworld too old to send it has no door items
        // in its pool, so holding a door for it would keep it shut for good.
        internal string[] LockedArchDoors { get; private set; } = Array.Empty<string>();

        internal long LocationIdBase { get; private set; } = ApLocationIds.DefaultBase;
        internal long RadioIdOffset { get; private set; } = ApLocationIds.DefaultRadioOffset;
        internal long DepositIdOffset { get; private set; } = ApLocationIds.DefaultDepositOffset;
        internal long CutIdOffset { get; private set; } = ApLocationIds.DefaultCutOffset;
        internal long KeyItemIdOffset { get; private set; } = ApLocationIds.DefaultKeyItemOffset;
        internal long GourdItemId { get; private set; } = ApLocationIds.DefaultGourdItemId;
        internal long ArchDoorIdOffset { get; private set; } = ApLocationIds.DefaultArchDoorOffset;

        // "vanilla" by default: an apworld too old to send it has no stairway
        // items in its pool, so holding the Gauntlet's stairways shut for it
        // would keep them shut for good. Only "locked_stages" is acted on.
        internal string GauntletMode { get; private set; } = "vanilla";

        // TRUE by default: the puzzle opens its wall as the game has it.
        internal bool GauntletPuzzlesRequired { get; private set; } = true;

        // FALSE by default, like every field that hides something: an apworld
        // that does not send it does not count on the Gauntlet's parts.
        internal bool LockGauntletNeeds { get; private set; }
        internal long GauntletIdOffset { get; private set; } = ApLocationIds.DefaultGauntletOffset;

        // The in-world teleport buttons: off, free, with_towers or items, the destinations in the
        // order of their Teleporter item ids, and where those ids start.
        internal string TeleportButtons { get; private set; } = "off";
        internal string[] TeleportDestinations { get; private set; } = new string[0];
        internal int TeleportIdOffset { get; private set; } = ApLocationIds.DefaultTeleportOffset;
        internal bool TeleportBackToHub { get; private set; } = true;

        // The waits of the two Cabin Fever puzzles: the mode, the seconds it resolves to (0 for the
        // game's own), and whether the hidden help button is there.
        // The Black Tower's door at its foot open from the start (false on a seed older than 0.1.3).
        internal bool OpenBlackTower { get; private set; }

        // Tile Thief's helping button: vanilla (none), easy or chaos.
        internal string TileThief { get; private set; } = "vanilla";

        // The resync stations: those of the towers, and whether guests may use them.
        internal bool TowerResyncStations { get; private set; }
        internal bool GuestsCanResync { get; private set; }

        internal string CabinFeverMode { get; private set; } = "vanilla";
        internal int CabinFeverSeconds { get; private set; }
        internal bool CabinFeverHelp { get; private set; }
        internal string CabinFeverLongMode { get; private set; } = "vanilla";
        internal int CabinFeverLongSeconds { get; private set; }
        internal bool CabinFeverLongHelp { get; private set; }

        // FALSE and empty by default, like the others that take something
        // away: an apworld too old to send them has no need items in its
        // pool, so hiding the objects would hide them for good.
        internal bool LockPuzzleNeeds { get; private set; }
        internal string[] PuzzleNeedKeys { get; private set; } = Array.Empty<string>();
        internal long PuzzleNeedIdOffset { get; private set; } = 11_000;

        // Traps and bonuses: each effect's key by its item id offset, how long one lasts, and
        // whether Big Trip and Big Meeting leave the Silent Gauntlet alone. Empty on a seed older
        // than 0.4.0, which has none of these items.
        internal Dictionary<long, string> EffectItems { get; private set; } = new();
        internal int TrapDuration { get; private set; } = 30;
        internal bool TrapsSpareTheGauntlet { get; private set; } = true;

        // DeathLink: off, send, receive or both; what sends one and after how many; what one
        // received does (drop or roulette); the traps the roulette draws from, with weights.
        internal string DeathLink { get; private set; } = "off";
        internal string[] DeathLinkTriggers { get; private set; } = Array.Empty<string>();
        internal int DeathLinkTolerance { get; private set; }
        internal bool DeathLinkTrapOnSend { get; private set; }
        internal string DeathLinkTarget { get; private set; } = "everyone";
        internal string DeathLinkEffect { get; private set; } = "drop";
        internal Dictionary<string, int> DeathLinkRoulette { get; private set; } = new();

        // The packs and fireworks as checks (0.4.0): the guids of the island's packs and the
        // landmarks of its launchers, in the order of their location ids. Off and empty on an
        // older seed, which has none of these locations.
        internal string PackSanity { get; private set; } = "off";
        internal string[] PickupGuids { get; private set; } = Array.Empty<string>();
        internal int PickupIdOffset { get; private set; } = 13_000;
        internal bool FireworkSanity { get; private set; }
        internal string[] FireworkKeys { get; private set; } = Array.Empty<string>();
        internal int FireworkIdOffset { get; private set; } = 13_100;

        // The flare guns as checks and the seed's colours (0.4.0, C1). Off and empty on an older seed.
        internal bool FlareGunSanity { get; private set; }
        internal string[] FlareGunGuids { get; private set; } = Array.Empty<string>();
        internal int FlareGunIdOffset { get; private set; } = 13_050;
        internal bool RandomColours { get; private set; }
        internal int[] ColourPalette { get; private set; } = Array.Empty<int>();

        // TrapLink (0.4.0): the traps received are sent to the other games, theirs play here.
        internal bool TrapLink { get; private set; }

        internal bool SendsDeathLink => DeathLink is "send" or "both";
        internal bool ReceivesDeathLink => DeathLink is "receive" or "both";

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
            data.LockedArchDoors = GetStringArray(raw, "locked_arch_doors");
            data.LockPuzzleNeeds = GetBool(raw, "lock_puzzle_needs", data.LockPuzzleNeeds);
            data.PuzzleNeedKeys = GetStringArray(raw, "puzzle_need_keys");
            data.GauntletMode = GetString(raw, "gauntlet_mode", data.GauntletMode);
            data.GauntletPuzzlesRequired = GetBool(raw, "gauntlet_puzzles_required", data.GauntletPuzzlesRequired);
            data.LockGauntletNeeds = GetBool(raw, "lock_gauntlet_needs", data.LockGauntletNeeds);

            data.LocationIdBase = GetInt(raw, "location_id_base", (int)data.LocationIdBase);
            data.RadioIdOffset = GetInt(raw, "radio_id_offset", (int)data.RadioIdOffset);
            data.DepositIdOffset = GetInt(raw, "deposit_id_offset", (int)data.DepositIdOffset);
            data.CutIdOffset = GetInt(raw, "cut_id_offset", (int)data.CutIdOffset);
            data.KeyItemIdOffset = GetInt(raw, "key_item_id_offset", (int)data.KeyItemIdOffset);
            data.GourdItemId = GetInt(raw, "gourd_item_id", (int)data.GourdItemId);
            data.ArchDoorIdOffset = GetInt(raw, "arch_door_id_offset", (int)data.ArchDoorIdOffset);
            data.PuzzleNeedIdOffset = GetInt(raw, "puzzle_need_id_offset", (int)data.PuzzleNeedIdOffset);
            data.GauntletIdOffset = GetInt(raw, "gauntlet_id_offset", (int)data.GauntletIdOffset);
            data.TeleportButtons = GetString(raw, "teleport_buttons", data.TeleportButtons);
            data.TeleportDestinations = GetStringArray(raw, "teleport_destinations");
            data.TeleportIdOffset = GetInt(raw, "teleport_id_offset", data.TeleportIdOffset);
            data.TeleportBackToHub = GetBool(raw, "teleport_back_to_hub", true);
            data.OpenBlackTower = GetBool(raw, "open_black_tower", false);
            data.TileThief = GetString(raw, "tile_thief", data.TileThief);
            data.TowerResyncStations = GetBool(raw, "tower_resync_stations", false);
            data.GuestsCanResync = GetBool(raw, "guests_can_resync", false);
            data.CabinFeverMode = GetString(raw, "cabin_fever_mode", data.CabinFeverMode);
            data.CabinFeverSeconds = GetInt(raw, "cabin_fever_seconds", 0);
            data.CabinFeverHelp = GetBool(raw, "cabin_fever_help", false);
            data.CabinFeverLongMode = GetString(raw, "cabin_fever_long_mode", data.CabinFeverLongMode);
            data.CabinFeverLongSeconds = GetInt(raw, "cabin_fever_long_seconds", 0);
            data.CabinFeverLongHelp = GetBool(raw, "cabin_fever_long_help", false);

            foreach (var pair in GetMap(raw, "effect_items"))
            {
                // Offset -> key, the offset written as text (a JSON key is always a string).
                if (long.TryParse(pair.Key, out var offset))
                    data.EffectItems[offset] = pair.Value.Text;
            }

            data.PackSanity = GetString(raw, "pack_sanity", "off");
            data.PickupGuids = GetStringArray(raw, "pickup_guids");
            data.PickupIdOffset = GetInt(raw, "pickup_id_offset", data.PickupIdOffset);
            data.FireworkSanity = GetBool(raw, "firework_sanity", false);
            data.FireworkKeys = GetStringArray(raw, "firework_keys");
            data.FireworkIdOffset = GetInt(raw, "firework_id_offset", data.FireworkIdOffset);
            data.FlareGunSanity = GetBool(raw, "flare_gun_sanity", false);
            data.FlareGunGuids = GetStringArray(raw, "flare_gun_guids");
            data.FlareGunIdOffset = GetInt(raw, "flare_gun_id_offset", data.FlareGunIdOffset);
            // "random_colours" and "colour_palette" in the first builds of 0.4.
            data.RandomColours = GetBool(raw, "random_colors", GetBool(raw, "random_colours", false));
            data.ColourPalette = raw.ContainsKey("color_palette") ? GetIntArray(raw, "color_palette") : GetIntArray(raw, "colour_palette");
            data.TrapLink = GetBool(raw, "trap_link", false);

            data.TrapDuration = GetInt(raw, "trap_duration", data.TrapDuration);
            data.TrapsSpareTheGauntlet = GetBool(raw, "traps_spare_the_gauntlet", data.TrapsSpareTheGauntlet);
            data.DeathLink = GetString(raw, "death_link", data.DeathLink);
            data.DeathLinkTriggers = GetStringArray(raw, "death_link_triggers");
            // "death_link_tolerance" until it took the usual name, amnesty.
            data.DeathLinkTolerance = GetInt(raw, "death_link_amnesty", GetInt(raw, "death_link_tolerance", 0));
            data.DeathLinkTrapOnSend = GetBool(raw, "death_link_trap_on_send", false);
            data.DeathLinkTarget = GetString(raw, "death_link_target", data.DeathLinkTarget);
            data.DeathLinkEffect = GetString(raw, "death_link_effect", data.DeathLinkEffect);
            foreach (var pair in GetMap(raw, "death_link_roulette"))
            {
                if (pair.Value.Number > 0)
                    data.DeathLinkRoulette[pair.Key] = pair.Value.Number;
            }

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
                   + $", big keys as items {(BigKeyItems ? "on" : "off")}"
                   + $", {LockedArchDoors.Length} arch door(s) locked"
                   + $", puzzle needs {(LockPuzzleNeeds ? "locked" : "free")}"
                   + $", gauntlet {GauntletMode}{(GauntletMode == "locked_stages" && !GauntletPuzzlesRequired ? " (puzzles optional)" : string.Empty)}"
                   + $", teleport buttons {TeleportButtons}"
                   + $", black tower {(OpenBlackTower ? "open" : "vanilla")}"
                   + $", cabin fever {CabinFeverMode} {CabinFeverSeconds}s{(CabinFeverHelp ? " +help" : string.Empty)}"
                   + $", cabin fever long {CabinFeverLongMode} {CabinFeverLongSeconds}s{(CabinFeverLongHelp ? " +help" : string.Empty)}"
                   + $", packs {PackSanity}, fireworks {(FireworkSanity ? FireworkKeys.Length.ToString() : "off")}"
                   + $", flare guns {(FlareGunSanity ? FlareGunGuids.Length.ToString() : "off")}, colours {(RandomColours ? ColourPalette.Length.ToString() : "off")}"
                   + $", {EffectItems.Count} trap/bonus item(s), {TrapDuration}s"
                   + $", deathlink {DeathLink}"
                   + (DeathLink != "off" ? $" [{string.Join(",", DeathLinkTriggers)}] tolerance {DeathLinkTolerance}, {DeathLinkEffect}" : string.Empty);
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

        private static string[] GetStringArray(Dictionary<string, object> raw, string key)
        {
            if (!raw.TryGetValue(key, out var value) || value is not IEnumerable sequence || value is string)
                return Array.Empty<string>();

            var result = new List<string>();
            foreach (var element in sequence)
            {
                if (element != null)
                    result.Add(element.ToString());
            }

            return result.ToArray();
        }

        internal struct MapValue
        {
            internal string Text;
            internal int Number;
        }

        // A JSON object of the slot data (Newtonsoft's JObject), as its keys and values: each value
        // as text, and as a number when it is one (0 otherwise).
        private static List<KeyValuePair<string, MapValue>> GetMap(Dictionary<string, object> raw, string key)
        {
            var result = new List<KeyValuePair<string, MapValue>>();
            if (!raw.TryGetValue(key, out var value) || value is not Newtonsoft.Json.Linq.JObject map)
                return result;

            foreach (var property in map.Properties())
            {
                var entry = new MapValue { Text = property.Value?.ToString() ?? string.Empty };
                if (property.Value != null && property.Value.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                    entry.Number = property.Value.ToObject<int>();
                result.Add(new KeyValuePair<string, MapValue>(property.Name, entry));
            }

            return result;
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
