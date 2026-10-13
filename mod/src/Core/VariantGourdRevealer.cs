using System.Collections.Generic;
namespace BigWalkArchipelago.Core
{
    // Logic shared between the debug hotkey (Debug/DebugVariantGourdReveal)
    // and the automation (Core/VariantGourdMapUnlocker): reveals "variant
    // challenge" gourds (purple, normally unlocked after a first game
    // completion) on the map. Cf. big-walk-archipelago-notes.md, session on
    // 2026-09-09: GourdMap.Initialize() forces GourdFlag.SetState(Hidden)
    // for every gourd with isVariantChallenge==true on EVERY zone
    // (re)load — a purely local/session state, never read from or written
    // to SaveManager.
    //
    // FIXED (2026-09-09): the first version directly invoked the static
    // event GourdMap.refreshFlag (Action<SaveablePropName, GourdState>).
    // Isolated in-game via A/B testing: revealing a purple gourd through
    // this Invoke() before resolving it reliably caused a total game
    // freeze (no exception, no log) a few dozen seconds later at the first
    // alt-tab — never reproduced without this call (normal gourd, or a
    // purple gourd discovered/resolved without ever calling refreshFlag).
    // Directly invoking a static Il2Cpp delegate from external managed code
    // appears to corrupt some internal state (likely on the IL2CPP interop
    // side) that only manifests later. Fix: call GourdFlag.SetState(...)
    // directly on the found instance (a normal method call on a component,
    // not a delegate invocation) — exactly what the private method
    // GourdMap.RefreshFlag does internally, so just as safe on the game
    // side, without going through the point that caused the problem.
    internal static class VariantGourdRevealer
    {
        internal static int RevealAll()
        {
            var flags = UnityEngine.Object.FindObjectsByType<GourdFlag>(UnityEngine.FindObjectsSortMode.None);
            if (flags == null || flags.Length == 0)
                return 0;

            // The hidden flags first: most polls find none, and then the gourds are not searched at all (Q6, 2026-10-14:
            // the gourds searched for every flag cost a 20 ms stall every two seconds).
            var hidden = new List<GourdFlag>();
            foreach (var flag in flags)
            {
                if (flag != null && flag.gourdState == GourdFlag.GourdState.Hidden)
                    hidden.Add(flag);
            }
            if (hidden.Count == 0)
                return 0;

            var variants = new HashSet<SaveablePropName>();
            foreach (var gourd in UnityEngine.Object.FindObjectsByType<RewardGourd>(UnityEngine.FindObjectsSortMode.None))
            {
                if (gourd != null && gourd.isVariantChallenge && gourd.prop != null)
                    variants.Add(gourd.prop.saveablePropName);
            }

            var count = 0;
            foreach (var flag in hidden)
            {
                if (!variants.Contains(flag.saveablePropName))
                    continue;

                flag.SetState(GourdFlag.GourdState.Locked);
                count++;
            }

            return count;
        }
    }
}
