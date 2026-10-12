using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The two sounds of Archipelago on this machine (ROADMAP U12, player 2026-10-09): an item
    // received, and a check sent. Both are the game's own clips (a beep made in Unity was not
    // heard in the game, TrapEffects), played through its audio system where the local player
    // stands. One sound stands for a burst: after one, none for BurstSeconds, so forty items
    // arriving on connection, or a few checks in a row, are one sound and not a rattle.
    internal static class ApSounds
    {
        // A small cowbell for an item received (player, 2026-10-11), and the
        // sound of a plinth opening for a check sent. Both can be changed in the .cfg while the right ones are found.
        private const string ReceivedDefault = "sfx_prop_cowbell_small_clang_random";
        private const string CheckDefault = "sfx_gourd_plinth_open_end_random";

        private static string ReceivedClip => Clip(ModConfig.ReceivedSound.Value, ReceivedDefault);
        private static string CheckClip => Clip(ModConfig.CheckSound.Value, CheckDefault);

        private static string Clip(string configured, string fallback) =>
            string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

        private const float BurstSeconds = 15f;

        private static readonly Dictionary<string, AudioAsset> Found = new();
        private static float _lastReceived = -100f;
        private static float _lastCheck = -100f;

        // For the debug key: the next sound plays whatever just played.
        internal static void ForgetBursts()
        {
            _lastReceived = -100f;
            _lastCheck = -100f;
            _lastDeathLinkSound = -100f;
        }

        // Plays a clip by name, whatever the burst window: the debug key's candidates.
        internal static bool PlayNamed(string clip)
        {
            try
            {
                var player = Debug.DebugPlayerLookup.FindLocalPlayer();
                var asset = Find(clip);
                if (player == null || asset == null)
                    return false;
                AudioPlayHelper.Play(asset, player.transform.position);
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApSounds)}] {clip} did not play: {ex.Message}");
                return false;
            }
        }

        // An objective done (the game's own confetti), and a DeathLink sent or received (a large cowbell, the player,
        // 2026-10-12). Not held to a burst: each is rare.
        internal static void Confetti()
        {
            Counts[2]++;
            PlayAlways("sfx_challengeWin_confetti_random");
        }

        private static float _lastDeathLinkSound = -100f;

        internal static void DeathLinkAlert()
        {
            Counts[3]++;
            if (Time.unscaledTime - _lastDeathLinkSound < 2f)
                return;

            _lastDeathLinkSound = Time.unscaledTime;
            PlayAlways("sfx_prop_cowbell_large_clang_random");
        }

        private static void PlayAlways(string clip)
        {
            if (!ModConfig.ApSounds.Value)
            {
                Plugin.Log.LogInfo($"[{nameof(ApSounds)}] {clip} not played: the sounds are off in the settings.");
                return;
            }

            var played = PlayNamed(clip);
            Plugin.Log.LogInfo($"[{nameof(ApSounds)}] {clip}: {(played ? "played" : "NOT played")}.");
        }

        // What the host played, counted by kind (received, check, confetti, DeathLink), for the guests: the snapshot carries the
        // counts, and a guest plays a sound when its count moved (the player, 2026-10-12: every sound on the guests too).
        internal static readonly byte[] Counts = new byte[4];

        internal static void PlayForGuest(int kind)
        {
            switch (kind)
            {
                case 0:
                    Play(ReceivedClip, ref _lastReceived);
                    break;
                case 1:
                    Play(CheckClip, ref _lastCheck);
                    break;
                case 2:
                    PlayAlways("sfx_challengeWin_confetti_random");
                    break;
                default:
                    if (Time.unscaledTime - _lastDeathLinkSound >= 2f)
                    {
                        _lastDeathLinkSound = Time.unscaledTime;
                        PlayAlways("sfx_prop_cowbell_large_clang_random");
                    }

                    break;
            }
        }

        internal static void Received()
        {
            Counts[0]++;
            Play(ReceivedClip, ref _lastReceived);
        }

        // A puzzle's own check already plays this when it is solved: only the others (a pack, a
        // firework, a deposit...) call here.
        internal static void CheckSent()
        {
            Counts[1]++;
            Play(CheckClip, ref _lastCheck);
        }

        private static void Play(string clip, ref float last)
        {
            if (!ModConfig.ApSounds.Value || Time.unscaledTime - last < BurstSeconds)
                return;

            try
            {
                var player = Debug.DebugPlayerLookup.FindLocalPlayer();
                var asset = Find(clip);
                if (player == null || asset == null)
                    return;

                last = Time.unscaledTime;
                AudioPlayHelper.Play(asset, player.transform.position);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApSounds)}] {clip} did not play: {ex.Message}");
            }
        }

        private static AudioAsset Find(string clip)
        {
            if (Found.TryGetValue(clip, out var known) && known != null)
                return known;

            foreach (var asset in Resources.FindObjectsOfTypeAll<AudioAsset>())
            {
                if (asset != null && asset.name == clip)
                {
                    Found[clip] = asset;
                    return asset;
                }
            }

            Plugin.Log.LogWarning($"[{nameof(ApSounds)}] No audio asset called '{clip}' in memory.");
            return null;
        }
    }
}
