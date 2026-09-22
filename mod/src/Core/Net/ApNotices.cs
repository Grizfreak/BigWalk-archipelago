using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core.Net
{
    // The short-lived lines the overlay shows under the connection status:
    // a check going out, an item coming in, a resync being pressed.
    //
    // Until now the only record of any of that was BepInEx's log, which
    // nobody reads while playing — so the one question a player actually has
    // ("did that puzzle count?") had no answer on screen at all, and a
    // report of "nothing happened" could not be told apart from "it happened
    // and I could not see it".
    //
    // Repeats collapse instead of scrolling: connecting to a slot that is
    // owed forty gourds posts forty items in one frame, and forty identical
    // lines would push everything else off the screen for no information.
    // "Gourd x40" is the same fact, and it leaves room for the rest.
    //
    // Main thread only. Everything that posts here — the check flush, the
    // item pump, the resync key — already runs in ApRuntime.Update; the
    // socket callbacks deliberately do not, and report themselves through
    // the status line instead.
    internal static class ApNotices
    {
        internal const int MaxLines = 6;

        internal sealed class Notice
        {
            internal string Text;
            internal string Display;
            internal int Count;
            internal bool Warning;
            internal float ExpiresAt;
        }

        // Oldest first, which is also the order they are drawn in.
        private static readonly List<Notice> Live = new();

        internal static IReadOnlyList<Notice> Lines => Live;

        internal static void Post(string text, bool warning = false)
        {
            if (string.IsNullOrEmpty(text) || !ModConfig.ShowItemFeed.Value)
                return;

            var expiry = Time.unscaledTime + ModConfig.NoticeSeconds.Value;

            // Only the last line collapses, never one further up: two
            // different items arriving either side of a third would
            // otherwise be merged into a count that never happened.
            if (Live.Count > 0)
            {
                var last = Live[Live.Count - 1];
                if (last.Text == text && last.Warning == warning)
                {
                    last.Count++;
                    last.Display = $"{text} x{last.Count}";
                    last.ExpiresAt = expiry;
                    return;
                }
            }

            Live.Add(new Notice
            {
                Text = text,
                Display = text,
                Count = 1,
                Warning = warning,
                ExpiresAt = expiry,
            });

            while (Live.Count > MaxLines)
                Live.RemoveAt(0);
        }

        // Called once a frame from ApRuntime.Update, not from OnGUI: OnGUI
        // runs several times per frame and must stay pure presentation.
        internal static void Prune()
        {
            var now = Time.unscaledTime;
            while (Live.Count > 0 && Live[0].ExpiresAt <= now)
                Live.RemoveAt(0);
        }

        // Dropped wholesale when a session ends: these lines belong to the
        // world that produced them, and carrying them across a return to the
        // main menu would be reporting on a game that is no longer running.
        internal static void Clear()
        {
            Live.Clear();
        }
    }
}
