using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using UnityEngine;

namespace BigWalkArchipelago
{
    // Single entry point to disable the Debug/ module without touching
    // anything else: ModConfig.DebugModeEnabled.Value == false is enough
    // (see Plugin.Load()). Named "ModConfig" (not "Config") to avoid a
    // conflict with the BasePlugin.Config property inherited by Plugin: a
    // plain "Config.Xxx" in Plugin.cs would have resolved to the inherited
    // instance, not this static class.
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> DebugModeEnabled;
        internal static ConfigEntry<bool> ArchipelagoEnabled;
        internal static ConfigEntry<string> ArchipelagoHostPort;
        internal static ConfigEntry<string> CosmeticGourdColor;
        internal static ConfigEntry<float> CosmeticGourdRestoreInterval;
        internal static ConfigEntry<bool> ShowConnectionStatus;
        internal static ConfigEntry<bool> SpawnGourdAtPlayer;
        internal static ConfigEntry<bool> PutGourdInHands;
        internal static ConfigEntry<KeyboardShortcut> ToggleFlightKey;
        internal static ConfigEntry<KeyboardShortcut> UnlockNextKey;
        internal static ConfigEntry<KeyboardShortcut> SimulateReceivedItemKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyKey;
        internal static ConfigEntry<KeyboardShortcut> DumpMonumentHomesKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyCombinatorKey;
        internal static ConfigEntry<KeyboardShortcut> DumpAllSaveEntriesKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyKeywordKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyPeckDevHelperKey;
        internal static ConfigEntry<KeyboardShortcut> TriggerUnlocksKey;
        internal static ConfigEntry<KeyboardShortcut> TriggerGourdKey;
        internal static ConfigEntry<KeyboardShortcut> DumpSpawnHubGateKey;
        internal static ConfigEntry<KeyboardShortcut> DumpPeckSwitchTargetKey;
        internal static ConfigEntry<KeyboardShortcut> RevealVariantGourdsKey;
        internal static ConfigEntry<KeyboardShortcut> FireRemotePeckSwitchKey;
        internal static ConfigEntry<string> RemotePeckSwitchName;
        internal static ConfigEntry<float> FlightSpeedMultiplier;
        internal static ConfigEntry<KeyboardShortcut> ForceNearbyCombinatorKey;
        internal static ConfigEntry<KeyboardShortcut> ForceEndingFlagsKey;
        internal static ConfigEntry<KeyboardShortcut> ApplyBigKeyOverflowKey;
        internal static ConfigEntry<KeyboardShortcut> SpawnCosmeticPickupKey;
        internal static ConfigEntry<KeyboardShortcut> ToggleNeverCullKey;
        internal static ConfigEntry<KeyboardShortcut> ForceCosmeticPinKey;
        internal static ConfigEntry<KeyboardShortcut> DumpHostMenuConfirmKey;

        internal static void Bind(ConfigFile file)
        {
            ArchipelagoEnabled = file.Bind(
                "Archipelago",
                "Enabled",
                true,
                "Connects to an Archipelago server when hosting a game. Disable to play with the mod's other features (hub shortcuts, purple gourds on the map, cosmetic spawns) while checks are only logged locally, as they were before the network client existed.");

            ArchipelagoHostPort = file.Bind(
                "Archipelago",
                "HostPort",
                "archipelago.gg:",
                "Last value entered in the host:port field of the hosting screen (Patches/HostMenuConfirmPatch.cs) — persisted so it doesn't need to be retyped on every launch.");

            CosmeticGourdColor = file.Bind(
                "Archipelago",
                "GourdColor",
                "#FFA62B",
                "Colour of the gourds received from Archipelago, as an HTML hex string (e.g. #FFA62B). Applied through the game's own variant-challenge colouring, so they stand out from the gourds sitting in puzzles. Leave empty to keep whatever the cloned template looked like.");

            CosmeticGourdRestoreInterval = file.Bind(
                "Archipelago",
                "GourdRestoreInterval",
                0.5f,
                "Seconds between each gourd when a session start restores several at once. They are dropped one at a time on the game's own spawn point and left to settle, which is both what keeps them from ending up outside the playable area and what stops a heap of them grinding against each other. Lower is faster but piles them up harder.");

            SpawnGourdAtPlayer = file.Bind(
                "Archipelago",
                "SpawnGourdAtPlayer",
                true,
                "Drops a received gourd just in front of you instead of at the hub, so you do not have to walk back across the island for it. Falls back to the hub whenever there is no player in the world yet (loading, menus). Turn off to always use the hub, the original behaviour.");

            PutGourdInHands = file.Bind(
                "Archipelago",
                "PutGourdInHands",
                true,
                "Puts a received gourd straight into your hands when they are free and the game considers the prop safe to pick up; otherwise it just drops in front of you. Note that a gourd received inside a sealed puzzle room stays there until the next world load, hands or not — the game does not let you carry it out.");

            ShowConnectionStatus = file.Bind(
                "Archipelago",
                "ShowConnectionStatus",
                true,
                "Shows a line in the corner of the screen when Archipelago is not connected, and briefly when it connects. Only ever appears for the host, since nobody else connects. Turn off for a clean screen — the same information stays in the BepInEx log either way.");

            DebugModeEnabled = file.Bind(
                "Debug",
                "Enabled",
                false,
                "Enables the debug tools (free camera, etc). Leave disabled for normal use.");

            ToggleFlightKey = file.Bind(
                "Debug",
                "ToggleFlightKey",
                new KeyboardShortcut(KeyCode.F2),
                "Key to toggle the free camera on/off (only has an effect if Debug.Enabled is active).");

            UnlockNextKey = file.Bind(
                "Debug",
                "UnlockNextKey",
                new KeyboardShortcut(KeyCode.F3),
                "Force-unlocks the next locked gourd/big key in the current zone, without solving the puzzle (only has an effect if Debug.Enabled is active).");

            SimulateReceivedItemKey = file.Bind(
                "Debug",
                "SimulateReceivedItemKey",
                new KeyboardShortcut(KeyCode.F4),
                "Simulates the remote receipt of an Archipelago item for the next locked gourd/big key in the current zone (only has an effect if Debug.Enabled is active).");

            DumpNearbyKey = file.Bind(
                "Debug",
                "DumpNearbyKey",
                new KeyboardShortcut(KeyCode.F5),
                "Logs the RewardGourd instances nearest to the player (name + state + distance), regardless of their state — diagnostic to see what's actually present around you (only has an effect if Debug.Enabled is active).");

            DumpMonumentHomesKey = file.Bind(
                "Debug",
                "DumpMonumentHomesKey",
                new KeyboardShortcut(KeyCode.F6),
                "Logs all the 'monoument*' PropHome instances actually registered in the current zone, with their fill state (only has an effect if Debug.Enabled is active).");

            DumpNearbyCombinatorKey = file.Bind(
                "Debug",
                "DumpNearbyCombinatorKey",
                new KeyboardShortcut(KeyCode.F7),
                "Logs nearby PeckCombinator instances (directControlSystem, onConditionMet/onConditionStop, rules with systems[]/minimumMatches) — diagnostic for combined-condition mechanisms (e.g. two buttons pressed simultaneously) (only has an effect if Debug.Enabled is active).");

            DumpAllSaveEntriesKey = file.Bind(
                "Debug",
                "DumpAllSaveEntriesKey",
                new KeyboardShortcut(KeyCode.F8),
                "Logs all int entries of SaveManager.currentData.entries — to diff before/after an in-game action and find the real key without guessing via the Peck graph (only has an effect if Debug.Enabled is active).");

            DumpNearbyKeywordKey = file.Bind(
                "Debug",
                "DumpNearbyKeywordKey",
                new KeyboardShortcut(KeyCode.F9),
                "Logs nearby GameObjects (name or component containing 'arch'/'door'/'switch'/'button'/'gate'/'peck'/'shortcut') along with their components — generic diagnostic to identify a mechanism with no known dedicated C# class (only has an effect if Debug.Enabled is active).");

            DumpNearbyPeckDevHelperKey = file.Bind(
                "Debug",
                "DumpNearbyPeckDevHelperKey",
                new KeyboardShortcut(KeyCode.F10),
                "Logs nearby PeckDevHelper instances along with their assigned UnlockRules (unlocks/lights/chairlift/train/bell/tunnel/map/gourd) — diagnostic for figuring out which rule triggers which cheat switch (only has an effect if Debug.Enabled is active).");

            TriggerUnlocksKey = file.Bind(
                "Debug",
                "TriggerUnlocksKey",
                new KeyboardShortcut(KeyCode.F11),
                "Calls PeckDevHelper.Trigger with UnlockRules.unlocks=true (cheat already built into the game) — test for the Arch doors/HubGate (only has an effect if Debug.Enabled is active).");

            TriggerGourdKey = file.Bind(
                "Debug",
                "TriggerGourdKey",
                new KeyboardShortcut(KeyCode.F12),
                "Calls PeckDevHelper.Trigger with UnlockRules.gourd=true (cheat already built into the game) — test for the purple gourds/variant challenges, normally unlocked after a first ending (only has an effect if Debug.Enabled is active).");

            DumpSpawnHubGateKey = file.Bind(
                "Debug",
                "DumpSpawnHubGateKey",
                new KeyboardShortcut(KeyCode.Insert),
                "Logs all TrackedPeckState instances of category SpawnHubGate (the 3 Arch doors) with their SaveIdentity.saveGuid and current SaveManager value — diagnostic to target these 3 doors directly without going through PeckDevHelper.Trigger (only has an effect if Debug.Enabled is active).");

            DumpPeckSwitchTargetKey = file.Bind(
                "Debug",
                "DumpPeckSwitchTargetKey",
                new KeyboardShortcut(KeyCode.Delete),
                "Logs all nearby PeckSwitch instances with their actual trackedStateSystem (the real TrackedPeckState modified on peck, not a guess) and the corresponding SaveManager key (only has an effect if Debug.Enabled is active).");

            RevealVariantGourdsKey = file.Bind(
                "Debug",
                "RevealVariantGourdsKey",
                new KeyboardShortcut(KeyCode.Home),
                "Reveals on the map all 'variant challenge' gourds (purple/postgame) in the loaded zone, via GourdMap.refreshFlag (only has an effect if Debug.Enabled is active).");

            RemotePeckSwitchName = file.Bind(
                "Debug",
                "RemotePeckSwitchName",
                "",
                "Substring (case-insensitive) of the name of the GameObject whose PeckSwitch should be triggered remotely by FireRemotePeckSwitchKey — find the name via F9/Delete beforehand. Empty = nothing gets triggered.");

            FlightSpeedMultiplier = file.Bind(
                "Debug",
                "FlightSpeedMultiplier",
                4f,
                "Multiplier applied to CameraCheatMover.movingSpeed when the free camera is enabled (ToggleFlightKey) — 1 = the game's vanilla camera-cheat speed.");

            FireRemotePeckSwitchKey = file.Bind(
                "Debug",
                "FireRemotePeckSwitchKey",
                new KeyboardShortcut(KeyCode.PageUp),
                "Remotely triggers (PeckSwitch.Peck(), without having to stand in front of it) all PeckSwitch instances in the zone whose name contains RemotePeckSwitchName — to solo-test a mechanism designed for 2 players (only has an effect if Debug.Enabled is active).");

            ForceNearbyCombinatorKey = file.Bind(
                "Debug",
                "ForceNearbyCombinatorKey",
                new KeyboardShortcut(KeyCode.End),
                "Directly forces (TrackedPeckState.SetState, no button simulation) the state of all systems[]/block.systems[] of PeckCombinator instances within 30m to their desiredState — to solo-satisfy an 'N simultaneous players' condition (e.g. N-hold bells) without depending on the press/hold system (only has an effect if Debug.Enabled is active).");

            ForceEndingFlagsKey = file.Bind(
                "Debug",
                "ForceEndingFlagsKey",
                new KeyboardShortcut(KeyCode.PageDown),
                "Remotely forces the 7 big keys (via ItemApplier) + SaveManager[EndingGate]=1 + SaveManager[GauntletComplete]=1 all at once — to test whether the hub's black sphere reacts to this flag combination without having to redo everything for real. Use on a save that has never seen the ending screen (only has an effect if Debug.Enabled is active).");

            ApplyBigKeyOverflowKey = file.Bind(
                "Debug",
                "ApplyBigKeyOverflowKey",
                new KeyboardShortcut(KeyCode.O),
                "Applies only bigKeyOverflow (via ItemApplier.ApplyBigKeyItem, so a real live pin on bigKeyPlinthGoodbye2) — isolated test (a single variable, unlike ForceEndingFlagsKey) of the 2026-09-11 hypothesis: the bigKeyPlinthGoodbye2 plinth sits just behind the hub's black sphere, possibly the real trigger for OpenSystem/PropHomeBlock rather than some notion of 'game already finished' (only has an effect if Debug.Enabled is active).");

            SpawnCosmeticPickupKey = file.Bind(
                "Debug",
                "SpawnCosmeticPickupKey",
                new KeyboardShortcut(KeyCode.P),
                "Triggers ReceivedItemSpawner.SpawnCosmeticPickup() directly (without going through a real item), to test in isolation the cloning/network spawn of the cosmetic gourd at the hub (only has an effect if Debug.Enabled is active).");

            ToggleNeverCullKey = file.Bind(
                "Debug",
                "ToggleNeverCullKey",
                new KeyboardShortcut(KeyCode.C),
                "Toggles CullingAgent.Instance.neverCull (dev cheat already built into the game) — isolated test of the 2026-09-11 hypothesis: the cosmetic gourd spawned by P stays invisible despite every Unity indicator being green (enabled/isVisible/observers), possibly due to the game's own region-based culling system, which might ignore an object never registered in any CullingRegion (only has an effect if Debug.Enabled is active).");

            ForceCosmeticPinKey = file.Bind(
                "Debug",
                "ForceCosmeticPinKey",
                new KeyboardShortcut(KeyCode.N),
                "Directly pins (Prop.ServerSetPinned, without going through manual interaction/holding) the nearest cosmetic gourd into the nearest empty PropHome (30m) — to test CosmeticMonumentFillTracker without having to redo the deposit interaction for real (only has an effect if Debug.Enabled is active).");

            DumpHostMenuConfirmKey = file.Bind(
                "Debug",
                "DumpHostMenuConfirmKey",
                new KeyboardShortcut(KeyCode.M),
                "Dumps the full hierarchy of the hosting screen (HostMenuConfirm) — press while on this screen, before coding an addition of the AP host:port field on it (only has an effect if Debug.Enabled is active).");
        }
    }
}
