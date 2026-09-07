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
        internal static ConfigEntry<KeyboardShortcut> UnlockNextKey;
        internal static ConfigEntry<KeyboardShortcut> SimulateReceivedItemKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyKey;

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

            UnlockNextKey = file.Bind(
                "Debug",
                "UnlockNextKey",
                new KeyboardShortcut(KeyCode.F3),
                "Débloque de force le prochain gourd/big key verrouillé de la zone actuelle, sans résoudre l'énigme (n'a d'effet que si Debug.Enabled est actif).");

            SimulateReceivedItemKey = file.Bind(
                "Debug",
                "SimulateReceivedItemKey",
                new KeyboardShortcut(KeyCode.F4),
                "Simule la réception à distance d'un item Archipelago pour le prochain gourd/big key verrouillé de la zone actuelle (n'a d'effet que si Debug.Enabled est actif).");

            DumpNearbyKey = file.Bind(
                "Debug",
                "DumpNearbyKey",
                new KeyboardShortcut(KeyCode.F5),
                "Logue les RewardGourd les plus proches du joueur (nom + état + distance), quel que soit leur état — diagnostic pour voir ce qui est réellement présent autour de soi (n'a d'effet que si Debug.Enabled est actif).");
        }
    }
}
