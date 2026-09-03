using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using UnityEngine;

namespace BigWalkArchipelago
{
    // Point d'entrée unique pour désactiver le module Debug/ sans toucher au
    // reste : ModConfig.DebugModeEnabled.Value == false suffit (voir Plugin.Load()).
    // Nommée "ModConfig" (pas "Config") pour éviter un conflit avec la propriété
    // BasePlugin.Config héritée par Plugin : un simple "Config.Xxx" dans Plugin.cs
    // se serait résolu vers l'instance héritée, pas cette classe statique.
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> DebugModeEnabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleFlightKey;

        internal static void Bind(ConfigFile file)
        {
            DebugModeEnabled = file.Bind(
                "Debug",
                "Enabled",
                false,
                "Active les outils de debug (caméra libre, etc). À laisser désactivé en usage normal.");

            ToggleFlightKey = file.Bind(
                "Debug",
                "ToggleFlightKey",
                new KeyboardShortcut(KeyCode.F2),
                "Touche pour activer/désactiver la caméra libre (n'a d'effet que si Debug.Enabled est actif).");
        }
    }
}
