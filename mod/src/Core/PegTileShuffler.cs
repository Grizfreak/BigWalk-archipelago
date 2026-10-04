using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The island's peg tiles swapped among their own places, per seed (`shuffle_peg_tiles`,
    // player's wish 2026-10-05: "they are always laid out the same").
    //
    //  - Within a place only: tiles swap among the homes of the same landmark (the first two
    //    levels of the scene path, LandmarksPlayerCountAny/TileThief...), never across the island,
    //    so no tile ends up behind a door the logic does not expect it behind.
    //  - Each tile takes another's spot: its home if it is pinned to one, else its place (most
    //    tiles simply lie there; each keeps its own turn). The draw is fixed by the seed and the place.
    //  - Once per save and seed (a key in the save): whether the game keeps a moved tile's place
    //    across a load is to be seen, and shuffling an already shuffled layout would never settle.
    //  - Left alone: the tiles in a puzzle's slots (PegSlot...), held ones, the mod's own.
    //  - The host moves them: a home is networked (ServerSetPinned), and so is a loose prop's place.
    internal static class PegTileShuffler
    {
        private static bool _enabled;
        private static string _seed = string.Empty;
        private static bool _doneThisWorld;
        private static float _at = -1f;

        // A few seconds after the world is ready, so its tiles have been pinned by the game first.
        private const float Delay = 5f;

        internal static void Configure(bool enabled, string seed)
        {
            // Once per world: a second pass would only see the tiles still at their own place and
            // shuffle those again. A reconnection to the same seed changes nothing.
            if ((seed ?? string.Empty) != _seed)
                _doneThisWorld = false;

            _enabled = enabled;
            _seed = seed ?? string.Empty;
            Plugin.Log.LogInfo($"[{nameof(PegTileShuffler)}] Peg tiles {(enabled ? "shuffled on their stands" : "as in the game")}.");
        }

        // Every couple of seconds (TeleportButtonRunner).
        internal static void Tick()
        {
            if (!_enabled || _doneThisWorld || !NetworkServer.active)
                return;

            if (_at < 0f)
            {
                _at = Time.unscaledTime + Delay;
                return;
            }

            if (Time.unscaledTime < _at)
                return;

            _doneThisWorld = true;
            Shuffle();
        }

        // A world went away: the next one is shuffled again, from its own starting places.
        internal static void ForgetWorld()
        {
            _doneThisWorld = false;
            _at = -1f;
        }

        // Where a tile is: the home it is pinned to, or, for a tile simply lying there (most of
        // them: the first version swapped homes and found none, 2026-10-05), its place.
        private struct Spot
        {
            internal PropHome Home;
            internal Vector3 Position;
        }

        private static void Shuffle()
        {
            // Once per save: whether the game keeps a moved tile's place across a load is not
            // known, and shuffling again from an already shuffled layout would never settle.
            var doneKey = "ap_tiles_shuffled_" + StableHash(_seed);
            if (SaveManager.GetIntValue(doneKey, 0, false) != 0)
            {
                Plugin.Log.LogInfo($"[{nameof(PegTileShuffler)}] Tiles already shuffled for this seed in this save.");
                return;
            }

            var groups = new Dictionary<string, List<Prop>>();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsSortMode.None))
            {
                if (prop == null || prop.propGroups == null || !prop.propGroups.Contains(PropGroup.PegTile)
                    || prop.name.Contains("(AP", StringComparison.Ordinal) || IsHeld(prop))
                    continue;

                // A tile in a puzzle's own slot is that puzzle's display or answer: left alone.
                // Nor one stowed in something carried (a backpack, a carton).
                var home = prop.currentHome;
                if (home != null && (home.name.StartsWith("PegSlot", StringComparison.Ordinal)
                                     || home.GetComponentInParent<Prop>() != null))
                    continue;

                var place = Place(prop.transform);
                if (!groups.TryGetValue(place, out var tiles))
                    groups[place] = tiles = new List<Prop>();
                tiles.Add(prop);
            }

            var moved = 0;
            var places = 0;
            foreach (var pair in groups)
            {
                var tiles = pair.Value;
                if (tiles.Count < 2)
                    continue;

                places++;
                tiles.Sort((a, b) => string.CompareOrdinal(Path(a.transform), Path(b.transform)));
                var spots = new List<Spot>();
                foreach (var tile in tiles)
                    spots.Add(new Spot { Home = tile.currentHome, Position = tile.transform.position });

                var random = new System.Random(StableHash(_seed + "|" + pair.Key));
                for (var i = spots.Count - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (spots[i], spots[j]) = (spots[j], spots[i]);
                }

                // Out of every home first, so no home is asked to hold two.
                foreach (var tile in tiles)
                {
                    if (tile.currentHome != null)
                        tile.ServerSetUnpinned();
                }

                for (var i = 0; i < tiles.Count; i++)
                {
                    try
                    {
                        var tile = tiles[i];
                        var spot = spots[i];
                        if (spot.Home != null)
                        {
                            tile.ServerSetPinned(spot.Home);
                        }
                        else
                        {
                            // The place only: each tile keeps its own turn (player, 2026-10-05).
                            tile.SetLoose();
                            tile.transform.position = spot.Position;
                        }

                        moved++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"[{nameof(PegTileShuffler)}] '{tiles[i].name}' could not be placed: {ex.Message}");
                    }
                }
            }

            SaveManager.SetIntValue(doneKey, 1);
            Plugin.Log.LogInfo($"[{nameof(PegTileShuffler)}] {moved} tile(s) swapped about, in {places} place(s).");
        }

        private static bool IsHeld(Prop prop)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return false;

            foreach (var pc in players)
            {
                if (pc != null && pc.hands != null && pc.hands.heldProp == prop)
                    return true;
            }

            return false;
        }

        // "LandmarksPlayerCountAny/TileThief/Positioner/..." -> "LandmarksPlayerCountAny/TileThief".
        private static string Place(Transform t)
        {
            var path = Path(t).Split('/');
            return path.Length >= 2 ? path[0] + "/" + path[1] : path[0];
        }

        private static string Path(Transform t)
        {
            var path = t.name;
            for (var parent = t.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        // The same number on every run and every machine (string.GetHashCode is not).
        private static int StableHash(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in text)
                    hash = (hash ^ c) * 16777619;
                return hash;
            }
        }
    }
}
