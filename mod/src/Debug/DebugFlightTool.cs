namespace BigWalkArchipelago.Debug
{
    // Toggles free camera by reusing as-is the "cheat camera" system already
    // present in the game (PlayerCharacter.cheater.cameraCheatMover,
    // investigated via Ghidra on 2026-09-03: Detach()/Attach() are public and
    // sufficient, no need to know the game's text cheat code nor to
    // reimplement movement from PlayerMover/Rigidbody).
    internal static class DebugFlightTool
    {
        private static bool _active;
        private static float _originalMovingSpeed;
        private static bool _hasOriginalMovingSpeed;

        internal static bool IsActive => _active;

        internal static void Toggle()
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugFlightTool)}] No local player found.");
                return;
            }

            var cheater = localPlayer.cheater;
            if (cheater == null)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugFlightTool)}] Local player has no PlayerCheater.");
                return;
            }

            var cameraCheatMover = cheater.cameraCheatMover;
            if (cameraCheatMover == null)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugFlightTool)}] cheater.cameraCheatMover not found.");
                return;
            }

            _active = !_active;
            if (_active)
            {
                cameraCheatMover.Detach();

                // movingSpeed is re-applied on every activation (not just
                // once at the game's Awake) to track changes to the
                // multiplier's config value without restarting the game.
                if (!_hasOriginalMovingSpeed)
                {
                    _originalMovingSpeed = cameraCheatMover.movingSpeed;
                    _hasOriginalMovingSpeed = true;
                }

                cameraCheatMover.movingSpeed = _originalMovingSpeed * ModConfig.FlightSpeedMultiplier.Value;
            }
            else
            {
                cameraCheatMover.Attach();

                if (_hasOriginalMovingSpeed)
                    cameraCheatMover.movingSpeed = _originalMovingSpeed;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugFlightTool)}] Free camera {(_active ? "enabled" : "disabled")}.");
        }
    }
}
