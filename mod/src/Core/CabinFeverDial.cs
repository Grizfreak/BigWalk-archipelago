using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // R6b (asked by a player 2026-10-05): the wait of a Cabin Fever room hidden, room by room
    // (`cabin_fever_hide_timer`, `cabin_fever_long_hide_timer`, Core/CabinFeverWaits). The dial is a TMP text,
    // "00:00" in a dot-matrix font, that `TimerTextController.Update` writes (recon 2026-10-13:
    // `TimerObject/sizer/textFront`); a postfix writes "??:??" over it while the wait runs, and every
    // FlashEvery seconds one cell flashes a digit for a moment, a different cell and digit each time (the
    // player's pick among four animations, 2026-10-13). Local to each machine, nothing networked. No cue for
    // the end: the game plays its own sound over the last ten seconds.
    internal static class CabinFeverDial
    {
        private const float FlashEvery = 15f;
        private const float FlashFor = 0.25f;
        private const string Hidden = "??:??";

        // Debug (Alt + 5): the animation on both dials even while no wait runs, to look at it.
        internal static bool Preview;

        // By dial: 0 Cabin Fever, 1 Cabin Fever Long, -1 another timer.
        private static readonly Dictionary<int, int> Rooms = new();
        private static readonly HashSet<int> Written = new();

        internal static void Apply(TimerTextController controller)
        {
            if (controller == null || controller.textField == null)
                return;
            var room = RoomOf(controller);
            if (room < 0)
                return;

            var timer = controller.peckEffectTimer;
            var running = timer != null && timer.GetTimeRemaining() > 0f;
            if (!Preview && !(running && CabinFeverWaits.HideTimer(room)))
            {
                // The game writes its text only when the second changes: forget the second it shows, so the next
                // Update puts the game's own text back over the animation.
                if (Written.Remove(controller.GetInstanceID()))
                    controller.displayedSeconds = -1;
                return;
            }

            Written.Add(controller.GetInstanceID());
            var text = Frame(Time.time);
            if (controller.textField.text != text)
                controller.textField.text = text;
        }

        private static string Frame(float now)
        {
            var period = (int)(now / FlashEvery);
            if (now - period * FlashEvery >= FlashFor)
                return Hidden;
            var hash = unchecked((uint)period * 2654435761u);
            var cells = new[] { '?', '?', '?', '?' };
            cells[hash % 4] = (char)('0' + (hash >> 8) % 10);
            return $"{cells[0]}{cells[1]}:{cells[2]}{cells[3]}";
        }

        private static int RoomOf(TimerTextController controller)
        {
            var id = controller.GetInstanceID();
            if (Rooms.TryGetValue(id, out var known))
                return known;

            var room = -1;
            for (var t = controller.transform; t != null; t = t.parent)
            {
                if (t.name.StartsWith("CabinFeverLong", StringComparison.Ordinal))
                {
                    room = 1;
                    break;
                }
                if (t.name.StartsWith("CabinFever", StringComparison.Ordinal))
                {
                    room = 0;
                    break;
                }
            }
            Rooms[id] = room;
            return room;
        }
    }
}
