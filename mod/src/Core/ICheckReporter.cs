namespace BigWalkArchipelago.Core
{
    // A patch calls Plugin.Reporter.ReportCheck(id) without knowing what
    // happens behind it: log locally today, talk to the Archipelago server
    // tomorrow — no line to touch in Patches/ for that switch.
    internal interface ICheckReporter
    {
        void ReportCheck(string locationId);
    }
}
