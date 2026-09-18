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
    // A custom network message would be the obvious answer and is closed to
    // us: Il2CppInterop cannot marshal a delegate taking a non-blittable
    // struct, which is what sank the first version of
    // CosmeticGourdSpawnHandler. So each machine watches its own hands
    // instead, and needs nothing from the wire that is not already there.
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

        private float _nextCheck;

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
            if (prop == null || !IsStale(prop))
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
