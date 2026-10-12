using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // `lock_endings_until_objectives`, Big Game's side (ROADMAP G5, player 2026-10-12): the secret room's printer prints a first
    // card by itself, and a refused later print left the request lost. So, while the lock holds, the cards are not there to take:
    // every `CueCard` under `GoodbyePrinter` is hidden (scale zero, which a LODGroup cannot turn back on, as it did for the towers'
    // buttons), and the first card's home refuses a grab (`blockGrabbing`). Local to each machine, like the puzzle hider; the host
    // tells the guests what is shut (ObjectiveBoard). The refusal of OnQueuePeck (Patches/GoodbyePrinterHoldPatch) stays as a
    // second guard.
    internal static class PrinterHold
    {
        private const string Tag = "[" + nameof(PrinterHold) + "]";

        private static Transform _printer;
        private static float _nextSearch;
        private static bool _hidden;
        private static readonly Dictionary<int, Vector3> Scales = new();

        // Every couple of seconds (ObjectiveBoard.Sync), on every machine.
        internal static void Tick()
        {
            var locked = ObjectiveBoard.HoldsTheLastStairway && ObjectiveBoard.Locked("big_game");
            if (!locked && !_hidden)
                return;

            if (_printer == null)
            {
                // Another world (a save loaded, or another one): its cards are new and shown, whatever the last one was
                // (2026-10-12: a lock from an earlier save left `_hidden` true, and the next save's cards were never hidden).
                _hidden = false;
                Scales.Clear();
                if (!locked)
                    return;

                if (Time.unscaledTime < _nextSearch)
                    return;

                Find();
                if (_printer == null)
                    return;
            }

            if (locked == _hidden)
                return;

            foreach (var prop in _printer.GetComponentsInChildren<Prop>(true))
            {
                if (prop == null || !prop.name.StartsWith("CueCard", StringComparison.Ordinal))
                    continue;

                var card = prop.transform;
                var id = card.GetInstanceID();
                if (locked)
                {
                    Scales[id] = card.localScale;
                    card.localScale = Vector3.zero;
                }
                else if (Scales.TryGetValue(id, out var scale))
                {
                    card.localScale = scale;
                }
            }

            foreach (var home in _printer.GetComponentsInChildren<PropHome>(true))
            {
                if (home != null && home.name == "PrinterPropHome")
                    home.blockGrabbing = locked;
            }

            _hidden = locked;
            if (!locked)
                Scales.Clear();
            Plugin.Log.LogInfo($"{Tag} The printer's cards are {(locked ? "hidden" : "back")}.");
        }

        private static void Find()
        {
            _nextSearch = Time.unscaledTime + 10f;
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform != null && transform.name == "GoodbyePrinter")
                {
                    _printer = transform;
                    return;
                }
            }
        }
    }
}
