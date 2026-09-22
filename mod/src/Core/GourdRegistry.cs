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

        // Cut from the Archipelago world on 2026-09-21; see BuildLocationIds.
        private static readonly HashSet<SaveablePropName> AbsentFromTheBuild = new()
        {
            SaveablePropName.gourdBunker,
            SaveablePropName.gourdHighPegBoard,
            SaveablePropName.gourdFirstPegBoard,
            SaveablePropName.gourdMagiciansTrick,
            SaveablePropName.gourdButtonBoothChallenge,
            SaveablePropName.gourdTileSoup,
            SaveablePropName.gourdPanopticon,
            SaveablePropName.gourdMaypole,
            SaveablePropName.gourdBlindfoldCircus,
            SaveablePropName.gourdMessengerRun,
            SaveablePropName.gourdHotPotato,
            SaveablePropName.gourdScoutTiles,
            SaveablePropName.gourdScoutCounting,
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

        // `PropHome.GetSaveableHome` THROWS when there is no world loaded —
        // it does not come back null. Found on 2026-09-21 the same way
        // `FmRadioManager.instance` was: a NullReferenceException raised
        // inside IL2CPP, crossing the managed trampoline out of
        // `ApRuntime.OnJustConnected` and killing the rest of that method.
        // Connecting before a world is loaded is the NORMAL case, not an
        // edge one, so nothing in this mod may reach that method except
        // through here.
        internal static PropHome TryGetHome(SaveableHomeName homeName)
        {
            try
            {
                return PropHome.GetSaveableHome(homeName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Same, from the prop's end: resolves the key's plinth, or null if
        // there is no world yet or no plinth maps to it.
        internal static PropHome TryGetHomeFor(SaveablePropName propName)
        {
            return TryGetHomeName(propName, out var homeName) ? TryGetHome(homeName) : null;
        }

        // Whether this home is one of the seven big-key plinths.
        //
        // Needed because a big key has TWO homes in play: the plinth it is
        // carried to, and the `notSavable` one it sits in at the foot of its
        // tower — and `Prop.Start()` pins it into the latter on every world
        // load. Anything keyed on "a big key is being pinned" therefore
        // fires seven times at load unless it also asks WHERE.
        internal static bool IsBigKeyPlinth(SaveableHomeName homeName)
        {
            return BigKeyHomesByProp.ContainsValue(homeName);
        }

        // `Prop.allProps` THROWS when no world is loaded, exactly as
        // `PropHome.GetSaveableHome` and `FmRadioManager.instance` do. Caught
        // on 2026-09-21 coming out of `KeyCustody.RearmFromLedger`, which runs
        // at connection time — when there is usually no world yet. Three of
        // these now, so the rule is worth stating: in this game a static
        // accessor reached before its scene exists is a throw, not a null.
        internal static Prop FindProp(SaveablePropName propName)
        {
            try
            {
                foreach (var prop in Prop.allProps)
                {
                    if (prop != null && prop.saveablePropName == propName)
                        return prop;
                }
            }
            catch (Exception)
            {
                // No world: there is no prop to find, which is the same
                // answer as not finding one.
            }

            return null;
        }

        // Every big key currently in the world, or nothing at all when there
        // is no world. Same guard, for the callers that walk all seven.
        internal static IEnumerable<Prop> LoadedBigKeys()
        {
            var found = new List<Prop>();

            try
            {
                foreach (var prop in Prop.allProps)
                {
                    if (prop != null && IsBigKey(prop.saveablePropName))
                        found.Add(prop);
                }
            }
            catch (Exception)
            {
            }

            return found;
        }

        // The seven, in one place, for the code that has to walk all of them
        // rather than ask about one: Core/KeyFeatures.cs' ledger. Same single
        // source of truth as IsBigKey, asked a different way — a second list
        // of the same seven keys is how two lists drift apart.
        internal static IEnumerable<SaveablePropName> BigKeys => BigKeyHomesByProp.Keys;

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

                // The thirteen below are in the enum, each with its own valet,
                // and nothing in the shipped build produces them: a finished
                // save of 2026-08-23 holds 45 gourds filling every monument
                // slot in the game and none of these (the full case is in
                // apworld/bigwalk/data.py, ABSENT_FROM_THE_BUILD). They cannot
                // fire, so this filter changes no behaviour today — it is here
                // so the two halves exclude the same set, and so that a patch
                // shipping them is a deliberate edit on both sides rather than
                // an id the server has no location for.
                if (AbsentFromTheBuild.Contains(propName))
                    continue;

                map[propName] = propName.ToString();
            }

            return map;
        }
    }
}
