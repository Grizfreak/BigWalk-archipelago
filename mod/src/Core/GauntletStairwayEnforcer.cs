using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Says when GauntletStairways opens the stairways its slot has given. The
    // game does not save a stairway, so a world that loads, or a stairway whose
    // object loads late, finds it shut again: this asks every couple of
    // seconds while a world is ready on the host. Like ArchDoorUnlocker, it is
    // a real feature and not a debug tool.
    internal class GauntletStairwayEnforcer : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public GauntletStairwayEnforcer(IntPtr ptr) : base(ptr)
        {
        }

        private const float IntervalSeconds = 2f;

        private bool _worldWasReady;
        private float _nextAt;

        private void Update()
        {
            var worldReady = NetworkServer.active && WorldManager.isReadyForEffects;
            if (!worldReady)
            {
                // The gates belong to the world that is going away.
                if (_worldWasReady)
                    GauntletStairways.ForgetGates();

                _worldWasReady = false;
                return;
            }

            if (!_worldWasReady && !ModConfig.ArchipelagoEnabled.Value)
                GauntletStairways.Forget();

            _worldWasReady = true;
            if (Time.unscaledTime < _nextAt)
                return;

            _nextAt = Time.unscaledTime + IntervalSeconds;
            GauntletStairways.Enforce();
        }
    }
}
