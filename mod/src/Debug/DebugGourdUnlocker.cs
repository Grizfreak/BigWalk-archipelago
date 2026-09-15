using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Forcibly unlocks a gourd/big key without solving the puzzle, to test
    // GourdStatePatch/SaveValuePatch without depending on puzzles (often
    // multiplayer) or a real playthrough sequence.
    internal static class DebugGourdUnlocker
    {
        internal static void UnlockNext()
        {
            var closest = DebugGourdLookup.FindNearestLocked();
            if (closest == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugGourdUnlocker)}] No more locked gourd/big key in the current area.");
                return;
            }

            var prop = closest.prop;
            string label = prop != null ? prop.saveablePropName.ToString() : "<unknown prop>";
            Plugin.Log.LogInfo($"[{nameof(DebugGourdUnlocker)}] Forced unlock (nearest one): {label}");

            // Important: we pin the prop to its home BEFORE moving the gourd
            // to Loose. GourdStatePatch also reacts to this transition (same
            // patched method) and does NetworkServer.UnSpawn + SetActive
            // (false) on this same GameObject (prop and gourd share the same
            // NetworkIdentity, confirmed in-game on 2026-09-07). If we pin
            // afterward, the object is already despawned (netIdentity.isServer
            // becomes false) and Prop.LocallySetPinned never writes to the
            // save. By pinning beforehand, we simulate the real game flow
            // (deposit in the basket before the vise hides the gourd).
            if (prop != null)
            {
                PropHome home = null;
                if (GourdRegistry.TryGetHomeName(prop.saveablePropName, out var homeName))
                {
                    home = PropHome.GetSaveableHome(homeName);
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Target home {homeName}: {(home != null ? "found" : "not found")}");
                }

                if (home == null)
                {
                    home = prop.startHome;
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Falling back to startHome: {(home != null ? "present" : "absent")}");
                }

                if (home != null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Pinning to saveableHomeName={home.saveableHomeName} before Loose transition.");
                    prop.ServerSetPinned(home);
                }
            }

            closest.ServerSetGourdState(GourdFlag.GourdState.Loose);
        }
    }
}
