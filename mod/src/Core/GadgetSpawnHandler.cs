using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Client-side counterpart of GadgetItemSpawner, exactly
    // CosmeticGourdSpawnHandler's reasoning applied to the island's own hand
    // props instead of a RewardGourd: NetworkServer.Spawn alone never
    // reaches a remote client for an object that is neither a scene object
    // (its sceneId was cleared) nor a registered prefab, so each client
    // registers a Mirror spawn handler per GadgetKind's assetId and builds
    // its own clone locally rather than trusting anything to travel over
    // the wire.
    //
    // One handler PER GadgetKind, each a separate static method rather than
    // a closure over the enum value — SpawnDelegate crosses the IL2CPP
    // interop boundary, and the only cast proven to work there (cf.
    // CosmeticGourdSpawnHandler) is a plain static method group; a capturing
    // lambda through that boundary is untested territory this mod has no
    // reason to be the first to try.
    internal class GadgetSpawnHandler : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public GadgetSpawnHandler(IntPtr ptr) : base(ptr)
        {
        }

        private bool _registeredForThisClient;
        private bool _registrationImpossible;
        private float _nextCheck;

        private const float CheckIntervalSeconds = 1f;

        private static readonly List<Prop> PendingSpawned = new();
        private static readonly Dictionary<int, float> SettleDeadline = new();
        private const float SettleSeconds = 3f;

        private void Update()
        {
            DrainPendingLoose();

            if (_registrationImpossible)
                return;

            if (!NetworkClient.active)
            {
                _registeredForThisClient = false;
                return;
            }

            if (Time.time < _nextCheck)
                return;

            _nextCheck = Time.time + CheckIntervalSeconds;

            if (IsStillRegistered())
                return;

            if (_registeredForThisClient)
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetSpawnHandler)}] Spawn handlers were cleared from under us; registering again.");

            _registeredForThisClient = true;

            try
            {
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Megaphone], (SpawnDelegate)SpawnMegaphone, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.WalkieTalkie], (SpawnDelegate)SpawnWalkieTalkie, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Backpack], (SpawnDelegate)SpawnBackpack, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Belt], (SpawnDelegate)SpawnBelt, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.FlareGun], (SpawnDelegate)SpawnFlareGun, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Laser], (SpawnDelegate)SpawnLaser, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Binoculars], (SpawnDelegate)SpawnBinoculars, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Compass], (SpawnDelegate)SpawnCompass, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.FoldingMap], (SpawnDelegate)SpawnFoldingMap, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Radio], (SpawnDelegate)SpawnRadio, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.GourdCarton], (SpawnDelegate)SpawnGourdCarton, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Torch], (SpawnDelegate)SpawnTorch, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.Lamp], (SpawnDelegate)SpawnLamp, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.XrayGoggles], (SpawnDelegate)SpawnXrayGoggles, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.FlareGunBlue], (SpawnDelegate)SpawnFlareGunBlue, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.FlareGunGreen], (SpawnDelegate)SpawnFlareGunGreen, (UnSpawnDelegate)UnSpawn);
                NetworkClient.RegisterSpawnHandler(
                    GadgetItemSpawner.AssetIds[GadgetKind.FlareGunYellow], (SpawnDelegate)SpawnFlareGunYellow, (UnSpawnDelegate)UnSpawn);

                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetSpawnHandler)}] Gadget spawn handlers registered ({GadgetItemSpawner.AssetIds.Count}).");
            }
            catch (Exception ex)
            {
                _registrationImpossible = true;
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetSpawnHandler)}] Could not register the spawn handlers; gadget items will not appear for this player, and this will not be retried: {ex.Message}");
            }
        }

        private static bool IsStillRegistered()
        {
            try
            {
                var handlers = NetworkClient.spawnHandlers;
                if (handlers == null)
                    return false;

                foreach (var assetId in GadgetItemSpawner.AssetIds.Values)
                {
                    if (!handlers.ContainsKey(assetId))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetSpawnHandler)}] Could not read Mirror's spawn handler table ({ex.Message}); registering anyway.");
                return false;
            }
        }

        // A clone inherits the template's physical state (fixed, no
        // gravity) — SetLoose() here is this machine's own copy of the
        // SpawnCosmeticPickup call the host already made for itself.
        // Deferred by a frame past the handler itself, same reasoning as
        // CosmeticGourdSpawnHandler: called immediately it would run before
        // Mirror has finished applying the spawn payload.
        [HideFromIl2Cpp]
        private static void DrainPendingLoose()
        {
            if (PendingSpawned.Count == 0)
                return;

            var now = Time.time;
            var doneWith = new List<Prop>();

            foreach (var prop in PendingSpawned)
            {
                if (prop == null)
                {
                    doneWith.Add(prop);
                    continue;
                }

                var id = prop.GetInstanceID();
                if (!SettleDeadline.TryGetValue(id, out var deadline))
                {
                    deadline = now + SettleSeconds;
                    SettleDeadline[id] = deadline;
                }

                // Claimed by someone: leave its physics alone, same
                // reasoning as the gourd handler.
                var holder = CosmeticGourdSpawnHandler.FindHolderName(prop);
                if (holder != null)
                {
                    var identity = prop.GetComponent<NetworkIdentity>();
                    Plugin.Log.LogInfo(
                        $"[{nameof(GadgetSpawnHandler)}] netId {(identity != null ? identity.netId : 0)} claimed by {holder}; leaving its physics alone.");
                    doneWith.Add(prop);
                    continue;
                }

                if (now < deadline)
                    continue;

                var timedOutIdentity = prop.GetComponent<NetworkIdentity>();
                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetSpawnHandler)}] netId {(timedOutIdentity != null ? timedOutIdentity.netId : 0)} unclaimed after {SettleSeconds:0}s; dropping it.");

                try
                {
                    prop.SetLoose();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(GadgetSpawnHandler)}] Could not let go of a cosmetic gadget physically: {ex.Message}");
                }

                doneWith.Add(prop);
            }

            foreach (var done in doneWith)
            {
                PendingSpawned.Remove(done);
                if (done != null)
                    SettleDeadline.Remove(done.GetInstanceID());
            }
        }

        private static GameObject SpawnMegaphone(Vector3 position, uint assetId) => Spawn(GadgetKind.Megaphone, position, assetId);
        private static GameObject SpawnWalkieTalkie(Vector3 position, uint assetId) => Spawn(GadgetKind.WalkieTalkie, position, assetId);
        private static GameObject SpawnBackpack(Vector3 position, uint assetId) => Spawn(GadgetKind.Backpack, position, assetId);
        private static GameObject SpawnBelt(Vector3 position, uint assetId) => Spawn(GadgetKind.Belt, position, assetId);
        private static GameObject SpawnFlareGun(Vector3 position, uint assetId) => Spawn(GadgetKind.FlareGun, position, assetId);
        private static GameObject SpawnLaser(Vector3 position, uint assetId) => Spawn(GadgetKind.Laser, position, assetId);
        private static GameObject SpawnBinoculars(Vector3 position, uint assetId) => Spawn(GadgetKind.Binoculars, position, assetId);
        private static GameObject SpawnCompass(Vector3 position, uint assetId) => Spawn(GadgetKind.Compass, position, assetId);
        private static GameObject SpawnFoldingMap(Vector3 position, uint assetId) => Spawn(GadgetKind.FoldingMap, position, assetId);
        private static GameObject SpawnRadio(Vector3 position, uint assetId) => Spawn(GadgetKind.Radio, position, assetId);
        private static GameObject SpawnGourdCarton(Vector3 position, uint assetId) => Spawn(GadgetKind.GourdCarton, position, assetId);
        private static GameObject SpawnTorch(Vector3 position, uint assetId) => Spawn(GadgetKind.Torch, position, assetId);
        private static GameObject SpawnLamp(Vector3 position, uint assetId) => Spawn(GadgetKind.Lamp, position, assetId);
        private static GameObject SpawnXrayGoggles(Vector3 position, uint assetId) => Spawn(GadgetKind.XrayGoggles, position, assetId);
        private static GameObject SpawnFlareGunBlue(Vector3 position, uint assetId) => Spawn(GadgetKind.FlareGunBlue, position, assetId);
        private static GameObject SpawnFlareGunGreen(Vector3 position, uint assetId) => Spawn(GadgetKind.FlareGunGreen, position, assetId);
        private static GameObject SpawnFlareGunYellow(Vector3 position, uint assetId) => Spawn(GadgetKind.FlareGunYellow, position, assetId);

        // THIS MUST NEVER RETURN NULL, and it used to (2026-09-22, found in
        // co-op the first time a guest was sent a gadget it could not build).
        // Mirror leaves the netId unresolved, the holder's
        // PlayerHeldInformation then dereferences a null identity inside
        // DeserializeSyncVars, and that player's network state is broken for
        // the rest of the session — hundreds of exceptions a second, gourds
        // no longer claimed, the lot. The reasoning and the two fallbacks are
        // in GadgetItemSpawner; the rule lives here, where the null was.
        private static GameObject Spawn(GadgetKind kind, Vector3 position, uint assetId)
        {
            try
            {
                var prop = GadgetItemSpawner.BuildNeutralizedClone(kind, position, Quaternion.identity);
                if (prop != null)
                {
                    Plugin.Log.LogInfo($"[{nameof(GadgetSpawnHandler)}] Cosmetic {kind} built at {position}.");
                    PendingSpawned.Add(prop);
                    return prop.gameObject;
                }

                var standIn = GadgetItemSpawner.BuildStandInClone(kind, position, Quaternion.identity);
                if (standIn != null)
                {
                    PendingSpawned.Add(standIn);
                    return standIn.gameObject;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(GadgetSpawnHandler)}] Failed to build an incoming {kind}: {ex.Message}");
            }

            return GadgetItemSpawner.BuildResolvablePlaceholder(kind, position);
        }

        private static void UnSpawn(GameObject spawned)
        {
            if (spawned == null)
                return;

            Plugin.Log.LogInfo($"[{nameof(GadgetSpawnHandler)}] Cosmetic gadget removed by the server.");
            UnityEngine.Object.Destroy(spawned);
        }
    }
}
