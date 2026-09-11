using System;
using BigWalkArchipelago.Core;
using HouseCulling;
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

        // Diagnostic 2026-09-11 : le clone spawné par P a tous les
        // indicateurs Unity au vert au moment même du spawn (enabled,
        // isVisible, activeInHierarchy), mais n'apparaît déjà plus dans un
        // dump F9 fait seulement quelques secondes après — suspicion que
        // quelque chose le désactive/détruit peu après coup plutôt qu'un
        // vrai problème de rendu figé. Revérifié ici 3s après coup pour
        // confirmer/infirmer sans deviner davantage.
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
        }

        private void RecheckCosmeticPickup()
        {
            var clone = _lastCosmeticPickup;
            if (clone == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Revérif. {CosmeticPickupRecheckDelaySeconds}s après spawn — clone == null (détruit, ou référence Unity invalidée).");
                return;
            }

            var renderer = clone.GetComponentInChildren<Renderer>(true);
            Plugin.Log.LogInfo(
                $"[{nameof(DebugHotkeys)}] Revérif. {CosmeticPickupRecheckDelaySeconds}s après spawn — clone toujours référencé : " +
                $"name={clone.name}, activeSelf={clone.activeSelf}, activeInHierarchy={clone.activeInHierarchy}, " +
                $"position={clone.transform.position}, renderer.enabled={(renderer != null ? renderer.enabled.ToString() : "<pas de renderer>")}, " +
                $"renderer.isVisible={(renderer != null ? renderer.isVisible.ToString() : "n/a")}.");
        }

        private static void ToggleNeverCull()
        {
            var instance = CullingAgent.Instance;
            if (instance == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] CullingAgent.Instance introuvable.");
                return;
            }

            instance.neverCull = !instance.neverCull;
            Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] CullingAgent.neverCull = {instance.neverCull}.");
        }
    }
}
