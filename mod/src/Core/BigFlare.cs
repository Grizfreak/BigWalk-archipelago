using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Big Flare (player, 2026-10-07): a flare gun's shot fired on each player, through the game's
    // own shot (PeckEffectParticleNetworked.RpcFire, the Rpc a real shot sends: every machine moves
    // the gun's FlareParticleSystem there and plays it), nothing copied, nothing new in the world.
    // The host borrows the island's flare guns, one per player (the same gun again a moment later
    // when there are more players than guns), and fires from above the head down onto them.
    //
    // Every machine, told by the trap (Core/TrapEffects): for a few seconds the guns' shots take
    // the Archipelago colours, running through them, then their own colours come back. A machine
    // with gentle effects on (ModConfig.GentleEffects, the accessibility setting) clears the shots
    // as they appear instead.
    internal static class BigFlare
    {
        private const string Tag = "[" + nameof(BigFlare) + "]";
        private const float Seconds = 6f;
        private const float FireDelay = 0.3f;
        private const float ReuseGap = 0.5f;
        private const double ColourStep = 0.25;

        // The Archipelago logo's colours.
        private static readonly Color[] Colours =
        {
            new(0.90f, 0.30f, 0.30f),
            new(0.98f, 0.58f, 0.22f),
            new(0.98f, 0.85f, 0.28f),
            new(0.40f, 0.82f, 0.40f),
            new(0.32f, 0.55f, 0.96f),
            new(0.70f, 0.42f, 0.92f),
        };

        private static float _until = -1f;
        private static int _shown = -1;
        private static readonly Dictionary<int, (ParticleSystem system, Color colour)> Saved = new();

        // Host: when each player's shot goes, and from which gun.
        private static readonly List<(float at, PeckEffectParticleNetworked gun, Vector3 position, Quaternion rotation)> Shots = new();

        // The shot's sound is not in RpcFire (that plays the particles only, 2026-10-09): it is a
        // PeckEffectAudio on the gun, state 1 playing sfx_prop_flaregun_flare_shoot_layer. Every
        // machine plays it itself, where each shot lands, as the shot goes.
        private static readonly List<(float at, Vector3 position)> Sounds = new();
        private static PeckEffectAudio _shootAudio;
        private static AudioAsset _shootAsset;
        private static PeckEffectAudio.PeckAudioOutcome _shootOutcome;
        private static readonly List<(GameObject at, float until)> SoundSpots = new();

        private static bool FindShootSound()
        {
            if (_shootAudio != null && _shootAsset != null)
                return true;

            foreach (var audio in UnityEngine.Object.FindObjectsByType<PeckEffectAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (audio == null || !audio.gameObject.name.StartsWith("FlareGunProp", StringComparison.Ordinal)
                    || audio.gameObject.name.Contains("(AP", StringComparison.Ordinal))
                    continue;
                var outcomes = audio.outcomesPerState;
                if (outcomes == null || outcomes.Length < 2)
                    continue;
                var action = outcomes[1].audioAction;
                var items = action != null ? action.Actions : null;
                if (items == null || items.Length == 0 || items[0].Asset == null)
                    continue;

                _shootAudio = audio;
                _shootAsset = items[0].Asset;
                _shootOutcome = outcomes[1];
                return true;
            }

            return false;
        }

        private static List<PeckEffectParticleNetworked> Guns()
        {
            var guns = new List<PeckEffectParticleNetworked>();
            foreach (var effect in UnityEngine.Object.FindObjectsByType<PeckEffectParticleNetworked>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (effect != null && effect.name == "FlareGunFlare" && effect.targetParticleSystem != null)
                    guns.Add(effect);
            }
            return guns;
        }

        // Host: the shots, one per player, a moment from now (once every machine has its colours).
        internal static void FireOn(IEnumerable<PlayerCharacter> players)
        {
            // Only a gun in play on the network (not the mod's inactive templates, whose netId
            // throws for want of an identity, 2026-10-07).
            var guns = Guns().FindAll(g =>
            {
                try
                {
                    return g.gameObject.activeInHierarchy && g.netIdentity != null && g.netIdentity.netId != 0;
                }
                catch (Exception)
                {
                    return false;
                }
            });
            if (guns.Count == 0)
            {
                Plugin.Log.LogInfo($"{Tag} No flare gun on the network to fire with.");
                return;
            }

            var i = 0;
            foreach (var player in players)
            {
                if (player == null)
                    continue;
                var gun = guns[i % guns.Count];
                var at = Time.unscaledTime + FireDelay + (i / guns.Count) * ReuseGap;
                // Close above the head: a shot keeps flying a little, less the shorter its fall (its speed
                // is out of the mod's reach: the engine's own setter is stripped, 2026-10-07).
                var position = player.transform.position + Vector3.up * 2.2f;
                Shots.Add((at, gun, position, Quaternion.LookRotation(Vector3.down, player.transform.forward)));
                i++;
            }

            Plugin.Log.LogInfo($"{Tag} {i} flare(s) to fire with {guns.Count} gun(s).");
        }

        // Every machine, as the trap plays: the colours, or the clearing, and the sounds to come.
        internal static void Play()
        {
            _until = Time.unscaledTime + Seconds;
            _shown = -1;

            var players = PlayerCharacter.allPlayerCharacters;
            if (players != null)
            {
                var i = 0;
                var many = Math.Max(1, Guns().Count);
                foreach (var player in players)
                {
                    if (player == null)
                        continue;
                    Sounds.Add((Time.unscaledTime + FireDelay + (i / many) * ReuseGap, player.transform.position + Vector3.up * 2.2f));
                    i++;
                }
            }

            if (ModConfig.GentleEffects.Value)
            {
                Plugin.Log.LogInfo($"{Tag} Gentle effects on this machine (Settings > Archipelago): the flares are not shown.");
                return;
            }

            foreach (var gun in Guns())
            {
                var system = gun.targetParticleSystem;
                var id = system.GetInstanceID();
                if (!Saved.ContainsKey(id))
                    Saved[id] = (system, system.main.startColor.m_ColorMax);
            }
        }

        // Every frame (TrapRunner).
        internal static void Tick()
        {
            if (Shots.Count > 0 && NetworkServer.active)
            {
                for (var i = Shots.Count - 1; i >= 0; i--)
                {
                    var shot = Shots[i];
                    if (Time.unscaledTime < shot.at)
                        continue;
                    Shots.RemoveAt(i);
                    try
                    {
                        if (shot.gun != null)
                            shot.gun.RpcFire(shot.position, shot.rotation);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"{Tag} A flare did not fire: {ex.Message}");
                    }
                }
            }

            TickSounds();

            if (_until < 0f)
                return;

            if (Time.unscaledTime >= _until)
            {
                _until = -1f;
                Restore();
                return;
            }

            if (ModConfig.GentleEffects.Value)
            {
                foreach (var gun in Guns())
                    gun.targetParticleSystem.Clear(true);
                return;
            }

            var step = (int)(Math.Floor(NetworkTime.time / ColourStep) % Colours.Length);
            if (step == _shown)
                return;
            _shown = step;
            foreach (var pair in Saved.Values)
            {
                if (pair.system != null)
                    FlareColours.Apply(pair.system, Colours[(step + Colours.Length) % Colours.Length]);
            }
        }

        private static void TickSounds()
        {
            for (var i = SoundSpots.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < SoundSpots[i].until)
                    continue;
                if (SoundSpots[i].at != null)
                    UnityEngine.Object.Destroy(SoundSpots[i].at);
                SoundSpots.RemoveAt(i);
            }

            for (var i = Sounds.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < Sounds[i].at)
                    continue;
                var position = Sounds[i].position;
                Sounds.RemoveAt(i);
                try
                {
                    if (!FindShootSound())
                    {
                        Plugin.Log.LogInfo($"{Tag} No flare gun sound to play.");
                        continue;
                    }

                    // A place of its own in the world to play it from.
                    var spot = new GameObject("Big Flare sound (AP)");
                    spot.transform.position = position;
                    _shootAudio.PlayAudioAsset(_shootAsset, _shootOutcome, spot.transform);
                    SoundSpots.Add((spot, Time.unscaledTime + 4f));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} A flare's sound did not play: {ex.Message}");
                }
            }
        }

        // The guns' own shot colours back (the flare's colour as it was, its smoke rebuilt from it),
        // and the seed's colours painted again over them (Core/ColourPainter).
        private static void Restore()
        {
            foreach (var pair in Saved.Values)
            {
                if (pair.system != null)
                    FlareColours.Apply(pair.system, pair.colour);
            }
            Saved.Clear();
            ColourPainter.Repaint();
        }

        internal static void Forget()
        {
            Shots.Clear();
            Sounds.Clear();
            Saved.Clear();
            _until = -1f;
        }
    }
}
