namespace BigWalkArchipelago.Core.Net
{
    // Latches the two end-of-game flags the moment the game first writes a
    // non-zero value to them, instead of reading them back later.
    //
    // This exists because of EndingGate specifically. It is not a "check
    // accomplished" flag at all: it is the chapel door's live open/closed
    // state, reused to drive the door animation, and it was observed going
    // back to 0 the instant the door finished opening (in-game, 2026-09-10).
    // Polling it would therefore miss the goal on most frames and catch it on
    // none. GauntletComplete looks stable by comparison, but is latched the
    // same way so the goal logic has one rule rather than two.
    //
    // Written from SaveValuePatch, which already sees every SaveManager write.
    internal static class ApGoalFlags
    {
        private const string KeyPrefix = "ap_flag_";

        internal static void Latch(SavableSystem system)
        {
            if (system != SavableSystem.EndingGate && system != SavableSystem.GauntletComplete)
                return;

            var key = KeyPrefix + system;
            if (SaveManager.GetIntValue(key, 0, false) != 0)
                return;

            SaveManager.SetIntValue(key, 1);
            Plugin.Log.LogInfo($"[{nameof(ApGoalFlags)}] {system} reached (latched as {key}).");
        }

        internal static bool IsLatched(SavableSystem system)
        {
            return SaveManager.GetIntValue(KeyPrefix + system, 0, false) != 0;
        }
    }
}
