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

        // 0.5 (ROADMAP G5): what the seed asks for, one by one. Empty for a seed that keeps the single
        // `goal` above, which is then played as before. See ApObjectives.
        internal string[] Objectives { get; private set; } = Array.Empty<string>();
        internal bool LockEndings { get; private set; }
        internal int KeysRequired { get; private set; } = 7;

        // `tower_mode` (ROADMAP G4, 0.5): "all_open", "in_order" or "by_item"; the order the monuments open in
        // (the towers' big key props); the Progressive Monument's item id; the towers `big_towers` asks for.
        internal string TowerMode { get; private set; } = "all_open";
        internal string[] TowerOrder { get; private set; } = Array.Empty<string>();
        internal bool FirstTowerOpen { get; private set; } = true;
        internal bool EveryoneGathers { get; private set; }
        internal int GreenDomeSlots { get; private set; } = 15;
        internal long MonumentItemId { get; private set; }
        internal int TowersRequired { get; private set; } = 7;

        // Reaching a tower (ROADMAP S8, H3): the check's ids (the position in the list) and the ids hinted on arrival.
        internal string[] TowerReachedKeys { get; private set; } = Array.Empty<string>();
        internal int TowerReachedIdOffset { get; private set; } = 13_200;
        internal Dictionary<string, long[]> TowerReachHints { get; private set; } = new();
        internal int RadioStationsRequired { get; private set; } = 7;
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
        internal bool LinearArchDoors { get; private set; }

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
        internal bool CabinFeverHideTimer { get; private set; }

        // R7: the tracker board, and the world's logic it evaluates (apworld logic_export; null without the board).
        internal bool TrackerBoard { get; private set; }
        internal bool HintBoard { get; private set; } = true;
        // U2: "off", "in_logic" or "all".
        internal string MapTrackerMode { get; private set; } = "off";
        internal bool MapTracker => MapTrackerMode != "off";
        internal ApLogic Logic { get; private set; }

        // R7: this slot's items and how many of each, for the hint sign's suggestions.
        internal Dictionary<string, int> HintItems { get; private set; } = new();
        internal bool CabinFeverLongHideTimer { get; private set; }

        // FALSE and empty by default, like the others that take something
        // away: an apworld too old to send them has no need items in its
        // pool, so hiding the objects would hide them for good.
        internal bool LockPuzzleNeeds { get; private set; }
        internal string[] PuzzleNeedKeys { get; private set; } = Array.Empty<string>();
        internal long PuzzleNeedIdOffset { get; private set; } = 11_000;
        // `lock_abilities` (ROADMAP R3, 0.6): disabled, handed or ambidextrous, and every ability item key in id order.
        internal string LockAbilities { get; private set; } = "disabled";
        internal string[] AbilityKeys { get; private set; } = Array.Empty<string>();
        internal long AbilityIdOffset { get; private set; } = 13_400;
        internal bool LockRunning { get; private set; }
        // `rides_require_seat` (1.0.4): the chairlift's and the train's seats are locked with the others until Seat arrives.
        internal bool RidesRequireSeat { get; private set; }
        // L4 (0.6): the purple puzzles are shown on the map only with from_start (the default, and older seeds).
        internal string IncludeBigGamePuzzles { get; private set; } = "from_start";
        // L1 (0.6): the towers whose monument full is a check, by their big key's prop, in id order.
        internal string[] MonumentFullKeys { get; private set; } = Array.Empty<string>();
        internal int MonumentFullIdOffset { get; private set; } = 13_800;
        // `linear_towers: one_deposit_box`: the towers whose monument key is a location, and for each the gourds its
        // monument must hold for it, in the same order.
        internal string[] MonumentKeyKeys { get; private set; } = Array.Empty<string>();
        internal int[] MonumentKeyGourds { get; private set; } = Array.Empty<int>();
        internal int MonumentKeyIdOffset { get; private set; } = 13_850;
        // Light sanity (0.6): the mode, every light's key in id order, and what big_lights asks for.
        internal string LightSanity { get; private set; } = "off";
        internal string[] LightKeys { get; private set; } = Array.Empty<string>();
        internal int LightIdOffset { get; private set; } = 14_000;
        internal int LightsRequiredCount { get; private set; }
        // The lights big_lights counts: light_sanity's, or the lamps without it (counted, no check).
        internal string[] LightsCounted { get; private set; } = Array.Empty<string>();
        // Zone locks (R12, 0.7.1): the gadgets each zone item keeps off the map until it arrives, by item id.
        internal bool ZoneLocks { get; private set; }
        internal Dictionary<long, string[]> ZoneLockedGadgets { get; } = new();
        internal int ObjectsRequiredCount { get; private set; }
        // fast_golf (R6, 0.6): the golf ball no longer too heavy.
        internal bool FastGolf { get; private set; }
        internal int BodyRequiredCount { get; private set; }

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
        // `object_sanity` (ROADMAP S7, 0.6): off, first_pickup or every; the objects by guid and the kinds by the
        // game's name, each list in id order.
        internal string ObjectSanity { get; private set; } = "off";
        internal string[] ObjectGuids { get; private set; } = Array.Empty<string>();
        internal int ObjectIdOffset { get; private set; } = 13_500;
        internal string[] ObjectKinds { get; private set; } = Array.Empty<string>();
        internal int ObjectKindIdOffset { get; private set; } = 13_700;
        // The kinds `object_sanity_kinds` counts; without the field (the first 0.6 builds), every kind.
        internal string[] ObjectSanityKinds { get; private set; }
        internal bool RandomColours { get; private set; }
        internal int[] ColourPalette { get; private set; } = Array.Empty<int>();
        // R8: the drawing each basic tile shows, PegTileA1 to PegTileA9 (`peg_tiles`); empty keeps the game's.
        internal string[] PegTiles { get; private set; } = Array.Empty<string>();
        // R8 `custom_drawings`: the drawing panels' scribbles replaced, as "<tile kind>=<drawing>".
        internal string[] PegDrawings { get; private set; } = Array.Empty<string>();

        // gourd_name (0.4.0); "Gourd" on older seeds.
        internal string GourdName { get; private set; } = GourdNames.Default;

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
            data.Objectives = GetStringArray(raw, "objectives");
            data.LockEndings = GetBool(raw, "lock_endings", false);
            data.KeysRequired = GetInt(raw, "keys_required", data.KeysRequired);
            data.TowerMode = GetString(raw, "tower_mode", data.TowerMode);
            data.TowerOrder = GetStringArray(raw, "tower_order");
            data.FirstTowerOpen = GetBool(raw, "first_tower_open", true);
            data.EveryoneGathers = GetBool(raw, "everyone_gathers", false);
            data.GreenDomeSlots = GetInt(raw, "green_dome_slots", 15);
            data.MonumentItemId = GetInt(raw, "monument_item_id", 0);
            data.TowersRequired = GetInt(raw, "towers_required", data.TowersRequired);
            data.TowerReachedKeys = GetStringArray(raw, "tower_reached_keys");
            data.TowerReachedIdOffset = GetInt(raw, "tower_reached_id_offset", data.TowerReachedIdOffset);
            foreach (var pair in GetMap(raw, "tower_reach_hints"))
            {
                try
                {
                    var ids = new List<long>();
                    foreach (var id in Newtonsoft.Json.Linq.JArray.Parse(pair.Value.Text))
                        ids.Add(id.ToObject<long>());
                    data.TowerReachHints[pair.Key] = ids.ToArray();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(ApSlotData)}] slot_data['tower_reach_hints'][{pair.Key}] is unreadable: {ex.Message}");
                }
            }
            data.RadioStationsRequired = GetInt(raw, "radio_stations_required", data.RadioStationsRequired);
            data.DepositGoalAmount = GetInt(raw, "deposit_goal_amount", data.DepositGoalAmount);
            data.RadioStationChecks = GetBool(raw, "radio_station_checks", data.RadioStationChecks);
            data.RadioStationItems = GetBool(raw, "radio_station_items", data.RadioStationItems);
            data.BigKeyFeatures = GetBool(raw, "big_key_features", data.BigKeyFeatures);
            data.BigKeyItems = GetBool(raw, "big_key_items", data.BigKeyItems);
            data.TotalMonumentSlots = GetInt(raw, "total_monument_slots", data.TotalMonumentSlots);
            data.DepositLocationAmounts = GetIntArray(raw, "deposit_location_amounts");
            data.LockedArchDoors = GetStringArray(raw, "locked_arch_doors");
            data.LinearArchDoors = GetBool(raw, "linear_arch_doors", false);
            data.LockPuzzleNeeds = GetBool(raw, "lock_puzzle_needs", data.LockPuzzleNeeds);
            data.PuzzleNeedKeys = GetStringArray(raw, "puzzle_need_keys");
            data.LockAbilities = GetString(raw, "lock_abilities", data.LockAbilities);
            data.AbilityKeys = GetStringArray(raw, "ability_keys");
            data.AbilityIdOffset = GetInt(raw, "ability_id_offset", (int)data.AbilityIdOffset);
            data.LockRunning = GetBool(raw, "lock_running", data.LockRunning);
            data.RidesRequireSeat = GetBool(raw, "rides_require_seat", data.RidesRequireSeat);
            data.IncludeBigGamePuzzles = GetString(raw, "include_big_game_puzzles", data.IncludeBigGamePuzzles);
            data.MonumentFullKeys = GetStringArray(raw, "monument_full_keys");
            data.MonumentFullIdOffset = GetInt(raw, "monument_full_id_offset", data.MonumentFullIdOffset);
            data.MonumentKeyKeys = GetStringArray(raw, "monument_key_keys");
            // In the order of monument_key_keys: not sorted (a sorted list gave the Drawbridge the smallest draw, 2026-10-15).
            data.MonumentKeyGourds = GetIntArray(raw, "monument_key_gourds", sorted: false);
            data.MonumentKeyIdOffset = GetInt(raw, "monument_key_id_offset", data.MonumentKeyIdOffset);
            data.LightSanity = GetString(raw, "light_sanity", data.LightSanity);
            data.LightKeys = GetStringArray(raw, "light_keys");
            data.LightIdOffset = GetInt(raw, "light_id_offset", data.LightIdOffset);
            data.LightsRequiredCount = GetInt(raw, "lights_required_count", 0);
            data.LightsCounted = GetStringArray(raw, "lights_counted");
            data.ZoneLocks = GetBool(raw, "zone_locks", false);
            foreach (var pair in GetMap(raw, "zone_locked_gadgets"))
            {
                try
                {
                    var guids = new List<string>();
                    foreach (var guid in Newtonsoft.Json.Linq.JArray.Parse(pair.Value.Text))
                        guids.Add(guid.ToObject<string>());
                    data.ZoneLockedGadgets[long.Parse(pair.Key)] = guids.ToArray();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(ApSlotData)}] slot_data['zone_locked_gadgets'][{pair.Key}] is unreadable: {ex.Message}");
                }
            }
            data.ObjectsRequiredCount = GetInt(raw, "objects_required_count", 0);
            data.FastGolf = GetBool(raw, "fast_golf", false);
            data.BodyRequiredCount = GetInt(raw, "body_required_count", 0);
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
            data.CabinFeverHideTimer = GetBool(raw, "cabin_fever_hide_timer", false);
            data.TrackerBoard = GetBool(raw, "tracker_board", false);
            data.HintBoard = GetBool(raw, "hint_board", true);
            data.MapTrackerMode = GetString(raw, "map_tracker", "off");
            foreach (var pair in GetMap(raw, "hint_items"))
                data.HintItems[pair.Key] = pair.Value.Number;
            if ((data.TrackerBoard || data.MapTracker) && raw.TryGetValue("logic", out var logic) && logic is Newtonsoft.Json.Linq.JObject logicObject)
                data.Logic = ApLogic.From(logicObject);
            data.CabinFeverLongHideTimer = GetBool(raw, "cabin_fever_long_hide_timer", false);

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
            data.ObjectSanity = GetString(raw, "object_sanity", data.ObjectSanity);
            data.ObjectGuids = GetStringArray(raw, "object_guids");
            data.ObjectIdOffset = GetInt(raw, "object_id_offset", data.ObjectIdOffset);
            data.ObjectKinds = GetStringArray(raw, "object_kinds");
            data.ObjectKindIdOffset = GetInt(raw, "object_kind_id_offset", data.ObjectKindIdOffset);
            data.ObjectSanityKinds = raw.ContainsKey("object_sanity_kinds") ? GetStringArray(raw, "object_sanity_kinds") : null;
            // "random_colours" and "colour_palette" in the first builds of 0.4.
            data.RandomColours = GetBool(raw, "random_colors", GetBool(raw, "random_colours", false));
            // In the drawn order (the first four paint the flare guns): sorted, they always took the lowest indices.
            data.ColourPalette = raw.ContainsKey("color_palette")
                ? GetIntArray(raw, "color_palette", sorted: false)
                : GetIntArray(raw, "colour_palette", sorted: false);
            data.PegTiles = GetStringArray(raw, "peg_tiles");
            data.PegDrawings = GetStringArray(raw, "peg_drawings");
            data.GourdName = GetString(raw, "gourd_name", data.GourdName);
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
            return $"apworld v{WorldVersion}, goal={(Objectives.Length > 0 ? "[" + string.Join(", ", Objectives) + "]" : Goal)}"
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
                   + $", cabin fever {CabinFeverMode} {CabinFeverSeconds}s{(CabinFeverHelp ? " +help" : string.Empty)}{(CabinFeverHideTimer ? " +hidden" : string.Empty)}"
                   + $", cabin fever long {CabinFeverLongMode} {CabinFeverLongSeconds}s{(CabinFeverLongHelp ? " +help" : string.Empty)}{(CabinFeverLongHideTimer ? " +hidden" : string.Empty)}"
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

        private static int[] GetIntArray(Dictionary<string, object> raw, string key, bool sorted = true)
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

            if (sorted)
                result.Sort();
            return result.ToArray();
        }
    }
}
