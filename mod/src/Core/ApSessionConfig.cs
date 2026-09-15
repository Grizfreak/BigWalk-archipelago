namespace BigWalkArchipelago.Core
{
    // Raw value currently in the host:port field added to the hosting screen
    // (Patches/HostMenuConfirmPatch.cs). Written as the player types,
    // mirrored into the BepInEx config so it survives a restart, and read by
    // Core/Net/ApEndpoint when the client connects.
    //
    // Only the address lives here: the slot name and the Archipelago
    // password are the hosting screen's own two fields, which the game
    // already persists into SaveData, so ApEndpoint reads them back from
    // there rather than having them shadowed in a second place.
    internal static class ApSessionConfig
    {
        internal static string HostAndPort = string.Empty;
    }
}
