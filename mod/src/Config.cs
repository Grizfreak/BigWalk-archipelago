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
        internal static ConfigEntry<KeyboardShortcut> HoldTrackedStateKey;
        internal static ConfigEntry<string> HoldTrackedStateName;
        internal static ConfigEntry<int> HoldTrackedStateValue;
        internal static ConfigEntry<bool> DebugModeEnabled;
        internal static ConfigEntry<bool> ArchipelagoEnabled;
        internal static ConfigEntry<string> ArchipelagoHostPort;
        internal static ConfigEntry<float> CosmeticGourdRestoreInterval;
        internal static ConfigEntry<bool> ShowConnectionStatus;
        internal static ConfigEntry<int> StatusFontSize;
        internal static ConfigEntry<bool> ShowItemFeed;
        internal static ConfigEntry<float> NoticeSeconds;
        internal static ConfigEntry<bool> ShowResyncHint;
        internal static ConfigEntry<bool> SpawnGourdAtPlayer;
        internal static ConfigEntry<bool> PutGourdInHands;
        internal static ConfigEntry<KeyboardShortcut> ResyncGourdsKey;
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
        internal static ConfigEntry<KeyboardShortcut> DumpRadioKey;
        internal static ConfigEntry<KeyboardShortcut> DumpKeysKey;
        internal static ConfigEntry<KeyboardShortcut> ForceBigKeyDoorKey;
        internal static ConfigEntry<KeyboardShortcut> GrantBigKeyItemKey;
        internal static ConfigEntry<KeyboardShortcut> GrantBigKeyFeatureKey;
        internal static ConfigEntry<string> BigKeyDoorName;
        internal static ConfigEntry<bool> ColorBigKeys;
        internal static ConfigEntry<string> KeyColorProperty;
        internal static ConfigEntry<KeyboardShortcut> DumpPropsKey;
        internal static ConfigEntry<KeyboardShortcut> DumpGourdRosterKey;
        internal static ConfigEntry<KeyboardShortcut> SimulateGadgetItemKey;
        internal static ConfigEntry<KeyboardShortcut> DumpHeldItemKey;
        internal static ConfigEntry<KeyboardShortcut> DumpCosmeticGadgetsKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNetworkKey;
        internal static ConfigEntry<KeyboardShortcut> LoopbackJoinKey;
        internal static ConfigEntry<KeyboardShortcut> LoopbackFocusKey;
        internal static ConfigEntry<KeyboardShortcut> LoopbackSummonKey;

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

            ColorBigKeys = file.Bind(
                "Archipelago",
                "ColorBigKeys",
                true,
                "Tints the five identical yellow big keys — the drawbridge and the four coloured towers — so they can be told apart once they start arriving from Archipelago and piling up at the spawn point. The Black Monolith and Green Dome keys are left alone: they already have models of their own, and repainting them would destroy the one distinction the game gives you for free.");

            KeyColorProperty = file.Bind(
                "Archipelago",
                "KeyColorProperty",
                "_TintColor",
                "Shader colour properties the tint above is written to, comma-separated. Measured in game on 2026-09-21: the keys run 'househouse/VertexColors', which declares _TintColor, _TintMask0 to _TintMask3, and _EmissionColor — and NOT the _RColor the Black Monolith key's own helper uses, which is why the first attempt silently painted nothing. _TintColor multiplies the mesh's baked vertex colours, which is the classic setup for a shader by that name. If a key still comes out yellow, add the masks (_TintColor,_TintMask0,_TintMask1,_TintMask2,_TintMask3) and see which one owns its body.");

            CosmeticGourdRestoreInterval = file.Bind(
                "Archipelago",
                "GourdRestoreInterval",
                0.5f,
                "Seconds between each gourd when a session start restores several at once. They are dropped one at a time on the game's own spawn point and left to settle, which is both what keeps them from ending up outside the playable area and what stops a heap of them grinding against each other. Lower is faster but piles them up harder.");

            SpawnGourdAtPlayer = file.Bind(
                "Archipelago",
                "SpawnGourdAtPlayer",
                true,
                "Drops a gourd or gadget received during play just in front of the player it goes to (see PutGourdInHands for who that is) instead of at the hub, so you do not have to walk back across the island for it. Only applies to gourds arriving mid-game: the batch rebuilt when a session starts always goes to the hub, since that is stock rather than a gift and the players are not necessarily near the hub when it happens. Falls back to the hub when there is no player in the world yet.");

            PutGourdInHands = file.Bind(
                "Archipelago",
                "PutGourdInHands",
                true,
                "Puts a gourd or gadget received during play straight into a player's hands. It goes to a player whose hands are free, the one given the fewest items this session first, ties at random; if nobody's hands are free, to one of everyone the same way, who drops what they were holding first. The count is not saved. With this off, items only drop in front of that player. Never applies to the batch rebuilt at the start of a session. Note that a gourd received inside a sealed puzzle room stays there until the next world load, hands or not — the game does not let you carry it out.");

            ResyncGourdsKey = file.Bind(
                "Archipelago",
                "ResyncGourdsKey",
                new KeyboardShortcut(KeyCode.R, KeyCode.LeftControl),
                "Puts everything Archipelago has given you back within reach. Every gourd of yours that is not in a monument, every filler item, and every big key not already in its plinth is swept up — out of your hands too — and the right number is put back from what the server says you have received. For when one has ended up somewhere you cannot reach it: stranded in a sealed puzzle room, say. Monument deposits and placed keys are never touched, so nothing that counts for Archipelago can be lost by pressing this. Host only.");

            ShowConnectionStatus = file.Bind(
                "Archipelago",
                "ShowConnectionStatus",
                true,
                "Shows the Archipelago connection in the corner of the screen, for the whole session rather than only when something is wrong — a silent, self-healing connection is still worth being able to check at a glance. Only ever appears for the host, since nobody else connects. Turn off for a clean screen; the same information stays in the BepInEx log either way. This is the master switch: the item feed and the resync hint below are drawn under this line and go with it.");

            StatusFontSize = file.Bind(
                "Archipelago",
                "StatusFontSize",
                22,
                "Size of the Archipelago status line, in points. It sits over whatever the game is rendering, so it is drawn with a shadow rather than a panel; raise this if it is hard to read at your resolution. The feed and the hint under it are drawn slightly smaller than this, proportionally.");

            ShowItemFeed = file.Bind(
                "Archipelago",
                "ShowItemFeed",
                true,
                "Lists the last few checks sent and items received under the status line, each fading after a few seconds. Big Walk itself never says whether a puzzle counted or what just arrived from the multiworld, which left 'nothing happened' and 'it happened and I could not see it' looking identical. Identical lines in a row collapse into a count, so the burst replayed on connect does not fill the screen.");

            NoticeSeconds = file.Bind(
                "Archipelago",
                "NoticeSeconds",
                8f,
                "How long, in seconds, a line in the feed above stays on screen.");

            ShowResyncHint = file.Bind(
                "Archipelago",
                "ShowResyncHint",
                true,
                "Shows the resync shortcut under the status line while connected, so the way out of a stranded gourd or key is on screen rather than in a setup guide. Reads whatever ResyncGourdsKey is actually bound to, so rebinding it changes the hint.");


            HoldTrackedStateKey = file.Bind(
                "Debug",
                "HoldTrackedStateKey",
                new KeyboardShortcut(KeyCode.Keypad4),
                "Holds down the nearest matching TrackedPeckState for as long as this key is held — a sustained hold, not a tap. For the sealed-box puzzles, whose door is opened by a momentary push button a second player would normally keep pressed (only has an effect if Debug.Enabled is active).");

            HoldTrackedStateName = file.Bind(
                "Debug",
                "HoldTrackedStateName",
                "BasicPushButton",
                "Substring (case-insensitive) of the name of the TrackedPeckState that HoldTrackedStateKey holds. Only the NEAREST match is touched: this default is a generic name shared by buttons all over the world, and forcing every match would press buttons in puzzles you are nowhere near. Find the right name with DumpPeckSwitchTargetKey.");

            HoldTrackedStateValue = file.Bind(
                "Debug",
                "HoldTrackedStateValue",
                1,
                "The state to hold it at. 1 is 'pressed' for a push button (its PeckSwitchTrigger writes 1, its UpSwitch writes 0).");

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

            DumpRadioKey = file.Bind(
                "Debug",
                "DumpRadioKey",
                new KeyboardShortcut(KeyCode.B, KeyCode.LeftControl),
                "Logs the radio dial (FmRadioManager.stationTrackGroups, one line per slot with its unlock state), every BroadcastStation currently loaded with the SavableSystem its peck system writes, and this save's station ledger. Answers the one question the decompilation could not: whether the dial is ordered like the FmStation* enum, which is what Core/RadioStations.cs falls back on when no tower is loaded. Letter + LeftControl because that is the only key format proven to register reliably in this game (only has an effect if Debug.Enabled is active).");

            DumpKeysKey = file.Bind(
                "Debug",
                "DumpKeysKey",
                new KeyboardShortcut(KeyCode.K, KeyCode.LeftControl),
                "Logs everything the \"big keys as forage checks\" design needs: every KeyBlank loaded (which key, how many segments and which are cut, its finishedPropGroup), every big-key plinth with the PropGroup it accepts and the TrackedPeckState it drives when filled, and what each key prop currently carries. Answers how many locations the apworld would gain, whether skipping RefreshPropGroup is enough to keep an uncut key out of its socket, and whether a feature switch exists separately from the key (only has an effect if Debug.Enabled is active).");

            ForceBigKeyDoorKey = file.Bind(
                "Debug",
                "ForceBigKeyDoorKey",
                new KeyboardShortcut(KeyCode.U, KeyCode.LeftControl),
                "Opens a big-key door without the key, by driving the TrackedPeckState the key itself carries in taggedPinSystems for its plinth's PropGroup — which is what Prop.SetPinDirectControlSystem does when a key goes in, and what a received feature item will do instead. Takes the key whose plinth is nearest unless BigKeyDoorName says otherwise, and prints all seven with their distances either way. Moved off Ctrl+D (player report, 2026-09-22: fired on its own during ordinary play) — D is a WASD movement key, so Ctrl+D collides with crouch-while-moving-right in this game's own controls. Letter + LeftControl otherwise, since that is the only key format proven to register reliably in this game — but never a WASD letter specifically, for the same reason (only has an effect if Debug.Enabled is active).");

            GrantBigKeyItemKey = file.Bind(
                "Debug",
                "GrantBigKeyItemKey",
                new KeyboardShortcut(KeyCode.G, KeyCode.LeftControl),
                "Simulates receiving the big KEY item for one tower (Core/KeyCustody.Grant), which unlocks that key from its stone and drops it at the spawn point. Not the same thing as the feature item that opens its door — that one is Ctrl+D, or F4. Uses BigKeyDoorName to choose, or the nearest plinth when that is empty. Needed because the keys are shuffled into the multiworld like everything else, so they no longer arrive on their own (only has an effect if Debug.Enabled is active).");

            GrantBigKeyFeatureKey = file.Bind(
                "Debug",
                "GrantBigKeyFeatureKey",
                new KeyboardShortcut(KeyCode.F, KeyCode.LeftControl),
                "Simulates receiving the FEATURE item for one tower (Core/KeyFeatures.Grant) — the map room, the chairlift, the train. Unlike ForceBigKeyDoorKey, which drives the door's state directly and bypasses the ledger, this takes the path a real Archipelago item takes and therefore also tests that the door reopens after a world reload. Uses BigKeyDoorName to choose, or the nearest plinth when that is empty. F4 is no substitute: it targets the nearest uncollected prop, which is rarely the one you are standing in front of (only has an effect if Debug.Enabled is active).");

            BigKeyDoorName = file.Bind(
                "Debug",
                "BigKeyDoorName",
                "",
                "Substring (case-insensitive) of the SaveablePropName of the big key whose door ForceBigKeyDoorKey should open — bigKeyGreenZone, bigKeyIntro, and so on. Empty = whichever plinth is nearest. An escape hatch for the case where the plinths turn out to be instantiated everywhere, like the key blanks are, and distance stops meaning anything.");

            DumpPropsKey = file.Bind(
                "Debug",
                "DumpPropsKey",
                new KeyboardShortcut(KeyCode.J, KeyCode.LeftControl),
                "Lists the objects loaded around you that could plausibly become Archipelago items — the ones carrying a savablePropGuid (the game's own per-prop identity, and the only thing a location could be keyed on), a radioVoiceAssigner (a walkie-talkie) or a use-while-held switch. The binary names none of these: they are plain Prop prefabs, so only the running game can inventory them (only has an effect if Debug.Enabled is active).");

            DumpGourdRosterKey = file.Bind(
                "Debug",
                "DumpGourdRosterKey",
                new KeyboardShortcut(KeyCode.V, KeyCode.LeftControl),
                "Lists every RewardGourd loaded around you with its saveablePropName and whether it is a purple 'variant challenge' gourd. Written to settle which puzzles sit behind the chairlift: the world's logic assumes the island is open apart from the ending, and a puzzle that is not would make some seeds unbeatable. Press it in the gated zone and again somewhere plainly open — the difference is the set that needs its own region (only has an effect if Debug.Enabled is active).");

            SimulateGadgetItemKey = file.Bind(
                "Debug",
                "SimulateGadgetItemKey",
                new KeyboardShortcut(KeyCode.I, KeyCode.LeftControl),
                "Simulates receiving the next filler gadget item (Megaphone, Walkie-Talkie, Backpack, Belt, Flare Gun, one per press, cycling), via ItemApplier.ApplyGadgetItem — same code path a real Archipelago item takes, without a network connection. Tests GadgetItemSpawner's clone/spawn in isolation (only has an effect if Debug.Enabled is active).");

            DumpHeldItemKey = file.Bind(
                "Debug",
                "DumpHeldItemKey",
                new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl),
                "Logs the GameObject name, guid, saveablePropName and propGroups of whatever the local player is currently holding — for identifying an object by description alone, without needing to know its prefab name first (only has an effect if Debug.Enabled is active).");

            DumpCosmeticGadgetsKey = file.Bind(
                "Debug",
                "DumpCosmeticGadgetsKey",
                new KeyboardShortcut(KeyCode.M, KeyCode.LeftControl),
                "Finds every cosmetic gadget clone currently loose in the scene (the ones a filler item spawns) and logs each renderer's actual shader/material/lightmapIndex — for diagnosing the pink/magenta clones directly rather than through a held item, whose visible mesh turned out not to live under prop.gameObject at all (only has an effect if Debug.Enabled is active).");

            DumpNetworkKey = file.Bind(
                "Debug",
                "DumpNetworkKey",
                new KeyboardShortcut(KeyCode.N, KeyCode.LeftControl),
                "Logs this process's Mirror setup without changing any of it: the NetworkManager, the active transport and every transport under it (with the Kcp port and whether its server is running), the authenticator, the server's connections with the identifier each one sent, Application.runInBackground, whether Steam is up, and the command line. Written for running a second instance of the game on the same PC as a network guest: it answers whether the host already listens on UDP and what the two instances would share (only has an effect if Debug.Enabled is active).");

            LoopbackJoinKey = file.Bind(
                "Debug",
                "LoopbackJoinKey",
                new KeyboardShortcut(KeyCode.L, KeyCode.LeftControl),
                "In the loopback guest only (the second instance tools/launch-guest.ps1 starts on the same PC): joins the host on 127.0.0.1 over Kcp, from the title menu. The launcher already makes the guest join on its own when the host is hosting; this is for when it was not yet, or to join again after leaving. Does nothing in any other instance (only has an effect if Debug.Enabled is active).");

            LoopbackFocusKey = file.Bind(
                "Debug",
                "LoopbackFocusKey",
                new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl),
                "With two instances of the game on this PC (a host and the loopback guest from tools/launch-guest.ps1): gives the keyboard and mouse to the other one. Press it in either window. Both keep running while unfocused (only has an effect if Debug.Enabled is active).");

            LoopbackSummonKey = file.Bind(
                "Debug",
                "LoopbackSummonKey",
                new KeyboardShortcut(KeyCode.C, KeyCode.LeftControl),
                "On the host, with a loopback guest connected: teleports the guest next to you, through the mod's own network channel and the game's own player teleport. Never reaches a player on another machine (only has an effect if Debug.Enabled is active).");
        }
    }
}
