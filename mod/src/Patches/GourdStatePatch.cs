using BigWalkArchipelago.Core;
using HarmonyLib;
using Mirror;

namespace BigWalkArchipelago.Patches
{
    // Postfix on RewardGourd.ServerSetGourdState: the transition to Loose
    // (the gourd has just come out of the vise, puzzle solved) counts as a
    // validated check.
    //
    // Choice revised during a test session on 2026-09-03: architecture-mod.md
    // originally targeted Stashed (deposit at home), but an in-game test
    // revealed that stashing a gourd requires TWO players (one opens the
    // slot, the other deposits the gourd into it) — so it's not a good check
    // detection point (rarely reached, not testable solo). Loose corresponds
    // to the actual moment the puzzle is resolved, as in
    // GourdInterceptPOC.cs. Stashed remains relevant later for the reverse
    // direction: when an Archipelago item is received remotely, we'll want
    // to force a home gourd to fill without going through the two-player
    // coordination (cf. big-walk-archipelago-notes.md, recommendation #2).
    //
    // We read newGourdState (the parameter received by the intercepted
    // call), not __instance.gourdState, to react to the right event even if
    // the SyncVar has already changed several times in the meantime.
    //
    // Once the check has been reported, the physical gourd must not remain
    // locally collectible: the "real" grant will go through the Archipelago
    // network (item received), not through the local pickup. The recursive
    // call to ServerSetGourdState(Hidden) re-enters this postfix, but the
    // guard above (newGourdState != Loose) prevents any infinite loop. This
    // applies only to gourds in the registry: test gourds (outside the
    // registry) keep their vanilla behavior.
    //
    // Confirmed in testing on 2026-09-07: ServerSetGourdState(Loose) is also
    // called for real (not just from Prop.Start()) when loading a zone for a
    // gourd already solved in a previous session — a legitimate restoration,
    // not a re-resolution by the player. Hence the importance of
    // CheckTracker.TryMarkReported being persistent (cf.
    // Core/CheckTracker.cs): without that, this call would re-trigger the
    // check on every game restart. The rest of the postfix (Hidden/UnSpawn/
    // SetActive), however, must keep running every time: each session
    // recreates a fresh instance of the GameObject, so re-hiding is
    // necessary every time — only the check *report* must be deduplicated.
    //
    // Tested in-game on 2026-09-03: Hidden alone only refreshes the icon on
    // the map (confirmed — GourdMap.refreshFlag, cf. notes.md), the 3D model
    // remains visible and collectible. A plain GameObject.SetActive(false)
    // would be purely local (Mirror doesn't automatically replicate that to
    // other clients) and would create a visual desync in co-op. So we go
    // through NetworkServer.UnSpawn (the correct Mirror API to remove a
    // networked object from the view of ALL clients without destroying it
    // permanently, unlike NetworkServer.Destroy) and then disable the
    // GameObject locally on the host side. Not yet verified: whether the
    // RewardGourd GameObject corresponds exactly to the gourd's visual
    // model, or to a broader hierarchy shared with the vise mechanism — to
    // keep an eye on.
    [HarmonyPatch(typeof(RewardGourd), nameof(RewardGourd.ServerSetGourdState))]
    internal static class GourdStatePatch
    {
        private static void Postfix(RewardGourd __instance, GourdFlag.GourdState newGourdState)
        {
            if (newGourdState != GourdFlag.GourdState.Loose)
                return;

            var prop = __instance.prop;
            if (prop == null)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(GourdStatePatch)}] RewardGourd has no associated prop, Loose transition ignored.");
                return;
            }

            if (!GourdRegistry.TryGetLocationId(prop.saveablePropName, out var locationId))
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(GourdStatePatch)}] '{prop.saveablePropName}' not in registry (test/notSavable), ignored.");
                return;
            }

            if (CheckTracker.TryMarkReported(locationId))
                Plugin.Reporter.ReportCheck(locationId);

            __instance.ServerSetGourdState(GourdFlag.GourdState.Hidden);

            // Not every puzzle releases its gourd into an empty room. On the
            // ones whose gourd is sealed in a box, the Loose transition
            // happens ON PICKUP, from inside the pick-up Command itself, which
            // goes on to put the gourd in the player's hands after this
            // returns. Removing it here broke that Command half-way; see
            // PuzzleGourdRetirer for what that did to a guest.
            PuzzleGourdRetirer.Retire(__instance);
        }
    }
}
