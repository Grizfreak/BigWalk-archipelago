using System;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Dump la hiérarchie complète de l'écran d'hébergement (HostMenuConfirm)
    // pendant qu'il est affiché — préalable demandé avant de toucher au vrai
    // écran de création de partie (cf. big-walk-archipelago-notes.md, projet
    // d'ajouter un champ host:port AP) : contrairement aux mécanismes de
    // scène en jeu (Peck/PropHome), une UI Unity casse facilement si on
    // clone/insère à l'aveugle (RectTransform/layout/navigation clavier) —
    // on regarde d'abord le layout réel avant d'y toucher, même philosophie
    // que DebugComponentLookup pour les mécanismes Peck inconnus.
    //
    // Pas de filtre par proximité joueur ici (contrairement à
    // DebugComponentLookup) : cet écran existe au menu principal, avant
    // qu'un PlayerCharacter local existe. On part directement du
    // GameObject racine de l'instance HostMenuConfirm active.
    internal static class DebugMenuLookup
    {
        internal static void DumpHostMenuConfirm()
        {
            var menu = UnityEngine.Object.FindObjectOfType<HostMenuConfirm>(true);
            if (menu == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugMenuLookup)}] Aucun HostMenuConfirm actif — ouvrir l'écran de création de partie d'abord.");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugMenuLookup)}] HostMenuConfirm trouvé sur '{menu.gameObject.name}'.");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}]   gameNameField -> '{DescribeGameObject(menu.gameNameField?.gameObject)}'");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}]   passwordField -> '{DescribeGameObject(menu.passwordField?.gameObject)}'");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}]   passwordRequired = {menu.passwordRequired}");

            // Racine = le plus haut ancêtre avec un RectTransform (le Canvas
            // ou l'écran lui-même) : on veut voir tout le panneau, pas juste
            // le sous-arbre de HostMenuConfirm.
            var root = menu.transform;
            var current = root;
            var guard = 0;
            while (current.parent != null && current.parent.GetComponent<RectTransform>() != null && guard++ < 20)
                current = current.parent;

            Dump(current, 0);
        }

        private static void Dump(Transform t, int depth)
        {
            if (t == null || depth > 12)
                return;

            var rect = t.GetComponent<RectTransform>();
            var rectInfo = rect != null
                ? $" — anchoredPosition={rect.anchoredPosition} sizeDelta={rect.sizeDelta}"
                : string.Empty;

            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}] {new string(' ', depth * 2)}{t.name} — composants: {DescribeComponents(t.gameObject)}{rectInfo}");

            for (var i = 0; i < t.childCount; i++)
                Dump(t.GetChild(i), depth + 1);
        }

        private static string DescribeGameObject(GameObject go)
        {
            return go == null ? "<null>" : $"{go.name} (chemin: {DescribePath(go.transform)})";
        }

        private static string DescribePath(Transform t)
        {
            var name = t.name;
            var current = t.parent;
            var guard = 0;
            while (current != null && guard++ < 20)
            {
                name = current.name + "/" + name;
                current = current.parent;
            }

            return name;
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
    }
}
