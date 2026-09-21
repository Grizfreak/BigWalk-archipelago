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

                // Handing it over is deferred by a frame rather than done
                // here. Putting it in the hands now would put the spawn of
                // this gourd and the player's "is holding it" SyncVar in the
                // same frame, and the other client resolves that SyncVar's
                // reference by netId: if it lands before the spawn has been
                // applied there, the reference comes back null and
                // PlayerNetworking.OnSetHeld throws on it — taking the rest
                // of that player's SyncVar batch down with it (the "read 62
                // bytes / hash mismatch" seen in co-op on 2026-09-15).
                //
                // Deferring is right whether or not that race is what caused
                // it: the two facts have no ordering guarantee between them,
                // and one frame is not perceptible. What the frame does NOT
                // buy is certainty that the guest has processed the spawn by
                // then — only that the server sent it first.
                if (toPlayer && ModConfig.PutGourdInHands.Value)
                    _pendingHandover = rewardGourd.prop;

                Plugin.Log.LogInfo(
                    $"[{nameof(ReceivedItemSpawner)}] Cosmetic gourd spawned at {rewardGourd.transform.position}.");
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

        // Re-applies the cosmetic look after Mirror has finished spawning the
        // object on a client.
        //
        // BuildNeutralizedClone already colours the clone, and on the host
        // that is enough. On a client it is not: the handler returns the
        // object and Mirror THEN runs the spawn payload over it —
        // OnStartClient, the gourdState SyncVar, the RewardGourd's own
        // initialisation — which puts the template's appearance back. The
        // gourd came out glaring white instead of orange on the second
        // player's screen (co-op, 2026-09-20) while looking correct on the
        // host, which is exactly the shape of "something ran after us".
        internal static void ReapplyCosmeticLook(RewardGourd rewardGourd)
        {
            if (rewardGourd == null)
                return;

            ApplyCosmeticColor(rewardGourd);
            RefreshCosmeticColor();
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

        // The assetId the cosmetic clones are spawned under. Mirror
        // identifies a scene object by its sceneId and a dynamically spawned
        // one by its assetId, looked up in the client's registered-prefab
        // table; a clone of a scene object has neither that a remote client
        // can resolve, which is why the spawn used to fail there with
        // "did you forget to add it to the NetworkManager?" while working
        // perfectly on the host — where nothing has to be instantiated
        // because the object is already local. Ours is a constant of our
        // own, matched by the handler CosmeticGourdSpawnHandler registers on
        // every client; the value is arbitrary, only the fact that both ends
        // agree on it matters.
        internal const uint CosmeticAssetId = 0xB16_9A00;

        // Builds the clone WITHOUT spawning it. Split out so a client can
        // run exactly the same construction from its spawn handler: the
        // server no longer ships the object, it ships the instruction to
        // build one, and each machine builds its own from its own scene.
        internal static RewardGourd BuildNeutralizedClone(Vector3 position, Quaternion rotation)
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

            RefreshCosmeticColor();

            return rewardGourd;
        }

        private static RewardGourd CreateNeutralizedClone(Vector3 position, Quaternion rotation)
        {
            var rewardGourd = BuildNeutralizedClone(position, rotation);
            if (rewardGourd == null)
                return null;

            // The assetId overload, rather than setting the property first:
            // NetworkIdentity.assetId has an internal setter, and Mirror
            // offers this exactly so a caller can say what a dynamically
            // spawned object should be identified as.
            NetworkServer.Spawn(rewardGourd.gameObject, CosmeticAssetId);

            // The netId is what the other player's client will be asked to
            // resolve when this gourd ends up in someone's hands, so it is
            // worth being able to line it up against their log.
            var identity = rewardGourd.GetComponent<NetworkIdentity>();
            Plugin.Log.LogInfo(
                $"[{nameof(ReceivedItemSpawner)}] Cosmetic gourd spawned on the network (netId {(identity != null ? identity.netId : 0)}).");

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

        // Destroying OR hiding a prop out of someone's hands would leave the
        // hands believing they still hold it, so it is dropped first. Also
        // called by GourdStatePatch, which hits exactly that case on the
        // puzzles whose gourd goes Loose on pickup.
        //
        // Drop takes a PlayerHeldInformation, which is a struct carrying a
        // NetworkIdentity — so the parameterless-looking `Drop()` passes one
        // with a null identity and the game dereferences it. The type's own
        // constructor takes the prop, which is what it wants.
        internal static void ReleaseFromHands(Prop prop)
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
        // Set by SpawnCosmeticPickup, consumed one tick later by ApRuntime.
        // A single slot rather than a queue: hands hold one thing, so a
        // second gourd arriving before the first is handed over would not
        // have been picked up anyway.
        private static Prop _pendingHandover;

        // Called from ApRuntime's Update, which already ticks on the host.
        internal static void DrainPendingHandover()
        {
            var prop = _pendingHandover;
            if (prop == null)
                return;

            _pendingHandover = null;

            // With the netId, so this line can be matched against the other
            // machine's "Clone settled: netId N" — the only way to tell
            // "the guest never got the object" from "the guest got a
            // different object" apart.
            var identity = prop.GetComponent<NetworkIdentity>();
            if (TryPutInHands(prop))
                Plugin.Log.LogInfo(
                    $"[{nameof(ReceivedItemSpawner)}] Cosmetic gourd handed over (netId {(identity != null ? identity.netId : 0)}).");
        }

        // Runs on the host, and every PickUp here is a server-side call by
        // design. playerHeldInformation is a SyncVar written by the server,
        // so this is the only place from which a player — any player — can
        // truthfully be given something to hold.
        //
        // The first attempt at handing a gourd to somebody else did it the
        // other way round, on each client for itself, and produced a plain
        // desync: the host saw the gourd in their own hands and the guest
        // saw the SAME gourd in theirs (in co-op, 2026-09-16). A client
        // calling PickUp only convinces itself.
        private static bool TryPutInHands(Prop prop)
        {
            if (prop == null || !ModConfig.PutGourdInHands.Value || !NetworkServer.active)
                return false;

            try
            {
                // The local player first: the gourd arrived for the host, it
                // spawned at their feet, and handing it to somebody across
                // the hub when the host could simply take it would be
                // surprising.
                var local = FindLocalPlayerCharacter();
                if (TryGiveTo(local, prop))
                    return true;

                var radius = ModConfig.CosmeticGourdHandoverRadius.Value;
                if (radius <= 0f)
                    return false;

                // Otherwise the nearest other player with free hands, within
                // the radius. Nearest rather than first found, so that with
                // three players it goes to whoever is actually standing
                // there rather than to whichever one the engine happens to
                // list first.
                var origin = prop.transform.position;
                PlayerCharacter best = null;
                var bestDistance = float.MaxValue;

                var players = PlayerCharacter.allPlayerCharacters;
                if (players == null)
                    return false;

                foreach (var pc in players)
                {
                    if (pc == null || pc == local || pc.hands == null || pc.hands.isHoldingSomething)
                        continue;

                    var distance = Vector3.Distance(origin, pc.transform.position);
                    if (distance > radius || distance >= bestDistance)
                        continue;

                    best = pc;
                    bestDistance = distance;
                }

                if (best == null)
                    return false;

                if (!TryGiveTo(best, prop))
                    return false;

                Plugin.Log.LogInfo(
                    $"[{nameof(ReceivedItemSpawner)}] Hands full, so the gourd went to another player {bestDistance:0.0}m away.");
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

        // PickUp, then a check that it actually took on the network — and
        // Prop.SetHeld as a fallback if it did not.
        //
        // PlayerHands.PickUp alone leaves the other player seeing nothing at
        // all. Measured on 2026-09-20 with both logs side by side: the clone
        // existed on the guest under the SAME netId the host had handed
        // over, and stayed unclaimed for three full seconds — neither
        // hands.heldProp nor the playerHeldInformation SyncVar ever pointed
        // at it there. So PickUp is the local, input-side half of picking
        // something up, and is not by itself what publishes the hold.
        //
        // Prop.SetHeld(PlayerCharacter) is its counterpart on the Prop, the
        // sibling of the SetLoose and SetFixed this file already relies on.
        // It is called only when PickUp has visibly failed to set the
        // SyncVar, so on a build where PickUp does the whole job nothing
        // changes.
        private static bool TryGiveTo(PlayerCharacter player, Prop prop)
        {
            var hands = player != null ? player.hands : null;
            if (hands == null || hands.isHoldingSomething || !hands.IsSafeToPickUp(prop))
                return false;

            // Local effects, on this machine.
            hands.PickUp(prop);

            // And the one write that other machines can see. Decompiled from
            // the game on 2026-09-20 rather than guessed at, after a day of
            // guessing: PlayerNetworking.UserCode_CmdPickUp — the SERVER
            // side of the game's own pick-up Command — validates with
            // IsSafeToPickUp and then does exactly this, sets
            // NetworkplayerHeldInformation. Neither PlayerHands.PickUp nor
            // Prop.SetHeld touches it; both are purely local, which is why
            // the guest saw the clone under the right netId and still
            // reported it unheld for three seconds.
            //
            // A Command cannot be sent on another client's behalf, and does
            // not need to be: this is a SyncVar, and writing it is the
            // server's prerogative.
            try
            {
                player.playerNetworking.NetworkplayerHeldInformation = new PlayerHeldInformation(prop);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ReceivedItemSpawner)}] Could not publish the hold to the other players: {ex.Message}");
            }

            if (!HoldIsPublished(player, prop))
                Plugin.Log.LogWarning(
                    $"[{nameof(ReceivedItemSpawner)}] The hold did not take on the network; the other player will not see this gourd in anyone's hands.");

            return true;
        }

        // "Published" means the networked field, not the local one: that is
        // the only one another machine can read.
        private static bool HoldIsPublished(PlayerCharacter player, Prop prop)
        {
            try
            {
                var networking = player.playerNetworking;
                var identity = prop.GetComponent<NetworkIdentity>();
                if (networking == null || identity == null)
                    return false;

                var held = networking.playerHeldInformation;
                return held.identity != null && held.identity == identity;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Could not read the published hold: {ex.Message}");
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
        internal static Vector3? ResolveSpawnPosition(bool toPlayer)
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
