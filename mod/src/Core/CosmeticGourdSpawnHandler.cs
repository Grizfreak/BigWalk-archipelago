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
        private static readonly List<RewardGourd> PendingSpawned = new();

        // When each pending clone stops being watched, by instance id.
        private static readonly Dictionary<int, float> SettleDeadline = new();

        // How long a freshly built clone is given to be claimed by its
        // holder before it is dropped on the floor. It has to cover a round
        // trip: the host spawns on one frame and picks up on the next, and
        // the SyncVar saying so only reaches this machine some frames after
        // that. Generous on purpose — the cost of waiting is a gourd that
        // hangs still for a moment, the cost of being early is a gourd
        // snatched out of someone's hands.
        private const float SettleSeconds = 3f;

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
            if (PendingSpawned.Count == 0)
                return;

            var now = Time.time;
            var doneWith = new List<RewardGourd>();

            foreach (var rewardGourd in PendingSpawned)
            {
                if (rewardGourd == null)
                {
                    doneWith.Add(rewardGourd);
                    continue;
                }

                var prop = rewardGourd.prop;
                if (prop == null)
                    continue;

                // The netId is the whole question. "This player is holding
                // object N" travels as a netId, so a clone Mirror never
                // registered cannot be the N in that sentence — and nothing
                // would be logged as an error, which is exactly what we see.
                // Printed with the holder so the two machines' logs can be
                // laid side by side.
                var id = rewardGourd.GetInstanceID();
                if (!SettleDeadline.TryGetValue(id, out var deadline))
                {
                    deadline = now + SettleSeconds;
                    SettleDeadline[id] = deadline;

                    // Once, on the first pass over a new clone: Mirror ran
                    // its spawn payload over the object after the handler
                    // returned and took the colour with it. Re-applying it
                    // every frame of the watch would be pure waste.
                    ReceivedItemSpawner.ReapplyCosmeticLook(rewardGourd);
                }

                // Claimed by someone: leave it alone for good. SetLoose is
                // what stops a clone hanging in mid-air, but it is also the
                // exact opposite of being held.
                // Before believing the claim, make it true here: the server
                // may have handed this to the local player without their own
                // hands ever hearing about it (see TryAttachToLocalHands).
                TryAttachToLocalHands(prop);

                var holder = FindHolderName(prop);
                if (holder != null)
                {
                    var identity = rewardGourd.GetComponent<Mirror.NetworkIdentity>();
                    Plugin.Log.LogInfo(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] netId {(identity != null ? identity.netId : 0)} claimed by {holder}; leaving its physics alone.");
                    doneWith.Add(rewardGourd);
                    continue;
                }

                // Still nobody's, and out of time: it really is lying
                // around, so let it fall.
                //
                // The first version asked this question ONE frame after the
                // spawn and acted on the answer immediately, which was
                // always going to be wrong: one frame is how long the host
                // takes to SEND the hand-over, not how long this machine
                // takes to receive it. Every gourd was therefore dropped
                // just before being told it was held, and no player ever saw
                // anything in anyone's hands (co-op, 2026-09-20; the netIds
                // matched on both sides, which is what ruled out everything
                // else).
                if (now < deadline)
                    continue;

                var timedOutIdentity = rewardGourd.GetComponent<Mirror.NetworkIdentity>();
                Plugin.Log.LogInfo(
                    $"[{nameof(CosmeticGourdSpawnHandler)}] netId {(timedOutIdentity != null ? timedOutIdentity.netId : 0)} unclaimed after {SettleSeconds:0}s; dropping it.");

                try
                {
                    prop.SetLoose();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] Could not let go of a cosmetic gourd physically: {ex.Message}");
                }

                doneWith.Add(rewardGourd);
            }

            foreach (var done in doneWith)
            {
                PendingSpawned.Remove(done);
                if (done != null)
                    SettleDeadline.Remove(done.GetInstanceID());
            }
        }

        // Asks BOTH places a hold can be recorded, because they are not the
        // same place on every machine.
        //
        // PlayerHands.heldProp is the local, gameplay-side answer: it is set
        // on the machine whose player did the picking up. PlayerNetworking.
        // playerHeldInformation is the networked one — a SyncVar carrying a
        // NetworkIdentity — and on a remote client it may well be the only
        // one that is filled in, since nothing there ever ran PickUp.
        //
        // Looking only at heldProp is what made this machine report "held by
        // nobody" for three full seconds while the host was plainly holding
        // the gourd (co-op, 2026-09-20). The netIds matched on both sides,
        // so the object and the message were never in doubt — the question
        // was being put to the wrong field.
        // Internal rather than private: GadgetSpawnHandler's own settle loop
        // (same "who is holding this, on either machine's record" question,
        // for the island's own hand props instead of a gourd) reuses this
        // rather than duplicating it.
        [HideFromIl2Cpp]
        // Puts into the local player's hands what the server says they are
        // already holding.
        //
        // MEASURED IN CO-OP, 2026-09-23: a gourd handed to the GUEST showed
        // in the guest's hands on the host's screen, and hung in mid-air on
        // the guest's own. The reverse case — a gourd handed to the host,
        // watched from the guest — has worked since 2026-09-20, which is why
        // this went unnoticed for three days.
        //
        // The log had already been naming it without anyone reading it that
        // way: `claimed by <player> (via SyncVar)` means the server's
        // PlayerHeldInformation points at this prop while that machine's own
        // `hands.heldProp` does not. For a REMOTE player that is normal and
        // nothing to act on — their machine holds it, ours only draws it.
        // For the LOCAL player it is the bug itself: nothing ever ran the
        // pickup here, because the pickup was decided on the host.
        //
        // So this machine runs it. `PlayerHands.PickUp` is what a real
        // pickup calls, joint and grasper included; re-asserting a state the
        // server already holds is idempotent, which is the harmless
        // direction for a race between the two.
        internal static bool TryAttachToLocalHands(Prop prop)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null || prop == null)
                return false;

            var propIdentity = prop.GetComponent<Mirror.NetworkIdentity>();
            if (propIdentity == null)
                return false;

            foreach (var pc in players)
            {
                if (pc == null || !pc.isLocalPlayer)
                    continue;

                var hands = pc.hands;
                if (hands == null || hands.heldProp == prop)
                    return false;

                var networking = pc.playerNetworking;
                if (networking == null)
                    return false;

                try
                {
                    var held = networking.playerHeldInformation;
                    if (held.identity == null || held.identity != propIdentity)
                        return false;

                    hands.PickUp(prop);
                    Plugin.Log.LogInfo(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] netId {propIdentity.netId} was handed to this player "
                        + "by the server but never reached their hands; picking it up locally.");
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] Could not pick up a prop the server says we hold: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        internal static string FindHolderName(Prop prop)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null || prop == null)
                return null;

            var propIdentity = prop.GetComponent<Mirror.NetworkIdentity>();

            foreach (var pc in players)
            {
                if (pc == null)
                    continue;

                if (pc.hands != null && pc.hands.heldProp == prop)
                    return pc.gameObject.name;

                var networking = pc.playerNetworking;
                if (networking == null || propIdentity == null)
                    continue;

                try
                {
                    var held = networking.playerHeldInformation;
                    if (held.identity != null && held.identity == propIdentity)
                        return $"{pc.gameObject.name} (via SyncVar)";
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(CosmeticGourdSpawnHandler)}] Could not read a player's held information: {ex.Message}");
                }
            }

            return null;
        }

        [HideFromIl2Cpp]
        private static bool IsHeldByAnyone(Prop prop)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return false;

            foreach (var pc in players)
            {
                if (pc != null && pc.hands != null && pc.hands.heldProp == prop)
                    return true;
            }

            return false;
        }

        // Mirror hands over where to put it and takes back the object to
        // attach the netId to, so the clone lands where the host put it.
        private static GameObject Spawn(Vector3 position, uint assetId)
        {
            Net.ModChannel.NoteHostHasMod("a gourd of ours was spawned");

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

                    PendingSpawned.Add(rewardGourd);

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
