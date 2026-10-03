using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Switches the game's own skip aids on, to see what they do (ROADMAP U7).
    //
    // Each Silent Gauntlet chamber has a `SkipAid Guantlet` object: a pole of two
    // buttons held together. It is switched off by `SkipAidToggler`, which reads
    // a global flag, `skipAidsActive`, when it wakes up, and that flag is false in
    // a normal game. So this sets the flag and then wakes every toggler's target
    // by hand, since the flag is only read once.
    //
    // Pure diagnostic. What the aid changes is read afterwards with the Keypad 5
    // dump, taken before and after holding its two buttons (End holds them).
    internal static class DebugSkipAids
    {
        internal static void EnableAll()
        {
            try
            {
                SkipAidToggler.skipAidsActive = true;

                var togglers = UnityEngine.Object.FindObjectsByType<SkipAidToggler>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                var woken = 0;
                if (togglers != null)
                {
                    foreach (var toggler in togglers)
                    {
                        if (toggler == null || toggler.target == null || toggler.target.gameObject.activeSelf)
                            continue;

                        toggler.target.gameObject.SetActive(true);
                        woken++;
                    }
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugSkipAids)}] skipAidsActive is on; {woken} skip aid(s) switched on "
                    + $"of {(togglers != null ? togglers.Length : 0)}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugSkipAids)}] Could not switch the skip aids on: {ex.Message}");
            }
        }
    }
}
