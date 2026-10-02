using System;

namespace BigWalkArchipelago.Debug
{
    // Noon on demand, so the night does not eat a test session — player
    // request, 2026-10-02. First press: the game's own SkyManager.SetFixedTime
    // at 12:00, which also stops the clock (Enviro's `simulate` goes off, as
    // for the game's dev tool). Second press: ClearFixedTime, and the day goes
    // on from there. Local to this machine; nothing is written to the save.
    internal static class DebugDaylight
    {
        private const float Noon = 12f;

        private static bool _frozen;

        internal static void Toggle()
        {
            try
            {
                if (_frozen)
                {
                    SkyManager.ClearFixedTime();
                    _frozen = false;
                    Plugin.Log.LogInfo($"[{nameof(DebugDaylight)}] Clock released.");
                }
                else
                {
                    SkyManager.SetFixedTime(Noon);
                    _frozen = true;
                    Plugin.Log.LogInfo($"[{nameof(DebugDaylight)}] Clock fixed at {Noon:F0}:00.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugDaylight)}] Failed: {ex.Message}");
            }
        }
    }
}
