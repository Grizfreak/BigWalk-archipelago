using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Takes a solved puzzle's gourd off the network, one frame after the
    // puzzle let go of it — never in the same breath.
    //
    // WHY NOT AT ONCE (2026-09-25, from a host's log of the first alpha and
    // the game's own code). On the puzzles whose gourd is sealed in a box,
    // the gourd goes Loose ON PICKUP, and it does so from inside the server
    // half of the pick-up Command. Decompiled, `PlayerNetworking.
    // UserCode_CmdPickUp` runs, in order: IsSafeToPickUp, then
    // `Prop.ServerSetUnpinned` — which is what frees the gourd and so runs
    // GourdStatePatch — then writes NetworkplayerHeldInformation and lets
    // its hook call PlayerHands.PickUp. GourdStatePatch used to unspawn and
    // deactivate the gourd right there, so the Command went on to hand a
    // player a gourd that no longer existed:
    //
    //   - the host threw a NullReferenceException in Prop.SetHeld, under
    //     set_NetworkplayerHeldInformation, in the middle of the Command;
    //   - the server was left saying the guest held an unspawned object
    //     ("Attempted to serialize unspawned GameObject: GourdProp" at every
    //     later join), and the guest was left unable to pick anything up on
    //     their own screen while the host watched them do it.
    //
    // Clearing the hands first did not help: the Command's own write came
    // after it and put the dead gourd straight back.
    //
    // A frame later the Command has finished, the hold it published is the
    // real one, and clearing it reaches the player like any other change.
    internal class PuzzleGourdRetirer : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public PuzzleGourdRetirer(IntPtr ptr) : base(ptr)
        {
        }

        private static readonly Queue<(RewardGourd gourd, int frame)> Pending = new();

        internal static void Retire(RewardGourd gourd)
        {
            if (gourd != null)
                Pending.Enqueue((gourd, Time.frameCount));
        }

        private void Update()
        {
            while (Pending.Count > 0 && Pending.Peek().frame < Time.frameCount)
            {
                var (gourd, _) = Pending.Dequeue();
                try
                {
                    RetireNow(gourd);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(PuzzleGourdRetirer)}] Could not remove a solved gourd: {ex.Message}");
                }
            }
        }

        private static void RetireNow(RewardGourd gourd)
        {
            if (gourd == null || !NetworkServer.active)
                return;

            // Whoever the Command just gave it to lets go first, so nobody is
            // left holding an object about to vanish. Observed in co-op on
            // 2026-09-15 with gourdTelescopeToBox, before any of this: the
            // gourd stayed in the host's hands until they tried to drop it.
            ReceivedItemSpawner.ReleaseFromHands(gourd.prop);

            NetworkServer.UnSpawn(gourd.gameObject);
            gourd.gameObject.SetActive(false);
        }
    }
}
