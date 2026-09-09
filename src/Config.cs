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
        internal static ConfigEntry<KeyboardShortcut> DumpMonumentHomesKey;
        internal static ConfigEntry<KeyboardShortcut> SimulateGreenZoneKeyKey;
        internal static ConfigEntry<KeyboardShortcut> SimulateBlueZoneKeyKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyKeywordKey;
        internal static ConfigEntry<KeyboardShortcut> DumpNearbyPeckDevHelperKey;
        internal static ConfigEntry<KeyboardShortcut> TriggerUnlocksKey;
        internal static ConfigEntry<KeyboardShortcut> TriggerGourdKey;
        internal static ConfigEntry<KeyboardShortcut> DumpSpawnHubGateKey;
        internal static ConfigEntry<KeyboardShortcut> DumpPeckSwitchTargetKey;
        internal static ConfigEntry<KeyboardShortcut> RevealVariantGourdsKey;

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

            DumpMonumentHomesKey = file.Bind(
                "Debug",
                "DumpMonumentHomesKey",
                new KeyboardShortcut(KeyCode.F6),
                "Logue tous les PropHome 'monoument*' réellement enregistrés dans la zone actuelle, avec leur état de remplissage (n'a d'effet que si Debug.Enabled est actif).");

            SimulateGreenZoneKeyKey = file.Bind(
                "Debug",
                "SimulateGreenZoneKeyKey",
                new KeyboardShortcut(KeyCode.F7),
                "Simule la réception directe de bigKeyGreenZone (téléphérique), sans recherche du prop le plus proche (n'a d'effet que si Debug.Enabled est actif).");

            SimulateBlueZoneKeyKey = file.Bind(
                "Debug",
                "SimulateBlueZoneKeyKey",
                new KeyboardShortcut(KeyCode.F8),
                "Simule la réception directe de bigKeyBlueZone (train), sans recherche du prop le plus proche (n'a d'effet que si Debug.Enabled est actif).");

            DumpNearbyKeywordKey = file.Bind(
                "Debug",
                "DumpNearbyKeywordKey",
                new KeyboardShortcut(KeyCode.F9),
                "Logue les GameObjects proches (nom ou composant contenant 'arch'/'door'/'switch'/'button'/'gate'/'peck'/'shortcut') avec leurs composants — diagnostic générique pour identifier un mécanisme sans classe C# dédiée connue (n'a d'effet que si Debug.Enabled est actif).");

            DumpNearbyPeckDevHelperKey = file.Bind(
                "Debug",
                "DumpNearbyPeckDevHelperKey",
                new KeyboardShortcut(KeyCode.F10),
                "Logue les PeckDevHelper proches avec leur UnlockRules assigné (unlocks/lights/chairlift/train/bell/tunnel/map/gourd) — diagnostic pour savoir quelle règle déclenche quel switch de triche (n'a d'effet que si Debug.Enabled est actif).");

            TriggerUnlocksKey = file.Bind(
                "Debug",
                "TriggerUnlocksKey",
                new KeyboardShortcut(KeyCode.F11),
                "Appelle PeckDevHelper.Trigger avec UnlockRules.unlocks=true (cheat déjà intégré au jeu) — test pour les Arch doors/HubGate (n'a d'effet que si Debug.Enabled est actif).");

            TriggerGourdKey = file.Bind(
                "Debug",
                "TriggerGourdKey",
                new KeyboardShortcut(KeyCode.F12),
                "Appelle PeckDevHelper.Trigger avec UnlockRules.gourd=true (cheat déjà intégré au jeu) — test pour les gourds violettes/variant challenges, normalement débloquées après une première fin de partie (n'a d'effet que si Debug.Enabled est actif).");

            DumpSpawnHubGateKey = file.Bind(
                "Debug",
                "DumpSpawnHubGateKey",
                new KeyboardShortcut(KeyCode.Insert),
                "Logue tous les TrackedPeckState de catégorie SpawnHubGate (les 3 Arch doors) avec leur SaveIdentity.saveGuid et la valeur SaveManager actuelle — diagnostic pour cibler directement ces 3 portes sans passer par PeckDevHelper.Trigger (n'a d'effet que si Debug.Enabled est actif).");

            DumpPeckSwitchTargetKey = file.Bind(
                "Debug",
                "DumpPeckSwitchTargetKey",
                new KeyboardShortcut(KeyCode.Delete),
                "Logue tous les PeckSwitch proches avec leur trackedStateSystem réel (le vrai TrackedPeckState modifié au peck, pas une supposition) et la clé SaveManager correspondante (n'a d'effet que si Debug.Enabled est actif).");

            RevealVariantGourdsKey = file.Bind(
                "Debug",
                "RevealVariantGourdsKey",
                new KeyboardShortcut(KeyCode.Home),
                "Révèle sur la carte tous les gourds 'variant challenge' (violettes/postgame) de la zone chargée, via GourdMap.refreshFlag (n'a d'effet que si Debug.Enabled est actif).");
        }
    }
}
