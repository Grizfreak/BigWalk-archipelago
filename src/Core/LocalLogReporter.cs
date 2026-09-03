namespace BigWalkArchipelago.Core
{
    internal sealed class LocalLogReporter : ICheckReporter
    {
        public void ReportCheck(string locationId)
        {
            Plugin.Log.LogInfo($"[Check] {locationId}");
        }
    }
}
