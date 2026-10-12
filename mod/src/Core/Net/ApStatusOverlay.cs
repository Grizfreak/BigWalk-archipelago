using System;
using System.Text;
using UnityEngine;

namespace BigWalkArchipelago.Core.Net
{
    // What the host is told on screen about Archipelago: the connection, what
    // the slot is playing for, the last few checks and items, and the way out
    // of a stranded gourd.
    //
    // Until this existed the only sign of any of it was a line in BepInEx's
    // log, which nobody reads while playing. The connection recovers on its
    // own (ApRuntime retries every 10s and resends everything the save has
    // validated), which is precisely why it needed saying out loud — a
    // silent, self-healing failure is still worth knowing about.
    //
    // The status line used to appear only when something was wrong, and
    // briefly on connecting. It now stays for the session, at the player's
    // request (2026-09-22): "and the Archipelago connection all the time".
    // A line that is only ever there when you are already in trouble cannot
    // be used to check that you are not.
    //
    // IMGUI rather than a Canvas: UnityEngine.IMGUIModule is present in this
    // build, and OnGUI needs no prefab, no canvas, no network object and no
    // cloning of the game's own UI. For a few lines of text in a corner,
    // that is the whole job.
    internal class ApStatusOverlay : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public ApStatusOverlay(IntPtr ptr) : base(ptr)
        {
        }

        private const int Margin = 12;

        // The feed and the hint are deliberately smaller than the status:
        // one thing is worth a glance from across the room, the others are
        // worth reading only if you were already looking.
        private const float SecondaryScale = 0.8f;

        private static readonly Color WarningColor = new(1f, 0.62f, 0.17f);
        private static readonly Color InfoColor = new(0.72f, 0.9f, 0.72f);
        private static readonly Color ProgressColor = new(0.95f, 0.88f, 0.6f);
        private static readonly Color FeedColor = new(0.86f, 0.86f, 0.86f);
        private static readonly Color DoneColor = new(0.6f, 0.6f, 0.55f);
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.75f);

        // Built on first use, not in a field initializer: anything touching
        // GUI.skin outside of OnGUI is invalid in Unity.
        private GUIStyle _style;
        private int _styleFontSize;

        private void OnGUI()
        {
            if (!ModConfig.ShowConnectionStatus.Value)
                return;

            // Null whenever there is nothing worth saying — ApRuntime owns
            // that decision on the host. A guest's ApRuntime never has
            // anything to say, since no client ever connects there, so a
            // guest shows what its host last told it instead (ModChannel) —
            // player request, 2026-09-23: "the host has the info in the top
            // left, the clients should have it too".
            var mirrored = string.IsNullOrEmpty(ApRuntime.StatusMessage) && ModChannel.HasFreshSnapshot;

            var message = mirrored ? GuestStatus(ModChannel.MirroredStatus) : ApRuntime.StatusMessage;

            // Another build of the mod on the other side (ModChannel): said even when there is
            // nothing else to say, which is exactly a guest that never hears from its host.
            var warnings = ModChannel.VersionWarnings();
            if (string.IsNullOrEmpty(message) && warnings.Count == 0)
                return;

            var statusIsWarning = mirrored ? ModChannel.MirroredStatusIsWarning : ApRuntime.StatusIsWarning;
            var goal = mirrored ? ModChannel.MirroredGoal : ApRuntime.GoalLine;
            var goalIsWarning = mirrored ? ModChannel.MirroredGoalIsWarning : ApRuntime.GoalLineIsWarning;

            var fontSize = Mathf.Clamp(ModConfig.StatusFontSize.Value, 8, 72);
            if (_style == null || _styleFontSize != fontSize)
            {
                // Alignment is left at the label default rather than set to
                // TextAnchor.UpperLeft: TextAnchor lives in
                // UnityEngine.TextRenderingModule, and pulling in a whole
                // extra interop assembly to restate a default is not worth
                // it.
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    wordWrap = false,
                };
                _styleFontSize = fontSize;
            }

            var y = (float)Margin;
            foreach (var warning in warnings)
                y = DrawLine(warning, WarningColor, fontSize, y);
            if (string.IsNullOrEmpty(message))
                return;

            y = DrawLine(message, statusIsWarning ? WarningColor : InfoColor, fontSize, y);

            // Full size, next to the status rather than down in the feed.
            // What the slot is playing for belongs with whether it is
            // connected: both are session-long facts, and neither is worth
            // hunting for in a log. The warning colour is reserved for a
            // goal this build cannot detect.
            var secondary = Mathf.Max(8, Mathf.RoundToInt(fontSize * SecondaryScale));

            // A goal of several lines (the objectives list): the first at full size, one line each after it.
            if (!string.IsNullOrEmpty(goal))
            {
                var goalLines = goal.Split('\n');
                for (var i = 0; i < goalLines.Length; i++)
                {
                    // A done objective ("[x]") is dimmed and struck through, like on the board in the hub.
                    var done = i > 0 && goalLines[i].TrimStart().StartsWith("[x]", StringComparison.Ordinal);
                    // The ending in bold (the player, 2026-10-12): rich text for these lines only, as an item name of
                    // another game may hold a `<`.
                    _style.richText = true;
                    y = DrawLine(goalLines[i], goalIsWarning ? WarningColor : done ? DoneColor : ProgressColor, i == 0 ? fontSize : secondary, y, done);
                    _style.richText = false;
                }
            }

            // The puzzle parts still missing are no longer listed here (player, 2026-10-09): the
            // permanent overlay is the connection and what arrives.
            if (mirrored)
            {
                foreach (var line in ModChannel.MirroredLines)
                    y = DrawLine(line.Text, line.Warning ? WarningColor : FeedColor, secondary, y);

                // And what this guest was told for itself (a press refused by the host).
                foreach (var notice in ApNotices.Lines)
                    y = DrawLine(notice.Display, notice.Warning ? WarningColor : FeedColor, secondary, y);

                return;
            }

            var lines = ApNotices.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var notice = lines[i];
                y = DrawLine(notice.Display, notice.Warning ? WarningColor : FeedColor, secondary, y);
            }
        }

        // "Archipelago: connected" on a guest's screen would read as the
        // GUEST being connected, which it never is. Says whose connection it
        // is instead.
        private static string GuestStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return status;

            const string prefix = "Archipelago:";
            return status.StartsWith(prefix, StringComparison.Ordinal)
                ? "Archipelago (host):" + status.Substring(prefix.Length)
                : status;
        }

        // Returns the y to draw the next line at.
        private float DrawLine(string text, Color color, int fontSize, float y, bool struck = false)
        {
            _style.fontSize = fontSize;

            var size = _style.CalcSize(new GUIContent(text));
            var rect = new Rect(Margin, y, size.x, size.y);

            // Drawn twice, offset, so the text stays legible over whatever
            // the game happens to be rendering behind it — cheaper and more
            // reliable than an outline shader.
            _style.normal.textColor = ShadowColor;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _style);

            _style.normal.textColor = color;
            GUI.Label(rect, text, _style);

            if (struck)
            {
                // The label's own text starts after the "[x]" box; the strike runs over the name only.
                var start = _style.CalcSize(new GUIContent(text.Substring(0, text.IndexOf(']') + 1))).x;
                var old = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(new Rect(rect.x + start + 4f, rect.y + size.y * 0.55f, size.x - start - 4f, 1.5f), Texture2D.whiteTexture);
                GUI.color = old;
            }

            return y + size.y;
        }

    }
}
