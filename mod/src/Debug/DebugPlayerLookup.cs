namespace BigWalkArchipelago.Debug
{
    // Lookup shared between the debug tools (DebugFlightTool, DebugGourdUnlocker).
    internal static class DebugPlayerLookup
    {
        internal static PlayerCharacter FindLocalPlayer()
        {
            var all = PlayerCharacter.allPlayerCharacters;
            if (all == null)
                return null;

            foreach (var pc in all)
            {
                if (pc != null && pc.isLocalPlayer)
                    return pc;
            }

            return null;
        }
    }
}
