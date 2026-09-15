using System.Collections.Generic;
using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // CosmeticMonumentFillTracker needs to know about ALL the game's
    // PropHome instances, not just the ones already loaded at the time of a
    // single "on session startup" pass — confirmed in testing on
    // 2026-09-11: a monument close to the hub (monoumentIntroSlot0) worked
    // on the first try, but a distant monument (Black Tower,
    // monoumentFinalSlot4) received NO onChangeServer subscription at all
    // (total silence in the logs after a pin nonetheless confirmed
    // successful elsewhere) — the game clearly loads some PropHome instances
    // on the fly (at least partially), not all simultaneously at startup as
    // the unverified hypothesis assumed.
    //
    // PropHome.OnEnable() is the only guaranteed pass-through point for
    // EVERY instance, whether it exists from initial load or appears later.
    // Replaces the old single pass (PropHome.allPropHomes scanned once in
    // CosmeticMonumentFillTracker.Update()) with a subscription made
    // incrementally, instance by instance, as each one activates.
    [HarmonyPatch(typeof(PropHome), nameof(PropHome.OnEnable))]
    internal static class PropHomeEnablePatch
    {
        // Guard against a double subscription if OnEnable is called back more
        // than once on the same instance (deactivation/reactivation).
        private static readonly HashSet<PropHome> RegisteredHomes = new HashSet<PropHome>();

        private static void Postfix(PropHome __instance)
        {
            if (__instance == null || !RegisteredHomes.Add(__instance))
                return;

            CosmeticMonumentFillTracker.RegisterHome(__instance);
        }
    }
}
