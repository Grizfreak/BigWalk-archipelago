using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Disables the hub's blocking black sphere (entrance to the "second
    // ending" / postgame) from the very first session on a given save.
    // User decision (cf. big-walk-archipelago-notes.md, session on
    // 2026-09-11): in vanilla, this sphere only breaks after a first full
    // completion of the game — a condition that could not be identified
    // with certainty after several investigation sessions (Ghidra + in-game
    // dumps), and the most promising hypothesis ("filling the
    // bigKeyPlinthGoodbye2 plinth, right behind it, triggers the break")
    // turned out to be the reverse: that plinth is only accessible ONCE the
    // sphere is already broken. Rather than continue hunting for the real
    // flag/Peck mechanism, the sphere is simply disabled every session:
    // there is no point in keeping it closed for an Archipelago world, the
    // same philosophy as ArchDoorUnlocker for the hub shortcuts.
    //
    // Key difference from ArchDoorUnlocker: there, the effect (a
    // TrackedPeckState/SaveManager write) is persisted by the game itself,
    // so a single "already done once" flag is enough to never touch it
    // again. Here, SetActive(false) is a state purely local to the session
    // (nothing is saved): it must therefore be replayed on EVERY startup,
    // never relying on a SaveManager flag to skip the step.
    //
    // Targeting by name prefix, restricted to the sphere itself — NOT plain
    // "Spawn_SecondEnding". This broader prefix was tried first and also
    // caught `Spawn_SecondEnding_Door (1)`: confirmed in-game (2026-09-11)
    // that it disabled the real entrance door to the second ending in
    // addition to the sphere, opening access to that zone right from the
    // start of the game — not intended, only the sphere should disappear,
    // real progression toward the second ending (Gauntlet, bells, etc.)
    // must remain intact. `Spawn_SecondEnding_Sphere` therefore precisely
    // targets `Spawn_SecondEnding_Sphere_Whole` (the sphere's mesh+collider)
    // without touching `Spawn_SecondEnding_Door (1)` or anything else in
    // the same scene set.
    internal class SecondEndingSphereUnlocker : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public SecondEndingSphereUnlocker(IntPtr ptr) : base(ptr)
        {
        }

        private const string BlockingObjectNamePrefix = "Spawn_SecondEnding_Sphere";

            // Re-armed on every world load, not latched once for the life
            // of the process. Going back to the main menu and loading
            // another save reloads the world without restarting the game,
            // and the old `_done` flag meant this simply never ran again —
            // observed in-game on 2026-09-15, where a brand-new save got
            // neither its hub shortcuts nor its sphere handled because a
            // previous save had already consumed the one-shot. Running
            // again is harmless in both cases: disabling an
            // already-disabled object does nothing.
        private bool _worldWasReady;

        private void Update()
        {
            // Host authority model, like the rest of the mod (cf. ItemApplier).
            var worldReady = NetworkServer.active && WorldManager.isReadyForEffects;
            if (!worldReady)
            {
                _worldWasReady = false;
                return;
            }

            if (_worldWasReady)
                return;

            // Runs on the first frame of each loaded world and not again,
            // so the scan over every loaded Transform below happens once
            // per world rather than every frame.
            _worldWasReady = true;

            var disabled = new List<string>();
            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t == null)
                    continue;

                var go = t.gameObject;
                if (!go.activeSelf)
                    continue;

                if (!go.name.StartsWith(BlockingObjectNamePrefix, StringComparison.Ordinal))
                    continue;

                go.SetActive(false);
                disabled.Add(go.name);
            }

            if (disabled.Count == 0)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(SecondEndingSphereUnlocker)}] No object '{BlockingObjectNamePrefix}*' found in the current zone (hub not loaded?).");
                return;
            }

            Plugin.Log.LogInfo(
                $"[{nameof(SecondEndingSphereUnlocker)}] {disabled.Count} object(s) disabled: {string.Join(", ", disabled)}.");
        }
    }
}
