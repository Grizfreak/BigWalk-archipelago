using System;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Patches
{
    // Big Run puts back the speeds it saved on its key: saved during a trap, they stayed for good.
    internal static class BigRunCompat
    {
        private const string RunnerType = "bigrun.bigrunner";
        private const string PluginType = "bigrun.runnnnnnn";

        private static bool _applied;
        private static Harmony _harmony;
        private static KeyboardShortcut? _key;

        // Big Run may load before or after this mod.
        internal static void TryApply(Harmony harmony)
        {
            _harmony = harmony;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                TryApply(assembly);
            if (!_applied)
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) => TryApply(args.LoadedAssembly);

        private static void TryApply(Assembly assembly)
        {
            if (_applied)
                return;

            try
            {
                var runner = assembly.GetType(RunnerType, false);
                var target = runner != null ? AccessTools.Method(runner, "Postfix") : null;
                if (target == null)
                    return;

                _applied = true;
                if (AccessTools.Field(assembly.GetType(PluginType, false), "keytotoggle")?.GetValue(null) is ConfigEntry<KeyCode> key)
                    _key = new KeyboardShortcut(key.Value);
                _harmony.Patch(target, prefix: new HarmonyMethod(typeof(BigRunCompat), nameof(Prefix)));
                Plugin.Log.LogInfo($"[{nameof(BigRunCompat)}] Big Run found: its key waits while a movement trap runs.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"[{nameof(BigRunCompat)}] Big Run left as it is: {ex.Message}");
            }
        }

        private static bool Prefix(PlayerCharacter __0)
        {
            if (!TrapEffects.MovementActive)
                return true;

            if (__0 != null && __0.isLocalPlayer && _key?.IsDown() == true)
                ApNotices.Post("Big Run waits for the trap to end", warning: true);
            return false;
        }
    }
}
