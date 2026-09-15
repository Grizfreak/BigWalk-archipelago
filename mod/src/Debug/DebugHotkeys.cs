using System;
using BigWalkArchipelago.Core;
using HouseCulling;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Injected in IL2CPP by Plugin.AddComponent<DebugHotkeys>() (see
    // Plugin.Load()), only if Config.DebugModeEnabled is active: the
    // component doesn't even exist in the scene otherwise, so there is no
    // polling cost during normal use.
    internal class DebugHotkeys : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected in IL2CPP.
        public DebugHotkeys(IntPtr ptr) : base(ptr)
        {
        }

        // Diagnostic 2026-09-11: the clone spawned by P has all Unity flags
        // green at the very moment of spawning (enabled, isVisible,
        // activeInHierarchy), but is already gone from an F9 dump taken just
        // a few seconds later — suspicion that something disables/destroys
        // it shortly afterward rather than a genuine frozen-rendering issue.
        // Rechecked here 3s later to confirm/refute without guessing further.
        private GameObject _lastCosmeticPickup;
        private float _lastCosmeticPickupCheckTime = -1f;
        private const float CosmeticPickupRecheckDelaySeconds = 3f;

        private void Update()
        {
            if (_lastCosmeticPickupCheckTime >= 0f && Time.time >= _lastCosmeticPickupCheckTime)
            {
                RecheckCosmeticPickup();
                _lastCosmeticPickupCheckTime = -1f;
            }

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

            if (ModConfig.DumpNearbyCombinatorKey.Value.IsDown())
                DebugPeckCombinatorLookup.DumpNearby(30f);

            if (ModConfig.DumpAllSaveEntriesKey.Value.IsDown())
                DebugSaveDump.DumpAll();

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

            if (ModConfig.FireRemotePeckSwitchKey.Value.IsDown())
                DebugPeckFire.FireByName(ModConfig.RemotePeckSwitchName.Value);

            if (ModConfig.ForceNearbyCombinatorKey.Value.IsDown())
                DebugPeckCombinatorForce.ForceNearby(30f);

            if (ModConfig.ForceEndingFlagsKey.Value.IsDown())
                DebugForceEndingFlags.ForceAll();

            if (ModConfig.ApplyBigKeyOverflowKey.Value.IsDown())
                DebugItemSimulator.SimulateReceive(SaveablePropName.bigKeyOverflow);

            if (ModConfig.SpawnCosmeticPickupKey.Value.IsDown())
            {
                _lastCosmeticPickup = ReceivedItemSpawner.SpawnCosmeticPickup();
                _lastCosmeticPickupCheckTime = _lastCosmeticPickup != null ? Time.time + CosmeticPickupRecheckDelaySeconds : -1f;
            }

            if (ModConfig.ToggleNeverCullKey.Value.IsDown())
                ToggleNeverCull();

            if (ModConfig.ForceCosmeticPinKey.Value.IsDown())
                DebugCosmeticPinForce.ForceNearby(30f);

            if (ModConfig.DumpHostMenuConfirmKey.Value.IsDown())
                DebugMenuLookup.DumpHostMenuConfirm();
        }

        private void RecheckCosmeticPickup()
        {
            var clone = _lastCosmeticPickup;
            if (clone == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Recheck {CosmeticPickupRecheckDelaySeconds}s after spawn — clone == null (destroyed, or Unity reference invalidated).");
                return;
            }

            var renderer = clone.GetComponentInChildren<Renderer>(true);
            Plugin.Log.LogInfo(
                $"[{nameof(DebugHotkeys)}] Recheck {CosmeticPickupRecheckDelaySeconds}s after spawn — clone still referenced: " +
                $"name={clone.name}, activeSelf={clone.activeSelf}, activeInHierarchy={clone.activeInHierarchy}, " +
                $"position={clone.transform.position}, renderer.enabled={(renderer != null ? renderer.enabled.ToString() : "<no renderer>")}, " +
                $"renderer.isVisible={(renderer != null ? renderer.isVisible.ToString() : "n/a")}.");
        }

        private static void ToggleNeverCull()
        {
            var instance = CullingAgent.Instance;
            if (instance == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] CullingAgent.Instance not found.");
                return;
            }

            instance.neverCull = !instance.neverCull;
            Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] CullingAgent.neverCull = {instance.neverCull}.");
        }
    }
}
