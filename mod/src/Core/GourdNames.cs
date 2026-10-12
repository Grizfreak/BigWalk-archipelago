using Mirror;

namespace BigWalkArchipelago.Core
{
    // What the gourds are called on screen (ROADMAP U14, player 2026-10-06): the host names them
    // in Settings > Archipelago, and everyone's overlay says that name instead of "Gourd". The
    // host's .cfg keeps it; guests are told it in the snapshot (ModChannel) and cannot change it.
    // Left at "Gourd", the host shows the seed's gourd_name instead.
    internal static class GourdNames
    {
        internal const string Default = "Gourd";
        internal const int MaxLength = 24;

        private static string _mirrored;
        private static string _seed;

        // The name in force on this machine: the host's own, or what the host said.
        internal static string Current
        {
            get
            {
                if (NetworkClient.active && !NetworkServer.active)
                    return string.IsNullOrEmpty(_mirrored) ? Default : _mirrored;

                var name = CleanedConfig(ModConfig.GourdName.Value);
                if (name.Length > 0 && name != Default)
                    return name;
                return string.IsNullOrEmpty(_seed) ? Default : _seed;
            }
        }

        internal static void Configure(Net.ApSlotData slot)
        {
            var name = Clean(slot.GourdName).Trim();
            if (name.Length > MaxLength)
                name = name.Substring(0, MaxLength);
            _seed = name.Length == 0 ? null : name;
            Net.ModChannel.SendSnapshotNow();
        }

        internal static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        // What a name may hold, the same as the apworld's (gourd_name.py): letters, marks and digits of any
        // script, spaces, and a few signs. The rest (control and invisible characters, emoji, `<` and `>`,
        // which TextMeshPro reads as tags, braces, `|`, `#`, slashes, quotes) is dropped.
        private const string AllowedSigns = "'-_.,!?&+:;()@";

        internal static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            // Decorated letters become plain ones first, as the apworld does (NFKC).
            try
            {
                name = name.Normalize(System.Text.NormalizationForm.FormKC);
            }
            catch (ArgumentException)
            {
                // A lone surrogate cannot be normalised: the filter below drops it anyway.
            }

            var text = new System.Text.StringBuilder();
            foreach (var character in name)
            {
                if (char.IsWhiteSpace(character))
                {
                    text.Append(' ');
                    continue;
                }

                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character);
                var letter = char.IsLetterOrDigit(character)
                             || category == System.Globalization.UnicodeCategory.NonSpacingMark
                             || category == System.Globalization.UnicodeCategory.SpacingCombiningMark
                             || category == System.Globalization.UnicodeCategory.EnclosingMark
                             || category == System.Globalization.UnicodeCategory.LetterNumber
                             || category == System.Globalization.UnicodeCategory.OtherNumber;
                if (letter || AllowedSigns.IndexOf(character) >= 0)
                    text.Append(character);
            }

            var cleaned = string.Join(" ", text.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

            // A name needs a letter: only digits and signs (`{0}` once the braces are dropped) is no name (the player, 2026-10-12).
            foreach (var character in cleaned)
            {
                if (char.IsLetter(character))
                    return cleaned;
            }

            return string.Empty;
        }

        internal static void Set(string name)
        {
            name = Clean(name).Trim();
            if (name.Length > MaxLength)
                name = name.Substring(0, MaxLength);
            ModConfig.GourdName.Value = name.Length == 0 ? Default : name;
            Net.ModChannel.SendSnapshotNow();
        }

        internal static void ApplyFromHost(string name)
        {
            _mirrored = Clean(name);
        }

        // Current is read often (the overlay, the board): the .cfg's text, which can be edited by hand, is
        // cleaned once and remembered.
        private static string _lastRaw;
        private static string _lastCleaned = string.Empty;

        private static string CleanedConfig(string raw)
        {
            if (!ReferenceEquals(raw, _lastRaw) && raw != _lastRaw)
            {
                _lastRaw = raw;
                _lastCleaned = Clean(raw);
                if (_lastCleaned.Length > MaxLength)
                    _lastCleaned = _lastCleaned.Substring(0, MaxLength).TrimEnd();
            }

            return _lastCleaned;
        }

        internal static void ForgetMirror()
        {
            _mirrored = null;
        }

        // Matched by id: the server may already send the seed's name.
        internal static string Display(long itemId, string itemName)
        {
            return itemId == Net.ApLocationIds.GourdItemId ? Current : itemName;
        }

        // "gourds" in a sentence: the custom name with an s, or the plain word.
        internal static string Plural => Current == Default ? "gourds" : Current + "s";
    }
}
