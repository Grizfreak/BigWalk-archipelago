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
    // One static method behind every asset id, reading the kind and the
    // vanilla instance back out of the id Mirror hands it. Still a plain
    // static method group rather than a closure: SpawnDelegate crosses the
    // IL2CPP interop boundary, and that is the only cast proven to work
    // there (cf. CosmeticGourdSpawnHandler). Until 2026-09-23 there was one
    // method per kind; the id already carried everything, it simply had
    // not needed to carry the instance.
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
                var spawn = (SpawnDelegate)SpawnFromAssetId;
                var unspawn = (UnSpawnDelegate)UnSpawn;
                var registered = 0;
                foreach (var assetId in GadgetItemSpawner.AllAssetIds())
                {
                    NetworkClient.RegisterSpawnHandler(assetId, spawn, unspawn);
                    registered++;
                }

                Plugin.Log.LogInfo(
                    $"[{nameof(GadgetSpawnHandler)}] Gadget spawn handlers registered ({registered}, one per kind and vanilla instance).");
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

                foreach (var assetId in GadgetItemSpawner.AllAssetIds())
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

                // Same two steps as the gourd handler: make the server's
                // claim true on this machine if it names the local player,
                // then leave a genuinely held prop's physics alone.
                CosmeticGourdSpawnHandler.TryAttachToLocalHands(prop);

                var holder = CosmeticGourdSpawnHandler.FindHolderName(prop);
                if (holder != null)
                {
                    var identity = prop.GetComponent<NetworkIdentity>();
                    Plugin.Log.LogInfo(
                        $"[{nameof(GadgetSpawnHandler)}] netId {(identity != null ? identity.netId : 0)} claimed by {holder}; leaving its physics alone.");
                    doneWith.Add(prop);
                    continue;
                }

                // Worn or stowed: taking it loose would pull a backpack off
                // somebody's back (see CosmeticGourdSpawnHandler.FindHomeName).
                var homedIn = CosmeticGourdSpawnHandler.FindHomeName(prop);
                if (homedIn != null)
                {
                    var identity = prop.GetComponent<NetworkIdentity>();
                    Plugin.Log.LogInfo(
                        $"[{nameof(GadgetSpawnHandler)}] netId {(identity != null ? identity.netId : 0)} sits in {homedIn}; leaving it there.");
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

        private static GameObject SpawnFromAssetId(Vector3 position, uint assetId)
        {
            Net.ModChannel.NoteHostHasMod("a gadget of ours was spawned");

            if (!GadgetItemSpawner.TryDecodeAssetId(assetId, out var kind, out var index))
            {
                // Not expected — the handler is only registered for ids this
                // decodes — but the rule below holds here too: never null.
                Plugin.Log.LogWarning(
                    $"[{nameof(GadgetSpawnHandler)}] Asset id {assetId:X} names no gadget; placing an empty stand-in.");
                return GadgetItemSpawner.BuildResolvablePlaceholder(GadgetKind.Megaphone, position);
            }

            return Spawn(kind, index, position, assetId);
        }

        // THIS MUST NEVER RETURN NULL, and it used to (2026-09-22, found in
        // co-op the first time a guest was sent a gadget it could not build).
        // Mirror leaves the netId unresolved, the holder's
        // PlayerHeldInformation then dereferences a null identity inside
        // DeserializeSyncVars, and that player's network state is broken for
        // the rest of the session — hundreds of exceptions a second, gourds
        // no longer claimed, the lot. The reasoning and the two fallbacks are
        // in GadgetItemSpawner; the rule lives here, where the null was.
        private static GameObject Spawn(GadgetKind kind, int index, Vector3 position, uint assetId)
        {
            try
            {
                var prop = GadgetItemSpawner.BuildNeutralizedClone(kind, position, Quaternion.identity, index);
                if (prop != null)
                {
                    Plugin.Log.LogInfo($"[{nameof(GadgetSpawnHandler)}] Cosmetic {kind} #{index} built at {position}.");
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
