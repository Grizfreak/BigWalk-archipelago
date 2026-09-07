namespace BigWalkArchipelago.Debug
{
    // Lookup partagé entre les outils de debug (DebugFlightTool, DebugGourdUnlocker).
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
