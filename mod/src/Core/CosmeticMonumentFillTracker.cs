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

        // All known monument PropHomes (filled or not), for
        // GetFilledMonumentCount below — Option A settled on 2026-09-15
        // (cf. big-walk-archipelago-notes.md): a single aggregated global
        // count, not per-tower location tracking, to eliminate the softlock
        // risk identified on 2026-09-11 (nothing prevents a player from
        // depositing everything into a single monument). RegisterHome is
        // guaranteed to be called exactly once per instance (cf. Patches/
        // PropHomeEnablePatch), so no duplicate is possible here.
        private static readonly List<PropHome> MonumentHomes = new List<PropHome>();

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

            if (ReceivedItemSpawner.IsMonumentHome(home))
                MonumentHomes.Add(home);
        }

        // Aggregated total across all monuments (Option A): the number of
        // monument PropHomes currently occupied by a cosmetic gourd.
        // Queried on the fly from already-persisted data (`ap_home_<slot>`)
        // rather than a cached counter — avoids any risk of desync with
        // OnHomeChanged/TryRestoreHome (two separate write paths).
        internal static int GetFilledMonumentCount()
        {
            var count = 0;
            foreach (var home in MonumentHomes)
            {
                if (home != null && SaveManager.GetIntValue(SaveKeyPrefix + home.saveableHomeName, 0, false) == 1)
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
