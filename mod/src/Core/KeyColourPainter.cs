using System;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Paints the big keys on EVERY machine, which is the only way they can be
    // painted at all.
    //
    // MEASURED IN CO-OP, 2026-09-23: the guest saw five identical yellow keys
    // while the host saw them tinted. `KeyColours` writes a
    // `MaterialPropertyBlock`, which is local rendering and travels nowhere —
    // the same reason the radio is local and the mid-air gourd of 2026-09-16
    // was. Prediction D in `docs/COOP-TESTS.md`, confirmed.
    //
    // The cause was narrower than the prediction, though: `PaintOnce` had only
    // ever been called from `KeyCustody.Tick`, which opens with
    // `if (!KeysAreItems || !NetworkServer.active) return`. It had never run
    // anywhere but the host, so there was nothing to replicate in the first
    // place.
    //
    // Nothing about painting needs the server. The palette is keyed on
    // `SaveablePropName`, and the keys are scene objects every machine can
    // see, so a client has everything it needs to paint its own. This is
    // therefore the same shape as `VanillaGadgetRemover`: poll
    // `WorldManager.isReadyForEffects`, re-arm on each world load because a
    // reload rebuilds every renderer, and let `PaintOnce` decide when it has
    // actually found keys — it treats "none loaded yet" as "try again", not
    // as "done".
    //
    // Harmless on the host, where `KeyCustody.Tick` also calls it: `PaintOnce`
    // is guarded by its own `_painted` flag, so the second caller returns
    // immediately.
    internal class KeyColourPainter : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public KeyColourPainter(IntPtr ptr) : base(ptr)
        {
        }

        private const float PaintIntervalSeconds = 1f;

        private bool _worldWasReady;
        private float _nextPaint;

        private void Update()
        {
            if (!WorldManager.isReadyForEffects)
            {
                _worldWasReady = false;
                return;
            }

            if (!_worldWasReady)
            {
                _worldWasReady = true;
                KeyColours.Rearm();
                _nextPaint = 0f;
            }

            if (Time.time < _nextPaint)
                return;

            _nextPaint = Time.time + PaintIntervalSeconds;

            try
            {
                KeyColours.PaintOnce();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyColourPainter)}] Painting failed, ignored: {ex.Message}");
            }
        }
    }
}
