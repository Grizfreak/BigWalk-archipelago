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
    //   a gadget   (C2, 0.7, player 2026-10-13: "the gadgets that are relevant") every renderer of the
    //              house shader (hhVertexColors, which multiplies its painted vertex colours by _TintColor,
    //              as the buoy) through a property block; one colour per kind for the seed. Left alone:
    //              the paintbrush and the kettles (puzzles), the folding map (paper, another shader), an
    //              emissive part (the laser's tip). The walkie-talkies' blue and yellow mean nothing (player,
    //              2026-10-13): their colour is a PropertyBlockHelper's (_TintColor, _Tint), written too.
    //   a firework the launcher's model (_TintColor) and its explosion (HangingSpark's _EmissionColor and
    //              start colour, the sparks' start colour); the smoke stays black (its start colour,
    //              tinted, did not show in game, 2026-10-13; the player let it go), through their PropertyBlockHelpers; never the four
    //              of the towers (player, 2026-10-13), whose colours are the towers' own. A colour per
    //              launcher, from its landmark.
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

        // C2: the gadgets painted, by kind; a kind's place in the seed's draw is 4 + its row (the four flare guns take
        // the first four), the three cowbells sharing one.
        private static readonly string[][] GadgetKinds =
        {
            new[] { "BinocularsProp" }, new[] { "ClockProp" }, new[] { "CompassProp" },
            new[] { "CowBellLargeProp", "CowBellMediumProp", "CowBellSmallProp" }, new[] { "FmRadioProp" },
            new[] { "LaserProp" }, new[] { "MegaphoneProp" }, new[] { "TorchProp" }, new[] { "XrayGogglesProp" },
            new[] { "WalkieTalkieProp" },
        };
        private const int FirstGadgetSlot = 4;
        private const string LauncherName = "FireworkLauncher";

        private static bool _dirty = true;
        private static bool _wasReady;
        private static readonly List<ParticleSystem> RainbowShots = new();
        private static Color _rainbowShown;

        internal static void Repaint() => _dirty = true;

        // P2 (0.4 playtest: an orange lamp gave a green light, a blue one a white light): the body holds its tint, but
        // switching a lamp on lets the game's own effects put back its light and its halo. So each painted lamp's light
        // and halo are remembered and put back, twice a second, when something else changed them.
        private static readonly List<(Light light, Renderer halo, Color colour, Renderer body, Color bodyColour)> Lamps = new();
        private static float _nextLampCheck;

        private static void KeepTheLamps()
        {
            if (Lamps.Count == 0 || Time.unscaledTime < _nextLampCheck)
                return;

            _nextLampCheck = Time.unscaledTime + 0.5f;
            var fixedCount = 0;
            for (var i = Lamps.Count - 1; i >= 0; i--)
            {
                var (light, halo, colour, body, bodyColour) = Lamps[i];
                if (light == null && halo == null && body == null)
                {
                    Lamps.RemoveAt(i);
                    continue;
                }

                if (light != null && light.enabled && light.color != colour)
                {
                    light.color = colour;
                    fixedCount++;
                }

                if (halo != null)
                {
                    var block = new MaterialPropertyBlock();
                    halo.GetPropertyBlock(block);
                    // A lamp switched off hides its halo (LobbyFlare inactive, colour probe 2026-10-12): only a shown one
                    // is put back. A block the game wrote again reads black, which is no sign of "off".
                    if (halo.gameObject.activeInHierarchy && block.GetColor("_MainColor") != colour)
                    {
                        block.SetColor("_MainColor", colour);
                        halo.SetPropertyBlock(block);
                        fixedCount++;
                    }
                }

                // The body too: the buoy's own PropertyBlockHelper writes its block again when the lamp is switched, and
                // that block has no tint, so a map lamp went back to its own colour while its light kept the seed's
                // (2026-10-12, colour probe: an orange pedestal buoy with the seed's lime light).
                if (body != null)
                {
                    var block = new MaterialPropertyBlock();
                    body.GetPropertyBlock(block);
                    if (block.GetColor("_TintColor") != bodyColour)
                    {
                        block.SetColor("_TintColor", bodyColour);
                        body.SetPropertyBlock(block);
                        fixedCount++;
                    }
                }
            }

            if (fixedCount > 0 && !_lampFixLogged)
            {
                _lampFixLogged = true;
                Plugin.Log.LogInfo($"{Tag} A lamp's light or halo had been changed by the game: put back to the seed's colour.");
            }
        }

        private static bool _lampFixLogged;

        // Every frame (TrapRunner): the world once it is ready or the colours change, and the
        // rainbow guns' shots.
        internal static void Tick()
        {
            var ready = WorldManager.isReadyForEffects;
            if (ready && !_wasReady)
                _dirty = true;
            if (!ready)
            {
                RainbowShots.Clear();
                Lamps.Clear();
            }
            _wasReady = ready;

            if (ready && _dirty)
            {
                _dirty = false;
                PaintWorld();
            }

            TickRainbow();
            if (ready)
                KeepTheLamps();
        }

        private static void PaintWorld()
        {
            int buoys = 0, guns = 0, gadgets = 0;
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
                    else if (PaintIfGadget(prop.gameObject))
                        gadgets++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not paint {prop.gameObject.name}: {ex.Message}");
                }
            }

            var fireworks = PaintFireworks();

            if (Palette.Active || RainbowShots.Count > 0)
                Plugin.Log.LogInfo($"{Tag} Painted {buoys} buoy(s), {guns} flare gun(s), {gadgets} gadget(s) and {fireworks} firework launcher(s){(Palette.Active ? string.Empty : " (rainbow only)")}.");
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
                else if (!PaintIfGun(clone))
                    PaintIfGadget(clone);
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
            if (root.name.StartsWith("BuoyLight", StringComparison.Ordinal))
                colour = ("Orange", Color.white, PedestalLampHalo);
            Renderer haloRenderer = null;
            Renderer bodyRenderer = null;
            Light lampLight = null;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "buoy")
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_TintColor", colour.body);
                    renderer.SetPropertyBlock(block);
                    bodyRenderer = renderer;
                }
                else if (renderer.name == "LobbyFlareMesh" && UnderBuoyLight(renderer.transform))
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_MainColor", colour.halo);
                    renderer.SetPropertyBlock(block);
                    haloRenderer = renderer;
                }
            }

            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.name == "bouyLight")
                {
                    light.color = colour.halo;
                    lampLight = light;
                }
            }

            if (lampLight != null || haloRenderer != null || bodyRenderer != null)
            {
                Lamps.RemoveAll(entry => entry.light == lampLight && entry.halo == haloRenderer && entry.body == bodyRenderer);
                Lamps.Add((lampLight, haloRenderer, colour.halo, bodyRenderer, colour.body));
            }

            return true;
        }

        // The pedestal lamps (BuoyLight): their sphere is orange in the model itself and no tint reaches it (all four
        // regions of househouse/VertexColors tried on 2026-10-13: only the cap took the tint). So their light follows the
        // sphere instead (player, 2026-10-13): an orange light and halo, the body left untinted (its cap stays black).
        private static readonly Color PedestalLampHalo = new Color(1.00f, 0.40f, 0.00f);


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
        // Gadgets (C2)
        // ------------------------------------------------------------------

        private static bool PaintIfGadget(GameObject root)
        {
            var row = Array.FindIndex(GadgetKinds, names => StartsWithAny(root.name, names));
            if (row < 0)
                return false;
            if (!Palette.Active)
                return true;

            var colour = Palette.At(FirstGadgetSlot + row).body;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || !material.name.StartsWith("hhVertexColors", StringComparison.Ordinal)
                    || material.name.StartsWith("hhVertexColorsEmissive", StringComparison.Ordinal))
                    continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_TintColor", colour);
                renderer.SetPropertyBlock(block);
            }

            // A helper would write its own block over ours (the walkie-talkie's WalkieColour): its tints too.
            foreach (var helper in root.GetComponentsInChildren<PropertyBlockHelper>(true))
            {
                var settings = helper != null ? helper.colorSettings : null;
                if (settings == null)
                    continue;
                var changed = false;
                for (var i = 0; i < settings.Length; i++)
                {
                    // An element of an IL2CPP struct array comes back as a copy (ReceivedItemSpawner).
                    var setting = settings[i];
                    if (setting.propertyName != "_TintColor" && setting.propertyName != "_Tint")
                        continue;
                    setting.color = colour;
                    settings[i] = setting;
                    changed = true;
                }
                if (changed)
                    helper.Refresh();
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Firework launchers (C2)
        // ------------------------------------------------------------------

        // The towers' launchers keep their own red, yellow, blue and green: the other four never take those (player,
        // 2026-10-13), but another of the seed's colours, or of the whole palette if the seed has none other.
        private static readonly string[] TowerColours = { "Red", "Yellow", "Blue", "Green" };

        private static (string name, Color body, Color halo) FireworkColour(string landmark)
        {
            var free = new List<(string name, Color body, Color halo)>();
            foreach (var index in Palette.Current)
            {
                if (Array.IndexOf(TowerColours, Palette.Colours[index].name) < 0)
                    free.Add(Palette.Colours[index]);
            }
            if (free.Count == 0)
            {
                foreach (var entry in Palette.Colours)
                {
                    if (Array.IndexOf(TowerColours, entry.name) < 0)
                        free.Add(entry);
                }
            }

            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in "firework " + landmark)
                    hash = (hash ^ c) * 16777619u;
                return free[(int)(hash % (uint)free.Count)];
            }
        }

        private static int PaintFireworks()
        {
            if (!Palette.Active)
                return 0;

            var count = 0;
            foreach (var launcher in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (launcher == null || !launcher.name.StartsWith(LauncherName, StringComparison.Ordinal))
                    continue;
                var landmark = PackChecks.LandmarkOf(launcher);
                if (string.IsNullOrEmpty(landmark) || landmark.StartsWith("Lookout_", StringComparison.Ordinal))
                    continue;

                try
                {
                    var colour = FireworkColour(landmark);
                    foreach (var helper in launcher.GetComponentsInChildren<PropertyBlockHelper>(true))
                    {
                        if (helper == null || helper.colorSettings == null)
                            continue;
                        var property = helper.gameObject.name switch
                        {
                            "fireworkLauncherModel" => "_TintColor",
                            "HangingSpark" => "_EmissionColor",
                            _ => null,
                        };
                        if (property == null)
                            continue;
                        var settings = helper.colorSettings;
                        for (var i = 0; i < settings.Length; i++)
                        {
                            // An element of an IL2CPP struct array comes back as a copy (ReceivedItemSpawner).
                            var setting = settings[i];
                            if (setting.propertyName != property)
                                continue;
                            setting.color = property == "_TintColor" ? colour.body : colour.halo;
                            settings[i] = setting;
                        }
                        helper.Refresh();
                    }

                    foreach (var system in launcher.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (system.name != "HangingSpark" && system.name != "SparkParticleSystem")
                            continue;
                        var main = system.main;
                        main.startColor = new ParticleSystem.MinMaxGradient(colour.halo);
                    }
                    count++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Could not paint the firework in {landmark}: {ex.Message}");
                }
            }
            return count;
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
