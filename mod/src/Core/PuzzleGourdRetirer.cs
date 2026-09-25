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

        // A sweep is a FindObjectsByType over every gourd, so not every frame.
        private const float SweepIntervalSeconds = 3f;

        private float _nextSweep;

        internal static void Retire(RewardGourd gourd)
        {
            if (gourd != null)
                Pending.Enqueue((gourd, Time.frameCount));
        }

        private void Update()
        {
            if (NetworkServer.active && WorldManager.isReadyForEffects && Time.time >= _nextSweep)
            {
                _nextSweep = Time.time + SweepIntervalSeconds;
                try
                {
                    SweepStrayGourds();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(PuzzleGourdRetirer)}] Sweep failed, ignored: {ex.Message}");
                }
            }

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

        // THE GOURDS THAT COME BACK IN A BAG (2026-09-25, first alpha: "any
        // gourds being stored in the inventory appear at spawn, and picking
        // them up makes them disappear").
        //
        // The game's inventory is not a list of gourds. Dumped on a vanilla
        // save, `SaveData.inventory` held only carrying gear and gadgets —
        // belts, backpacks, the gourd carton — which the game puts back at
        // an InventorySpawn on every load, contents and all. A puzzle gourd
        // stowed in one comes back at the spawn with it. In this world no
        // puzzle gourd should be anywhere but in its vise or gone: the alpha's
        // got into a bag while the sealed-box pick-up still left the gourd in
        // the host's hands (see the top of this file). Picking one up then
        // made it go Loose, GourdStatePatch hid it, and it vanished — and at
        // every guest's join the server complained about three unspawned
        // GourdProps still referenced, the ones sitting in those bags.
        //
        // So, host side and on a timer since zones stream in: any puzzle
        // gourd that is neither in its vise (Locked) nor in a real home is
        // taken out of whatever holds it, its check reported if it never was,
        // and retired like any solved gourd. Locked is never touched — that
        // is an unsolved puzzle — and neither is a gourd in a home with no
        // parent prop (a valet or a monument slot).
        private static void SweepStrayGourds()
        {
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return;

            foreach (var gourd in all)
            {
                var prop = gourd != null ? gourd.prop : null;
                if (prop == null || ReceivedItemSpawner.IsCosmeticClone(prop)
                    || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out var locationId))
                    continue;

                var state = gourd.gourdState;
                if (state == GourdFlag.GourdState.Locked)
                    continue;

                var home = prop.currentHome;
                var inContainer = home != null && home.parentProp != null;
                var strayOnItsOwn = home == null && state != GourdFlag.GourdState.Hidden
                    && gourd.gameObject.activeInHierarchy;
                if (!inContainer && !strayOnItsOwn)
                    continue;

                Plugin.Log.LogInfo(
                    $"[{nameof(PuzzleGourdRetirer)}] {prop.saveablePropName} was {state} "
                    + (inContainer ? $"inside {home.parentProp.gameObject.name}" : "outside its vise")
                    + "; taking it out of play.");

                if (inContainer)
                {
                    try { prop.ServerSetUnpinned(); }
                    catch (Exception ex) { Plugin.Log.LogWarning($"[{nameof(PuzzleGourdRetirer)}] Could not unpin it: {ex.Message}"); }
                }

                if (CheckTracker.TryMarkReported(locationId))
                    Plugin.Reporter.ReportCheck(locationId);

                if (state != GourdFlag.GourdState.Hidden)
                    gourd.ServerSetGourdState(GourdFlag.GourdState.Hidden);

                Retire(gourd);
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
