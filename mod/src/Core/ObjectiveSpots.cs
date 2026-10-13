using System;
using System.Globalization;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The two objectives of the island's far corners (ROADMAP R5, 0.5, player 2026-10-12), both latched in the
    // save, like the endings, and both noticed by the host, which sees everything:
    //
    //  - `big_climb`, the island's highest point: a player standing there. The place is the .cfg's
    //    "x, y, z, radius" (metres), so it can be moved without a rebuild; the default is the Peak fireworks (the
    //    highest launcher, y = 179.6). Alt + 1 writes where the player stands to BepInEx/objective-spots.txt.
    //  - `big_fiesta`, the party: not a place but an action (the player, 2026-10-12): a radio plugged into the big
    //    speakers on the beach. The Speaker Stage has a dock for the radio (`FmRadioDockPropHome`, found in the
    //    relevé F9 of 2026-10-12) and the speakers' own logic (`SpeakerStageLogic`, an unsaved state), so the
    //    party is the radio sitting in the dock for a few seconds.
    internal static class ObjectiveSpots
    {
        private const string Tag = "[" + nameof(ObjectiveSpots) + "]";
        private const string LatchPrefix = "ap_objective_";

        private struct Spot
        {
            internal Vector3 Centre;
            internal float Radius;
        }

        internal static bool IsSpot(string key) => key == "big_climb" || key == "big_fiesta";

        private static Spot? Of(string key)
        {
            if (key != "big_climb")
                return null;

            var text = ModConfig.ClimbSpot.Value;
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = text.Split(',');
            if (parts.Length != 4)
                return null;

            var numbers = new float[4];
            for (var i = 0; i < 4; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]))
                    return null;
            }

            return new Spot { Centre = new Vector3(numbers[0], numbers[1], numbers[2]), Radius = Mathf.Max(1f, numbers[3]) };
        }

        // U2 (Core/MapTracker): where big_climb is.
        internal static Vector3? ClimbCentre() => Of("big_climb")?.Centre;

        // Whether this build knows where the place is.
        internal static bool Known(string key) => key == "big_fiesta" || Of(key) != null;

        internal static bool IsDone(string key) => SaveManager.GetIntValue(LatchPrefix + key, 0, false) != 0;

        // Every couple of seconds (TeleportButtons.Sync), on the host.
        internal static void Tick()
        {
            if (!NetworkServer.active || !WorldManager.isReadyForEffects || !ApObjectives.Active)
                return;

            foreach (var key in ApObjectives.Keys)
            {
                if (!IsSpot(key) || IsDone(key))
                    continue;

                if (key == "big_fiesta")
                {
                    CheckTheParty();
                    continue;
                }

                var spot = Of(key);
                if (spot == null || !Gathered(spot.Value))
                {
                    _atTheTopSince = -1f;
                    continue;
                }

                // Not on the way past: ten seconds there.
                if (_atTheTopSince < 0f)
                    _atTheTopSince = Time.unscaledTime;
                if (Time.unscaledTime - _atTheTopSince < ClimbSeconds)
                    continue;

                SaveManager.SetIntValue(LatchPrefix + key, 1);
                Plugin.Log.LogInfo($"{Tag} {key} is done: a player is at {spot.Value.Centre}.");
                ApNotices.Post(key == "big_climb" ? "Big Climb: the highest point is reached" : "Big Fiesta: the party is reached");
            }
        }

        // The radio in the dock of the Speaker Stage, for how long (since when, in unscaled seconds).
        private const float PartySeconds = 10f;
        private const float ClimbSeconds = 10f;
        private static float _atTheTopSince = -1f;
        private static float _dockedSince = -1f;
        private static bool _loggedDocked;
        private static string _lastSeen;

        private static void CheckTheParty()
        {
            PropHome dock = null;
            var homes = PropHome.allPropHomes;
            if (homes != null)
            {
                foreach (var home in homes)
                {
                    if (home != null && home.name == "FmRadioDockPropHome")
                    {
                        dock = home;
                        break;
                    }
                }
            }

            if (dock == null || dock.pinnedProp == null || (EveryoneGathers && !Gathered(dock.transform.position, PartyRadius)))
            {
                _dockedSince = -1f;
                _loggedDocked = false;
                return;
            }

            if (_dockedSince < 0f)
                _dockedSince = Time.unscaledTime;

            // What the speakers' own logic says while a radio sits there, for the tests (it is not saved, so
            // nothing else shows it): the state of `SpeakerStageLogic`.
            var parent = dock.transform.parent;
            var logic = parent != null ? parent.Find("SpeakerStageLogic") : null;
            var logicState = logic != null ? logic.GetComponent<TrackedPeckState>() : null;
            var radio = logic != null ? logic.Find("RadioSystem") : null;
            var radioState = radio != null ? radio.GetComponent<TrackedPeckState>() : null;
            var amplitude = SpeakersAmplitude(parent);
            // The speakers' music only plays on a machine near them, so a host far from the stage hears silence while the
            // guests dance (the player, 2026-10-12): the dial is read too. The radio system's state is the dial position
            // plus one (measured: Bobby, position 0, is 1), and a station is a music once granted (when stations are items).
            var playing = amplitude > 0.0005f || TunedOnAGrantedStation(radioState);
            var seen = $"logic {(logicState != null ? logicState.currentPeckContext.state.ToString() : "?")}, "
                       + $"radio system {(radioState != null ? radioState.currentPeckContext.state.ToString() : "?")}, "
                       + $"music {(playing ? "playing" : "silent")}";
            if (!_loggedDocked || seen != _lastSeen)
            {
                _loggedDocked = true;
                _lastSeen = seen;
                Plugin.Log.LogInfo($"{Tag} A radio ({dock.pinnedProp.name}) is in the Speaker Stage's dock: {seen}.");
            }

            // A radio in the dock AND a music coming out of the speakers, ten seconds in a row (the player,
            // 2026-10-12: a radio with a locked station, or none, is no party). The speakers' music players
            // carry the amplitude of what plays.
            if (!playing)
            {
                _dockedSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _dockedSince < PartySeconds)
                return;

            SaveManager.SetIntValue(LatchPrefix + "big_fiesta", 1);
            Plugin.Log.LogInfo($"{Tag} big_fiesta is done: a radio has been in the Speaker Stage's dock for {PartySeconds} s.");
            ApNotices.Post("Big Fiesta: the party is on");
        }

        private static bool TunedOnAGrantedStation(TrackedPeckState radioSystem)
        {
            if (radioSystem == null || !RadioStations.ItemsInPlay || !int.TryParse(radioSystem.currentPeckContext.state.ToString(), out var state)
                || state < 1)
                return false;

            foreach (var station in RadioStations.All)
            {
                if (RadioStations.LearnedDialPosition(station) == state - 1)
                    return RadioStations.IsGranted(station);
            }

            return false;
        }

        // The loudest of the two speakers' music players (`SpeakerStage_Speaker` and `(1)`).
        private static float SpeakersAmplitude(Transform stage)
        {
            if (stage == null)
                return 0f;

            var loudest = 0f;
            foreach (var name in new[] { "SpeakerStage_Speaker", "SpeakerStage_Speaker (1)" })
            {
                var speaker = stage.Find(name);
                var player = speaker != null ? speaker.Find("MusicPlayer") : null;
                var music = player != null ? player.GetComponent<MusicPlayer>() : null;
                if (music != null)
                    loudest = Mathf.Max(loudest, music.Amplitude);
            }

            return loudest;
        }

        // `everyone_gathers` (the player, 2026-10-12): every player in the place, else one is enough.
        private static bool EveryoneGathers => ApRuntime.SlotData != null && ApRuntime.SlotData.EveryoneGathers;

        private static bool Gathered(Spot spot) => Gathered(spot.Centre, spot.Radius);

        private static bool Gathered(Vector3 centre, float radius)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return false;

            var anyone = false;
            foreach (var player in players)
            {
                if (player == null)
                    continue;

                var there = Vector3.Distance(player.transform.position, centre) <= radius;
                if (there && !EveryoneGathers)
                    return true;
                if (!there && EveryoneGathers)
                    return false;
                anyone |= there;
            }

            return anyone;
        }

        // How near the Speaker Stage's dock everyone must stand for the party, with `everyone_gathers`.
        private const float PartyRadius = 30f;

        // Debug key (Alt + 2): forgets the two latches, to try them again.
        internal static void ResetLatches()
        {
            SaveManager.SetIntValue(LatchPrefix + "big_climb", 0);
            SaveManager.SetIntValue(LatchPrefix + "big_fiesta", 0);
            _atTheTopSince = -1f;
            _dockedSince = -1f;
            Plugin.Log.LogInfo($"{Tag} big_climb and big_fiesta forgotten.");
            ApNotices.Post("Debug: climb and club forgotten");
        }

        // Debug key (Alt + 1): where the local player stands, written for the .cfg line of a place.
        internal static void Mark()
        {
            var player = Debug.DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
                return;

            var at = player.transform.position;
            var line = string.Format(CultureInfo.InvariantCulture, "{0:0.0}, {1:0.0}, {2:0.0}, 20", at.x, at.y, at.z);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "objective-spots.txt"),
                $"{DateTime.Now:HH:mm:ss}  {line}\n");
            Plugin.Log.LogInfo($"{Tag} Marked: {line}");
            ApNotices.Post($"Spot marked: {line}");
        }
    }
}
