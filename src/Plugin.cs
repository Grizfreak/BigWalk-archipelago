using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BigWalkArchipelago.Core;
using HarmonyLib;

// NOTE IMPORTANT SUR LES RÉFÉRENCES :
// Ce projet référence l'assembly interop générée par BepInEx IL2CPP (via
// Il2CppInterop) au premier lancement du jeu avec BepInEx installé, jamais le
// DLL "dummy" généré par Il2CppInspectorRedux (utilisé uniquement pour la
// lecture statique du code, non invocable au runtime). Voir lib/README.md.

namespace BigWalkArchipelago
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.antonin.bigwalk.archipelago";
        public const string PluginName = "Big Walk Archipelago";
        public const string PluginVersion = "0.1.0";

        // BasePlugin.Log est une propriété d'INSTANCE (confirmé par réflexion sur
        // BepInEx.Unity.IL2CPP.dll) : on la masque volontairement (`new`) par un
        // champ statique pour pouvoir écrire Plugin.Log.LogInfo(...) depuis
        // n'importe quelle classe statique (patches Harmony, etc.) sans avoir à
        // se passer une instance de Plugin partout.
        internal static new ManualLogSource Log;

        // Point d'accroche unique pour les Patches/ : ils appellent
        // Plugin.Reporter.ReportCheck(id) sans connaître l'implémentation.
        internal static ICheckReporter Reporter { get; private set; }

        private Harmony _harmony;

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo($"{PluginName} v{PluginVersion} chargé.");

            ModConfig.Bind(base.Config);

            Reporter = new LocalLogReporter();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            AddComponent<Core.ArchDoorUnlocker>();
            AddComponent<Core.VariantGourdMapUnlocker>();
            AddComponent<Core.SecondEndingSphereUnlocker>();

            if (ModConfig.DebugModeEnabled.Value)
            {
                AddComponent<Debug.DebugHotkeys>();
                Log.LogInfo($"[Debug] Module de debug actif (touche : {ModConfig.ToggleFlightKey.Value}).");
            }

            Log.LogInfo("Harmony initialisé.");
        }
    }
}
