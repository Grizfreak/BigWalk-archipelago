using System.Text;
using BigWalkArchipelago.Core.Net;
using HarmonyLib;
using UnityEngine;

namespace BigWalkArchipelago.Patches
{
    // The two ways the game STARTS an ending, as opposed to finishing one.
    //
    // WHY THIS EXISTS, and it is worth being precise because two hooks have
    // already failed here. `EndingTransition.SetActive()` carries no address
    // in the IL2CPP dump, the signature of an inlined method, so it was never
    // a candidate. `EndingTransition.OnTransitionEnd()` does carry one, and
    // Harmony patched it happily — and in game on 2026-09-22 the player
    // reached the ending behind the Hub Secret Door, watched a 30-second
    // fade, landed back on the main menu, and the log held nothing at all.
    // A method having an address does not mean anything calls it: `Update()`
    // almost certainly carries an inlined copy of its body, exactly the trap
    // `BroadcastStation.Unlock` sprang on the radio work.
    //
    // So these hook the START of the transition instead, where the game has
    // two distinct entry points and both carry addresses:
    //
    //   - `PeckEffectEndingTransition.OnPeck` — an ending begun by a peck.
    //     The Hub Secret Door's two buttons are peck-driven, so this is the
    //     one expected to fire for the second ending.
    //   - `AutomaticDisconnector.StartEndingTransition` — an ending begun by
    //     walking into a `PlayerZone`. Logged, not acted on: nobody has seen
    //     which ending uses which.
    //
    // Both log the full hierarchy path of the object they fired on. That is
    // the identity nothing else gives us, and it is what a later pass will
    // use to tell the two endings apart properly.
    internal static class EndingStartPatch
    {
        [HarmonyPatch(typeof(PeckEffectEndingTransition), "OnPeck")]
        internal static class ByPeck
        {
            private static void Prefix(PeckEffectEndingTransition __instance)
            {
                if (__instance == null)
                    return;

                ApSecondEnding.OnEndingStartedByPeck(PathOf(__instance.gameObject));
            }
        }

        [HarmonyPatch(typeof(AutomaticDisconnector), nameof(AutomaticDisconnector.StartEndingTransition))]
        internal static class ByZone
        {
            private static void Prefix(AutomaticDisconnector __instance)
            {
                if (__instance == null)
                    return;

                ApSecondEnding.OnEndingStartedByZone(PathOf(__instance.gameObject));
            }
        }

        // "A/B/C" from the scene root. Object names alone repeat all over
        // this game — `ButtonSystem`, `OpenSystem` and `NHoldLogic 2` each
        // exist many times over — so the parent chain is the only thing that
        // identifies one of them in a log.
        private static string PathOf(GameObject go)
        {
            if (go == null)
                return "<null>";

            var path = new StringBuilder(go.name);
            var parent = go.transform != null ? go.transform.parent : null;
            while (parent != null)
            {
                path.Insert(0, parent.gameObject.name + "/");
                parent = parent.parent;
            }

            return path.ToString();
        }
    }
}
