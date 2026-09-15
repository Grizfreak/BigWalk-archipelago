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

            // Queued, not sent: this runs inside a Harmony patch on a game
            // write, and ApRuntime owns when anything reaches the socket.
            ApRuntime.QueueLocation(locationId);
        }
    }
}
