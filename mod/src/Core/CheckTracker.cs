namespace BigWalkArchipelago.Core
{
    // Deduplication shared by every code path that can detect the same
    // check. Confirmed necessary during a test session on 2026-09-03:
    // RewardGourd.ServerSetGourdState(Loose) AND
    // PeckEffectSavableHome.Peck() (on the "vice launch switch") both write
    // to SaveManager.SetIntValue for the same gourd.
    //
    // Persisted in SaveManager (not a simple in-memory HashSet): confirmed
    // in testing on 2026-09-07, an in-memory HashSet only protects WITHIN
    // the current session. On zone load, the game re-asserts the state of
    // an already-resolved gourd via (at least) two independent paths —
    // Prop.Start() re-pinning the prop, AND a repeat call to
    // RewardGourd.ServerSetGourdState(Loose) — each one re-triggering
    // GourdStatePatch/SaveValuePatch on every game restart if the
    // deduplication doesn't survive the session. The "ap_reported_" prefix
    // avoids any collision with the game's normal
    // SaveablePropName/SaveableHomeName keys.
    internal static class CheckTracker
    {
        private const string KeyPrefix = "ap_reported_";

        internal static bool TryMarkReported(string locationId)
        {
            var key = KeyPrefix + locationId;
            if (SaveManager.GetIntValue(key, 0, false) != 0)
                return false;

            SaveManager.SetIntValue(key, 1);
            return true;
        }
    }
}
