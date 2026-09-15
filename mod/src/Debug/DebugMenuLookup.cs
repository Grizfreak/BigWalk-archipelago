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

        private static void Dump(Transform t, int depth)
        {
            if (t == null || depth > 12)
                return;

            var rect = t.GetComponent<RectTransform>();
            var rectInfo = rect != null
                ? $" — anchoredPosition={rect.anchoredPosition} sizeDelta={rect.sizeDelta}"
                : string.Empty;

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
