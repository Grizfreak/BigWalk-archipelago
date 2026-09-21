using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Tells the big keys apart on sight, once they start arriving from
    // Archipelago and piling up at the spawn point.
    //
    // WHY IT IS NEEDED. Five of the seven keys look identical — the drawbridge
    // and the four coloured towers all carry the same yellow blank. Only the
    // Black Monolith's and the Green Dome's have a design of their own, and
    // those two are the ones a player is least likely to confuse. In the
    // vanilla game that is fine, because a key is released at the tower it
    // belongs to and never meets another. In this world they are items, and
    // three of them lying in a heap at the hub are three identical objects.
    //
    // WHY NOT THE WAY THE GOURDS DO IT. `ReceivedItemSpawner.ApplyCosmeticColor`
    // sets `RewardGourd.isVariantChallenge` + `variantChallengeColor`, and a
    // big key has no `RewardGourd` at all — that is long established, and it
    // is why `ItemApplier` has a separate path for keys in the first place.
    //
    // WHAT THE GAME ACTUALLY EXPOSES (measured 2026-09-21). The five yellow
    // keys carry **no `PropertyBlockHelper`** and 26 renderers each, every one
    // on a material named `hhVertexColors` — so the yellow is baked into the
    // mesh's vertex colours rather than held in a material property. This
    // therefore writes a `MaterialPropertyBlock` straight onto each renderer,
    // since there is no helper to go through.
    //
    // WHICH PROPERTY, and the wrong answer is worth recording. The first
    // attempt wrote `_RColor`, taken from the Black Monolith key's own
    // helper — the one key that carries one. It is a real name on that prop
    // and it does not exist on this shader, so Unity accepted every write and
    // changed nothing: five keys reported painted, five keys still yellow.
    // **A MaterialPropertyBlock never complains about a property the shader
    // does not have.**
    //
    // The shader is `househouse/VertexColors` and declares `_TintColor`,
    // `_TintMask0` to `_TintMask3`, and `_EmissionColor`. `_TintColor`
    // multiplies the baked vertex colours, which is the classic setup for a
    // shader by that name and is the default; the masks are there for a body
    // whose colour turns out to live in one of them. The list is configurable
    // (`Archipelago/KeyColorProperty`, comma-separated) precisely so that can
    // be tried without a rebuild.
    internal static class KeyColours
    {
        // SATURATED HUES ONLY, and that is not a matter of taste. `_TintColor`
        // MULTIPLIES the mesh's baked vertex colours, so white is the identity
        // and grey merely dims: the drawbridge key was first given `#C8C8C8`
        // and came out looking exactly as yellow as before, just darker. Only a
        // hue can move a yellow key off yellow.
        //
        // The four coloured towers take their own colours, which is both
        // legible and already true of the towers themselves. The drawbridge has
        // no colour of its own, so it takes the one nothing else uses.
        //
        // The two that already look different are left alone: the Black
        // Monolith's key and the Green Dome's are the secret-ending and chapel
        // keys, with models of their own. Repainting them would destroy the one
        // distinction the game already gives the player.
        private static readonly Dictionary<SaveablePropName, string> Palette = new()
        {
            { SaveablePropName.bigKeyIntro, "#A64AE0" },
            { SaveablePropName.bigKeyRedZone, "#E03A3A" },
            { SaveablePropName.bigKeyGreenZone, "#3ACF63" },
            { SaveablePropName.bigKeyBlueZone, "#3A78E0" },
            { SaveablePropName.bigKeyYellowZone, "#E8C53C" },
        };

        // Painted once per world rather than on every tick: a property block is
        // 26 renderers per key, and nothing the game does afterwards puts the
        // original material back — verified by the keys staying painted across
        // a delivery and a pick-up.
        private static bool _painted;

        private static bool _propertyLogged;

        // A world reload rebuilds every renderer, so the paint has to go on
        // again. Called from the same place the ledgers are re-armed.
        internal static void Rearm()
        {
            _painted = false;
        }

        internal static void PaintOnce()
        {
            if (_painted || !ModConfig.ColorBigKeys.Value)
                return;

            var properties = ParseProperties(ModConfig.KeyColorProperty.Value);
            if (properties.Length == 0)
                return;

            var keys = GourdRegistry.LoadedBigKeys();
            var painted = 0;
            var seen = 0;

            foreach (var prop in keys)
            {
                seen++;
                if (!Palette.TryGetValue(prop.saveablePropName, out var html))
                    continue;

                if (!ColorUtility.TryParseHtmlString(html, out var colour))
                    continue;

                if (Paint(prop, properties, colour))
                    painted++;
            }

            // Nothing loaded yet means "try again next tick", not "done".
            if (seen == 0)
                return;

            _painted = true;
            Plugin.Log.LogInfo(
                $"[{nameof(KeyColours)}] Painted {painted} of {seen} big key(s) through "
                + $"{string.Join(", ", properties)}. A property the shader does not declare is accepted "
                + "silently and does nothing, so if they still look alike the line above names the ones "
                + "it really has.");
        }

        // Comma-separated, trimmed, empties dropped.
        private static string[] ParseProperties(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return Array.Empty<string>();

            var parts = configured.Split(',');
            var kept = new List<string>();
            foreach (var part in parts)
            {
                var name = part.Trim();
                if (name.Length > 0)
                    kept.Add(name);
            }

            return kept.ToArray();
        }

        private static bool Paint(Prop prop, string[] properties, Color colour)
        {
            try
            {
                var renderers = prop.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    return false;

                LogShaderPropertiesOnce(renderers[0]);

                var block = new MaterialPropertyBlock();
                foreach (var renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    // Read-modify-write: the block carries whatever the game
                    // already set on this renderer, and replacing it wholesale
                    // would drop settings this has no business touching.
                    renderer.GetPropertyBlock(block);
                    foreach (var property in properties)
                        block.SetColor(property, colour);
                    renderer.SetPropertyBlock(block);
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(KeyColours)}] Could not paint {prop.saveablePropName}: {ex.Message}");
                return false;
            }
        }

        // Once per session: what the shader on a key actually declares. This is
        // the answer to "the colour did not take, so what IS the property
        // called", and printing it here costs one call and saves a round trip
        // through the game.
        private static void LogShaderPropertiesOnce(Renderer renderer)
        {
            if (_propertyLogged)
                return;

            _propertyLogged = true;

            try
            {
                var material = renderer.sharedMaterial;
                var shader = material != null ? material.shader : null;
                if (shader == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(KeyColours)}] A key renderer has no shader to inspect.");
                    return;
                }

                var count = shader.GetPropertyCount();
                var names = new List<string>();
                for (var i = 0; i < count; i++)
                    names.Add($"{shader.GetPropertyName(i)}({shader.GetPropertyType(i)})");

                Plugin.Log.LogInfo(
                    $"[{nameof(KeyColours)}] Shader '{shader.name}' on the keys declares: {string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(KeyColours)}] Could not read the key shader's properties ({ex.Message}).");
            }
        }
    }
}
