using System;
using System.Collections.Generic;

namespace BigWalkArchipelago.Core
{
    // What the MOD itself is doing right now, as a stack of names.
    //
    // Why it exists (2026-10-03): a gourd handed to a player completed
    // "Coordinates Holding Puzzle". Nothing in the log said the check had
    // been caused by the mod's own hand-over rather than by the player, and
    // finding out took a reproduction, a forced test and a stack trace. A
    // check that fires while the mod is spawning, handing over or releasing
    // something is never a genuine solve, so the reporter asks this class and
    // says so loudly instead (Core/Net/ApReporter.cs).
    //
    // Main thread only, like everything that touches the game.
    internal static class ModActivity
    {
        private static readonly List<string> Names = new();

        // Null when the mod is not acting, i.e. the game is.
        internal static string Current => Names.Count == 0 ? null : string.Join(" > ", Names);

        internal static IDisposable Enter(string name)
        {
            Names.Add(name);
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private bool _done;

            public void Dispose()
            {
                if (_done)
                    return;

                _done = true;
                if (Names.Count > 0)
                    Names.RemoveAt(Names.Count - 1);
            }
        }
    }
}
