namespace BigWalkArchipelago.Debug
{
    // Bascule la caméra libre en réutilisant tel quel le système de "cheat
    // camera" déjà présent dans le jeu (PlayerCharacter.cheater.cameraCheatMover,
    // investigué via Ghidra le 2026-09-03 : Detach()/Attach() sont publiques et
    // suffisent, pas besoin de connaître le code de triche texte du jeu ni de
    // réimplémenter le mouvement depuis PlayerMover/Rigidbody).
    internal static class DebugFlightTool
    {
        private static bool _active;

        internal static bool IsActive => _active;

        internal static void Toggle()
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugFlightTool)}] Aucun joueur local trouvé.");
                return;
            }

            var cheater = localPlayer.cheater;
            if (cheater == null)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugFlightTool)}] Le joueur local n'a pas de PlayerCheater.");
                return;
            }

            var cameraCheatMover = cheater.cameraCheatMover;
            if (cameraCheatMover == null)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugFlightTool)}] cheater.cameraCheatMover est introuvable.");
                return;
            }

            _active = !_active;
            if (_active)
                cameraCheatMover.Detach();
            else
                cameraCheatMover.Attach();

            Plugin.Log.LogInfo($"[{nameof(DebugFlightTool)}] Caméra libre {(_active ? "activée" : "désactivée")}.");
        }
    }
}
