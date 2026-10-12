using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Measuring pass for U12 (ROADMAP, player 2026-10-09): which clip a gourd dropped into a monument,
    // a puzzle's end or a click really plays. Every sound the game asks its audio system for goes
    // through AudioPlayHelper (or a peck effect's PlayAudioAsset, which ends there), so a prefix on
    // each writes the asset's name, the object that asked and where, to BepInEx/audio-played.tsv,
    // while Debug.Enabled is active. The same asset within half a second is written once (a
    // footstep or an ambience would flood the file). Do the action in game, read the last lines.
    internal static class DebugAudioLog
    {
        // Set while the cycling keys play a clip, so the test is not logged as a game sound.
        internal static bool Mute;

        private static readonly Dictionary<string, float> Last = new();
        private static string _file;

        internal static void Write(string source, AudioAsset asset, Vector3 position, UnityEngine.Object owner)
        {
            if (Mute || asset == null || !ModConfig.DebugModeEnabled.Value)
                return;
            try
            {
                DebugAudioProbe.Remember(asset);
                var now = Time.unscaledTime;
                if (Last.TryGetValue(asset.name, out var before) && now - before < 0.5f)
                    return;
                Last[asset.name] = now;

                _file ??= Path.Combine(BepInEx.Paths.BepInExRootPath, "audio-played.tsv");
                var who = owner != null ? owner.name : "";
                File.AppendAllText(_file,
                    $"{now:0.0}\t{source}\t{asset.name}\t{who}\t{position.x:0.#},{position.y:0.#},{position.z:0.#}\n");
            }
            catch (System.Exception)
            {
                // A diagnostic: never let it break a sound.
            }
        }
    }

    [HarmonyPatch(typeof(AudioPlayHelper), nameof(AudioPlayHelper.Play))]
    internal static class DebugAudioPlayPatch
    {
        private static void Prefix(AudioAsset audioAsset, Vector3 worldPosition, UnityEngine.Object owner) =>
            DebugAudioLog.Write("Play", audioAsset, worldPosition, owner);
    }

    [HarmonyPatch(typeof(AudioPlayHelper), nameof(AudioPlayHelper.CreateEventAndPlay))]
    internal static class DebugAudioCreateEventPatch
    {
        private static void Prefix(AudioAsset audioAsset, Vector3 worldPosition, UnityEngine.Object owner) =>
            DebugAudioLog.Write("CreateEventAndPlay", audioAsset, worldPosition, owner);
    }
}
