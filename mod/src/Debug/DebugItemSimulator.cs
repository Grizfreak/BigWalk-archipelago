using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Simulates receiving an Archipelago item remotely, without a real
    // Archipelago network connection: exercises exactly the same code path
    // (Core.ItemApplier) that a real client will call later on. Targets the
    // nearest non-collected savable Prop to the player.
    //
    // Goes through DebugGourdLookup.FindNearestUncollectedProp
    // (Prop.allProps), not RewardGourd: confirmed in a test session on
    // 2026-09-07, big keys have no RewardGourd component (hence no
    // gourdState), contrary to the notes' initial hypothesis —
    // FindNearestLocked (which filters on RewardGourd.gourdState == Locked)
    // never finds them. ItemApplier never needed RewardGourd anyway: only
    // saveablePropName matters.
    internal static class DebugItemSimulator
    {
        internal static void SimulateReceiveNext()
        {
            var prop = DebugGourdLookup.FindNearestUncollectedProp();
            if (prop == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugItemSimulator)}] No more uncollected gourd/big key in the current area.");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(DebugItemSimulator)}] Simulating item reception: {prop.saveablePropName}");
            Dispatch(prop.saveablePropName);
        }

        // Targets a specific SaveablePropName directly, without going
        // through FindNearestUncollectedProp: useful for testing a big key
        // that is not in the currently loaded area (cable car/train, cf.
        // big-walk-archipelago-notes.md "To test" from 2026-09-09), or one
        // whose application we want to force regardless of the player's
        // proximity.
        internal static void SimulateReceive(SaveablePropName propName)
        {
            Plugin.Log.LogInfo(
                $"[{nameof(DebugItemSimulator)}] Simulating item reception (targeted): {propName}");
            Dispatch(propName);
        }

        // A gourd simulated via a specific SaveablePropName (gourdXxx) no
        // longer makes 1:1 sense since the 2026-09-11 FIX (big-walk-
        // archipelago-notes.md): ApplyGourdItem is now generic, without a
        // propName. We keep the parameter here (useful for targeting a
        // specific big key, or for choosing which gourd to simulate via
        // keyboard), and just route to the right ItemApplier path based on
        // the type.
        private static void Dispatch(SaveablePropName propName)
        {
            if (GourdRegistry.IsBigKey(propName))
                ItemApplier.ApplyBigKeyItem(propName);
            else
                ItemApplier.ApplyGourdItem(toPlayer: true);
        }
    }
}
