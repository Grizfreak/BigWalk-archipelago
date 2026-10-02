namespace BigWalkArchipelago.Core.Net
{
    // The ICheckReporter the mod actually runs with. This is the switch the
    // whole Patches/ layer was designed around: the detection patches still
    // call Plugin.Reporter.ReportCheck(id) and know nothing about any of
    // this (see architecture-mod.md).
    //
    // Still logs every check exactly as before, by delegating to
    // LocalLogReporter rather than reimplementing it — the log line is how
    // every in-game test session so far has been read, and losing it the day
    // the network client arrived would have been a bad trade.
    internal sealed class ApReporter : ICheckReporter
    {
        private readonly ICheckReporter _log = new LocalLogReporter();

        public void ReportCheck(string locationId)
        {
            _log.ReportCheck(locationId);

            // Which side caused it: the game, or the mod's own hands. A check
            // reported while the mod is spawning, handing over or releasing
            // something is a bug, never a solve — the stack says who.
            var activity = ModActivity.Current;
            if (activity != null)
                Plugin.Log.LogWarning(
                    $"[{nameof(ApReporter)}] '{locationId}' was reported while the MOD was acting ({activity}); "
                    + $"this is not a genuine solve. Stack: {new System.Diagnostics.StackTrace(1, false)}");

            SessionJournal.Write("check", locationId);

            // Queued, not sent: this runs inside a Harmony patch on a game
            // write, and ApRuntime owns when anything reaches the socket.
            ApRuntime.QueueLocation(locationId);
        }
    }
}
