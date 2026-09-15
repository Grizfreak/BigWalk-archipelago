using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Manual trigger for Core/VariantGourdRevealer, to test without waiting
    // for the next Core/VariantGourdMapUnlocker poll.
    internal static class DebugVariantGourdReveal
    {
        internal static void RevealAll()
        {
            var count = VariantGourdRevealer.RevealAll();
            Plugin.Log.LogInfo($"[{nameof(DebugVariantGourdReveal)}] {count} variant challenge gourd(s) revealed on the map.");
        }
    }
}
