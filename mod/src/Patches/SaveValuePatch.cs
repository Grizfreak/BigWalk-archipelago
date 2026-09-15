using System;
using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Safety net: SaveManager.SetIntValue is the low-level write point
    // common to all code paths that persist a gourd. We don't know all of
    // these paths (cf. big-walk-archipelago-notes.md) — confirmed during a
    // test session on 2026-09-03: in addition to
    // RewardGourd.ServerSetGourdState, PeckEffectSavableHome.Peck() (on a
    // "vice launch switch") calls Prop.SavePropHome independently, never
    // going through RewardGourd. This patch generically captures every
    // write, at the cost of not knowing exactly which gourd state triggered
    // it.
    //
    // CheckTracker.TryMarkReported (persisted, cf. Core/CheckTracker.cs)
    // guarantees that the same gourd already reported — by this patch, by
    // GourdStatePatch, or during a previous session — is never reported
    // twice.
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetIntValue))]
    internal static class SaveValuePatch
    {
        private static void Postfix(string key, int value)
        {
            // value == 0 means "unpinned" (cf. Prop.SavePropHome in the
            // notes): this is never a check, potentially a removal.
            if (value == 0)
                return;

            if (!Enum.TryParse<SaveablePropName>(key, out var propName))
                return;

            if (!GourdRegistry.TryGetLocationId(propName, out var locationId))
                return;

            if (!CheckTracker.TryMarkReported(locationId))
                return;

            Plugin.Reporter.ReportCheck(locationId);
        }
    }
}
