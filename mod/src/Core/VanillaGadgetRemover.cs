using System;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Removes the vanilla instances of the gadget props used as Archipelago
    // filler items (megaphone, walkie-talkie, backpack, belt, flare gun)
    // from the map, so a filler item cannot also be found lying around for
    // free — player decision, 2026-09-22 ("they should be removed also in
    // the map to avoid having backpacks, and standard gunflares").
    //
    // Two halves, and since 2026-09-22 both run the same way on every
    // machine — which is the fix, not a simplification:
    //   1. GadgetItemSpawner.CaptureTemplates() — purely local. Each machine
    //      clones a private, inactive copy of whatever it currently sees
    //      loaded, BEFORE anything is taken away, so it has something to
    //      build future cosmetic pickups from later in the session (mirrors
    //      CosmeticGourdSpawnHandler's "each client builds its own from its
    //      own scene").
    //   2. GadgetItemSpawner.HideVanillaInstances() — also purely local, and
    //      also on every machine.
    //
    // Half 2 was host-only and used NetworkServer.Destroy until a co-op
    // session on 2026-09-22, on the reasoning that SetActive(false) does not
    // replicate and the props therefore had to be destroyed to leave
    // everybody's map. The replication was the bug: a guest arrives in a
    // world whose props the host destroyed during its own load, half 1 finds
    // nothing to capture, and the first gadget the host is sent reaches a
    // client that cannot build it — which, through a Mirror spawn handler
    // returning null, breaks that player's network state outright. Each
    // machine hiding its own copies needs nothing to travel, and a guest who
    // joins an hour later still finds the props in its scene long enough to
    // capture them.
    //
    // A consequence worth knowing: the switch below is each machine's own.
    // A guest who turns Archipelago off in their own config keeps seeing the
    // vanilla gadgets the others have hidden, and could pick one up. That is
    // the same shape as the radio wrinkle — only the host runs a client, so
    // only the host can know — and the default is on.
    //
    // Timing and re-arming follow ArchDoorUnlocker's already-learned lesson:
    // poll WorldManager.isReadyForEffects rather than a one-shot start
    // event (too early — "no peck manager instance" observed there), and
    // re-arm the "already ran" flag on every world load rather than latching
    // it once for the life of the process, since going back to the main menu
    // and hosting another save reloads the world without restarting BepInEx.
    // A freshly loaded world also has fresh instances of these props to
    // capture and remove — nothing suggests they are scoped to a single
    // zone any more than gourds, broadcast stations or key blanks were
    // (2026-09-21 finding: the game instantiates those everywhere).
    internal class VanillaGadgetRemover : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public VanillaGadgetRemover(IntPtr ptr) : base(ptr)
        {
        }

        private bool _worldWasReady;
        private float _nextRecheck;

        private const float RecheckIntervalSeconds = 1f;

        private void Update()
        {
            var worldReady = WorldManager.isReadyForEffects;
            if (!worldReady)
            {
                _worldWasReady = false;
                GadgetItemSpawner.ForgetWorld();
                return;
            }

            if (_worldWasReady)
            {
                // Mirror calls SetActive(true) on a scene object when it
                // spawns it, so a spawn wave arriving after the sweep — a
                // late joiner's, or interest management rebuilding observers
                // — can put back what was hidden. Cheap to check: it walks
                // the objects already hidden, not every Prop in the world.
                if (ModConfig.ArchipelagoEnabled.Value && Time.time >= _nextRecheck)
                {
                    _nextRecheck = Time.time + RecheckIntervalSeconds;
                    GadgetItemSpawner.ReHideVanillaInstances();
                }

                return;
            }

            _worldWasReady = true;
            _nextRecheck = Time.time + RecheckIntervalSeconds;

            // Every machine, host or guest: this is a local snapshot, not an
            // authoritative write.
            GadgetItemSpawner.CaptureTemplates();

            // Not while Archipelago is switched off. The removal only makes
            // sense as the other half of the filler items: a prop that the
            // multiworld now hands out must not also be findable lying
            // around for free. With no multiworld there are no filler items,
            // so removing the originals subtracts 68 objects from the island
            // and puts nothing back.
            //
            // Harmless while this was a .cfg key nobody opened. It stopped
            // being harmless the day the switch moved onto the hosting
            // screen (2026-09-22) — seen in the log that same day, a session
            // started with Archipelago off still reported "Removed 68
            // vanilla gadget prop(s) from the map".
            if (!ModConfig.ArchipelagoEnabled.Value)
                return;

            GadgetItemSpawner.HideVanillaInstances();
        }
    }
}
