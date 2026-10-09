using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // What a trap or a bonus does, on THIS machine's own player (ROADMAP step 3). The host
    // decides what fires and where a player goes (Core/Traps.cs); every machine, the host
    // included, then plays the effect on its own player, because a player's hands, movement and
    // position belong to that player's machine (the game is owner-authoritative for players).
    //
    // Everything here goes through the game's own code paths, read with Ghidra on 2026-10-05:
    //
    //   drop     PlayerActions.ActionDropPropOrPlayer, what letting go of the drop button calls.
    //            It throws with the wind-up the arms show; ClearWindUp first makes it a plain drop.
    //   throw    PlayerDecisions.SetWindUpStartTime(NetworkTime.time), what pressing the button
    //            does; put back every frame if the player cancels it, and one second past a full
    //            wind-up (PlayerTunings.maxWindUpDuration) ActionDropPropOrPlayer throws.
    //   speed    PlayerTunings, a per-player copy: the forward speeds, scaled for a while.
    //   jump     PlayerTunings.jumpForce (read by PlayerJumper.LocalFixedUpdate) and the cap on
    //            upward velocity, scaled for a while.
    //   sleep    asleep two seconds out of seven for a while (player, 2026-10-05): the game's own
    //            sleep (PlayerSleeper.forceSleeping), and the same speeds and jump at 0 meanwhile.
    //   night,   SkyManager.SetFixedTime, as DebugDaylight, stepped every frame: the clock
    //   day      fast-forwards to midnight (noon), then ClearFixedTime and it runs on from there.
    //   trip,    PlayerGrease.Teleport, the game's own player teleport, as the world buttons.
    //   meeting
    internal static class TrapEffects
    {
        internal const string Drop = "drop";
        internal const string Throw = "throw";
        internal const string Trip = "trip";
        internal const string Meeting = "meeting";
        internal const string Night = "night";
        internal const string Day = "day";
        internal const string Speed = "speed";
        internal const string Jump = "jump";
        internal const string Sleep = "sleep";
        internal const string Load = "load";

        // A DeathLink received (`death_link_effect: knockout`, players 2026-10-07): what is held
        // dropped, the packs off (the host's half, Core/Traps), and dazed as after a big fall.
        internal const string Knockout = "knockout";

        // The blindfold helmet on every unmasked player for the trap's duration (host only, Core/Traps).
        internal const string Mask = "mask";

        // A flare in front of every player, in the Archipelago colours (Core/BigFlare).
        internal const string Flare = "flare";

        // Every effect this build plays, so a key from a newer apworld is refused by name.
        internal static readonly string[] Known = { Drop, Throw, Trip, Meeting, Night, Sleep, Load, Mask, Flare, Day, Speed, Jump, Knockout };

        internal static string DisplayName(string key)
        {
            if (key == Knockout)
                return "Knock-out";
            return string.IsNullOrEmpty(key) ? "?" : "Big " + char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        // How long past a full wind-up the throw goes: a second at most (player, 2026-10-05).
        private const float ThrowHoldSeconds = 1f;
        private const float SpeedFactor = 1.75f;
        private const float JumpFactor = 1.8f;
        private const float MidnightHour = 0f;
        private const float NoonHour = 12f;

        private const string Tag = "[" + nameof(TrapEffects) + "]";

        // --- throw ---
        private static PlayerCharacter _throwPlayer;
        private static float _throwAt = -1f;
        private static double _throwWindUpStart;
        private static bool _cancelRefusedLogged;

        // --- Big Speed, Big Jump, Big Sleep: the tunings, the player's own values, until when ---
        private static PlayerTunings _moveTunings;
        private static float[] _moveBase;
        private static float _speedUntil = -1f;
        private static float _jumpUntil = -1f;
        private static float _sleepUntil = -1f;
        private static float _sleepStart;
        private static PlayerCharacter _sleeper;

        // Big Sleep's rhythm (player, 2026-10-05): asleep and still this long, then awake this
        // long, again and again for `trap_duration`.
        private const float AsleepSeconds = 2f;
        private const float AwakeSeconds = 5f;

        // Lasting effects received before this machine's player existed, and until when.
        private static readonly Dictionary<string, float> PendingMovement = new();

        // --- night and day ---
        private const float FastForwardSeconds = 4f;
        private static bool _skyRunning;
        private static float _skyFrom;
        private static float _skyTo;
        private static float _skySpan;
        private static float _skyStart;

        // --- the masks some puzzles put on the screen (PeckEffectMask: binoculars, telescope,
        // blindfold), by instance, while on. Big Meeting and Big Trip leave a masked player alone:
        // they may be in that puzzle, and moving them out or others in could strand someone. ---
        private static readonly HashSet<int> ActiveMasks = new();

        internal static bool LocalMasked => ActiveMasks.Count > 0;

        internal static void NoteMask(PeckEffectMask mask, bool active)
        {
            if (mask == null)
                return;

            var id = mask.GetInstanceID();
            var changed = active ? ActiveMasks.Add(id) : ActiveMasks.Remove(id);
            if (changed)
                Plugin.Log.LogInfo($"{Tag} Screen mask {mask.maskType} {(active ? "on" : "off")} ('{mask.gameObject.name}'); {ActiveMasks.Count} on.");
        }

        // Plays an effect on this machine's player. `destination` is for trip and meeting.
        internal static void Play(string key, int seconds, Vector3? destination, float yaw)
        {
            var player = Debug.DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                // A guest told on joining, before its player exists: kept for when it does.
                if (key is Speed or Jump or Sleep)
                {
                    PendingMovement[key] = Time.unscaledTime + seconds;
                    Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: no local player yet; kept for {seconds}s.");
                }
                else
                {
                    Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: no local player, nothing played.");
                }

                return;
            }

            try
            {
                switch (key)
                {
                    case Drop:
                        DropHeld(player);
                        break;
                    case Flare:
                        BigFlare.Play();
                        break;
                    case Knockout:
                        DropHeld(player);
                        if (player.faller != null)
                            player.faller.TriggerFall();
                        break;
                    case Throw:
                        StartThrow(player);
                        break;
                    case Trip:
                    case Meeting:
                        if (destination != null)
                            StartCountdown(key, player, destination.Value, Quaternion.Euler(0f, yaw, 0f));
                        else
                            Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: no destination for this player (spared).");
                        break;
                    case Night:
                        StartSky(MidnightHour); // the same hour for everyone (player, 2026-10-07: no softer dusk)
                        break;
                    case Day:
                        StartSky(NoonHour);
                        break;
                    case Speed:
                    case Jump:
                    case Sleep:
                        StartMovement(player, seconds, key);
                        break;
                    default:
                        Plugin.Log.LogWarning($"{Tag} Unknown effect '{key}', ignored (a newer apworld than this mod?).");
                        return;
                }

                Plugin.Log.LogInfo($"{Tag} {DisplayName(key)} played ({seconds}s).");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {DisplayName(key)} failed: {ex.Message}");
            }
        }

        // Every frame: the throw that is due, and whatever has run its time.
        internal static void Tick()
        {
            var now = Time.unscaledTime;

            if (_throwAt >= 0f && now >= _throwAt)
                FinishThrow();
            else if (_throwAt >= 0f)
                HoldWindUp();

            if (PendingMovement.Count > 0)
                PlayPendingMovement(now);

            TickMovement(now);

            if (_skyRunning)
                StepSky();

            if (_countdownEnd >= 0f)
                StepCountdown();
        }

        // The world is going away: everything put back now, while the objects still exist.
        internal static void EndAll()
        {
            _throwAt = -1f;
            _throwPlayer = null;
            _speedUntil = _jumpUntil = _sleepUntil = -1f;
            SetAsleep(false);
            RestoreMovement();
            _countdownEnd = -1f;
            _countdownPlayer = null;

            if (_skyRunning)
            {
                // Never leave the clock frozen part way.
                _skyRunning = false;
                try { SkyManager.ClearFixedTime(); }
                catch (Exception) { }
            }

            ActiveMasks.Clear();
        }

        internal static string Describe()
        {
            var now = Time.unscaledTime;
            return $"masked={LocalMasked} ({ActiveMasks.Count}), "
                   + $"throw in {(_throwAt >= 0f ? (_throwAt - now).ToString("F1") + "s" : "-")}, "
                   + $"speed {(_speedUntil >= 0f ? (_speedUntil - now).ToString("F0") + "s left" : "off")}, "
                   + $"jump {(_jumpUntil >= 0f ? (_jumpUntil - now).ToString("F0") + "s left" : "off")}, "
                   + $"sleep {(_sleepUntil >= 0f ? (_sleepUntil - now).ToString("F0") + "s left" : "off")}, "
                   + $"sky {(_skyRunning ? $"fast forward to {_skyTo:F2}h" : "free")}, "
                   + $"hour {SafeHour():F2}";
        }

        private static float SafeHour()
        {
            try
            {
                return SkyManager.GetCurrentTime();
            }
            catch (Exception)
            {
                return -1f;
            }
        }

        // ------------------------------------------------------------------
        // The wait before a teleport (player, 2026-10-05): three seconds, then the teleport. No
        // text on screen and no sound (player: a beep made in Unity was not heard in the game).
        // ------------------------------------------------------------------

        // The tomato is set going 0.3 s after it is handed over and pops 3 s later
        // (GadgetItemSpawner.TomatoSeconds); the teleport comes a second after the pop, so the pop
        // is heard where the player stood (player, 2026-10-06).
        private const float CountdownSeconds = 0.3f + GadgetItemSpawner.TomatoSeconds + 1f;
        private static float _countdownEnd = -1f;
        private static string _countdownName;
        private static PlayerCharacter _countdownPlayer;
        private static Vector3 _countdownDestination;
        private static Quaternion _countdownRotation;

        private static void StartCountdown(string key, PlayerCharacter player, Vector3 destination, Quaternion rotation)
        {
            _countdownName = DisplayName(key);
            _countdownPlayer = player;
            _countdownDestination = destination;
            _countdownRotation = rotation;
            _countdownEnd = Time.unscaledTime + CountdownSeconds;
        }

        private static void StepCountdown()
        {
            if (Time.unscaledTime < _countdownEnd)
                return;

            _countdownEnd = -1f;
            var player = _countdownPlayer;
            _countdownPlayer = null;
            try
            {
                WorldButtons.TeleportTo(player, _countdownDestination, _countdownRotation);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {_countdownName}: the teleport failed: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Hands
        // ------------------------------------------------------------------

        private static void DropHeld(PlayerCharacter player)
        {
            if (player.hands == null || !player.hands.isHoldingSomething)
            {
                Plugin.Log.LogInfo($"{Tag} Big Drop: hands already empty.");
                return;
            }

            player.decisions?.ClearWindUp();
            player.actions.ActionDropPropOrPlayer();
        }

        private static void StartThrow(PlayerCharacter player)
        {
            if (player.hands == null || !player.hands.isHoldingSomething)
            {
                Plugin.Log.LogInfo($"{Tag} Big Throw: nothing in the hands to throw.");
                return;
            }

            _throwWindUpStart = NetworkTime.time;
            player.decisions.SetWindUpStartTime(_throwWindUpStart);
            _throwPlayer = player;
            _cancelRefusedLogged = false;
            var windUp = player.tunings != null ? Mathf.Max(0f, player.tunings.maxWindUpDuration) : 1f;
            _throwAt = Time.unscaledTime + windUp + ThrowHoldSeconds;
        }

        // The throw is forced (player, 2026-10-05): the game's cancel (right click) clears the
        // wind-up, and every frame until the throw it is put back as it was.
        private static void HoldWindUp()
        {
            var player = _throwPlayer;
            try
            {
                if (player == null || player.decisions == null || player.hands == null || !player.hands.isHoldingSomething)
                    return;

                if (player.decisions.localWindUpStartTime == _throwWindUpStart)
                    return;

                player.decisions.SetWindUpStartTime(_throwWindUpStart);
                if (!_cancelRefusedLogged)
                {
                    _cancelRefusedLogged = true;
                    Plugin.Log.LogInfo($"{Tag} Big Throw: the wind-up was cancelled or changed; put back.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"{Tag} Big Throw: could not hold the wind-up ({ex.Message}).");
            }
        }

        private static void FinishThrow()
        {
            var player = _throwPlayer;
            _throwAt = -1f;
            _throwPlayer = null;

            try
            {
                if (player == null || player.hands == null)
                    return;

                // Let go before the five seconds (or never picked up): the wind-up goes.
                if (!player.hands.isHoldingSomething)
                {
                    player.decisions?.ClearWindUp();
                    Plugin.Log.LogInfo($"{Tag} Big Throw: the hands were empty by the end of the wind-up.");
                    return;
                }

                player.actions.ActionDropPropOrPlayer();
                Plugin.Log.LogInfo($"{Tag} Big Throw: thrown.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Throw failed at the end of the wind-up: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Movement: Big Speed, Big Jump, Big Sleep
        // ------------------------------------------------------------------

        // All three change the same PlayerTunings, so none of them scales and puts back on its
        // own: the player's own values are read once, and every frame while any is running the
        // tunings are written as those values times what is running (asleep beats faster). An
        // effect ending can then never leave a player faster, slower or stuck for good, whatever
        // order they come and go in. A second one of a kind lengthens it, never stacks.
        private static void StartMovement(PlayerCharacter player, int seconds, string key)
        {
            var tunings = player.tunings;
            if (tunings == null)
            {
                Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: the player has no tunings.");
                return;
            }

            if (_moveTunings != null && _moveTunings.Pointer != tunings.Pointer)
                RestoreMovement();

            if (_moveTunings == null)
            {
                _moveTunings = tunings;
                _moveBase = new[]
                {
                    tunings.forwardSpeed, tunings.forwardSprintSpeed, tunings.crouchForwardSpeed,
                    tunings.crouchForwardSprintSpeed, tunings.swimForwardSpeed, tunings.swimForwardSprintSpeed,
                    tunings.forwardSprintGhostSpeed, tunings.jumpForce, tunings.maxUpwardsVelocity,
                    tunings.mouseLookSpeed, tunings.stickLookSpeed,
                };
            }

            var until = Time.unscaledTime + seconds;
            switch (key)
            {
                case Speed:
                    _speedUntil = until;
                    break;
                case Jump:
                    _jumpUntil = until;
                    break;
                case Sleep:
                    if (_sleepUntil < 0f)
                        _sleepStart = Time.unscaledTime;
                    _sleepUntil = until;
                    _sleeper = player;
                    break;
            }

            ApplyMovement();
        }

        private static void PlayPendingMovement(float now)
        {
            var player = Debug.DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
                return;

            foreach (var pair in new List<KeyValuePair<string, float>>(PendingMovement))
            {
                var left = Mathf.CeilToInt(pair.Value - now);
                if (left > 0)
                {
                    StartMovement(player, left, pair.Key);
                    Plugin.Log.LogInfo($"{Tag} {DisplayName(pair.Key)} played now that the player is here ({left}s left).");
                }
            }

            PendingMovement.Clear();
        }

        private static void TickMovement(float now)
        {
            if (_speedUntil >= 0f && now >= _speedUntil)
            {
                _speedUntil = -1f;
                Plugin.Log.LogInfo($"{Tag} Big Speed over.");
            }

            if (_jumpUntil >= 0f && now >= _jumpUntil)
            {
                _jumpUntil = -1f;
                Plugin.Log.LogInfo($"{Tag} Big Jump over.");
            }

            if (_sleepUntil >= 0f && now >= _sleepUntil)
            {
                _sleepUntil = -1f;
                SetAsleep(false);
                Plugin.Log.LogInfo($"{Tag} Big Sleep over.");
            }

            if (_moveTunings == null)
                return;

            if (_speedUntil < 0f && _jumpUntil < 0f && _sleepUntil < 0f)
                RestoreMovement();
            else
                ApplyMovement();
        }

        private static void ApplyMovement()
        {
            var t = _moveTunings;
            var b = _moveBase;
            if (t == null || b == null)
                return;

            try
            {
                // Asleep for AsleepSeconds out of every AsleepSeconds + AwakeSeconds of Big Sleep.
                var frozen = _sleepUntil >= 0f
                             && (Time.unscaledTime - _sleepStart) % (AsleepSeconds + AwakeSeconds) < AsleepSeconds;
                SetAsleep(frozen);
                var move = frozen ? 0f : _speedUntil >= 0f ? SpeedFactor : 1f;
                var jump = frozen ? 0f : _jumpUntil >= 0f ? JumpFactor : 1f;
                t.forwardSpeed = b[0] * move;
                t.forwardSprintSpeed = b[1] * move;
                t.crouchForwardSpeed = b[2] * move;
                t.crouchForwardSprintSpeed = b[3] * move;
                t.swimForwardSpeed = b[4] * move;
                t.swimForwardSprintSpeed = b[5] * move;
                t.forwardSprintGhostSpeed = b[6] * move;
                t.jumpForce = b[7] * jump;
                t.maxUpwardsVelocity = frozen ? b[8] : b[8] * jump;

                // Asleep, the camera does not move either (player, 2026-10-05: looking round woke
                // the walker up).
                t.mouseLookSpeed = frozen ? 0f : b[9];
                t.stickLookSpeed = frozen ? 0f : b[10];
            }
            catch (Exception ex)
            {
                // The player object went with its world.
                Plugin.Log.LogInfo($"{Tag} Movement effects dropped (the player is gone: {ex.Message}).");
                _moveTunings = null;
                _moveBase = null;
            }
        }

        // The game's own sleep (PlayerSleeper.forceSleeping): the walker nods off, as when left
        // alone, for the asleep part of Big Sleep.
        private static bool _asleep;

        // Whether Big Sleep has this machine's walker asleep right now (Patches/TrapPatches: no
        // crouching meanwhile either).
        internal static bool Asleep => _asleep;

        private static void SetAsleep(bool asleep)
        {
            if (_asleep == asleep)
                return;

            _asleep = asleep;
            try
            {
                if (_sleeper != null && _sleeper.sleeper != null)
                {
                    _sleeper.sleeper.forceSleeping = asleep;

                    // Nearly asleep at once, not after the game's slow nod-off, so the picture and
                    // the stillness come together (player, 2026-10-05). Not all the way: the game
                    // only moves the animation when the sleepiness changes from one frame to the
                    // next, so set to its target it played the sound and never the animation.
                    if (asleep)
                        _sleeper.sleeper.smoothSleepiness = Mathf.Max(_sleeper.sleeper.smoothSleepiness, 0.85f);
                    else
                        // forceSleeping zeroed the idle clock: without a fresh action the game
                        // would keep the walker asleep until a button is pressed.
                        _sleeper.sleeper.RecordAction();
                }
            }
            catch (Exception)
            {
                // The player went with its world.
            }

            if (!asleep && _sleepUntil < 0f)
                _sleeper = null;
        }

        private static void RestoreMovement()
        {
            var t = _moveTunings;
            var b = _moveBase;
            _moveTunings = null;
            _moveBase = null;
            if (t == null || b == null)
                return;

            try
            {
                t.forwardSpeed = b[0];
                t.forwardSprintSpeed = b[1];
                t.crouchForwardSpeed = b[2];
                t.crouchForwardSprintSpeed = b[3];
                t.swimForwardSpeed = b[4];
                t.swimForwardSprintSpeed = b[5];
                t.forwardSprintGhostSpeed = b[6];
                t.jumpForce = b[7];
                t.maxUpwardsVelocity = b[8];
                t.mouseLookSpeed = b[9];
                t.stickLookSpeed = b[10];
            }
            catch (Exception)
            {
                // The player object went with its world: nothing left to put back.
            }
        }

        // ------------------------------------------------------------------
        // Sky
        // ------------------------------------------------------------------

        // The clock fast-forwards to the hour (never backwards: it goes round to it), then runs on
        // from there. Nothing is put back afterwards: the day has moved (player, 2026-10-05).
        // A second one during the first starts again from wherever the sky has got to.
        private static void StartSky(float hour)
        {
            _skyFrom = SkyManager.GetCurrentTime();
            _skyTo = hour;
            _skySpan = Mathf.Repeat(hour - _skyFrom, 24f);
            _skyStart = Time.unscaledTime;
            _skyRunning = true;
            Plugin.Log.LogInfo($"{Tag} Sky: fast forward from {_skyFrom:F2}h to {hour:F2}h.");
        }

        private static void StepSky()
        {
            var t = Mathf.Clamp01((Time.unscaledTime - _skyStart) / FastForwardSeconds);
            try
            {
                // Eased, so the sun speeds up and settles rather than jerking.
                var eased = t * t * (3f - 2f * t);
                SkyManager.SetFixedTime(Mathf.Repeat(_skyFrom + _skySpan * eased, 24f));
                if (t < 1f)
                    return;

                _skyRunning = false;
                SkyManager.ClearFixedTime();
                Plugin.Log.LogInfo($"{Tag} Sky at {_skyTo:F2}h; the clock runs on from there.");
            }
            catch (Exception ex)
            {
                _skyRunning = false;
                Plugin.Log.LogInfo($"{Tag} Sky fast forward stopped (no sky now: {ex.Message}).");
            }
        }
    }
}
