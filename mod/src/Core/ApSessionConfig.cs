namespace BigWalkArchipelago.Core
{
    // Raw value entered in the host:port field added to the hosting screen
    // (Patches/HostMenuConfirmPatch.cs) — no separate host/port parsing yet
    // and no consumer: the real AP network client (not written yet) will
    // read this value when connecting. Deliberately kept minimal until
    // there is more to do with it.
    internal static class ApSessionConfig
    {
        internal static string HostAndPort = string.Empty;
    }
}
