using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using HarmonyLib;
using Mirror;

namespace BigWalkArchipelago.Patches
{
    // The 25 forage checks: one per segment cut out of a big key.
    //
    // Five segments each on the drawbridge and the four coloured towers,
    // measured in game on 2026-09-21 rather than taken from a third-party
    // document. The Black Monolith and Green Dome keys are born finished,
    // have no covers and no segments at all, so they contribute none — they
    // have only their placement location and their feature item.
    //
    // WHY NOT `ServerCutSegment`. It is the obvious target, it is public, and
    // the notes named it as the check hook. It has **no code address** in the
    // il2cpp export, while `OnBite`, `OnPinUpdated`, `OnCutsUpdated`,
    // `RefreshPropGroup` and `OnStartClient` — its immediate neighbours in
    // the same class — all have one, and the 32 bytes between the two nearest
    // are padding rather than a body. It was inlined into its caller. That is
    // exactly the trap this project has already paid for once, when
    // `BroadcastStation.Unlock` turned out to be an inlined copy of
    // `FmRadioManager.Unlock` and patching the obvious target would have
    // suppressed nothing, silently.
    //
    // `OnCutsUpdated` is the honest hook: it has a body, it is the game's own
    // notification for this exact change, and it carries both halves of the
    // answer — `__instance.prop.saveablePropName` names the tower and the
    // index names the segment.
    //
    // WHAT STOPS THE BURST. `cuts` is a Mirror `SyncList` sized at runtime
    // from scene data, so a world load appends its entries one at a time and
    // this fires once per append with `newValue` false. Reporting only on a
    // transition to true covers that, and it also covers the guest, whose
    // initial sync replays the whole list.
    //
    // The trap recorded for whoever implemented this was that pinning a key
    // marks its blank complete, so a naive hook would fire the drawbridge's
    // five checks the moment the mod pinned the precollected Tutorial Key at
    // connection time. That trap dissolved rather than being worked around:
    // under this design the mod pins nothing (see Core/ItemApplier.cs), so a
    // key that is in its plinth is one the player carried there, and the five
    // cuts behind it are genuinely theirs. A save that had it placed before
    // this feature existed reports them on its next load, once, which is the
    // correct answer to "did this player cut that key".
    [HarmonyPatch(typeof(KeyBlank), nameof(KeyBlank.OnCutsUpdated))]
    internal static class KeyBlankCutPatch
    {
        // Arguments by index, not by name: parameter names survive into the
        // interop assembly only by convention, and a mismatch throws at
        // PatchAll and takes the whole plugin down. `__0` is the SyncList
        // operation, `__1` the index, `__2` the old value, `__3` the new one.
        private static void Postfix(KeyBlank __instance, int __1, bool __2, bool __3)
        {
            // Host only, like every other check in this mod: Big Walk's save
            // data belongs to the host, and it is the only process that runs
            // an Archipelago client at all.
            if (!NetworkServer.active)
                return;

            // The append pass at world load, and any re-assert of a value
            // already true. Only the false -> true transition is a cut.
            if (!__3 || __2)
                return;

            var prop = __instance != null ? __instance.prop : null;
            if (prop == null)
                return;

            var propName = prop.saveablePropName;
            if (!GourdRegistry.IsBigKey(propName))
                return;

            var locationId = ApLocationIds.CutLocationName(propName, __1);
            if (!CheckTracker.TryMarkReported(locationId))
                return;

            Plugin.Log.LogInfo($"[{nameof(KeyBlankCutPatch)}] Segment {__1} cut out of {propName}.");
            Plugin.Reporter.ReportCheck(locationId);
        }
    }
}
