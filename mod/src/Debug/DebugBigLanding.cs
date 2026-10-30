using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Big Landing's three settings, written each second instead of patched in: its DLL crashes the loopback guest.
    internal static class DebugBigLanding
    {
        private static float _next;
        private static bool _logged;

        internal static void Tick()
        {
            if (!ModConfig.EmulateBigLanding.Value || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + 1f;

            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return;

            foreach (var player in players)
            {
                if (player == null)
                    continue;
                if (player.actions != null)
                {
                    player.actions.predictivePickup = true;
                    player.actions.predictiveDrop = true;
                }
                if (player.faller != null)
                    player.faller.ignoreFalling = true;
            }

            if (!_logged && players.Count > 0)
            {
                _logged = true;
                Plugin.Log.LogInfo($"[{nameof(DebugBigLanding)}] Big Landing emulated on {players.Count} player character(s).");
            }
        }
    }
}
