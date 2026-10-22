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
    //  - `big_party`, the party: not a place but an action (the player, 2026-10-12): a radio plugged into the big
    //    speakers on the beach. The Speaker Stage has a dock for the radio (`FmRadioDockPropHome`, found in the
    //    relevé F9 of 2026-10-12) and the speakers' own logic (`SpeakerStageLogic`, an unsaved state), so the
    //    party is the radio sitting in the dock for a few seconds.
    //  - `big_doof` (the player, 2026-10-20), the Doof Chamber, the music room in the tunnels: its five light
    //    switches on at once (House, Ring, Purple, Reds, White Spots: toggles, the apworld's LIGHT_SWITCHES) and its
    //    smoke machine's push button held, ten seconds, with the players there. The button is a BasicPushButton: its
    //    PeckSwitchTrigger is pecked when it goes down, its UpSwitch when it is let go (the puzzle-needs dump).
    //  - `big_makeover` (the player, 2026-10-20), the Secret Salon in the tunnels: a player painted there. The game
    //    keeps a player's colours in three synced ids on PlayerNetworking (lookIdHead, lookIdTorso, lookIdLegs),
    //    which the salon's brush changes; the mod never writes them (Big Paint only changes what is drawn, through
    //    PlayerLooks.SetColor), so an id changing near the salon is a brush.
    internal static class ObjectiveSpots
    {
        private const string Tag = "[" + nameof(ObjectiveSpots) + "]";
        private const string LatchPrefix = "ap_objective_";

        // Where the save keeps an objective done. The party keeps the name it had until 1.0.2, big_fiesta, so a save that
        // threw it then still has it done.
        private static string Latch(string key) => LatchPrefix + (key == "big_party" ? "big_fiesta" : key);

        private struct Spot
        {
            internal Vector3 Centre;
            internal float Radius;
        }

        internal static bool IsSpot(string key) => key is "big_climb" or "big_party" or "big_doof" or "big_makeover";

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
        internal static bool Known(string key) => key is "big_party" or "big_doof" or "big_makeover" || Of(key) != null;

        internal static bool IsDone(string key) => SaveManager.GetIntValue(Latch(key), 0, false) != 0;

        // Every couple of seconds (TeleportButtons.Sync), on the host.
        internal static void Tick()
        {
            if (!NetworkServer.active || !WorldManager.isReadyForEffects || !ApObjectives.Active)
                return;

            foreach (var key in ApObjectives.Keys)
            {
                if (!IsSpot(key) || IsDone(key))
                    continue;

                if (key == "big_party")
                {
                    CheckTheParty();
                    continue;
                }

                if (key == "big_doof")
                {
                    CheckTheDoof();
                    continue;
                }

                if (key == "big_makeover")
                {
                    CheckTheMakeover();
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

                SaveManager.SetIntValue(Latch(key), 1);
                _atTheTopSince = -1f;
                Plugin.Log.LogInfo($"{Tag} {key} is done: a player is at {spot.Value.Centre}.");
                ApNotices.Post(key == "big_climb" ? "Big Climb: the highest point is reached" : "Big Party: the party is reached");
            }
        }

        // The radio in the dock of the Speaker Stage, for how long (since when, in unscaled seconds).
        private const float PartySeconds = 10f;
        private const float ClimbSeconds = 10f;
        private static float _atTheTopSince = -1f;
        private static float _dockedSince = -1f;
        private static bool _loggedDocked;
        private static string _lastSeen;

        // Whether the party's ten seconds are running: a radio in the dock and its music playing.
        private static bool _partyPlaying;

        // THE COUNTDOWN (the player, 2026-10-20): the ten seconds were counted in silence, and nobody knew whether they
        // ran. Cabin Fever's tick when they start and on each of the last three, on every machine (ApSounds); no text,
        // and no sound when they stop. Every frame on the host (Traps' runner); while one runs, the place is looked at
        // four times a second instead of every two, so the objective comes right after the last tick.
        private static int _climbTicks;
        private static int _partyTicks;
        private static int _doofTicks;
        private static float _nextLook;
        private const float LookWhileCounting = 0.25f;

        internal static void TickCountdown()
        {
            if (!NetworkServer.active)
                return;

            Count(_atTheTopSince, ClimbSeconds, ref _climbTicks);
            Count(_partyPlaying ? _dockedSince : -1f, PartySeconds, ref _partyTicks);
            Count(_doofSince, DoofSeconds, ref _doofTicks);

            if ((_atTheTopSince >= 0f || _partyPlaying || _doofSince >= 0f) && Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + LookWhileCounting;
                Tick();
            }
        }

        // The ticks of one count: at its start, then three, two and one second before its end.
        private static void Count(float since, float seconds, ref int played)
        {
            if (since < 0f)
            {
                played = 0;
                return;
            }

            var elapsed = Time.unscaledTime - since;
            while (played < 4 && elapsed >= (played == 0 ? 0f : seconds - 4 + played))
            {
                played++;
                ApSounds.CountdownTick();
            }
        }

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
                _partyPlaying = false;
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
            _partyPlaying = playing;
            if (!playing)
            {
                _dockedSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _dockedSince < PartySeconds)
                return;

            SaveManager.SetIntValue(Latch("big_party"), 1);
            _dockedSince = -1f;
            _partyPlaying = false;
            Plugin.Log.LogInfo($"{Tag} big_party is done: a radio has been in the Speaker Stage's dock for {PartySeconds} s.");
            ApNotices.Post("Big Party: the party is on");
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

        // ------------------------------------------------------------------
        // big_doof: the Doof Chamber's five lights
        // ------------------------------------------------------------------

        private const string DoofRoot = "LandmarksNonChallenge/DoofChamber/Positioner";
        private static readonly string[] DoofSwitches =
        {
            DoofRoot + "/FXControls/BasicLightSwitchHouse/LightSwitchPeckLogic",
            DoofRoot + "/FXControls/BasicLightSwitchRing/LightSwitchPeckLogic",
            DoofRoot + "/FXControls/ColouredSpotSwitches/BasicLightSwitchPurple/LightSwitchPeckLogic",
            DoofRoot + "/FXControls/ColouredSpotSwitches/BasicLightSwitchReds/LightSwitchPeckLogic",
            DoofRoot + "/FXControls/ColouredSpotSwitches/BasicLightSwitchWhiteSpots/LightSwitchPeckLogic",
        };
        private const float DoofSeconds = 10f;
        private const float DoofRadius = 25f;
        private static float _doofSince = -1f;
        private static TrackedPeckState[] _doofStates;

        // The smoke machine's push button, held or not, from its two switches' pecks (Patches/TrapPatches).
        private const string SmokeDown = DoofRoot + "/FXControls/BasicPushButton/PeckSwitchTrigger";
        private const string SmokeUp = SmokeDown + "/UpSwitch";
        private static bool _smokeHeld;

        // From the PeckSwitch.Peck patches, every peck: only the two already found are compared, nothing is looked for.
        internal static void OnPeck(PeckSwitch peckSwitch)
        {
            if (peckSwitch == null || !NetworkServer.active)
                return;

            var at = peckSwitch.transform;
            if (Found.TryGetValue(SmokeDown, out var down) && down == at)
                _smokeHeld = true;
            else if (Found.TryGetValue(SmokeUp, out var up) && up == at)
                _smokeHeld = false;
            else
                return;

            Plugin.Log.LogInfo($"{Tag} The Doof Chamber's smoke button is {(_smokeHeld ? "held" : "let go")}.");
        }

        // U2 (Core/MapTracker): where the two rooms are, once loaded.
        internal static Vector3? DoofCentre() => Find(DoofRoot)?.position;
        internal static Vector3? SalonCentre() => Find(SalonRoot)?.position;

        private static void CheckTheDoof()
        {
            var centre = Find(DoofRoot);
            if (centre == null || !FindTheSwitches())
            {
                _doofSince = -1f;
                return;
            }

            var lit = 0;
            foreach (var state in _doofStates)
            {
                if (state != null && state.currentPeckContext != null && state.currentPeckContext.compressedState != 0)
                    lit++;
            }

            // The button's two switches are looked for with the rest: found once the room is.
            Find(SmokeDown);
            if (lit < _doofStates.Length || !_smokeHeld || !Gathered(centre.position, DoofRadius))
            {
                _doofSince = -1f;
                return;
            }

            if (_doofSince < 0f)
            {
                _doofSince = Time.unscaledTime;
                Plugin.Log.LogInfo($"{Tag} The Doof Chamber's {lit} lights are on and its smoke is going: {DoofSeconds} s to go.");
            }

            if (Time.unscaledTime - _doofSince < DoofSeconds)
                return;

            SaveManager.SetIntValue(Latch("big_doof"), 1);
            _doofSince = -1f;
            Plugin.Log.LogInfo($"{Tag} big_doof is done: the Doof Chamber's lights all on and its smoke going for {DoofSeconds} s.");
            ApNotices.Post("Big Doof: the Doof Chamber is lit");
        }

        private static bool FindTheSwitches()
        {
            if (_doofStates != null && Array.TrueForAll(_doofStates, state => state != null))
                return true;

            _doofStates = new TrackedPeckState[DoofSwitches.Length];
            for (var i = 0; i < DoofSwitches.Length; i++)
            {
                var t = Find(DoofSwitches[i]);
                _doofStates[i] = t != null ? t.GetComponent<TrackedPeckState>() : null;
            }

            return Array.TrueForAll(_doofStates, state => state != null);
        }

        // ------------------------------------------------------------------
        // big_makeover: painted at the Secret Salon
        // ------------------------------------------------------------------

        private const string SalonRoot = "LandmarksNonChallenge/Salon_Secret/Positioner";
        private const float SalonRadius = 20f;

        // Each player's colours as last seen, by instance: a player first seen sets them, without counting.
        private static readonly System.Collections.Generic.Dictionary<int, (int head, int torso, int legs)> Looks = new();

        private static void CheckTheMakeover()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return;

            var salon = Find(SalonRoot);
            foreach (var player in players)
            {
                if (player == null || player.playerNetworking == null)
                    continue;

                var networking = player.playerNetworking;
                var now = (networking.lookIdHead, networking.lookIdTorso, networking.lookIdLegs);
                var id = player.GetInstanceID();
                var seen = Looks.TryGetValue(id, out var before);
                Looks[id] = now;
                if (!seen || before == now || salon == null)
                    continue;

                if (Vector3.Distance(player.transform.position, salon.position) > SalonRadius)
                    continue;

                // With `everyone_gathers`, every player painted there, each in turn (the player, 2026-10-20).
                Painted.Add(id);
                if (EveryoneGathers && !EveryonePainted(players))
                {
                    Plugin.Log.LogInfo($"{Tag} {player.gameObject.name} was painted at the Secret Salon; not every player yet.");
                    continue;
                }

                SaveManager.SetIntValue(Latch("big_makeover"), 1);
                Plugin.Log.LogInfo($"{Tag} big_makeover is done: {player.gameObject.name} was painted at the Secret Salon "
                                   + $"({before} -> {now}).");
                ApNotices.Post("Big Makeover: painted at the Secret Salon");
                return;
            }
        }

        // The players painted at the Secret Salon in this session, by instance.
        private static readonly System.Collections.Generic.HashSet<int> Painted = new();

        private static bool EveryonePainted(Il2CppSystem.Collections.Generic.List<PlayerCharacter> players)
        {
            foreach (var player in players)
            {
                if (player != null && !Painted.Contains(player.GetInstanceID()))
                    return false;
            }
            return true;
        }

        // A scene object by its path: looked for at most every ten seconds while it is not there, since the walk is
        // over every Transform.
        private static readonly System.Collections.Generic.Dictionary<string, Transform> Found = new();
        private static float _nextFind;

        private static Transform Find(string path)
        {
            if (Found.TryGetValue(path, out var known) && known != null)
                return known;

            if (Time.unscaledTime < _nextFind)
                return null;

            var wanted = new System.Collections.Generic.HashSet<string>(DoofSwitches) { DoofRoot, SalonRoot, SmokeDown, SmokeUp, path };
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null)
                    continue;
                var full = t.name;
                for (var p = t.parent; p != null; p = p.parent)
                    full = p.name + "/" + full;
                if (wanted.Contains(full))
                    Found[full] = t;
            }

            if (!Found.TryGetValue(path, out known) || known == null)
            {
                _nextFind = Time.unscaledTime + 10f;
                return null;
            }

            return known;
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
            SaveManager.SetIntValue(Latch("big_climb"), 0);
            SaveManager.SetIntValue(Latch("big_party"), 0);
            SaveManager.SetIntValue(Latch("big_doof"), 0);
            SaveManager.SetIntValue(Latch("big_makeover"), 0);
            _doofSince = -1f;
            Looks.Clear();
            Painted.Clear();
            _atTheTopSince = -1f;
            _dockedSince = -1f;
            _partyPlaying = false;
            Plugin.Log.LogInfo($"{Tag} big_climb and big_party forgotten.");
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
