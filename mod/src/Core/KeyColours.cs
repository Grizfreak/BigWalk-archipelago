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
        // WHAT A MULTIPLY TINT CAN AND CANNOT REACH, learned one wrong colour at
        // a time and worth stating once.
        //
        // `_TintColor` MULTIPLIES the mesh's baked vertex colours, and that base
        // is yellow — which is to say its blue channel is almost zero. So:
        //
        //   - white is the IDENTITY and grey merely DIMS. `#C8C8C8` came out
        //     looking exactly as yellow as before, just darker.
        //   - blue and cyan are UNREACHABLE. Nothing multiplied by a near-zero
        //     blue channel becomes blue: `#28D8D8` came out green.
        //   - purple lands close to red, close enough to be mistaken for the red
        //     key at a glance.
        //   - BLACK is exact, and it is the one colour that always is — zero
        //     times anything is zero.
        //
        // The reachable gamut on these keys therefore runs from red through
        // orange and yellow to green, plus black. The four coloured towers take
        // the closest thing to their own colour, which is legible because it is
        // already true of the towers. The drawbridge takes black, being the one
        // value that owes nothing to the base.
        //
        // NOTE for anyone rebalancing this: the blue tower's `#3A78E0` cannot
        // come out blue either, for the same reason. It reads as a dark olive.
        // That is still distinct from the others, which is all that is asked of
        // it, but it is not blue and no value in this table can make it so.
        //
        // The two that already look different are left alone: the Black
        // Monolith's key and the Green Dome's are the secret-ending and chapel
        // keys, with models of their own. Repainting them would destroy the one
        // distinction the game already gives the player.
        private static readonly Dictionary<SaveablePropName, string> Palette = new()
        {
            { SaveablePropName.bigKeyIntro, "#1E1E1E" },
            { SaveablePropName.bigKeyRedZone, "#E03A3A" },
            { SaveablePropName.bigKeyGreenZone, "#3ACF63" },
            // Settled 2026-09-22, after four measured attempts. True blue is
            // provably unreachable (see the block comment above — the base
            // mesh colour has ~0 blue, and nothing multiplied OR added
            // against ~0 blue becomes blue; confirmed by also trying an
            // additive _EmissionColor, which visibly brightened the key but
            // still came out orange, meaning emission is gated by the same
            // near-zero-blue data). Every hue-based attempt (#3A78E0,
            // #8FC0E0, #6FA8A0, orange #F07E28) either collided with
            // bigKeyGreenZone's vivid green or abandoned "cool" entirely.
            // This is the last lever: a near-NEUTRAL tint (R and G almost
            // equal, instead of G standing above R the way every green
            // attempt did) reads as grey/steel rather than a clear hue,
            // which is as close to "cool-toned" as this material can get.
            { SaveablePropName.bigKeyBlueZone, "#8898A0" },
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
