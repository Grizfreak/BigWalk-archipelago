using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The seven FM stations, as Archipelago items.
    //
    // Why this exists: switching a station on used to be the one check in the
    // world that rewarded itself. Every other check grants nothing locally —
    // gourds are hidden and unspawned, big keys arrive from Archipelago — so
    // the radio was an inconsistency, not a bug. With slot_data's
    // `radio_station_items` the music becomes an item like everything else:
    // switching a station on still reports its check, and the music starts
    // when the Broadcast arrives.
    //
    // HOW THE GAME UNLOCKS A STATION (decompiled 2026-09-21, Ghidra, see
    // ../../reverse-engineering-notes.md). Three facts shape everything here:
    //
    //   1. `BroadcastStation.Unlock(PeckContext)` is an INLINED COPY of
    //      `FmRadioManager.Unlock(MusicGroup)` — it does not call it. So
    //      patching FmRadioManager.Unlock would suppress nothing, and
    //      conversely this class can call FmRadioManager.Unlock freely
    //      without re-entering the patch on BroadcastStation.Unlock.
    //
    //   2. Neither of them writes to the save. The persistence is one level
    //      up: the station's `TrackedPeckState` carries
    //      `savableSystem = FmStationXxx` and writes it through
    //      SaveManager.SetIntValue, which is exactly where SaveValuePatch
    //      already reads the check from. Suppressing the unlock therefore
    //      costs no check.
    //
    //   3. There is NO relock. `FmRadioManager._stationStates[i]` is only
    //      ever set to true, and the manager reads nothing back from the
    //      save. Writing `FmStationXxx` back to 0 — the obvious first idea —
    //      stops nothing. Suppression has to happen before the unlock, which
    //      is what Patches/BroadcastStationUnlockPatch.cs does.
    //
    // Because (3) also means the unlock lives in RAM and nowhere else, a
    // granted station has to be re-applied to every world that loads. That is
    // what the ledger below is for: the grant is persisted under
    // `ap_radio_<SavableSystem>`, and re-applied on each world.
    internal static class RadioStations
    {
        // Same reasoning as CheckTracker's "ap_reported_": a prefix the game's
        // own enum names can never produce, so SaveValuePatch never mistakes
        // one of these writes for a station being switched on.
        private const string KeyPrefix = "ap_radio_";

        // Where each station sits on the radio dial, learned from the world
        // and kept per save. Stored as position + 1 so that 0 means "never
        // learned" (see LearnDialPositions).
        private const string DialKeyPrefix = "ap_radio_dial_";

        // The seven stations that exist. SavableSystem also declares
        // FmStation7/8/9, which the apworld leaves out (no names, never seen
        // written); keeping the same seven here means an id that resolves to
        // one of those can never grant anything.
        internal static readonly SavableSystem[] All =
        {
            SavableSystem.FmStationSleuthFm,
            SavableSystem.FmStationKosmische,
            SavableSystem.FmStationDanceFm,
            SavableSystem.FmStationBreathwork,
            SavableSystem.FmStationJourneyBeat,
            SavableSystem.FmStationAFJ,
            SavableSystem.FmStationFourthSpace,
        };

        // Set from slot_data on connection. While false the game's own radio
        // is left completely alone — that is what an older seed, a seed with
        // the option off, and a guest who never connects all look like.
        //
        // Deliberately NOT cleared when the connection drops: the seed still
        // says the music is shuffled, and handing out free stations on a
        // network hiccup would be a real loss. Only a new session clears it.
        internal static bool ItemsInPlay { get; private set; }

        // Granted stations still waiting for a world able to take them.
        private static readonly HashSet<SavableSystem> Pending = new();

        private static bool _pendingBlockedLogged;

        internal static void Configure(bool itemsInPlay)
        {
            if (ItemsInPlay == itemsInPlay)
                return;

            ItemsInPlay = itemsInPlay;
            Plugin.Log.LogInfo(
                $"[{nameof(RadioStations)}] Radio stations are "
                + (itemsInPlay
                    ? "Archipelago items: the game's own unlock is suppressed until each one arrives."
                    : "left vanilla: switching one on unlocks its music on the spot."));
        }

        internal static bool IsGranted(SavableSystem system)
        {
            return SaveManager.GetIntValue(KeyPrefix + system, 0, false) != 0;
        }

        // A Broadcast item arrived. Persisted first, applied second: the
        // ledger is what survives the world reload, and the live unlock is
        // only this session's copy of it.
        internal static bool Grant(SavableSystem system)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(RadioStations)}] Ignored: {system} received while not host.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + system, 1);
            Pending.Add(system);

            var unlocked = TickPending();
            Plugin.Log.LogInfo(
                $"[{nameof(RadioStations)}] {system} granted"
                + (unlocked ? " and playing." : "; it will start playing once the radio is loaded."));

            return true;
        }

        // A new (seed, slot) binding replays every item from scratch, so the
        // stations this save was granted under the old one must go with it —
        // otherwise a save rebound to another seed keeps music that seed
        // never gave it. Mirrors what ApItemCursor does to the item cursor.
        internal static void ClearLedger()
        {
            var cleared = 0;
            foreach (var system in All)
            {
                if (!IsGranted(system))
                    continue;

                SaveManager.SetIntValue(KeyPrefix + system, 0);
                cleared++;
            }

            Pending.Clear();
            _pendingBlockedLogged = false;

            if (cleared > 0)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(RadioStations)}] {cleared} station grant(s) cleared: this save is being replayed "
                    + "against a different seed or slot.");
            }
        }

        // Queues every station this save has been granted, for TickPending to
        // apply. Called on two occasions, both of which leave a world whose
        // radio knows nothing of the ledger: a world that has just become
        // ready (the live unlock does not survive a reload — fact 3 above —
        // and a reload does not need the process to restart, quitting to the
        // menu and hosting again is enough), and a connection established
        // while a world is ALREADY loaded, where nothing else would ever put
        // the stations back, because the server's replay of them is exactly
        // what the item cursor skips.
        internal static void RearmFromLedger()
        {
            Pending.Clear();
            _pendingBlockedLogged = false;

            if (!ItemsInPlay)
                return;

            foreach (var system in All)
            {
                if (IsGranted(system))
                    Pending.Add(system);
            }

            if (Pending.Count > 0)
                TickPending();
        }

        // Called from ApRuntime's poll tick until nothing is left. FmRadioManager
        // is a scene singleton, so right after a world becomes ready it may not
        // exist yet; this simply tries again a second later rather than losing
        // the station.
        //
        // Returns true when nothing is pending any more.
        internal static bool TickPending()
        {
            if (Pending.Count == 0)
                return true;

            if (!TryGetManager(out var manager))
            {
                LogBlockedOnce("no FmRadioManager in the world yet");
                return false;
            }

            // Learn the dial while the towers happen to be loaded, not only
            // for the station being unlocked right now: it costs one pass and
            // it is what lets a later session unlock one with no tower in
            // sight (see FindMusicGroup).
            LearnDialPositions(manager);

            // Copied before iterating: Unlock runs the game's own listeners,
            // and nothing guarantees they leave this set alone.
            var attempts = new List<SavableSystem>(Pending);
            foreach (var system in attempts)
            {
                if (!TryUnlock(manager, system))
                    continue;

                Pending.Remove(system);
                Plugin.Log.LogInfo($"[{nameof(RadioStations)}] {system} is now playing.");
            }

            if (Pending.Count > 0)
            {
                // Reached when the manager exists but no MusicGroup could be
                // resolved — neither a loaded BroadcastStation nor the dial.
                // Silent retries every second would be indistinguishable from
                // a feature that simply does not work, which is the mistake
                // this project has already paid for twice.
                LogBlockedOnce("the radio is there but none of its stations could be matched");
                return false;
            }

            _pendingBlockedLogged = false;
            return true;
        }

        // Once per stuck spell, not once per poll tick: this runs every second
        // for as long as something is waiting.
        private static void LogBlockedOnce(string reason)
        {
            if (_pendingBlockedLogged)
                return;

            _pendingBlockedLogged = true;
            Plugin.Log.LogInfo(
                $"[{nameof(RadioStations)}] {Pending.Count} station(s) waiting: {reason}. Still trying.");
        }

        private static bool TryUnlock(FmRadioManager manager, SavableSystem system)
        {
            var group = FindMusicGroup(manager, system);
            if (group == null)
                return false;

            try
            {
                FmRadioManager.Unlock(group);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[{nameof(RadioStations)}] FmRadioManager.Unlock failed for {system}: {ex}");
                return false;
            }
        }

        // Two ways to reach a station's MusicGroup, and one of them had to be
        // thrown away.
        //
        // The BroadcastStation in the world is authoritative: it holds both
        // the MusicGroup and — through its peck system — the SavableSystem the
        // save is keyed on, so the pairing comes from the game's own data.
        //
        // What used to sit behind it was `stationTrackGroups[(int)system - 30]`,
        // on the assumption that the dial is ordered like the FmStation* block
        // of the enum. **That assumption is false**, proven in game on
        // 2026-09-21: the dial reads bobby / FourthSpace / breathwork /
        // JourneyBeat / DanceFM / Mallets / Bristol, while the stations owning
        // them are Breathwork / SleuthFm / FourthSpace / JourneyBeat / DanceFm
        // / AFJ / Kosmische. Only one of the seven lines up. The fallback would
        // have unlocked a different station — silently, and only in the case it
        // existed for.
        //
        // So the dial position is LEARNED instead of guessed: every time the
        // towers are loaded, each station's position is written to the save,
        // and a later session with no tower in sight reads it back. A save that
        // has never had them loaded simply waits, which is the honest failure.
        private static MusicGroup FindMusicGroup(FmRadioManager manager, SavableSystem system)
        {
            var fromStation = FindMusicGroupFromBroadcastStation(system);
            if (fromStation != null)
                return fromStation;

            var groups = manager.stationTrackGroups;
            var learned = SaveManager.GetIntValue(DialKeyPrefix + system, 0, false) - 1;
            if (groups == null || learned < 0 || learned >= groups.Length)
                return null;

            var group = groups[learned];
            if (group != null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(RadioStations)}] {system}: no BroadcastStation loaded, using the dial position "
                    + $"this save learned earlier ({learned} -> '{group.name}').");
            }

            return group;
        }

        // Writes down where each loaded station sits on the dial. Cheap, and
        // deliberately done for every station rather than the one being asked
        // for: the towers are loaded together, and the point is to have the
        // answer ready for the session where they are not.
        private static void LearnDialPositions(FmRadioManager manager)
        {
            var groups = manager.stationTrackGroups;
            if (groups == null)
                return;

            var stations = UnityEngine.Object.FindObjectsByType<BroadcastStation>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var station in stations)
            {
                if (station == null || station.musicGroup == null
                    || !TryGetSystem(station, out var system))
                    continue;

                for (var i = 0; i < groups.Length; i++)
                {
                    if (groups[i] == null || groups[i].Pointer != station.musicGroup.Pointer)
                        continue;

                    // Stored as position + 1, because GetIntValue hands back 0
                    // for a key that was never written and position 0 is real.
                    var key = DialKeyPrefix + system;
                    if (SaveManager.GetIntValue(key, 0, false) != i + 1)
                    {
                        SaveManager.SetIntValue(key, i + 1);
                        Plugin.Log.LogInfo(
                            $"[{nameof(RadioStations)}] Learned {system} -> dial position {i} ('{groups[i].name}').");
                    }

                    break;
                }
            }
        }

        // `FmRadioManager.instance` THROWS when there is no instance — it does
        // not come back null. Found the hard way on 2026-09-21: the exception
        // crossed the IL2CPP-to-managed trampoline out of ApRuntime.Update()
        // and took the rest of OnJustConnected with it, which silently skipped
        // ResendKnownChecks(). Nothing in this file may reach the property
        // except through here.
        internal static bool TryGetManager(out FmRadioManager manager)
        {
            try
            {
                manager = FmRadioManager.instance;
            }
            catch (Exception)
            {
                manager = null;
            }

            return manager != null;
        }

        private static MusicGroup FindMusicGroupFromBroadcastStation(SavableSystem system)
        {
            var stations = UnityEngine.Object.FindObjectsByType<BroadcastStation>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var station in stations)
            {
                if (station == null || station.musicGroup == null)
                    continue;

                if (TryGetSystem(station, out var stationSystem) && stationSystem == system)
                    return station.musicGroup;
            }

            return null;
        }

        internal static bool TryGetSystem(BroadcastStation station, out SavableSystem system)
        {
            system = SavableSystem.NotSavable;
            if (station == null)
                return false;

            var peckSystem = station.peckSystemReference.peckSystem;
            if (peckSystem == null)
                return false;

            system = peckSystem.savableSystem;
            return IsRealStation(system);
        }

        // The station's learned position on the dial, or -1 if this save has
        // never had its tower loaded. NOT the position in All: the two are
        // unrelated, which is the whole lesson of 2026-09-21.
        internal static int LearnedDialPosition(SavableSystem system)
        {
            return SaveManager.GetIntValue(DialKeyPrefix + system, 0, false) - 1;
        }

        // One of the seven the apworld knows about — the single test both the
        // location ids and the item ids are filtered through
        // (Core/Net/ApLocationIds.cs), so the two can never disagree about
        // which stations exist.
        internal static bool IsRealStation(SavableSystem system)
        {
            return Array.IndexOf(All, system) >= 0;
        }
    }
}
