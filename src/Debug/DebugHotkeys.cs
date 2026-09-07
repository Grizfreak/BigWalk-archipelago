using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Injecté en IL2CPP par Plugin.AddComponent<DebugHotkeys>() (voir Plugin.Load()),
    // seulement si Config.DebugModeEnabled est actif : le composant n'existe
    // même pas dans la scène sinon, donc pas de coût de polling en usage normal.
    internal class DebugHotkeys : MonoBehaviour
    {
        // Constructeur requis par Il2CppInterop pour tout type injecté en IL2CPP.
        public DebugHotkeys(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            if (ModConfig.ToggleFlightKey.Value.IsDown())
                DebugFlightTool.Toggle();

            if (ModConfig.UnlockNextKey.Value.IsDown())
                DebugGourdUnlocker.UnlockNext();

            if (ModConfig.SimulateReceivedItemKey.Value.IsDown())
                DebugItemSimulator.SimulateReceiveNext();

            if (ModConfig.DumpNearbyKey.Value.IsDown())
                DebugGourdLookup.LogNearby(50);
        }
    }
}
