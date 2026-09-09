using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Déclenchement manuel de Core/VariantGourdRevealer, pour tester sans
    // attendre le prochain poll de Core/VariantGourdMapUnlocker.
    internal static class DebugVariantGourdReveal
    {
        internal static void RevealAll()
        {
            var count = VariantGourdRevealer.RevealAll();
            Plugin.Log.LogInfo($"[{nameof(DebugVariantGourdReveal)}] {count} gourd(s) variant challenge révélé(s) sur la carte.");
        }
    }
}
