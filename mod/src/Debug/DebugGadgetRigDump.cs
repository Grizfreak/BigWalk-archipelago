using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // What a belt is made of, vanilla against clone (2026-09-25).
    //
    // The first alpha reported received belts "attached to an unseen object"
    // on the ground, a second, bugged copy floating in front of whoever wears
    // one, and that copy in their shadow for everyone else — while the
    // backpacks, cloned the same way from 2026-09-23, behave. The suspicion is
    // a SkinnedMeshRenderer whose bones live outside the prop: Instantiate
    // only remaps references inside the copied hierarchy, so a clone would go
    // on following the bones of the vanilla belt it was copied from, which
    // this mod hides. This prints, for each HolsterProp (and one backpack to
    // compare), every renderer and where its bones really are.
    //
    // Runs once per loaded world, a few seconds in, when Debug is enabled,
    // then watches the belts for changes.
    internal class DebugGadgetRigDump : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public DebugGadgetRigDump(IntPtr ptr) : base(ptr)
        {
        }

        private const float DelayAfterReadySeconds = 8f;

        private static readonly string[] Names = { "HolsterProp", "BackpackProp" };

        // After the first full dump, belts are re-checked on this interval and
        // logged again only when something visible about them changed: what
        // holds them and which of their meshes show. That catches the moment
        // one is worn, which is where the alpha saw the bug.
        private const float WatchIntervalSeconds = 1f;

        private static readonly Dictionary<int, string> LastSeen = new();

        private float _readySince = -1f;
        private bool _done;
        private float _nextWatch;

        private void Update()
        {
            if (!WorldManager.isReadyForEffects)
            {
                _readySince = -1f;
                _done = false;
                LastSeen.Clear();
                return;
            }

            if (_readySince < 0f)
                _readySince = Time.time;

            if (Time.time - _readySince < DelayAfterReadySeconds)
                return;

            try
            {
                if (!_done)
                {
                    _done = true;
                    Dump();
                }
                else if (Time.time >= _nextWatch)
                {
                    _nextWatch = Time.time + WatchIntervalSeconds;
                    WatchBelts();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugGadgetRigDump)}] Dump failed: {ex.Message}");
            }
        }

        private static void WatchBelts()
        {
            var props = Prop.allProps;
            if (props == null)
                return;

            foreach (var prop in props)
            {
                if (prop == null || !prop.gameObject.name.StartsWith("HolsterProp", StringComparison.Ordinal))
                    continue;

                var signature = Signature(prop);
                var id = prop.GetInstanceID();
                if (LastSeen.TryGetValue(id, out var last) && last == signature)
                    continue;

                LastSeen[id] = signature;
                DumpOne(prop, ReceivedItemSpawner.IsCosmeticClone(prop));
            }

            // A worn belt's Kernal (its meshes and its IsWornEffects) leaves the
            // prop for the wearer, so it is followed on its own.
            var kernals = UnityEngine.Object.FindObjectsByType<PropKernal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (kernals == null)
                return;

            foreach (var kernal in kernals)
            {
                var owner = kernal != null ? kernal.prop : null;
                if (owner == null || !owner.gameObject.name.StartsWith("HolsterProp", StringComparison.Ordinal))
                    continue;

                var parts = new List<string> { FullPath(kernal.transform) };
                foreach (var renderer in kernal.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null)
                        parts.Add($"{renderer.gameObject.name}={renderer.enabled && renderer.gameObject.activeInHierarchy}");
                var signature = string.Join(", ", parts);

                var id = kernal.GetInstanceID();
                if (LastSeen.TryGetValue(id, out var last) && last == signature)
                    continue;

                LastSeen[id] = signature;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugGadgetRigDump)}] kernal of {owner.gameObject.name} "
                    + $"({(ReceivedItemSpawner.IsCosmeticClone(owner) ? "clone" : "vanilla")}, #{id}): {signature}");
            }
        }

        private static string Signature(Prop prop)
        {
            var home = prop.currentHome;
            var parts = new List<string> { home != null ? home.gameObject.name : "-", prop.gameObject.activeInHierarchy.ToString() };
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                if (renderer != null)
                    parts.Add(renderer.gameObject.name + "=" + (renderer.enabled && renderer.gameObject.activeInHierarchy));
            return string.Join(",", parts);
        }

        private static void Dump()
        {
            var props = Prop.allProps;
            if (props == null)
                return;

            var backpacksShown = 0;
            foreach (var prop in props)
            {
                if (prop == null)
                    continue;

                var name = prop.gameObject.name;
                if (Array.FindIndex(Names, n => name.StartsWith(n, StringComparison.Ordinal)) < 0)
                    continue;

                var isClone = ReceivedItemSpawner.IsCosmeticClone(prop);
                if (name.StartsWith("BackpackProp", StringComparison.Ordinal) && (isClone || backpacksShown++ > 0))
                    continue;

                DumpOne(prop, isClone);
                LastSeen[prop.GetInstanceID()] = Signature(prop);
            }
        }

        private static void DumpOne(Prop prop, bool isClone)
        {
            var root = prop.transform;
            var home = prop.currentHome;
            Plugin.Log.LogInfo(
                $"[{nameof(DebugGadgetRigDump)}] === {prop.gameObject.name} ({(isClone ? "clone" : "vanilla")})"
                + $" active={prop.gameObject.activeInHierarchy} at {root.position}"
                + $" home={(home != null ? home.gameObject.name : "<none>")}"
                + $" parent={(root.parent != null ? root.parent.name : "<none>")}");

            var components = new List<string>();
            foreach (var component in prop.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.TryCast<Transform>() != null || component.TryCast<Renderer>() != null
                    || component.TryCast<MeshFilter>() != null)
                    continue;
                components.Add(PathFrom(root, component.transform) + ":" + component.GetIl2CppType().Name);
            }
            Plugin.Log.LogInfo($"[{nameof(DebugGadgetRigDump)}]   components: {string.Join(", ", components)}");

            // Which state drives which mesh: on a guest the hung mesh came back
            // after the clone was built, so something networked switches it.
            foreach (var state in prop.GetComponentsInChildren<TrackedPeckState>(true))
            {
                if (state == null)
                    continue;
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugGadgetRigDump)}]   state '{state.label}' on '{PathFrom(root, state.transform)}' #{state.GetInstanceID()}"
                    + $" = {state.currentPeckContext.state} (initial {(state.hasInitialState ? state.initialState.ToString() : "-")},"
                    + $" ticket {state.ticket}, savable {state.savableSystem})");
            }

            foreach (var toggle in prop.GetComponentsInChildren<PeckEffectToggle>(true))
            {
                if (toggle == null)
                    continue;
                var system = toggle.peckSystemReference.peckSystem;
                var targets = new List<string>();
                if (toggle.target != null)
                    targets.Add(toggle.target.name);
                if (toggle.targets != null)
                    foreach (var t in toggle.targets)
                        if (t != null)
                            targets.Add(t.name);
                var perState = new List<string>();
                if (toggle.settingsPerState != null)
                    foreach (var on in toggle.settingsPerState)
                        perState.Add(on ? "1" : "0");
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugGadgetRigDump)}]   toggle on '{PathFrom(root, toggle.transform)}' -> [{string.Join(", ", targets)}]"
                    + $" per state [{string.Join("", perState)}] following "
                    + (system == null ? "<none>" : $"'{system.label}' #{system.GetInstanceID()} on '{FullPath(system.transform)}'"));
            }

            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var line = $"[{nameof(DebugGadgetRigDump)}]   {renderer.GetIl2CppType().Name} '{PathFrom(root, renderer.transform)}'"
                    + $" enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy}";

                var skinned = renderer.TryCast<SkinnedMeshRenderer>();
                if (skinned != null)
                {
                    var bones = skinned.bones;
                    var outside = 0;
                    string firstOutside = null;
                    var count = bones != null ? bones.Length : 0;
                    for (var i = 0; i < count; i++)
                    {
                        var bone = bones[i];
                        if (bone == null || bone.IsChildOf(root))
                            continue;

                        outside++;
                        firstOutside ??= FullPath(bone);
                    }

                    var rootBone = skinned.rootBone;
                    line += $" | rootBone={(rootBone == null ? "<none>" : (rootBone.IsChildOf(root) ? PathFrom(root, rootBone) : "OUTSIDE " + FullPath(rootBone)))}"
                        + $" | bones={count}, outside the prop={outside}"
                        + (firstOutside != null ? $" (e.g. {firstOutside})" : "");
                }

                Plugin.Log.LogInfo(line);
            }
        }

        private static string PathFrom(Transform root, Transform t)
        {
            if (t == root)
                return ".";

            var parts = new List<string>();
            for (var cur = t; cur != null && cur != root; cur = cur.parent)
                parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string FullPath(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent)
                parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
