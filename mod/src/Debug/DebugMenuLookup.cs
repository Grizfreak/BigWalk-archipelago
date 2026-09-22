using System;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Dumps the full hierarchy of the hosting screen (HostMenuConfirm) while
    // it is displayed — a prerequisite requested before touching the real
    // game-creation screen (cf. big-walk-archipelago-notes.md, plan to add
    // an AP host:port field): unlike in-game scene mechanisms (Peck/
    // PropHome), a Unity UI breaks easily if you clone/insert blindly
    // (RectTransform/layout/keyboard navigation) — we look at the real
    // layout first before touching it, same philosophy as
    // DebugComponentLookup for unknown Peck mechanisms.
    //
    // No player-proximity filter here (unlike DebugComponentLookup): this
    // screen exists in the main menu, before any local PlayerCharacter
    // exists. We start directly from the root GameObject of the active
    // HostMenuConfirm instance.
    internal static class DebugMenuLookup
    {
        internal static void DumpHostMenuConfirm()
        {
            var menu = UnityEngine.Object.FindObjectOfType<HostMenuConfirm>(true);
            if (menu == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugMenuLookup)}] No active HostMenuConfirm — open the game-creation screen first.");
                return;
            }

            Plugin.Log.LogInfo($"[{nameof(DebugMenuLookup)}] HostMenuConfirm found on '{menu.gameObject.name}'.");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}]   gameNameField -> '{DescribeGameObject(menu.gameNameField?.gameObject)}'");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}]   passwordField -> '{DescribeGameObject(menu.passwordField?.gameObject)}'");
            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}]   passwordRequired = {menu.passwordRequired}");

            // Root = the highest ancestor with a RectTransform (the Canvas or
            // the screen itself): we want to see the whole panel, not just
            // HostMenuConfirm's subtree.
            var root = menu.transform;
            var current = root;
            var guard = 0;
            while (current.parent != null && current.parent.GetComponent<RectTransform>() != null && guard++ < 20)
                current = current.parent;

            Dump(current, 0);
        }

        // anchoredPosition and sizeDelta alone are not enough to say where a
        // thing IS, and two wrong guesses on 2026-09-22 paid for this method.
        // The hosting screen mixes anchors and pivots freely — Continue does
        // not share either with the fields beside it — so two children of one
        // parent can carry coordinates that look far apart and render on top
        // of each other, which is exactly what happened to the Archipelago
        // toggle and the game's delete button.
        //
        // The world span below settles it with no inference at all: left and
        // right edges in world units, directly comparable between any two
        // rows of this dump. If two spans overlap, the elements overlap.
        private static string Describe(RectTransform rect)
        {
            var size = rect.rect;
            var scale = rect.lossyScale;
            var centre = rect.position;

            // rect.rect is local and already expressed relative to the pivot
            // (its x is -pivot.x * width), so the pivot must NOT be applied a
            // second time here — adding it was wrong in the first draft of
            // this method and would have produced exactly the kind of
            // plausible, wrong number this is meant to replace.
            var left = centre.x + size.x * scale.x;
            var bottom = centre.y + size.y * scale.y;

            return $" — anchoredPosition={rect.anchoredPosition} sizeDelta={rect.sizeDelta}"
                 + $" pivot={rect.pivot} anchors={rect.anchorMin}..{rect.anchorMax}"
                 + $" worldX=[{left:0.#}..{left + size.width * scale.x:0.#}]"
                 + $" worldY=[{bottom:0.#}..{bottom + size.height * scale.y:0.#}]";
        }

        private static void Dump(Transform t, int depth)
        {
            if (t == null || depth > 12)
                return;

            var rect = t.GetComponent<RectTransform>();
            var rectInfo = rect != null ? Describe(rect) : string.Empty;

            Plugin.Log.LogInfo(
                $"[{nameof(DebugMenuLookup)}] {new string(' ', depth * 2)}{t.name} — components: {DescribeComponents(t.gameObject)}{rectInfo}");

            for (var i = 0; i < t.childCount; i++)
                Dump(t.GetChild(i), depth + 1);
        }

        private static string DescribeGameObject(GameObject go)
        {
            return go == null ? "<null>" : $"{go.name} (path: {DescribePath(go.transform)})";
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
