using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Lets a player who is NOT the host actually see the cosmetic gourds the
    // host receives from Archipelago.
    //
    // The problem it solves (found in the first co-op session, 2026-09-15).
    // ReceivedItemSpawner clones a RewardGourd that is already in the scene
    // and spawns it over the network. That works on the host, where nothing
    // has to be instantiated, and fails on every remote client:
    //
    //     Failed to spawn server object, did you forget to add it to the
    //     NetworkManager? assetId=3383279366
    //
    // Mirror resolves a SCENE object by its sceneId and a dynamically
    // spawned one by its assetId, looked up in the client's table of
    // registered prefabs. A clone of a scene object is neither — its sceneId
    // is deliberately cleared (it is no longer that scene object) and its
    // assetId was never registered as a spawnable prefab. So the client had
    // nothing to build from and simply dropped the object.
    //
    // It did not stop there. The gourd the host was holding still made it
    // into the SyncVars, and the guest then tried to resolve a netId that
    // had never spawned:
    //
    //     OnDeserialize failed ... component=PlayerNetworking
    //       at PlayerNetworking.OnSetHeld (...)
    //
    // — breaking held-item sync for that player. Both symptoms come from the
    // one missing spawn.
    //
    // The fix is Mirror's own answer for objects that are not prefabs:
    // RegisterSpawnHandler. The server spawns under a constant assetId
    // (ReceivedItemSpawner.CosmeticAssetId), and every client registers a
    // handler for it that builds the clone from ITS OWN scene, with exactly
    // the same construction the host uses. Nothing about the gourd travels
    // over the wire beyond "build one here" — which is just as well, since
    // what is being cloned is a scene object each machine already has.
    //
    // NOT gated on NetworkServer.active, like VariantGourdMapUnlocker and
    // SecondEndingSphereUnlocker: registering on the host is harmless (host
    // mode never invokes the handler, the object is already local) and the
    // whole point is the machine that is NOT the host.
    internal class CosmeticGourdSpawnHandler : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public CosmeticGourdSpawnHandler(IntPtr ptr) : base(ptr)
        {
        }

        private bool _registeredForThisClient;
        private bool _registrationImpossible;
        private float _nextCheck;

        private const float CheckIntervalSeconds = 1f;

        // Props built by the handler, waiting to be let go of physically on
        // the next frame (see DrainPendingLoose). Static because the handler
        // Mirror calls is static, and there is only ever one of these
        // components.
        private static readonly List<Prop> PendingLoose = new();

        private void Update()
        {
            DrainPendingLoose();

            // Registration lives on NetworkClient, which is torn down and
            // rebuilt around each session: the handler has to be re-armed
            // every time rather than once per process. Same lesson as
            // ArchDoorUnlocker's one-shot, which silently never ran again
            // after the first world.
            if (_registrationImpossible)
                return;

            var clientActive = NetworkClient.active;
            if (!clientActive)
            {
                _registeredForThisClient = false;
                return;
            }

            if (Time.time < _nextCheck)
                return;

            _nextCheck = Time.time + CheckIntervalSeconds;

            // The authority on "am I registered" is Mirror's own table, not
            // a flag of ours: NetworkClient.ClearSpawners() empties it, and
            // a remembered "already done" would then be a lie for the rest
            // of the session — the handler silently gone while we believed
            // it armed. Polled once a second rather than every frame; a
            // dictionary lookup is cheap but not free.
            if (IsStillRegistered())
                return;

            if (_registeredForThisClient)
                Plugin.Log.LogInfo(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] Spawn handler was cleared from under us; registering again.");

            _registeredForThisClient = true;

            try
            {
                // The SpawnDelegate overload, NOT the SpawnHandlerDelegate
                // one. Both exist on NetworkClient, and the richer one takes
                // a SpawnMessage, which Il2CppInterop refuses to marshal:
                //
                //   Delegate has parameter of type Mirror.SpawnMessage
                //   (non-blittable struct) which is not supported
                //
                // (observed 2026-09-15 — the registration had never once
                // succeeded, on either machine). This signature is
                // (Vector3, uint), both blittable, so it goes through. What
                // it costs is the rotation carried by the message; a gourd
                // dropped on the ground has no meaningful orientation, and
                // the host's own copy does not use it either.
                NetworkClient.RegisterSpawnHandler(
                    ReceivedItemSpawner.CosmeticAssetId,
                    (SpawnDelegate)Spawn,
                    (UnSpawnDelegate)UnSpawn);

                Plugin.Log.LogInfo(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] Cosmetic gourd spawn handler registered (assetId {ReceivedItemSpawner.CosmeticAssetId}).");
            }
            catch (Exception ex)
            {
                // Said once and then dropped for good. This failure is a
                // property of the build, not a transient condition, so the
                // once-a-second retry above would otherwise write the same
                // line until the player quits — which is exactly what it did
                // before this guard.
                _registrationImpossible = true;
                Plugin.Log.LogWarning(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] Could not register the spawn handler; cosmetic gourds will not appear for this player, and this will not be retried: {ex.Message}");
            }
        }

        // Returns false when the answer cannot be had — better to re-register
        // needlessly (Mirror overwrites the entry) than to skip it because a
        // lookup threw.
        private static bool IsStillRegistered()
        {
            try
            {
                var handlers = NetworkClient.spawnHandlers;
                return handlers != null && handlers.ContainsKey(ReceivedItemSpawner.CosmeticAssetId);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] Could not read Mirror's spawn handler table ({ex.Message}); registering anyway.");
                return false;
            }
        }

        // A clone inherits the template's PHYSICAL state, which for a gourd
        // sitting in its clamp means fixed and weightless — the host calls
        // Prop.SetLoose() for exactly that reason, and each client has to do
        // the same for its own copy. Without it the gourd hangs in mid-air
        // on the other player's screen, ignoring gravity and refusing to be
        // picked up (seen in co-op, 2026-09-16).
        //
        // gourdState does NOT cover this. That SyncVar carries the puzzle's
        // visual and logical state and arrives from the server on its own;
        // the Prop's physics is local to each machine and no part of it
        // travels over the wire.
        //
        // Deferred by a frame, mirroring the host, where SetLoose comes
        // after NetworkServer.Spawn: called from inside the spawn handler it
        // would run before Mirror has finished applying the spawn, and the
        // state arriving with that spawn would overwrite ours.
        [HideFromIl2Cpp]
        private static void DrainPendingLoose()
        {
            if (PendingLoose.Count == 0)
                return;

            foreach (var prop in PendingLoose)
            {
                if (prop == null)
                    continue;

                try
                {
                    prop.SetLoose();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] Could not let go of a cosmetic gourd physically: {ex.Message}");
                }
            }

            PendingLoose.Clear();
        }

        // Mirror hands over where to put it and takes back the object to
        // attach the netId to, so the clone lands where the host put it.
        private static GameObject Spawn(Vector3 position, uint assetId)
        {
            try
            {
                var rewardGourd = ReceivedItemSpawner.BuildNeutralizedClone(position, Quaternion.identity);
                if (rewardGourd != null)
                {
                    // Logged because the ORDER matters and is not otherwise
                    // observable: the player's held-item SyncVar carries a
                    // reference to this object by netId, and Mirror resolves
                    // such a reference to null if the object has not spawned
                    // yet — which is what PlayerNetworking.OnSetHeld then
                    // dereferences. Seeing this line before or after that
                    // error is what tells the two apart.
                    Plugin.Log.LogInfo(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] Cosmetic gourd built at {position}.");

                    if (rewardGourd.prop != null)
                        PendingLoose.Add(rewardGourd.prop);

                    return rewardGourd.gameObject;
                }

                Plugin.Log.LogWarning(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] Nothing to clone from for an incoming cosmetic gourd; it will be missing for this player.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] Failed to build an incoming cosmetic gourd: {ex.Message}");
            }

            // Returning null is what Mirror expects when a handler cannot
            // produce the object; it logs and moves on rather than tearing
            // the connection down.
            return null;
        }

        private static void UnSpawn(GameObject spawned)
        {
            // Symmetrical with the build above: this clone belongs to no
            // pool and nothing else references it, so it is destroyed
            // outright rather than deactivated and kept.
            if (spawned == null)
                return;

            // Also logged: a clone that disappears moments after arriving
            // would look exactly like one that never arrived, and there is
            // previous form here (2026-09-11, a cosmetic clone found gone
            // from a dump seconds after spawning).
            Plugin.Log.LogInfo($"[{nameof(CosmeticGourdSpawnHandler)}] Cosmetic gourd removed by the server.");
            UnityEngine.Object.Destroy(spawned);
        }
    }
}
