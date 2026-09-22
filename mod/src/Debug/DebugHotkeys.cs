using System;
using BigWalkArchipelago.Core;
using HouseCulling;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

            // Every hotkey below is a single plain letter with no modifier
            // (ApplyBigKeyOverflowKey = O, SpawnCosmeticPickupKey = P, and
            // so on — predating the "<letter>+LeftControl" convention this
            // file's own Config.cs comments now recommend for new keys).
            // Typing a host address or slot name that happens to contain
            // one of those letters fires the matching debug tool while the
            // player is only trying to type — measured in-game, 2026-09-22:
            // typing "localhost:38281" into the host field's two 'o's each
            // fired ApplyBigKeyOverflowKey, and the resulting exception
            // permanently broke GourdRegistry's static state for the rest
            // of the process (a failed .NET type initializer never runs
            // again — every later call to it rethrows), silently failing
            // every item received for the rest of that session. Skipping
            // every hotkey below while a text input has focus is the fix.
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != null && focused.GetComponent<TMP_InputField>() != null)
                return;

            if (ModConfig.ToggleFlightKey.Value.IsDown())
                DebugFlightTool.Toggle();

            if (ModConfig.UnlockNextKey.Value.IsDown())
                DebugGourdUnlocker.UnlockNext();

            if (ModConfig.SimulateReceivedItemKey.Value.IsDown())
                DebugItemSimulator.SimulateReceiveNext();

            if (ModConfig.SimulateGadgetItemKey.Value.IsDown())
                DebugItemSimulator.SimulateReceiveNextGadget();

            if (ModConfig.DumpHeldItemKey.Value.IsDown())
                DebugHeldItemLookup.LogHeldItem();

            if (ModConfig.DumpCosmeticGadgetsKey.Value.IsDown())
                DebugPropLookup.DumpCosmeticGadgets();

            if (ModConfig.DumpNearbyKey.Value.IsDown())
                DebugGourdLookup.LogNearby(50);

            if (ModConfig.DumpMonumentHomesKey.Value.IsDown())
                DebugGourdLookup.LogMonumentHomes();

            if (ModConfig.DumpNearbyCombinatorKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Combinator lookup key pressed; calling LogNearby...");
                DebugPeckCombinatorLookup.DumpNearby(30f);
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] LogNearby returned.");
            }

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

            // Polled with IsPressed, not IsDown: this one is a hold, and
            // the tool needs the frames in between, plus the release.
            DebugTrackedStateHold.Update(
                ModConfig.HoldTrackedStateKey.Value.IsPressed(),
                ModConfig.HoldTrackedStateName.Value,
                ModConfig.HoldTrackedStateValue.Value,
                30f);

            if (ModConfig.ForceNearbyCombinatorKey.Value.IsDown())
            {
                // Logged BEFORE the call, and on purpose. On 2026-09-18 four
                // different keys bound to the two PeckCombinator tools
                // produced nothing at all — no output, no exception — while
                // other debug keys in the same Update answered normally.
                // "The tool printed nothing" has two very different causes
                // (the key was never seen, or the call died before its first
                // log line) and no way to tell them apart from the outside.
                // This line does exactly that and nothing else.
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Combinator force key pressed; calling ForceNearby...");
                DebugPeckCombinatorForce.ForceNearby(120f);
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] ForceNearby returned.");
            }

            if (ModConfig.ForceEndingFlagsKey.Value.IsDown())
                DebugForceEndingFlags.ForceAll();

            if (ModConfig.ApplyBigKeyOverflowKey.Value.IsDown())
                DebugItemSimulator.SimulateReceive(SaveablePropName.bigKeyOverflow);

            if (ModConfig.SpawnCosmeticPickupKey.Value.IsDown())
            {
                _lastCosmeticPickup = ReceivedItemSpawner.SpawnCosmeticPickup(toPlayer: true);
                _lastCosmeticPickupCheckTime = _lastCosmeticPickup != null ? Time.time + CosmeticPickupRecheckDelaySeconds : -1f;
            }

            if (ModConfig.ToggleNeverCullKey.Value.IsDown())
                ToggleNeverCull();

            if (ModConfig.ForceCosmeticPinKey.Value.IsDown())
                DebugCosmeticPinForce.ForceNearby(30f);

            if (ModConfig.DumpHostMenuConfirmKey.Value.IsDown())
                DebugMenuLookup.DumpHostMenuConfirm();

            if (ModConfig.DumpRadioKey.Value.IsDown())
            {
                // Bracketed, like the combinator key above: "the tool printed
                // nothing" and "the key was never seen" look identical from
                // the outside, and hours went into that confusion twice.
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Radio dump key pressed; calling Dump...");
                DebugRadioLookup.Dump();
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Dump returned.");
            }

            if (ModConfig.DumpKeysKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Big key dump key pressed; calling Dump...");
                DebugKeyLookup.Dump();
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Dump returned.");
            }

            if (ModConfig.ForceBigKeyDoorKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Big key door key pressed; calling ForceOne...");
                DebugBigKeyDoorForce.ForceOne(ModConfig.BigKeyDoorName.Value);
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] ForceOne returned.");
            }

            if (ModConfig.GrantBigKeyItemKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Big key item key pressed; calling GrantOne...");
                DebugBigKeyDoorForce.GrantOne(ModConfig.BigKeyDoorName.Value);
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] GrantOne returned.");
            }

            if (ModConfig.GrantBigKeyFeatureKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Big key feature key pressed; calling GrantFeature...");
                DebugBigKeyDoorForce.GrantFeature(ModConfig.BigKeyDoorName.Value);
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] GrantFeature returned.");
            }

            if (ModConfig.DumpPropsKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Prop inventory key pressed; calling Dump...");
                DebugPropLookup.Dump();
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Dump returned.");
            }

            if (ModConfig.DumpGourdRosterKey.Value.IsDown())
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] Gourd roster key pressed; calling LogAllLoaded...");
                DebugGourdLookup.LogAllLoaded();
                Plugin.Log.LogInfo($"[{nameof(DebugHotkeys)}] LogAllLoaded returned.");
            }
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
