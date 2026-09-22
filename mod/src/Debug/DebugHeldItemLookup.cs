using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Answers "what is in my hands right now, and what is it actually
    // called" — the question that came up trying to identify an object by
    // description alone (2026-09-22, "the ray-gun I can hold in hands").
    // heldProp is the local, gameplay-side answer (cf.
    // CosmeticGourdSpawnHandler.FindHolderName's comment on the same field),
    // which is exactly what is wanted here: this machine's own player,
    // holding whatever they are currently holding.
    internal static class DebugHeldItemLookup
    {
        internal static void LogHeldItem()
        {
            var player = DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHeldItemLookup)}] Local player not found.");
                return;
            }

            var prop = player.hands != null ? player.hands.heldProp : null;
            if (prop == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugHeldItemLookup)}] Not holding anything.");
                return;
            }

            var groups = "<none>";
            var list = prop.propGroups;
            if (list != null && list.Count > 0)
            {
                var parts = new string[list.Count];
                for (var i = 0; i < list.Count; i++)
                    parts[i] = list[i].ToString();
                groups = string.Join(", ", parts);
            }

            var hasGuid = !string.IsNullOrEmpty(prop.savablePropGuid);

            Plugin.Log.LogInfo(
                $"[{nameof(DebugHeldItemLookup)}] Holding '{prop.gameObject.name}' | guid="
                + (hasGuid ? prop.savablePropGuid : "<none>")
                + $" | saveablePropName={prop.saveablePropName} | saveType={prop.propSaveType} | groups [{groups}]");

            // Added 2026-09-22 to tell apart two candidate causes of the
            // pink/magenta clones: a stale lightmapIndex (ruled out — the
            // pink persisted after clearing it) versus a shader/material
            // that isn't what MaterialPropertyBlock capture/reapply assumed.
            // Prints exactly what each renderer is actually drawing with.
            var renderers = prop.gameObject.GetComponentsInChildren<Renderer>(true);
            Plugin.Log.LogInfo($"[{nameof(DebugHeldItemLookup)}]   {renderers.Length} renderer(s):");
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                var sharedMaterial = renderer.sharedMaterial;
                var materialName = sharedMaterial != null ? sharedMaterial.name : "<null>";
                var shaderName = sharedMaterial != null && sharedMaterial.shader != null
                    ? sharedMaterial.shader.name
                    : "<no shader>";

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                var blockEmpty = block.isEmpty;

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugHeldItemLookup)}]     '{renderer.gameObject.name}' | material={materialName} | shader={shaderName}"
                    + $" | propertyBlockEmpty={blockEmpty} | lightmapIndex={renderer.lightmapIndex} | enabled={renderer.enabled}");
            }
        }
    }
}
