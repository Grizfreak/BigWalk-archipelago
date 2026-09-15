using System;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
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

            if (Enum.TryParse<SaveablePropName>(key, out var propName))
            {
                if (GourdRegistry.TryGetLocationId(propName, out var locationId)
                    && CheckTracker.TryMarkReported(locationId))
                    Plugin.Reporter.ReportCheck(locationId);

                return;
            }

            // SavableSystem is the game's other key space in this same
            // store: the radio stations (locations in the Archipelago world)
            // and the two end-of-game flags live here. Everything else in
            // that enum — hub shortcuts, lookout lights, gauntlet chambers,
            // doors — is not a location, which is why this reports through
            // GourdRegistry-style filtering rather than blanket-reporting
            // every SavableSystem write.
            if (!Enum.TryParse<SavableSystem>(key, out var system) || system == SavableSystem.NotSavable)
                return;

            ApGoalFlags.Latch(system);

            // Only the ids the apworld actually defines resolve here (the
            // seven named radio stations); ApRuntime then filters again
            // against what this particular slot contains.
            if (!ApLocationIds.TryResolveLocation(key, out _))
                return;

            if (CheckTracker.TryMarkReported(key))
                Plugin.Reporter.ReportCheck(key);
        }
    }
}
