using System;

namespace BigWalkArchipelago.Core.Net
{
    // "Has the second ending been reached" — the state behind
    // `goal: second_ending`, latched by this mod because the game keeps no
    // record of it anywhere. Confirmed working end to end in game on
    // 2026-09-22, after three hooks that were not.
    //
    // WHAT ENDS THAT ZONE. Two buttons held together behind the Hub Secret
    // Door. They start an ending transition through a peck, which is what
    // Patches/EndingStartPatch catches, and the game returns to the main
    // menu — no SaveManager key is written anywhere along the way
    // (decompiled 2026-09-10: `AutomaticDisconnector.StartEndingTransition`,
    // `EndingTransition.SetActive`/`OnTransitionEnd` and
    // `PeckEffectEndingTransition.OnPeck` touch it nowhere). Hence a latch of
    // the mod's own, under a prefix the game's enum names can never produce.
    //
    // THE GOURD BESIDE THEM IS SCENERY, and it cost two runs to establish.
    // There is a 46th RewardGourd there, one more than this world has
    // puzzles, and it looked like the conclusion of the zone. The buttons
    // unlock its vise and it **stays in place**: `ServerSetGourdState` is
    // never called for it, and a roster taken afterwards still reads
    // `state=Locked`. Watching it is gone from the mod — and with it the
    // trap that the mod's own cosmetic gourds, which are `notSavable` too,
    // would have goaled a seed the first time the player was sent one.
    //
    // TWO HOOKS FAILED BEFORE THE RIGHT ONE, and both failures are worth
    // keeping. `EndingTransition.SetActive()` carries no address in the
    // IL2CPP dump, the signature of an inlined method, so it was never a
    // candidate. `EndingTransition.OnTransitionEnd()` does carry one and
    // Harmony patched it happily — and it was **never called**, through a
    // full ending watched in game. An address is not a call site: `Update()`
    // almost certainly holds an inlined copy of its body, the same trap
    // `BroadcastStation.Unlock` sprang on the radio work. That is why the
    // ending is identified by a hierarchy path here and not by
    // `EndingTransition.entryMode` / `WorldMenuManager.secondEndingTransition`,
    // which were the obvious discriminator and are never reached.
    internal static class ApSecondEnding
    {
        // Same prefix as ApGoalFlags' own latches, and for the same reason: a
        // key the game's enum names can never produce, so SaveValuePatch
        // never mistakes one of these for a check.
        private const string ReachedKey = "ap_flag_SecondEnding";

        // Matched against the hierarchy path of whatever starts an ending.
        // See OnEndingStartedByPeck for where this string came from.
        private const string SecondEndingPathMarker = "SecondGoodbye";

        internal static bool Reached => IsLatched(ReachedKey);

        // WHICH ENDING, measured rather than guessed. The run that worked
        // printed the path
        //
        //   LandmarksPlayerCount2/Contents/SecondGoodbye PlayerCount2/
        //   Positioner/GoToVoidSystem
        //
        // and `SecondGoodbye` is the game's own word for this ending —
        // `GoodbyeChapel` and `GoodbyeVoid` sit on the first one's path, so
        // the prefix is what separates them. Matching on it keeps the
        // Gauntlet's ending, which plays a cinematic of its own, from goaling
        // a `second_ending` slot.
        //
        // A peck-started ending that does NOT match is logged and left alone.
        // That is how the other one gets identified when somebody finishes it
        // with this build on: its path lands in the log, and nothing was
        // reported on the strength of a guess.
        internal static void OnEndingStartedByPeck(string path)
        {
            var second = path != null
                         && path.Contains(SecondEndingPathMarker, StringComparison.Ordinal);

            Plugin.Log.LogInfo(
                $"[{nameof(ApSecondEnding)}] An ending was started by a peck, on '{path}' — "
                + $"{(second ? "the SECOND ending" : "NOT the second ending, left alone")}. "
                + $"(EndingGate latched: {ApGoalFlags.IsLatched(SavableSystem.EndingGate)}, "
                + $"GauntletComplete latched: {ApGoalFlags.IsLatched(SavableSystem.GauntletComplete)})");

            if (second)
                Latch("started by a peck");
        }

        // An ending begun by walking into a zone rather than by a peck. Never
        // seen firing, and kept for that reason rather than in spite of it:
        // the second ending does not come through here, so this is where the
        // FIRST one's identity will turn up if it does.
        internal static void OnEndingStartedByZone(string path)
        {
            Plugin.Log.LogInfo(
                $"[{nameof(ApSecondEnding)}] An ending was started by a player zone, on '{path}'. "
                + "Recorded, not goaled.");
        }

        private static void Latch(string how)
        {
            if (IsLatched(ReachedKey))
                return;

            SaveManager.SetIntValue(ReachedKey, 1);
            Plugin.Log.LogInfo(
                $"[{nameof(ApSecondEnding)}] Second ending reached ({how}), latched as {ReachedKey}.");
        }

        private static bool IsLatched(string key)
        {
            return SaveManager.GetIntValue(key, 0, false) != 0;
        }
    }
}
