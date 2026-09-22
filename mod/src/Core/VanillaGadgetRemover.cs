using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Removes the vanilla instances of the gadget props used as Archipelago
    // filler items (megaphone, walkie-talkie, backpack, belt, flare gun)
    // from the map, so a filler item cannot also be found lying around for
    // free — player decision, 2026-09-22 ("they should be removed also in
    // the map to avoid having backpacks, and standard gunflares").
    //
    // Two halves, run on every machine but not symmetrically:
    //   1. GadgetItemSpawner.CaptureTemplates() — every machine, purely
    //      local. Each client clones a private, inactive copy of whatever
    //      it currently sees loaded, BEFORE anything is removed, so it has
    //      something to build future cosmetic pickups from later in the
    //      session (mirrors CosmeticGourdSpawnHandler's "each client builds
    //      its own from its own scene").
    //   2. GadgetItemSpawner.RemoveVanillaInstances() — host only. A plain
    //      GameObject.SetActive(false) is not something Mirror replicates,
    //      so making these props disappear for every player needs the same
    //      networked call the mod already uses to clean up its own cosmetic
    //      clones (NetworkServer.Destroy) rather than hiding them locally.
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

        private void Update()
        {
            var worldReady = WorldManager.isReadyForEffects;
            if (!worldReady)
            {
                _worldWasReady = false;
                return;
            }

            if (_worldWasReady)
                return;

            _worldWasReady = true;

            // Every machine, host or guest: this is a local snapshot, not an
            // authoritative write.
            GadgetItemSpawner.CaptureTemplates();

            // Host authority model, like the rest of the mod (cf.
            // ItemApplier, ArchDoorUnlocker): only the host removes the
            // originals, and NetworkServer.Destroy is what makes that
            // removal visible to every other player.
            if (!NetworkServer.active)
                return;

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

            GadgetItemSpawner.RemoveVanillaInstances();
        }
    }
}
