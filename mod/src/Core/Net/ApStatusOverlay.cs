using System;
using UnityEngine;

namespace BigWalkArchipelago.Core.Net
{
    // Tells the host, on screen, when Archipelago is not connected.
    //
    // Until now the only sign was a line in BepInEx's log, which nobody
    // reads while playing: the game carries on perfectly happily while
    // checks pile up unsent, so a dropped connection was invisible. It
    // recovers on its own (ApRuntime retries every 10s and resends
    // everything the save has validated), which is precisely why it needed
    // saying out loud — a silent, self-healing failure is still worth
    // knowing about, if only to stop playing for a minute.
    //
    // IMGUI rather than a Canvas: UnityEngine.IMGUIModule is present in this
    // build, and OnGUI needs no prefab, no canvas, no network object and no
    // cloning of the game's own UI. For one line of text in a corner, that
    // is the whole job.
    //
    // Deliberately silent when all is well. The only exception is a brief
    // confirmation just after connecting, which is what tells the host their
    // details were right without them having to go and read the log.
    internal class ApStatusOverlay : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public ApStatusOverlay(IntPtr ptr) : base(ptr)
        {
        }

        private const int Margin = 12;
        private const int FontSize = 16;

        private static readonly Color WarningColor = new(1f, 0.62f, 0.17f);
        private static readonly Color InfoColor = new(0.72f, 0.9f, 0.72f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.75f);

        // Built on first use, not in a field initializer: anything touching
        // GUI.skin outside of OnGUI is invalid in Unity.
        private GUIStyle _style;

        private void OnGUI()
        {
            if (!ModConfig.ShowConnectionStatus.Value)
                return;

            // Null whenever there is nothing worth saying — ApRuntime owns
            // that decision, so this stays pure presentation and OnGUI (which
            // runs several times per frame) does no work in the common case.
            var message = ApRuntime.StatusMessage;
            if (string.IsNullOrEmpty(message))
                return;

            // Alignment is left at the label default rather than set to
            // TextAnchor.UpperLeft: TextAnchor lives in
            // UnityEngine.TextRenderingModule, and pulling in a whole extra
            // interop assembly to restate a default is not worth it.
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                wordWrap = false,
            };

            var size = _style.CalcSize(new GUIContent(message));
            var rect = new Rect(Margin, Margin, size.x, size.y);

            // Drawn twice, offset, so the text stays legible over whatever
            // the game happens to be rendering behind it — cheaper and more
            // reliable than an outline shader.
            _style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), message, _style);

            _style.normal.textColor = ApRuntime.StatusIsWarning ? WarningColor : InfoColor;
            GUI.Label(rect, message, _style);
        }
    }
}
