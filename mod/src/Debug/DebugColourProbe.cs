using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Measuring pass for C1 (ROADMAP, player 2026-10-06): what carries the colour of the buoys,
    // the flare guns and their flares, before writing any. One example of each kind of object
    // (by name, up to the first " ("), everything that could hold a colour: the
    // PropertyBlockHelpers (the game's own way, which the received gourds already use), every
    // renderer's material and the colour properties its shader really has, the property block,
    // the lights and the particle systems. Ctrl+F5; fire a flare first to catch one in flight.
    internal static class DebugColourProbe
    {
        private const string Tag = "[" + nameof(DebugColourProbe) + "]";
        private static readonly string[] Words = { "Buoy", "Flare", "Blind", "Helmet", "Mask" };
        private static HashSet<string> _lastRoots;

        internal static void Dump()
        {
            var seen = new Dictionary<string, int>();
            var examples = new List<GameObject>();

            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform == null || transform.parent != null && Matches(transform.parent.name))
                    continue;
                var name = transform.gameObject.name;
                if (!Matches(name))
                    continue;

                var key = BaseName(name);
                seen[key] = seen.TryGetValue(key, out var count) ? count + 1 : 1;
                if (count == 0)
                    examples.Add(transform.gameObject);
            }

            var log = new StringBuilder();
            log.AppendLine($"{Tag} === colour probe: {examples.Count} kind(s) ===");
            DescribeShots(log);

            // Every active object whose name appeared since the last press, whatever the name: a flare in flight is not
            // called "flare" (2026-10-06), so press once, fire, press again.
            var now = new HashSet<string>();
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (transform != null)
                    now.Add(BaseName(transform.gameObject.name));
            }
            if (_lastRoots != null)
            {
                foreach (var name in now)
                {
                    if (!_lastRoots.Contains(name))
                    {
                        log.AppendLine($"  NEW since the last press: {name}");
                        foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        {
                            if (transform != null && BaseName(transform.gameObject.name) == name)
                            {
                                Describe(transform.gameObject, 1, log);
                                break;
                            }
                        }
                    }
                }
            }
            _lastRoots = now;
            foreach (var root in examples)
                Describe(root, seen[BaseName(root.name)], log);
            Plugin.Log.LogInfo(log.ToString());
        }

        // Every flare gun's shot, system by system: how its start colour is made (the smoke keeps
        // its own colour when the flare's is changed, 2026-10-06).
        private static void DescribeShots(StringBuilder log)
        {
            foreach (var effect in UnityEngine.Object.FindObjectsByType<PeckEffectParticleNetworked>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (effect == null || effect.name != "FlareGunFlare" || effect.targetParticleSystem == null)
                    continue;
                var gun = effect.transform.parent != null ? effect.transform.parent.name : "?";
                log.AppendLine($"  shot of {gun} ({Path(effect.transform)}):");
                foreach (var system in effect.targetParticleSystem.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var start = system.main.startColor;
                    log.Append($"    {system.name}: mode {start.m_Mode}, min {Hex(start.m_ColorMin)}, max {Hex(start.m_ColorMax)}");
                    foreach (var (label, gradient) in new[] { ("gradient min", start.m_GradientMin), ("gradient max", start.m_GradientMax) })
                    {
                        if (gradient == null)
                            continue;
                        log.Append($", {label}");
                        foreach (var key in gradient.colorKeys)
                            log.Append($" {Hex(key.color)}@{key.time:0.##}");
                    }
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    var material = renderer != null ? renderer.sharedMaterial : null;
                    log.AppendLine(material != null ? $" | material {material.name}" : string.Empty);
                }
            }
        }

        private static bool Matches(string name)
        {
            foreach (var word in Words)
            {
                if (name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string BaseName(string name)
        {
            var paren = name.IndexOf(" (", System.StringComparison.Ordinal);
            return paren > 0 ? name.Substring(0, paren) : name;
        }

        private static void Describe(GameObject root, int count, StringBuilder log)
        {
            log.AppendLine($"--- {BaseName(root.name)} x{count} (active={root.activeInHierarchy}, path {Path(root.transform)})");

            foreach (var helper in root.GetComponentsInChildren<PropertyBlockHelper>(true))
            {
                var target = helper.targetRenderer != null ? helper.targetRenderer.name : "<none>";
                var extra = helper.additonalRenderers != null ? helper.additonalRenderers.Length : 0;
                log.AppendLine($"  helper on {helper.name}: target {target} (+{extra}), submesh {helper.targetSubmesh}");
                if (helper.colorSettings != null)
                {
                    foreach (var setting in helper.colorSettings)
                        log.AppendLine($"    colour {setting.propertyName} = {Hex(setting.color)}");
                }
                if (helper.floatSettings != null)
                {
                    foreach (var setting in helper.floatSettings)
                        log.AppendLine($"    float {setting.propertyName} = {setting.floatValue}");
                }
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; materials != null && i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                        continue;
                    var shader = material.shader;
                    log.Append($"  renderer {renderer.name} [{i}] {renderer.GetIl2CppType().Name}: material {material.name}, shader {(shader != null ? shader.name : "<none>")}");
                    if (shader != null)
                    {
                        for (var p = 0; p < shader.GetPropertyCount(); p++)
                        {
                            if (shader.GetPropertyType(p) != UnityEngine.Rendering.ShaderPropertyType.Color)
                                continue;
                            var property = shader.GetPropertyName(p);
                            log.Append($" | {property}={Hex(material.GetColor(property))}");
                        }
                    }
                    log.AppendLine();
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                if (!block.isEmpty)
                {
                    log.Append($"    block on {renderer.name}:");
                    foreach (var property in new[] { "_Color", "_BaseColor", "_TintColor", "_EmissionColor", "_RColor", "_GColor", "_BColor", "_LightColor" })
                    {
                        if (block.HasColor(property))
                            log.Append($" {property}={Hex(block.GetColor(property))}");
                    }
                    log.AppendLine();
                }
            }

            // What a peck effect fires (the flare gun's shot, 2026-10-06): its particle system can
            // live outside the object, so it is followed by reference.
            foreach (var effect in root.GetComponentsInChildren<PeckEffectParticleNetworked>(true))
            {
                var target = effect.targetParticleSystem;
                log.AppendLine($"  peck particles (networked) on {effect.name}: target {(target != null ? Path(target.transform) + "/" + target.name : "<none>")}");
                if (target != null && !target.transform.IsChildOf(root.transform))
                    Describe(target.gameObject, 1, log);
            }
            foreach (var effect in root.GetComponentsInChildren<PeckEffectParticle>(true))
            {
                var target = effect.targetParticleSystem;
                log.AppendLine($"  peck particles on {effect.name}: target {(target != null ? Path(target.transform) + "/" + target.name : "<none>")}");
                if (target != null && !target.transform.IsChildOf(root.transform))
                    Describe(target.gameObject, 1, log);
            }

            // Big Mask (2026-10-07): what a blindfold helmet is made of, and who wears what.
            var components = new List<string>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;
                var type = component.GetIl2CppType().Name;
                if (type is "Transform" or "MeshFilter" or "MeshRenderer" or "PropertyBlockHelper" or "Light" or "ParticleSystem" or "ParticleSystemRenderer")
                    continue;
                var entry = $"{component.name}:{type}";
                if (!components.Contains(entry))
                    components.Add(entry);
            }
            if (components.Count > 0)
                log.AppendLine($"  components: {string.Join(", ", components)}");

            // The sounds a prop's peck effects play, state by state (Big Flare's shot has none of its
            // own, 2026-10-09): which effect, under which state system, playing which asset.
            foreach (var audio in root.GetComponentsInChildren<PeckEffectAudio>(true))
            {
                var system = audio.systemReference != null && audio.systemReference.peckSystem != null
                    ? audio.systemReference.peckSystem.gameObject.name : "<none>";
                log.Append($"  audio on {audio.gameObject.name} (state system {system}):");
                var outcomes = audio.outcomesPerState;
                for (var i = 0; outcomes != null && i < outcomes.Length; i++)
                {
                    var action = outcomes[i].audioAction;
                    var items = action != null ? action.Actions : null;
                    if (items == null || items.Length == 0)
                        continue;
                    foreach (var item in items)
                        log.Append($" [state {i}] {item.ActionType} {(item.Asset != null ? item.Asset.name : "<none>")}");
                }
                log.AppendLine();
            }

            foreach (var light in root.GetComponentsInChildren<Light>(true))
                log.AppendLine($"  light {light.name}: {Hex(light.color)} intensity {light.intensity} range {light.range} enabled={light.enabled}");

            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                log.AppendLine($"  particles {particles.name}: start colour {Hex(main.startColor.color)}");
            }
        }

        // By hand: ColorUtility.ToHtmlStringRGBA is stripped from the game ("Method unstripping
        // failed", 2026-10-06). HDR values (above 1) are shown as numbers too.
        private static string Hex(Color colour)
        {
            int Byte(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
            var hex = $"#{Byte(colour.r):X2}{Byte(colour.g):X2}{Byte(colour.b):X2}{Byte(colour.a):X2}";
            var max = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            return max > 1f ? $"{hex} (HDR {colour.r:0.##},{colour.g:0.##},{colour.b:0.##})" : hex;
        }

        private static string Path(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform.parent; t != null && parts.Count < 3; t = t.parent)
                parts.Insert(0, t.name);
            return string.Join("/", parts);
        }
    }
}
