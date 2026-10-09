using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Paints the buoys and the flare guns with the seed's colours (Core/Palette), on every machine
    // for its own copy of the world: nothing here is networked, every machine draws the same
    // colour for the same object from things they all share.
    //
    // Measured on 2026-10-06/07 (Debug: DebugColourProbe, DebugBuoyTint, DebugFlareTint):
    //   a buoy     its body "buoy" (househouse/VertexColors) by _TintColor through a property
    //              block; its halo "LobbyFlareMesh" under "bouyLight" (househouse/flare) by
    //              _MainColor; its Light "bouyLight". Its colour comes from its guid (a received
    //              lamp carries its template's, and looks like it).
    //   a gun      body and barrel through their PropertyBlockHelpers (_TintColor, submesh 0; the
    //              grip's accent, submesh 1, stays); its shot through Core/FlareColours. The four
    //              island guns take the first four colours of the seed's draw, by kind (base,
    //              Blue, Green, Yellow, as the apworld's items); a received one, its kind's.
    //   rainbow    a white body; its shot's colour follows Palette.RainbowNow, set every frame.
    //
    // All of it holds once written (a property block, a helper's setting, a light's colour, a
    // particle system's start colour stay put when the gun is picked up and its kernel moves to
    // the hand), so the world is painted once when it is ready or the colours arrive, and a clone
    // as it is built (GadgetItemSpawner).
    internal static class ColourPainter
    {
        private const string Tag = "[" + nameof(ColourPainter) + "]";
        internal const string RainbowMarker = " rainbow";

        private static readonly string[] BuoyNames = { "BuoyLight", "BuoyProp", "BuoyRedProp" };
        private static readonly string[] GunNames = { "FlareGunPropBlue", "FlareGunPropGreen", "FlareGunPropYellow", "FlareGunProp" };

        private static bool _dirty = true;
        private static bool _wasReady;
        private static readonly List<ParticleSystem> RainbowShots = new();
        private static Color _rainbowShown;

        internal static void Repaint() => _dirty = true;

        // Every frame (TrapRunner): the world once it is ready or the colours change, and the
        // rainbow guns' shots.
        internal static void Tick()
        {
            var ready = WorldManager.isReadyForEffects;
            if (ready && !_wasReady)
                _dirty = true;
            if (!ready)
                RainbowShots.Clear();
            _wasReady = ready;

            if (ready && _dirty)
            {
                _dirty = false;
                PaintWorld();
            }

            TickRainbow();
        }

        private static void PaintWorld()
        {
            int buoys = 0, guns = 0;
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null)
                    continue;
                try
                {
                    if (PaintIfBuoy(prop.gameObject, prop.savablePropGuid))
                        buoys++;
                    else if (PaintIfGun(prop.gameObject))
                        guns++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not paint {prop.gameObject.name}: {ex.Message}");
                }
            }

            if (Palette.Active || RainbowShots.Count > 0)
                Plugin.Log.LogInfo($"{Tag} Painted {buoys} buoy(s) and {guns} flare gun(s){(Palette.Active ? string.Empty : " (rainbow only)")}.");
        }

        // A clone just built (GadgetItemSpawner), on every machine.
        internal static void PaintClone(GadgetKind kind, GameObject clone, bool rainbow)
        {
            try
            {
                if (rainbow)
                {
                    clone.name += RainbowMarker;
                    PaintIfGun(clone);
                    return;
                }

                var prop = clone.GetComponent<Prop>();
                if (kind == GadgetKind.Lamp)
                    PaintIfBuoy(clone, prop != null ? prop.savablePropGuid : clone.name);
                else
                    PaintIfGun(clone);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not paint a {kind} clone: {ex.Message}");
            }
        }

        private static bool StartsWithAny(string name, string[] names)
        {
            foreach (var candidate in names)
            {
                if (name.StartsWith(candidate, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Buoys
        // ------------------------------------------------------------------

        private static bool PaintIfBuoy(GameObject root, string key)
        {
            if (!StartsWithAny(root.name, BuoyNames))
                return false;
            if (!Palette.Active)
                return true;

            var colour = Palette.For(string.IsNullOrEmpty(key) ? root.name : key);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "buoy")
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_TintColor", colour.body);
                    renderer.SetPropertyBlock(block);
                }
                else if (renderer.name == "LobbyFlareMesh" && UnderBuoyLight(renderer.transform))
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_MainColor", colour.halo);
                    renderer.SetPropertyBlock(block);
                }
            }

            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.name == "bouyLight")
                    light.color = colour.halo;
            }

            return true;
        }

        // The halo sits at bouyLight/LobbyFlare/LobbyFlareMesh, two levels under the light.
        private static bool UnderBuoyLight(Transform transform)
        {
            for (var t = transform.parent; t != null; t = t.parent)
            {
                if (t.name == "bouyLight")
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Flare guns
        // ------------------------------------------------------------------

        // A gun's place in the seed's draw: base 0, Blue 1, Green 2, Yellow 3 (the apworld's items).
        private static int FlareGunSlot(string name)
        {
            if (name.StartsWith("FlareGunPropBlue", StringComparison.Ordinal))
                return 1;
            if (name.StartsWith("FlareGunPropGreen", StringComparison.Ordinal))
                return 2;
            if (name.StartsWith("FlareGunPropYellow", StringComparison.Ordinal))
                return 3;
            return 0;
        }

        private static bool PaintIfGun(GameObject root)
        {
            if (!StartsWithAny(root.name, GunNames))
                return false;

            var rainbow = root.name.Contains(RainbowMarker, StringComparison.Ordinal);
            if (!rainbow && !Palette.Active)
                return true;

            var colour = rainbow ? default : Palette.At(FlareGunSlot(root.name));
            var body = rainbow ? Palette.RainbowBody : colour.body;

            foreach (var helper in root.GetComponentsInChildren<PropertyBlockHelper>(true))
            {
                var target = helper != null ? helper.targetRenderer : null;
                if (target == null || helper.targetSubmesh != 0 || target.name != "flaregun" && target.name != "flaregunbarrel")
                    continue;
                var settings = helper.colorSettings;
                if (settings == null || settings.Length == 0)
                    continue;

                // An element of an IL2CPP struct array comes back as a copy (ReceivedItemSpawner).
                var setting = settings[0];
                setting.color = body;
                settings[0] = setting;
                helper.Refresh();
            }

            foreach (var effect in root.GetComponentsInChildren<PeckEffectParticleNetworked>(true))
            {
                if (effect == null || effect.name != "FlareGunFlare" || effect.targetParticleSystem == null)
                    continue;
                if (rainbow)
                {
                    if (!RainbowShots.Contains(effect.targetParticleSystem))
                        RainbowShots.Add(effect.targetParticleSystem);
                }
                else
                {
                    FlareColours.Apply(effect.targetParticleSystem, colour.halo);
                }
            }

            return true;
        }

        private static void TickRainbow()
        {
            if (RainbowShots.Count == 0)
                return;
            var colour = Palette.RainbowNow();
            if (colour == _rainbowShown)
                return;
            _rainbowShown = colour;

            for (var i = RainbowShots.Count - 1; i >= 0; i--)
            {
                var shot = RainbowShots[i];
                if (shot == null)
                {
                    RainbowShots.RemoveAt(i);
                    continue;
                }
                FlareColours.Apply(shot, colour);
            }
        }
    }
}
