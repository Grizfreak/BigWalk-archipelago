using System.Collections.Generic;

namespace BigWalkArchipelago.Core.Net
{
    // Latches the two end-of-game flags when the game changes them during
    // play, instead of reading them back later.
    //
    // This exists because of EndingGate specifically. It is not a "check
    // accomplished" flag at all: it is the chapel door's live state, reused
    // to drive the door animation, and it was observed changing back on its
    // own the instant the door finished opening (in-game, 2026-09-10).
    // Polling it would therefore miss the goal on most frames and catch it
    // on none. GauntletComplete looks stable by comparison, but is handled
    // the same way so the goal logic has one rule rather than two.
    //
    // WHAT THE FIRST RULE GOT WRONG (found in co-op, 2026-09-15). It used to
    // latch on the first non-zero write, which fired on the very first frame
    // of every session: the game writes EndingGate at world load, and a save
    // dump taken on a save that had never been anywhere near the chapel
    // showed EndingGate=1. So a non-zero value does not mean "the ending was
    // reached" — it is simply the door's resting state, whatever that means
    // exactly. With goal=ending, the seed was therefore goaled the instant
    // the player pressed Continue, and the server auto-released everything.
    //
    // The rule now is "it CHANGED while the world was playable":
    //   - every write seen before WorldManager.isReadyForEffects is load
    //     noise and only records a baseline, never latches;
    //   - once the world is ready, a write whose value differs from that
    //     baseline is a real event.
    // MEASURED, same evening: EndingGate goes 1 -> 2 when the chapel opens.
    // It is not a boolean at all — it has at least three states, and its
    // RESTING state is already 1, which is the whole reason "first non-zero
    // write" fired on the first frame of every session. The goal was
    // reported to the server on that 1 -> 2 transition, end to end.
    //
    // The rule is deliberately left as "it changed" rather than hardcoded to
    // == 2: one observation is thin ground for a magic number, the door may
    // well have states nobody has seen, and "changed while playable" already
    // catches all of them. The value is recorded here because it cost a
    // co-op session to find, not because the code depends on it.
    //
    // GauntletComplete behaves differently and benignly: it is absent from a
    // fresh save, so there is no baseline and its first write during play
    // latches — which is what that flag means anyway.
    internal static class ApGoalFlags
    {
        private const string KeyPrefix = "ap_flag_";

        // Value each flag held while the world was loading, per loaded
        // world — cleared on unload rather than on load, since the writes
        // being baselined arrive BEFORE the world is ready.
        private static readonly Dictionary<SavableSystem, int> LoadBaseline = new();

        internal static void OnWorldUnloaded()
        {
            LoadBaseline.Clear();
        }

        private static bool IsGoalFlag(SavableSystem system)
        {
            return system == SavableSystem.EndingGate || system == SavableSystem.GauntletComplete;
        }

        // Called for EVERY write to these two keys, zero included — unlike
        // the check reporting around it, which has no use for a zero.
        internal static void Observe(SavableSystem system, int value)
        {
            if (!IsGoalFlag(system) || IsLatched(system))
                return;

            if (!WorldManager.isReadyForEffects)
            {
                LoadBaseline[system] = value;
                Plugin.Log.LogInfo(
                    $"[{nameof(ApGoalFlags)}] {system}={value} written while the world was still loading: baseline, not the goal.");
                return;
            }

            if (LoadBaseline.TryGetValue(system, out var baseline) && baseline == value)
                return;

            var previous = LoadBaseline.TryGetValue(system, out var known) ? known.ToString() : "unknown";
            LoadBaseline[system] = value;

            var key = KeyPrefix + system;
            SaveManager.SetIntValue(key, 1);
            Plugin.Log.LogInfo(
                $"[{nameof(ApGoalFlags)}] {system} changed during play ({previous} -> {value}): goal reached, latched as {key}.");
        }

        internal static bool IsLatched(SavableSystem system)
        {
            return SaveManager.GetIntValue(KeyPrefix + system, 0, false) != 0;
        }
    }
}
