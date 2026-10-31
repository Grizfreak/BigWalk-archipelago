using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Patches
{
    // A destroyed audio behaviour left registered halts AudioManager's loop.
    internal static class AudioBehaviourGuard
    {
        private const string Tag = "[" + nameof(AudioBehaviourGuard) + "]";

        private struct Entry
        {
            internal AudioUpdateType Type;
            internal IAudioBehaviour Behaviour;
            internal Component Owner;
            internal string Name;
        }

        private static readonly Dictionary<(IntPtr, AudioUpdateType), Entry> Registered = new();
        private static readonly List<(IntPtr, AudioUpdateType)> Dead = new();

        internal static void TryApply(Harmony harmony)
        {
            try
            {
                harmony.Patch(AccessTools.Method(typeof(AudioManager), nameof(AudioManager.RegisterBehaviour)),
                    postfix: new HarmonyMethod(typeof(AudioBehaviourGuard), nameof(RegisterPostfix)));
                harmony.Patch(AccessTools.Method(typeof(AudioManager), nameof(AudioManager.DeregisterBehaviour)),
                    postfix: new HarmonyMethod(typeof(AudioBehaviourGuard), nameof(DeregisterPostfix)));
                harmony.Patch(AccessTools.Method(typeof(CollisionSound), nameof(CollisionSound.OnDisable)),
                    postfix: new HarmonyMethod(typeof(AudioBehaviourGuard), nameof(CollisionSoundDisabled)));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"{Tag} Not applied on this build: {ex.Message}");
            }
        }

        private static void RegisterPostfix(AudioUpdateType type, IAudioBehaviour behaviour)
        {
            try
            {
                var owner = behaviour?.TryCast<Component>();
                if (owner == null)
                    return;
                Registered[(behaviour.Pointer, type)] = new Entry
                {
                    Type = type, Behaviour = behaviour, Owner = owner, Name = $"{owner.gameObject.name} ({owner.GetIl2CppType().Name})",
                };
            }
            catch (Exception)
            {
                // Noting it is a comfort: the game's own registration has already happened.
            }
        }

        private static void DeregisterPostfix(AudioUpdateType type, IAudioBehaviour behaviour)
        {
            if (behaviour != null)
                Registered.Remove((behaviour.Pointer, type));
        }

        // A rolling prop destroyed (the gourds a resync clears) left its LateUpdate behind.
        private static void CollisionSoundDisabled(CollisionSound __instance)
        {
            if (__instance == null)
                return;

            foreach (AudioUpdateType type in Enum.GetValues(typeof(AudioUpdateType)))
            {
                if (!Registered.TryGetValue((__instance.Pointer, type), out var entry))
                    continue;
                try
                {
                    AudioManager.DeregisterBehaviour(type, entry.Behaviour);
                    Plugin.Log.LogInfo($"{Tag} '{entry.Name}' switched off while in the audio {type} loop; taken out.");
                }
                catch (Exception)
                {
                    // Tick takes it out if this could not.
                }
            }
        }

        // Every couple of seconds (TeleportButtonRunner).
        internal static void Tick()
        {
            Dead.Clear();
            foreach (var pair in Registered)
            {
                if (pair.Value.Owner == null)
                    Dead.Add(pair.Key);
            }

            foreach (var key in Dead)
            {
                var entry = Registered[key];
                Registered.Remove(key);
                try
                {
                    AudioManager.DeregisterBehaviour(entry.Type, entry.Behaviour);
                    Plugin.Log.LogWarning($"{Tag} '{entry.Name}' was destroyed but still in the audio {entry.Type} loop; taken out.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} '{entry.Name}' is destroyed but could not be taken out: {ex.Message}");
                }
            }
        }
    }
}
