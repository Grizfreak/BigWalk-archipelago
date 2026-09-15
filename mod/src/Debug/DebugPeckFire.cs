using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Action tool (not a plain diagnostic): triggers a PeckSwitch remotely,
    // without having to physically stand in front of it. Useful for testing
    // alone a mechanism designed for 2 players (e.g. the simultaneous
    // "N-hold" buttons of the bells/Gauntlet, cf.
    // big-walk-archipelago-notes.md): one player normally pecks the first
    // button, then triggers this hotkey to peck the second one remotely by
    // its name (identified beforehand via F9/Delete), without needing a
    // second player physically present.
    //
    // PeckSwitch.Peck() is the same public method called by the game itself
    // during a normal interaction (confirmed via il2cpp.cs) — this is not a
    // workaround, just a remote call to the same action.
    internal static class DebugPeckFire
    {
        // Safety radius: many PeckSwitch names are generic and reused
        // everywhere in the game (e.g. "UpSwitch", seen identically on the
        // chapel bell, the Gauntlet entrance, AND the summit bell) — without
        // a distance limit, FireByName would trigger ALL objects with that
        // name across the entire loaded area, not just the one targeted next
        // to the player.
        private const float MaxFireRadius = 40f;

        internal static void FireByName(string nameSubstring)
        {
            if (string.IsNullOrWhiteSpace(nameSubstring))
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] No name configured (ModConfig.RemotePeckSwitchName empty) — nothing to trigger.");
                return;
            }

            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Local player not found.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = MaxFireRadius * MaxFireRadius;

            var all = UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] No PeckSwitch found in the current area.");
                return;
            }

            var fired = 0;
            foreach (var sw in all)
            {
                if (sw == null)
                    continue;

                string goName;
                float sqrDistance;
                try
                {
                    goName = sw.gameObject.name;
                    sqrDistance = (sw.transform.position - origin).sqrMagnitude;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckFire)}] Unreadable PeckSwitch (name/position): {ex.Message}");
                    continue;
                }

                if (goName.IndexOf(nameSubstring, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Filtered by distance last (not inside the name-matching
                // loop) so the "no match" message distinguishes "unknown
                // name" from "known name but too far" — see the log below.
                if (sqrDistance > sqrRadius)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugPeckFire)}]   {goName} matches the name but is {Math.Sqrt(sqrDistance):F1}m away (> {MaxFireRadius}m) — skipped for safety.");
                    continue;
                }

                try
                {
                    sw.Peck();
                    fired++;
                    Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] Peck() triggered remotely on {goName}.");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(DebugPeckFire)}] {goName} — exception during Peck(), ignored: {ex.Message}");
                }
            }

            if (fired == 0)
                Plugin.Log.LogInfo($"[{nameof(DebugPeckFire)}] No PeckSwitch whose name contains '{nameSubstring}' found within {MaxFireRadius}m.");
        }
    }
}
