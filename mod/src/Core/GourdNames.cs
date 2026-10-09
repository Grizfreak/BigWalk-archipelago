using Mirror;

namespace BigWalkArchipelago.Core
{
    // What the gourds are called on screen (ROADMAP U14, player 2026-10-06): the host names them
    // in Settings > Archipelago, and everyone's overlay says that name instead of "Gourd". The
    // host's .cfg keeps it; guests are told it in the snapshot (ModChannel) and cannot change it.
    internal static class GourdNames
    {
        internal const string Default = "Gourd";
        internal const int MaxLength = 24;

        private static string _mirrored;

        // The name in force on this machine: the host's own, or what the host said.
        internal static string Current
        {
            get
            {
                if (NetworkClient.active && !NetworkServer.active)
                    return string.IsNullOrEmpty(_mirrored) ? Default : _mirrored;

                var name = ModConfig.GourdName.Value;
                return string.IsNullOrWhiteSpace(name) ? Default : name.Trim();
            }
        }

        internal static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        internal static void Set(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length > MaxLength)
                name = name.Substring(0, MaxLength);
            ModConfig.GourdName.Value = name.Length == 0 ? Default : name;
            Net.ModChannel.SendSnapshotNow();
        }

        internal static void ApplyFromHost(string name)
        {
            _mirrored = name;
        }

        internal static void ForgetMirror()
        {
            _mirrored = null;
        }

        // An item's name as shown: the gourd's renamed, every other one as it is.
        internal static string Display(string itemName)
        {
            return itemName == Default ? Current : itemName;
        }

        // "gourds" in a sentence: the custom name with an s, or the plain word.
        internal static string Plural => Current == Default ? "gourds" : Current + "s";
    }
}
