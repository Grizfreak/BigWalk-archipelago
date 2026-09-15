using System;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Spawns a cosmetic/pickup gourd at the hub when an Archipelago item is
    // received — purely a visual notification ("hey, you just received an
    // item"). For a gourd (ItemApplier.ApplyGourdItem), this is even its
    // ONLY effect since the 2026-09-11 FIX (no more SaveManager write at
    // all); for a big key (ItemApplier.ApplyBigKeyItem), the real work
    // (SaveManager write, live pin) is still done separately before calling
    // this spawner. Decided on 2026-09-09 (cf.
    // big-walk-archipelago-notes.md, "On the appearance of the received
    // gourd item"): lifetime = until picked up (no timer), visible to all
    // players in the session (standard network spawn, no per-connection
    // scoping).
    //
    // Approach: clone an already-loaded RewardGourd in the scene
    // (Instantiate), rather than introducing a new network prefab (none
    // registered for this mod, and NetworkServer.Spawn requires a prefab
    // already known to NetworkManager so a client can instantiate it itself
    // upon receiving the spawn message). Cloning an already-present
    // instance works because its NetworkIdentity/PrefabHash is already that
    // of a prefab ALL clients already know.
    //
    // This kind of cloning (an object PLACED IN THE SCENE, not a real
    // prefab dynamically instantiated by the game) turned out to be riddled
    // with Mirror/engine pitfalls, all encountered and resolved during a
    // test session on 2026-09-11 — see inline comments for details on each:
    //   1. NetworkIdentity.sceneId + hasSpawned (copied as-is by
    //      Instantiate) make Awake() believe this clone is "the same"
    //      already-known scene object -> "already spawned" warning from the
    //      engine.
    //   2. NetworkIdentity.SpawnedFromInstantiate (set to true by Awake())
    //      must also be reset to false afterward.
    //   3. `saveablePropName` copied as-is would create a SaveManager
    //      collision if the clone is ever pinned to a real plinth —
    //      neutralized (`notSavable`/`PropSaveType.Never`) BEFORE any
    //      activation. Doing this AFTER ServerSetGourdState would be too
    //      late: the latter can trigger a real check via GourdStatePatch if
    //      the original name is still present (confirmed in testing — it
    //      triggered a false "gourdTellerWindow").
    //   4. `startHome` (a serialized reference to the template's original
    //      slot) teleports the clone far from the hub via the `Prop.Start()`
    //      restoration mechanism — neutralized (null) for the same reason
    //      as saveablePropName.
    //   5. `Prop.SetLoose()` must be called explicitly for the "dropped at
    //      the hub" case: the gourdState SyncVar (driven by
    //      ServerSetGourdState) only controls the puzzle's visual/logical
    //      state, not the Prop's own physical state (otherwise the clone
    //      stays "fixed"/without gravity as in its original clamp).
    //   6. A per-instance MaterialPropertyBlock (not copied by Instantiate,
    //      unlike serialized references) is captured and then reapplied to
    //      the clone as a precaution.
    //
    // DECISION (2026-09-11, player): contrary to the initial intuition
    // ("prevent any pin into a real monument, cf. propGroups.Clear() —
    // since removed"), the DESIRED behavior is the opposite: puzzles no
    // longer grant a directly usable gourd (the real donation goes through
    // the AP network), so these cosmetic clones must become the ONLY way
    // to fill monuments. The template's `propGroups` is therefore kept
    // intact (allows a real pin into any real PropHome) — persistence of
    // that pin is handled separately by `Core/CosmeticMonumentFillTracker.cs`
    // (a dedicated `SaveManager` key per PropHome, independent of
    // `saveablePropName`/`SaveableHomeName`, see that file for details).
    internal static class ReceivedItemSpawner
    {
        internal const string CosmeticNameSuffix = "(AP cosmetic)";

        // A short step ahead, and a little above the ground so it drops
        // rather than starting half-buried.
        private const float PlayerSpawnDistance = 1.3f;
        private const float PlayerSpawnLift = 0.6f;

        // Returns the created GameObject (or null on failure/no-op) — useful
        // for ad-hoc diagnostics (cf. DebugHotkeys); ItemApplier (real usage)
        // simply ignores the return value.
        // toPlayer distinguishes the two ways a gourd can arrive, which
        // want opposite things. One turning up mid-game is a gift: it
        // belongs in the player's hands, or at worst at their feet. The
        // dozens rebuilt at the start of a session are stock, and stock
        // belongs at the hub — dropping them around whoever just loaded in
        // would bury them, and the players are not necessarily at the hub
        // when it happens (player's call, 2026-09-15).
        internal static GameObject SpawnCosmeticPickup(bool toPlayer)
        {
            if (!NetworkServer.active)
                return null;

            try
            {
                var resolved = ResolveSpawnPosition(toPlayer);
                if (resolved == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] Nowhere to put a gourd yet (no player and no InventorySpawn loaded), cosmetic spawn skipped.");
                    return null;
                }

                var position = resolved.Value;
                var rewardGourd = CreateNeutralizedClone(position, Quaternion.identity);
                if (rewardGourd == null)
                    return null;

                rewardGourd.ServerSetGourdState(GourdFlag.GourdState.Loose);

                // Without this, the clone stays "fixed" (no gravity) as in
                // its original clamp/cabinet — cf. point 5 at the top of the
                // file.
                rewardGourd.prop?.SetLoose();

                var inHands = toPlayer && TryPutInHands(rewardGourd.prop);

                Plugin.Log.LogInfo(
                    inHands
                        ? $"[{nameof(ReceivedItemSpawner)}] Cosmetic gourd spawned and handed to the player."
                        : $"[{nameof(ReceivedItemSpawner)}] Cosmetic gourd spawned at {rewardGourd.transform.position}.");
                return rewardGourd.gameObject;
            }
            catch (Exception ex)
            {
                // Must never make the actual item reception fail (already
                // applied by ItemApplier before this call): purely
                // cosmetic, an exception here must have no consequence on
                // the real persistence.
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Cosmetic spawn failed, ignored: {ex}");
                return null;
            }
        }

        // Variant used by CosmeticMonumentFillTracker to restore, on load,
        // a cosmetic gourd already deposited in a previous session: clone +
        // pin directly into propHome (rather than dropping it at the hub).
        // Returns the created GameObject, or null on failure (never
        // propagates an exception, same defensive logic as
        // SpawnCosmeticPickup).
        internal static GameObject SpawnCosmeticPickupPinnedTo(PropHome propHome)
        {
            if (!NetworkServer.active || propHome == null)
                return null;

            try
            {
                var rewardGourd = CreateNeutralizedClone(propHome.transform.position, propHome.transform.rotation);
                if (rewardGourd == null || rewardGourd.prop == null)
                    return null;

                // Stashed (not Loose): represents a gourd already deposited
                // in its home, not a gourd that just appeared and is waiting
                // to be picked up — consistent with the fact that it is
                // pinned directly right below, without ever passing through
                // a "dropped on the ground" state. Ignored by GourdStatePatch
                // (guarded on newGourdState == Loose only), so no risk of a
                // false check here either.
                rewardGourd.ServerSetGourdState(GourdFlag.GourdState.Stashed);
                rewardGourd.prop.ServerSetPinned(propHome);

                return rewardGourd.gameObject;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Failed to restore a cosmetic gourd in {propHome.saveableHomeName}, ignored: {ex}");
                return null;
            }
        }

        // Core logic shared by both entry points above: clones an existing
        // RewardGourd at the given position/rotation, neutralized and ready
        // to use (networked, activated, visually correct). Sets neither the
        // puzzle state (gourdState) nor the physics/pin — left to the
        // caller's discretion.
        // Gives received gourds a colour of their own, so they read at a
        // glance as "this came from Archipelago" rather than as a gourd that
        // wandered out of a puzzle. A third party suggested exactly this
        // (see apworld/design-decisions.md, community feedback).
        //
        // Reuses the game's own mechanism rather than poking at shaders:
        // RewardGourd already carries `isVariantChallenge` + a
        // `variantChallengeColor`, which is how the postgame purple gourds
        // get their look, and PropertyBlockHelper.Refresh() is public. Set
        // before activation so Awake picks it up, and refreshed after
        // activation in case the helper only applies on demand.
        //
        // Until now the colour was whatever the cloned template happened to
        // look like — FindTemplate takes the first RewardGourd the engine
        // returns, in unspecified order, so it was arbitrary and could
        // differ between sessions. It came out purple in-game on
        // 2026-09-15, which was luck rather than design.
        private static void ApplyCosmeticColor(RewardGourd rewardGourd)
        {
            var configured = ModConfig.CosmeticGourdColor.Value;
            if (string.IsNullOrWhiteSpace(configured))
                return;

            if (!ColorUtility.TryParseHtmlString(configured.Trim(), out var color))
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ReceivedItemSpawner)}] '{configured}' is not a readable colour (expected something like #FFA62B); leaving the gourd as cloned.");
                return;
            }

            rewardGourd.isVariantChallenge = true;
            rewardGourd.variantChallengeColor = color;
            _pendingColorRefresh = rewardGourd;

            LogColorSettingsOnce(rewardGourd);
        }

        // Applied after activation: Awake may be what pushes
        // variantChallengeColor into the property block, and if it is not,
        // this is. Harmless either way.
        private static void RefreshCosmeticColor()
        {
            var target = _pendingColorRefresh;
            _pendingColorRefresh = null;
            if (target == null)
                return;

            try
            {
                target.propertyBlockHelper?.Refresh();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Could not refresh the gourd's colour: {ex.Message}");
            }
        }

        // Logged once per session: if the colour above does not take, these
        // are the shader properties the game actually drives, and the next
        // fix can target one by name instead of guessing.
        private static void LogColorSettingsOnce(RewardGourd rewardGourd)
        {
            if (_colorSettingsLogged)
                return;

            _colorSettingsLogged = true;

            var helper = rewardGourd.propertyBlockHelper;
            if (helper == null)
            {
                Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] Cloned gourd has no PropertyBlockHelper.");
                return;
            }

            var settings = helper.colorSettings;
            if (settings == null || settings.Length == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] PropertyBlockHelper exposes no colour setting.");
                return;
            }

            for (var i = 0; i < settings.Length; i++)
                Plugin.Log.LogInfo(
                    $"[{nameof(ReceivedItemSpawner)}] colorSettings[{i}] '{settings[i].propertyName}' = {settings[i].color}");
        }

        private static RewardGourd _pendingColorRefresh;
        private static bool _colorSettingsLogged;

        private static RewardGourd CreateNeutralizedClone(Vector3 position, Quaternion rotation)
        {
            var template = FindTemplate();
            if (template == null)
            {
                Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] No RewardGourd loaded to clone, cosmetic spawn skipped.");
                return null;
            }

            // Per-instance MaterialPropertyBlock: captured on the template
            // BEFORE cloning (cf. point 6 at the top of the file), to be
            // explicitly reapplied to the clone further below.
            var templateRenderers = template.gameObject.GetComponentsInChildren<Renderer>(true);
            var templatePropertyBlocks = new MaterialPropertyBlock[templateRenderers.Length];
            for (var i = 0; i < templateRenderers.Length; i++)
            {
                if (templateRenderers[i] == null)
                    continue;

                var block = new MaterialPropertyBlock();
                templateRenderers[i].GetPropertyBlock(block);
                templatePropertyBlocks[i] = block;
            }

            // Workaround for the Mirror pitfall (point 1 at the top of the
            // file): disabling the template before Instantiate makes the
            // clone inherit the inactive state, which defers Awake()/
            // OnEnable() until SetActive(true) further below — giving time
            // to fix sceneId and neutralize saveablePropName/startHome while
            // the clone is still inactive.
            var templateWasActive = template.gameObject.activeSelf;
            GameObject clone;
            try
            {
                template.gameObject.SetActive(false);
                clone = UnityEngine.Object.Instantiate(template.gameObject, position, rotation);
            }
            finally
            {
                if (templateWasActive)
                    template.gameObject.SetActive(true);
            }

            clone.name = $"{template.gameObject.name} {CosmeticNameSuffix}";

            var cloneIdentity = clone.GetComponent<NetworkIdentity>();
            if (cloneIdentity != null)
            {
                cloneIdentity.sceneId = 0;

                // hasSpawned is exposed as an Il2CppInterop-generated
                // property (not a genuinely reflectable FieldInfo despite
                // being declared as a private field on the game side) —
                // reflection on the property's setter, not on a guessed
                // field name.
                GetPrivatePropertySetter<NetworkIdentity>("hasSpawned")?.Invoke(cloneIdentity, new object[] { false });
            }

            var rewardGourd = clone.GetComponent<RewardGourd>();
            if (rewardGourd == null)
            {
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Clone without a RewardGourd, destroying and aborting.");
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            ApplyCosmeticColor(rewardGourd);

            // Neutralization BEFORE activation (points 3 and 4 at the top of
            // the file) — not after ServerSetGourdState, which actually
            // triggered a false check in testing.
            NeutralizeProgression(rewardGourd.prop);
            var propsToMakeSavable = rewardGourd.propsToMakeSavable;
            if (propsToMakeSavable != null)
            {
                foreach (var block in propsToMakeSavable)
                {
                    if (block == null || block.props == null)
                        continue;

                    foreach (var prop in block.props)
                        NeutralizeProgression(prop);
                }
            }

            // Reapplying the MaterialPropertyBlocks captured above, by index
            // (the clone's renderer hierarchy is an exact copy of the
            // template's) — before activation, to avoid even a single frame
            // with the default bare appearance.
            var cloneRenderers = clone.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < cloneRenderers.Length && i < templatePropertyBlocks.Length; i++)
            {
                if (cloneRenderers[i] != null && templatePropertyBlocks[i] != null)
                    cloneRenderers[i].SetPropertyBlock(templatePropertyBlocks[i]);
            }

            clone.SetActive(true);

            // SpawnedFromInstantiate (point 2 at the top of the file): set
            // to true BY Awake() itself (so only once SetActive is called
            // above), reset to false afterward via reflection on the
            // property's setter.
            if (cloneIdentity != null)
                GetPrivatePropertySetter<NetworkIdentity>(nameof(NetworkIdentity.SpawnedFromInstantiate))?.Invoke(cloneIdentity, new object[] { false });

            NetworkServer.Spawn(clone);

            RefreshCosmeticColor();

            return rewardGourd;
        }

        // Shared with CosmeticMonumentFillTracker (detection in the
        // PropHome.onAnyChangeServer event) and the debug tools: a cosmetic
        // clone is unambiguously recognized by saveablePropName ==
        // notSavable (never true for a normal game prop) + the name suffix,
        // as a double check.
        // Clears away every cosmetic gourd that is not sitting in a
        // monument, so the session-start reconciliation can put the right
        // number back. Returns how many were removed.
        //
        // This is the escape hatch for a gourd that has become unreachable
        // without the world being reloaded — stranded in a sealed puzzle
        // room, dropped somewhere awkward. Those are recovered on the next
        // world load anyway, since loose clones are never persisted and the
        // ledger recomputes; this simply makes that recovery available
        // without quitting.
        //
        // Monument deposits are deliberately untouched: they are persisted
        // (`ap_home_*`), they are what the Archipelago logic counts, and
        // nothing about them can go wrong in a way this would fix.
        // Everything else goes — on the ground, in a player's hands, on a
        // carried belt — because the rebuild counts what exists, and
        // leaving one behind would have it counted twice.
        internal static int DestroyLooseCosmeticGourds()
        {
            var destroyed = 0;
            var seen = 0;

            // Inactive included: a prop that has been picked up can leave
            // the active hierarchy, and the default scan would quietly walk
            // past exactly the gourd this is meant to reclaim. The mod's
            // own debug lookup learned this already.
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return 0;

            foreach (var gourd in all)
            {
                if (gourd == null || !IsCosmeticClone(gourd.prop))
                    continue;

                var home = gourd.prop != null ? gourd.prop.currentHome : null;
                if (home != null && IsMonumentHome(home))
                    continue;

                seen++;

                // Tidying the hands is best effort; removing the gourd is
                // not. Keeping these apart matters: the first version let a
                // failure to drop abort the removal, so the one gourd that
                // most needed reclaiming — the one being held — was the
                // only one left behind, and the rebuild then counted it
                // twice (observed in-game, 2026-09-15).
                ReleaseFromHands(gourd.prop);

                try
                {
                    NetworkServer.Destroy(gourd.gameObject);
                    destroyed++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Could not remove a cosmetic gourd: {ex.Message}");
                }
            }

            if (destroyed != seen)
                Plugin.Log.LogWarning(
                    $"[{nameof(ReceivedItemSpawner)}] {seen} loose cosmetic gourd(s) found but only {destroyed} removed; the rebuild will over-count.");

            return destroyed;
        }

        // Destroying a prop out of someone's hands would leave the hands
        // believing they still hold it, so it is dropped first.
        //
        // Drop takes a PlayerHeldInformation, which is a struct carrying a
        // NetworkIdentity — so the parameterless-looking `Drop()` passes one
        // with a null identity and the game dereferences it. The type's own
        // constructor takes the prop, which is what it wants.
        private static void ReleaseFromHands(Prop prop)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (prop == null || players == null)
                return;

            foreach (var pc in players)
            {
                if (pc?.hands == null || pc.hands.heldProp != prop)
                    continue;

                try
                {
                    pc.hands.Drop(new PlayerHeldInformation(prop));
                }
                catch (Exception ex)
                {
                    // Last resort: sever the reference by hand rather than
                    // leave the player holding an object about to vanish.
                    Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Drop failed, clearing the hand directly: {ex.Message}");
                    try { pc.hands.heldProp = null; }
                    catch (Exception inner) { Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] ...and that failed too: {inner.Message}"); }
                }
            }
        }

        internal static bool IsCosmeticClone(Prop prop)
        {
            return prop != null
                && prop.saveablePropName == SaveablePropName.notSavable
                && prop.gameObject.name.Contains(CosmeticNameSuffix, StringComparison.Ordinal);
        }

        // Found in testing on 2026-09-11: PropHome is a generic game
        // concept, also reused for slots carried by the player (e.g. an
        // inventory "belt", saveableHomeName == notSavable, always at ~0
        // distance from the player since attached to their own character)
        // — not just monuments. DebugCosmeticPinForce used to pick the
        // nearest empty PropHome WITHOUT this filter, and pinned a cosmetic
        // gourd into a "belt" instead of a real monument (log: "pinned into
        // notSavable"). All real monument slots (`SaveableHomeName`) start
        // with the prefix "monoument" (monoumentIntro/monoument0-3Slot.../
        // monoumentFinalSlot.../monoumentOverflowSlot..., cf. the enum) —
        // a shared filter to never reproduce this confusion.
        internal static bool IsMonumentHome(PropHome home)
        {
            return home != null
                && home.saveableHomeName.ToString().StartsWith("monoument", StringComparison.OrdinalIgnoreCase);
        }

        private static MethodInfo GetPrivatePropertySetter<T>(string propertyName)
        {
            return typeof(T)
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetSetMethod(nonPublic: true);
        }

        private static void NeutralizeProgression(Prop prop)
        {
            if (prop == null)
                return;

            prop.saveablePropName = SaveablePropName.notSavable;
            prop.propSaveType = PropSaveType.Never;

            // Point 4 at the top of the file: without this, Prop.Start()
            // (the load-time restoration mechanism already documented in
            // big-walk-archipelago-notes.md, section "RESOLVED 2026-09-03")
            // teleports the clone to the template's original slot location,
            // potentially at the other end of the map.
            prop.startHome = null;

            // `propGroups` is NO LONGER cleared here (unlike an earlier
            // version): player decision (2026-09-11) to explicitly allow
            // pinning into a real monument, cf. the "DECISION" section at
            // the top of the file — CosmeticMonumentFillTracker handles the
            // persistence of that pin separately.
        }

        // Picks the InventorySpawn closest to the local player rather than
        // the first one found — several InventorySpawn instances are loaded
        // simultaneously (one per zone?), not a single point dedicated to
        // the hub.
        private static InventorySpawn FindSpawnPoint()
        {
            var all = UnityEngine.Object.FindObjectsByType<InventorySpawn>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;

            var origin = FindLocalPlayerPosition();
            if (origin == null || all.Length == 1)
                return all[0];

            InventorySpawn closest = null;
            var closestSqrDistance = float.MaxValue;
            foreach (var candidate in all)
            {
                if (candidate == null)
                    continue;

                var sqrDistance = (candidate.transform.position - origin.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = candidate;
                }
            }

            return closest ?? all[0];
        }

        private static Vector3? FindLocalPlayerPosition()
        {
            var player = FindLocalPlayerCharacter();
            return player != null ? player.transform.position : null;
        }

        // Hands the gourd straight to the player when their hands are free,
        // rather than making them stoop for something the server just gave
        // them (player request, 2026-09-15).
        //
        // Both conditions come from the game rather than from a guess:
        // `isHoldingSomething` is the "are their hands free" it already
        // tracks, and `IsSafeToPickUp` is its own judgement about whether a
        // given prop may be picked up at all. Asking the game beats
        // reasoning about it, which is the lesson from placing props by
        // computed offsets.
        //
        // Self-limiting during a bulk restore: the first gourd fills the
        // hands and every one after it simply drops as before.
        //
        // NOTE, and it is not solved by this: a gourd handed over inside a
        // sealed puzzle room is stranded there for the session, exactly as
        // one dropped at the player's feet would be, because the game does
        // not let you carry it out. That is recovered on the next world
        // load — the ledger recomputes received-minus-deposited and puts it
        // back — but not before.
        private static bool TryPutInHands(Prop prop)
        {
            if (prop == null || !ModConfig.PutGourdInHands.Value)
                return false;

            try
            {
                var hands = FindLocalPlayerCharacter()?.hands;
                if (hands == null || hands.isHoldingSomething || !hands.IsSafeToPickUp(prop))
                    return false;

                hands.PickUp(prop);
                return true;
            }
            catch (Exception ex)
            {
                // Cosmetic to the last: the gourd already exists and is
                // pickable, so failing to place it in the hands costs
                // nothing.
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Could not hand the gourd over: {ex.Message}");
                return false;
            }
        }

        private static PlayerCharacter FindLocalPlayerCharacter()
        {
            var all = PlayerCharacter.allPlayerCharacters;
            if (all == null)
                return null;

            foreach (var pc in all)
            {
                if (pc != null && pc.isLocalPlayer)
                    return pc;
            }

            return null;
        }

        // Where a received gourd actually lands.
        //
        // The original decision (2026-09-09) was the hub, deliberately, so a
        // received item could not drop in some arbitrary corner of the map.
        // In play that turned out to mean walking back across the island for
        // something the server just handed you — so when there is a player
        // standing in the world, the gourd now appears just in front of
        // them instead (player request, 2026-09-15).
        //
        // The hub remains the answer whenever there is no player character
        // to speak of: loading, the menu, or any moment the world is not
        // really inhabited yet. That is the "if the players are connected"
        // condition, expressed as the only thing actually checkable.
        //
        // Kept close on purpose. Placing props at a computed offset has
        // already cost two rounds of gourds lost in geometry (see the
        // session-start restore), and the further in front this reaches the
        // more likely it is to reach through a wall. A short step is enough
        // to be visible, and the player is by definition standing somewhere
        // valid.
        private static Vector3? ResolveSpawnPosition(bool toPlayer)
        {
            if (toPlayer && ModConfig.SpawnGourdAtPlayer.Value)
            {
                var player = FindLocalPlayerCharacter();
                if (player != null)
                {
                    var t = player.transform;
                    return t.position + t.forward * PlayerSpawnDistance + Vector3.up * PlayerSpawnLift;
                }
            }

            var spawnPoint = FindSpawnPoint();
            return spawnPoint != null ? spawnPoint.GetNextSpawnPosition() : null;
        }

        // Last template found, reused while it is still alive. Without this,
        // every single spawn paid for a full FindObjectsByType scan of the
        // scene — unnoticeable for one gourd, but the session-start
        // reconciliation can restore dozens at four per frame, and that was
        // four scene-wide scans per frame (visible stutter reported
        // in-game, 2026-09-15). Cleared implicitly when the object dies: a
        // world reload invalidates it and the next call re-scans once.
        private static RewardGourd _cachedTemplate;

        private static RewardGourd FindTemplate()
        {
            if (_cachedTemplate != null && _cachedTemplate.prop != null)
                return _cachedTemplate;

            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;

            // Excludes our own previous clones (marked by CosmeticNameSuffix):
            // otherwise an already-spawned clone would serve as the template
            // for the next clone, which clones a clone which clones a
            // clone... — always clone from a real game RewardGourd.
            //
            // Prefers a gourd that is already "Loose" (rendered/visuals in
            // the "normally pickable" state); failing that, any loaded gourd
            // will do, since its state will be forced explicitly by the
            // caller right after cloning anyway.
            foreach (var candidate in all)
            {
                if (candidate != null && candidate.prop != null && candidate.gourdState == GourdFlag.GourdState.Loose
                    && !candidate.gameObject.name.Contains(CosmeticNameSuffix, StringComparison.Ordinal))
                {
                    _cachedTemplate = candidate;
                    return candidate;
                }
            }

            foreach (var candidate in all)
            {
                if (candidate != null && candidate.prop != null
                    && !candidate.gameObject.name.Contains(CosmeticNameSuffix, StringComparison.Ordinal))
                {
                    _cachedTemplate = candidate;
                    return candidate;
                }
            }

            return null;
        }
    }
}
