using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // C1 (player, 2026-10-06: the buoy's body matches its halo). Measured: the body's shader
    // (househouse/VertexColors) multiplies its painted vertex colours by _TintColor; _TintMask0
    // does the same, 1 to 3 nothing; the mesh is not readable from script, so no compensation by
    // its paint. A plain tint already gives frank colours (deep blue, pink-red), the paint being
    // light and warm, so each colour of the palette has a body tint tuned by eye beside its halo
    // colour. Each Ctrl+F6 paints every buoy with the next one; the last step puts them back.
    internal static class DebugBuoyTint
    {
        private const string Tag = "[" + nameof(DebugBuoyTint) + "]";

        internal static (string name, Color body, Color halo)[] Palette => BigWalkArchipelago.Core.Palette.Colours;

        private static int _step = -1;

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

        internal static void Next()
        {
            _step = (_step + 1) % (Palette.Length + 1);
            var reset = _step == Palette.Length;
            var colour = reset ? Palette[0] : Palette[_step];
            var bodies = 0;

            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;

                if (renderer.name == "buoy")
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_TintColor", reset ? Color.white : colour.body);
                    block.SetColor("_TintMask0", Color.white);
                    renderer.SetPropertyBlock(block);
                    bodies++;
                }
                else if (renderer.name == "LobbyFlareMesh" && UnderBuoyLight(renderer.transform))
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_MainColor", reset ? new Color(1f, 0.227f, 0f, 1f) : colour.halo);
                    renderer.SetPropertyBlock(block);
                }
            }

            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light != null && light.name == "bouyLight")
                    light.color = reset ? new Color(1f, 0.718f, 0.4f, 1f) : colour.halo;
            }

            Plugin.Log.LogInfo(reset
                ? $"{Tag} Back to normal ({bodies} bodies)."
                : $"{Tag} Colour {_step + 1}/{Palette.Length}: {colour.name} on {bodies} bodies, halos and lights.");
        }
    }
}
