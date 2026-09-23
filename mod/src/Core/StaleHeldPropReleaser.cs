using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Frees the LOCAL player's hands when what they are holding has been
    // taken away by the server.
    //
    // Why this cannot be done host-side. When a puzzle's gourd goes Loose on
    // pickup (the sealed-box kind, freed by a remote button),
    // GourdStatePatch reports the check and then hides and unspawns the
    // gourd. It calls ReceivedItemSpawner.ReleaseFromHands first so nobody
    // is left holding a vanished object — and that works, for the host.
    // Tried against a real second player on 2026-09-15: the guest picked up
    // the gourd, the check fired correctly, and the gourd STAYED IN THEIR
    // HANDS until they threw it, at which point it vanished. Neither reading
    // a remote player's hands nor calling Drop() on them from the host
    // replicates — which the co-op test list had flagged as unverified, and
    // now is not.
    //
    // A custom network message looked like the obvious answer and was
    // believed closed: Il2CppInterop cannot marshal a delegate taking a
    // non-blittable struct, which is what sank the first version of
    // CosmeticGourdSpawnHandler. That is true of Mirror's TYPED messages
    // only — the raw handler route is open, and Core/Net/ModChannel uses it
    // since 2026-09-23. This stays as it is regardless: each machine watching
    // its own hands needs nothing from the wire, which is the sturdier design
    // for something that must work on a guest the host cannot reach.
    //
    // The signal is "still held, but no longer really there": a prop whose
    // GameObject has been deactivated, or whose NetworkIdentity no longer
    // has a netId because the server unspawned it. A legitimately held prop
    // is active and spawned, so this does not fire on ordinary play.
    //
    // Deliberately polled rather than hooked. The SyncVar hook
    // (RewardGourd.OnChangeGourdState) looked like the natural place, but
    // the server unspawns the object in the same breath as changing the
    // state: whether the client still has an object to run a hook ON is a
    // race, and a poll does not care who won it.
    internal class StaleHeldPropReleaser : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public StaleHeldPropReleaser(IntPtr ptr) : base(ptr)
        {
        }

        private const float CheckIntervalSeconds = 0.25f;

        // How long the server and these hands have to disagree before the
        // hands give in. Four polls, and the reason it is not zero is the
        // ordinary pickup: a client attaches a prop locally first and tells
        // the server after, so for a few frames every legitimate pickup looks
        // exactly like the desync below. One second is far longer than that
        // round trip and far shorter than a player notices.
        private const float DisagreementGraceSeconds = 1f;

        private float _nextCheck;

        private static Prop _disputed;

        private static float _disputedSince;

        private void Update()
        {
            if (Time.time < _nextCheck)
                return;

            _nextCheck = Time.time + CheckIntervalSeconds;

            try
            {
                ReleaseIfStale();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(StaleHeldPropReleaser)}] Check failed, ignored: {ex.Message}");
            }
        }

        private static void ReleaseIfStale()
        {
            var hands = FindLocalHands();
            var prop = hands != null ? hands.heldProp : null;
            if (prop == null)
            {
                _disputed = null;
                return;
            }

            if (!IsStale(prop) && !ServerDisagrees(prop))
                return;

            Plugin.Log.LogInfo(
                $"[{nameof(StaleHeldPropReleaser)}] Holding a prop the server has taken away; letting go of it.");

            try
            {
                hands.Drop(new PlayerHeldInformation(prop));
            }
            catch (Exception ex)
            {
                // Same last resort as ReceivedItemSpawner.ReleaseFromHands:
                // better empty hands than hands pointing at nothing.
                Plugin.Log.LogWarning($"[{nameof(StaleHeldPropReleaser)}] Drop failed, clearing the hand directly: {ex.Message}");
                try { hands.heldProp = null; }
                catch (Exception inner) { Plugin.Log.LogWarning($"[{nameof(StaleHeldPropReleaser)}] ...and that failed too: {inner.Message}"); }
            }
        }

        // The second way a held prop can be a lie, and the one Ctrl+R
        // produces (measured in co-op, 2026-09-23): the host reclaims a big
        // key a guest is carrying, and on the guest it stays in their hands.
        // Nothing about it is stale in the sense above — the object is alive,
        // spawned and active, because reclaiming a key TELEPORTS it rather
        // than unspawning it. What has changed is the only thing the server
        // can tell us without a message of its own: this player's
        // PlayerHeldInformation no longer names what these hands are holding.
        //
        // Exactly the mirror of CosmeticGourdSpawnHandler.TryAttachToLocalHands,
        // which fixes the same disagreement pointing the other way, and it
        // needs nothing from the wire that is not already there.
        private static bool ServerDisagrees(Prop prop)
        {
            var identity = prop.GetComponent<NetworkIdentity>();
            if (identity == null)
                return false;

            var networking = FindLocalNetworking();
            if (networking == null)
                return false;

            bool disagrees;
            try
            {
                var held = networking.playerHeldInformation;
                disagrees = held.identity == null || held.identity != identity;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(StaleHeldPropReleaser)}] Could not read our own held information: {ex.Message}");
                return false;
            }

            if (!disagrees)
            {
                _disputed = null;
                return false;
            }

            if (_disputed != prop)
            {
                _disputed = prop;
                _disputedSince = Time.time;
                return false;
            }

            if (Time.time - _disputedSince < DisagreementGraceSeconds)
                return false;

            Plugin.Log.LogInfo(
                $"[{nameof(StaleHeldPropReleaser)}] The server stopped saying we hold netId {identity.netId} "
                + $"{DisagreementGraceSeconds:0}s ago; letting go of it.");
            _disputed = null;
            return true;
        }

        private static PlayerNetworking FindLocalNetworking()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return null;

            foreach (var pc in players)
            {
                if (pc != null && pc.isLocalPlayer)
                    return pc.playerNetworking;
            }

            return null;
        }

        private static bool IsStale(Prop prop)
        {
            var go = prop.gameObject;
            if (go == null || !go.activeInHierarchy)
                return true;

            // netId drops back to 0 when the server unspawns an object, so a
            // networked prop still in hand with no netId is one the server
            // has already withdrawn. A prop with no NetworkIdentity at all
            // is not networked and is none of our business.
            var identity = prop.GetComponent<NetworkIdentity>();
            return identity != null && identity.netId == 0;
        }

        private static PlayerHands FindLocalHands()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return null;

            foreach (var pc in players)
            {
                if (pc != null && pc.isLocalPlayer)
                    return pc.hands;
            }

            return null;
        }
    }
}
