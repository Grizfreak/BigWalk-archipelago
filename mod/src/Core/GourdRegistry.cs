using System;
using System.Collections.Generic;

namespace BigWalkArchipelago.Core
{
    // Translates a SaveablePropName (internal game identifier) into an
    // Archipelago location id. For now the location id is simply the enum
    // name itself (e.g. "gourdCabinFever"): a stable, readable key,
    // sufficient until the real Archipelago protocol is wired in. The only
    // place to modify to change the mapping or exclude other values.
    internal static class GourdRegistry
    {
        private static readonly Dictionary<SaveablePropName, string> LocationIdsByProp = BuildLocationIds();

        // Unlike gourdXxx/valetXxx, big keys are named by their zone of
        // origin (color) while their plinths are named by physical deposit
        // location: no naming-based mapping is derivable. Confirmed by the
        // player in-game (session on 2026-09-07, cf.
        // big-walk-archipelago-notes.md) — not decompilable via Ghidra,
        // this wiring only exists in Unity scene assets.
        private static readonly Dictionary<SaveablePropName, SaveableHomeName> BigKeyHomesByProp = new()
        {
            { SaveablePropName.bigKeyIntro, SaveableHomeName.bigKeyPlinthIntro },
            { SaveablePropName.bigKeyRedZone, SaveableHomeName.bigKeyPlinthMapRoom },
            { SaveablePropName.bigKeyGreenZone, SaveableHomeName.bigKeyPlinthSkiLift },
            { SaveablePropName.bigKeyBlueZone, SaveableHomeName.bigKeyPlinthTrain },
            { SaveablePropName.bigKeyYellowZone, SaveableHomeName.bigKeyPlinthTunnels },
            { SaveablePropName.bigKeyBoss, SaveableHomeName.bigKeyPlinthEnding },
            { SaveablePropName.bigKeyOverflow, SaveableHomeName.bigKeyPlinthGoodbye2 },
        };

        // 3 gourds whose corresponding valet is NOT the naive
        // "gourd"->"valet" substitution: a typo on the game's own side
        // between the two enums (found by comparing all 58 pairs one by
        // one, cf. big-walk-archipelago-notes.md, session on 2026-09-07).
        // Without this table, TryGetHomeName silently fails for these 3
        // gourds (Enum.TryParse doesn't find the generated name) and
        // ItemApplier ignores the corresponding item.
        private static readonly Dictionary<SaveablePropName, SaveableHomeName> GourdHomeNameExceptions = new()
        {
            { SaveablePropName.gourdCenturonSong, SaveableHomeName.valetCenturionSong },
            { SaveablePropName.gourdSingerAndSelecter, SaveableHomeName.valetSingerAndSelector },
            { SaveablePropName.gourdDancerAndSelecter, SaveableHomeName.valetDancerAndSelector },
        };

        internal static bool TryGetLocationId(SaveablePropName propName, out string locationId)
        {
            return LocationIdsByProp.TryGetValue(propName, out locationId);
        }

        // Distinguishes the two item-reception paths in ItemApplier
        // (ApplyBigKeyItem, 1:1, vs ApplyGourdItem, generic): the table of
        // 7 big keys above is already the source of truth for "which
        // SaveablePropName values are big keys", no need to duplicate it.
        internal static bool IsBigKey(SaveablePropName propName)
        {
            return BigKeyHomesByProp.ContainsKey(propName);
        }

        // Naming convention confirmed in-game (cf.
        // big-walk-archipelago-notes.md): gourdXxx (SaveablePropName, the
        // reward prop) corresponds 1:1 to valetXxx (SaveableHomeName, its
        // storage/basket slot). Centralized here (rather than duplicated in
        // each caller) since it's used both by debug tooling
        // (DebugGourdUnlocker) and by materializing a received item
        // (ItemApplier).
        internal static bool TryGetHomeName(SaveablePropName propName, out SaveableHomeName homeName)
        {
            if (BigKeyHomesByProp.TryGetValue(propName, out homeName))
                return true;

            if (GourdHomeNameExceptions.TryGetValue(propName, out homeName))
                return true;

            var name = propName.ToString();
            if (name.StartsWith("gourd", StringComparison.Ordinal))
                return Enum.TryParse("valet" + name.Substring("gourd".Length), out homeName);

            homeName = default;
            return false;
        }

        private static Dictionary<SaveablePropName, string> BuildLocationIds()
        {
            var map = new Dictionary<SaveablePropName, string>();

            foreach (SaveablePropName propName in Enum.GetValues(typeof(SaveablePropName)))
            {
                if (propName == SaveablePropName.notSavable)
                    continue;

                // Excludes gourdTesting00-39 as well as bigKeyTesting0-4 /
                // bigKeyTestingOverflow (the same family of dev-only values
                // the game never triggers in normal play): none of these
                // values is a real check.
                if (propName.ToString().Contains("Testing", StringComparison.OrdinalIgnoreCase))
                    continue;

                // The only gourd-prefixed value with no matching valetXxx
                // home, never observed in normal play, purpose unknown. The
                // Archipelago world leaves it out (see apworld/protocol.md
                // §3), so reporting it would send an id the server has no
                // location for — a protocol error rather than a lost check.
                if (propName == SaveablePropName.gourdSecretZoneVice)
                    continue;

                map[propName] = propName.ToString();
            }

            return map;
        }
    }
}
