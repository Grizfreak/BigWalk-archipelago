using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Diagnostic pur (aucune écriture), pour des mécanismes de scène sans
    // classe C# dédiée trouvable par nom dans il2cpp.cs — ex. les "Arch
    // doors"/boutons de raccourci du document tiers (cf.
    // big-walk-archipelago-notes.md), introuvables statiquement (aucune
    // classe "Arch"/"Shortcut" dans le dump, `strings` sur GameAssembly.dll
    // ne remonte aucun identifiant "Arch" côté jeu non plus). Probablement,
    // comme pour les plinthes de big key, un câblage scène générique
    // (système Peck) plutôt qu'une classe dédiée — ce outil liste, en jeu,
    // les GameObjects proches dont le nom ou un composant matche un mot-clé,
    // avec la liste de leurs composants, pour identifier le vrai mécanisme
    // sans deviner à l'aveugle via Ghidra.
    //
    // Étendu le 2026-09-11 (chemin hiérarchique + savableSystem) pour la
    // sphère noire du hub : le dump F9 du 2026-09-10 a révélé
    // "Spawn_SecondEnding_Sphere_Whole" (mesh+collider, à 3,5m du joueur au
    // hub) et, plus loin (~21m), un cluster "ModelOrb Colliders"/_Wall*/
    // "SecondEndingDoorLogic" (PeckEffectAnimancer) — donc PAS un mécanisme
    // sans aucun Peck comme supposé le 2026-09-10 (cf. big-walk-archipelago-
    // notes.md), juste pas porté par les objets Sphere/_Wall eux-mêmes. Objet
    // clé encore à confirmer : "OpenSystem" (TrackedPeckState+PeckSystemBlock
    // +PeckBusConnection+2xPeckEffectToggle+PeckEffectAudio, à 4,8m, juste à
    // côté du sphere). Sans le chemin hiérarchique on ne peut pas dire si ces
    // objets appartiennent au même assemblage scène — d'où l'ajout ci-dessous.
    internal static class DebugComponentLookup
    {
        private static readonly string[] DefaultKeywords = { "arch", "door", "switch", "button", "gate", "peck", "shortcut", "bell", "chime", "gong", "cowbell", "sphere", "orb", "void", "block", "barrier", "wall", "unlock", "skip", "proven", "reward", "postgame", "complet", "second", "ending" };

        internal static void DumpNearbyByKeyword(float radius)
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            if (localPlayer == null)
            {
                Plugin.Log.LogInfo($"[{nameof(DebugComponentLookup)}] Joueur local introuvable.");
                return;
            }

            var origin = localPlayer.transform.position;
            var sqrRadius = radius * radius;

            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var entries = new List<(GameObject go, float sqrDistance, string components)>();

            foreach (var t in allTransforms)
            {
                if (t == null)
                    continue;

                var sqrDistance = (t.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                    continue;

                var go = t.gameObject;
                var components = DescribeComponents(go);
                if (!Matches(go.name, components))
                    continue;

                entries.Add((go, sqrDistance, components));
            }

            entries.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));

            Plugin.Log.LogInfo($"[{nameof(DebugComponentLookup)}] {entries.Count} objet(s) correspondant dans un rayon de {radius}m :");
            foreach (var entry in entries)
            {
                var distance = Math.Sqrt(entry.sqrDistance).ToString("F1");
                var path = DescribePath(entry.go.transform);
                Plugin.Log.LogInfo($"[{nameof(DebugComponentLookup)}]   {entry.go.name} — distance={distance}m — chemin={path} — composants: {entry.components}");

                var trackedState = entry.go.GetComponent<TrackedPeckState>();
                if (trackedState != null)
                    DescribeTrackedPeckState(trackedState);
            }
        }

        private static string DescribePath(Transform t)
        {
            var names = new List<string>();
            var current = t.parent;
            var guard = 0;
            while (current != null && guard++ < 20)
            {
                names.Insert(0, current.name);
                current = current.parent;
            }

            return names.Count == 0 ? "<racine>" : string.Join("/", names);
        }

        private static void DescribeTrackedPeckState(TrackedPeckState state)
        {
            try
            {
                var saveIdentity = state.saveIdentity;
                var saveGuid = saveIdentity != null ? saveIdentity.saveGuid : "<pas de SaveIdentity>";
                var savableSystem = state.savableSystem;
                var key = savableSystem != SavableSystem.NotSavable ? savableSystem.ToString() : saveGuid;
                var currentValue = SaveManager.GetIntValue(key, -12345, false);

                Plugin.Log.LogInfo(
                    $"[{nameof(DebugComponentLookup)}]     TrackedPeckState — label='{state.label}' — savableSystem={savableSystem} — " +
                    $"saveGuid={saveGuid} — clé réelle='{key}' — SaveManager[{key}]={currentValue}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(DebugComponentLookup)}]     TrackedPeckState — exception pendant la lecture, ignorée : {ex.Message}");
            }
        }

        private static string DescribeComponents(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var names = new StringBuilder();
            foreach (var c in components)
            {
                if (c == null)
                    continue;
                if (names.Length > 0)
                    names.Append(", ");
                names.Append(c.GetIl2CppType().Name);
            }

            return names.ToString();
        }

        private static bool Matches(string goName, string components)
        {
            foreach (var kw in DefaultKeywords)
            {
                if (goName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (components.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
