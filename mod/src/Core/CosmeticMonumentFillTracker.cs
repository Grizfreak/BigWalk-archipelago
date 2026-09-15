using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Persists and restores monument filling by cosmetic gourds
    // (ReceivedItemSpawner) — player decision (2026-09-11): puzzles no
    // longer grant a directly usable gourd (the real donation goes through
    // the AP network), so cosmetic clones must become the ONLY way to fill
    // a monument, and that filling must survive a reload.
    //
    // Why not the vanilla mechanism (saveablePropName -> Prop.Start()):
    // that mechanism is fundamentally IDENTITY-based (each prop finds its
    // place on load by re-reading ITS OWN SaveManager key). A cosmetic
    // clone has no stable reusable identity without risking a collision
    // with a real check (cf. ReceivedItemSpawner, points 3 and 4) — the
    // game's only "free" identity pool (gourdTesting00-39, ~40 values
    // never used in normal gameplay) would be too small against the ~45
    // real monument slots in the game (F6 count, cf.
    // big-walk-archipelago-notes.md) if cosmetic gourds must be able to
    // fill ANY of them. Hence this entirely separate mechanism, indexed by
    // PropHome (not by Prop): one SaveManager key per SLOT
    // (`saveableHomeName`, the stable identity of the PropHome itself, not
    // of the prop occupying it), prefixed so it never collides with the
    // `SaveablePropName`/`SaveableHomeName` keys the game actually writes.
    //
    // Pin detection — confirmed in testing on 2026-09-11: PropHome.
    // onAnyChangeServer (a global STATIC event) NEVER fires for a pin done
    // via Prop.ServerSetPinned — the real signal is PropHome.onChangeServer,
    // the PER-INSTANCE version of the same event type, which must be
    // subscribed to individually on EACH PropHome.
    //
    // Per-instance subscription (2nd fix, same session): a first attempt
    // scanned `PropHome.allPropHomes` ONCE at startup (polling on
    // WorldManager.isReadyForEffects, cf. git history) — worked for a
    // monument near the hub but NOT for a distant monument (Black Tower):
    // the game clearly loads some PropHome instances on the fly, not all
    // simultaneously. Replaced with Patches/PropHomeEnablePatch.cs (a
    // postfix on PropHome.OnEnable(), the only guaranteed touchpoint for
    // EVERY instance whether it exists from the start or appears later)
    // which calls RegisterHome below for each PropHome, as they stream in.
    //
    // PropHome is also used for slots carried by the player (e.g. an
    // inventory "belt", saveableHomeName == notSavable, always at ~0
    // distance since attached to the character) — not just monuments.
    // Filtered via ReceivedItemSpawner.IsMonumentHome (prefix "monoument")
    // so a pin is never persisted/restored for a slot of that kind.
    internal class CosmeticMonumentFillTracker : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public CosmeticMonumentFillTracker(IntPtr ptr) : base(ptr)
        {
        }

        private const string SaveKeyPrefix = "ap_home_";

        // Queue fed by PropHomeEnablePatch, as instances stream in (initial
        // load AND later streaming alike) — drained continuously in
        // Update() once the world is ready for effects, not in a single
        // fixed pass at startup.
        private static readonly Queue<PropHome> PendingRestoreCheck = new Queue<PropHome>();

        // Called by Patches/PropHomeEnablePatch for EACH PropHome, as soon
        // as it becomes active. The subscription itself (just a +=) does
        // not depend on any timing condition and happens immediately; the
        // restore check, on the other hand, is deferred (cf. Update below)
        // — spawning/pinning a clone too early (before the game's "peck
        // manager" exists) can fail silently, the same pitfall already
        // encountered for ArchDoorUnlocker.
        internal static void RegisterHome(PropHome home)
        {
            if (home == null)
                return;

            home.onChangeServer += (Action<PropHome, Prop, Prop>)OnHomeChanged;
            PendingRestoreCheck.Enqueue(home);
        }

        // Aggregated total across all monuments (Option A settled on
        // 2026-09-15): how many monument slots hold a cosmetic gourd, with
        // no notion of which tower — that is what makes it impossible for a
        // bad distribution of gourds to lock a seed.
        //
        // Counted by scanning the save's own `ap_home_*` entries rather than
        // by walking the loaded PropHomes. An earlier version did the
        // latter, and it under-reported for as long as a monument had not
        // streamed in yet: at session start, before walking anywhere near
        // the Black Tower, its filled slots simply did not exist as far as
        // this count was concerned. That fed the deposit checks, the goal,
        // and (worse) the loose-gourd reconciliation in Core/Net/ApRuntime,
        // which would have spawned duplicates for every monument not yet
        // loaded. The save always knows; the scene does not.
        internal static int GetFilledMonumentCount()
        {
            var data = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (data == null || data.entries == null)
                return 0;

            var count = 0;
            var entries = data.entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.value != 0 && entry.key != null
                    && entry.key.StartsWith(SaveKeyPrefix, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private void Update()
        {
            if (!NetworkServer.active || !WorldManager.isReadyForEffects || PendingRestoreCheck.Count == 0)
                return;

            var restoredCount = 0;
            while (PendingRestoreCheck.Count > 0)
            {
                if (TryRestoreHome(PendingRestoreCheck.Dequeue()))
                    restoredCount++;
            }

            if (restoredCount > 0)
                Plugin.Log.LogInfo($"[{nameof(CosmeticMonumentFillTracker)}] {restoredCount} cosmetic gourd(s) restored to their monuments.");
        }

        private static bool TryRestoreHome(PropHome home)
        {
            // Never overwrite a home that is already occupied (by a real
            // gourd normally restored via Prop.Start(), or already by a
            // clone restored just before in this same pass).
            if (home == null || home.pinnedProp != null || !ReceivedItemSpawner.IsMonumentHome(home))
                return false;

            var key = SaveKeyPrefix + home.saveableHomeName;
            if (SaveManager.GetIntValue(key, 0, false) == 0)
                return false;

            return ReceivedItemSpawner.SpawnCosmeticPickupPinnedTo(home) != null;
        }

        private static void OnHomeChanged(PropHome propHome, Prop propBefore, Prop propAfter)
        {
            if (propHome == null || !ReceivedItemSpawner.IsMonumentHome(propHome))
                return;

            // Single rule that covers pin/unpin/replacement: the key simply
            // reflects "a cosmetic clone occupies THIS propHome RIGHT NOW",
            // regardless of what was there before.
            var isCosmeticNow = ReceivedItemSpawner.IsCosmeticClone(propAfter);
            var key = SaveKeyPrefix + propHome.saveableHomeName;
            var newValue = isCosmeticNow ? 1 : 0;

            if (SaveManager.GetIntValue(key, 0, false) == newValue)
                return;

            SaveManager.SetIntValue(key, newValue);
            Plugin.Log.LogInfo(
                $"[{nameof(CosmeticMonumentFillTracker)}] {key} = {newValue} (cosmetic gourd {(isCosmeticNow ? "deposited" : "removed")}).");
        }
    }
}
