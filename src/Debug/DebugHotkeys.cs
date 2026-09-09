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

            if (ModConfig.DumpMonumentHomesKey.Value.IsDown())
                DebugGourdLookup.LogMonumentHomes();

            if (ModConfig.SimulateGreenZoneKeyKey.Value.IsDown())
                DebugItemSimulator.SimulateReceive(SaveablePropName.bigKeyGreenZone);

            if (ModConfig.SimulateBlueZoneKeyKey.Value.IsDown())
                DebugItemSimulator.SimulateReceive(SaveablePropName.bigKeyBlueZone);

            if (ModConfig.DumpNearbyKeywordKey.Value.IsDown())
                DebugComponentLookup.DumpNearbyByKeyword(30f);

            if (ModConfig.DumpNearbyPeckDevHelperKey.Value.IsDown())
                DebugPeckDevHelper.DumpNearby(30f);

            if (ModConfig.TriggerUnlocksKey.Value.IsDown())
                DebugPeckDevHelper.Trigger(new PeckDevHelper.UnlockRules { unlocks = true });

            if (ModConfig.TriggerGourdKey.Value.IsDown())
                DebugPeckDevHelper.Trigger(new PeckDevHelper.UnlockRules { gourd = true });

            if (ModConfig.DumpSpawnHubGateKey.Value.IsDown())
                DebugTrackedPeckStateLookup.DumpSpawnHubGate();

            if (ModConfig.DumpPeckSwitchTargetKey.Value.IsDown())
                DebugPeckSwitchTarget.DumpNearby(30f);

            if (ModConfig.RevealVariantGourdsKey.Value.IsDown())
                DebugVariantGourdReveal.RevealAll();
        }
    }
}
