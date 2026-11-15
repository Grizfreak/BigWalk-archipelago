using System;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The mod's version, in a corner of every menu (player request,
    // 2026-09-25). The first thing worth comparing when two players compare
    // notes is whether they run the same build — the fingerprint check in
    // tools/deploy-mod.ps1 exists because three co-op tests were once lost to
    // a guest on older code — and until now the only place a player could
    // read it was the first lines of BepInEx's log.
    //
    // Menus only: in the world the corner belongs to the game, and the
    // Archipelago overlay already has the top-left; in the world, only while its menus are open.
    internal class ModVersionLabel : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public ModVersionLabel(IntPtr ptr) : base(ptr)
        {
        }

        private const int Margin = 10;
        private const int FontSize = 14;

        private static readonly Color TextColor = new(0.8f, 0.8f, 0.8f, 0.85f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.7f);

        private static readonly string Text = $"{Plugin.PluginName} v{Plugin.PluginVersion}";

        // Built on first use: anything touching GUI.skin outside OnGUI is
        // invalid in Unity.
        private GUIStyle _style;

        private void OnGUI()
        {
            if (WorldManager.isReadyForEffects && !WorldMenuOpen())
                return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = FontSize, wordWrap = false };

            var size = _style.CalcSize(new GUIContent(Text));
            var rect = new Rect(Screen.width - size.x - Margin, Screen.height - size.y - Margin, size.x, size.y);

            _style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), Text, _style);

            _style.normal.textColor = TextColor;
            GUI.Label(rect, Text, _style);
        }

        // In the world, the pause and settings menus too (Tyrope, 2026-11-15).
        private static bool WorldMenuOpen()
        {
            var menus = WorldMenuManager.instance;
            if (menus == null)
                return false;
            return Shown(menus.pauseMenuHost) || Shown(menus.pauseMenuClient) || Shown(menus.settingsMenu);
        }

        private static bool Shown(Component menu) => menu != null && menu.gameObject.activeInHierarchy;
    }
}
