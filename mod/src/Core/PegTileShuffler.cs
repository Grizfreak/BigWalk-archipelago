using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The island's peg tiles swapped among their own places, per seed, in every Archipelago game
    // (player's wish 2026-10-05: "they are always laid out the same"; no option since: "it has to
    // be native"). Never in a save hosted without Archipelago (ApRuntime.ShufflesPegTiles).
    //
    //  - Within one set of tiles only: the rack or the set the scene lays them out in
    //    (PegTileRack_SetA, PegTileSetInvisibleInk...), and within one family of symbols (A, B,
    //    Priest A...). It used to be the whole landmark (the first two levels of the scene path),
    //    and LandmarksPlayerCount2/Contents is one landmark for the Silent Gauntlet, the Ring Room,
    //    the Charades and Semaphore rooms and the Trap Room: 452 tiles of five puzzles in one draw
    //    (players, 2026-10-15: tiles strewn about, duplicated in one puzzle and missing in another,
    //    invisible ink in a plain puzzle and plain tiles in the ink one). A rack belongs to one
    //    puzzle, holds one kind of tile, ink or not, and one puzzle need.
    //  - Each tile takes another's spot: its home if it is pinned to one, else its place (most
    //    tiles simply lie there; each keeps its own turn). The draw is fixed by the seed and the set.
    //  - A set is drawn a few seconds after the world is ready, wherever the players are (the player,
    //    2026-10-20: drawn when one came near, the tiles changed in front of them). A tile lying loose far from
    //    anyone has no ground loaded under it and would fall through (the strewn tiles of 2026-10-15), so it is
    //    held still (kinematic) after it is placed, and let go once a player is within NearMetres of it.
    //  - Every world load, the same result. A reload put the tiles back where the scene has them
    //    (players, 2026-10-15: "reloading fixed it"), and a key in the save used to stop the
    //    shuffle there. The draw now gives each tile, in a fixed order, a spot in a fixed order
    //    (the spots by their place in the set, which a swap does not change), so it lands on the
    //    same layout from the scene's places and leaves an already shuffled layout as it is.
    //  - Left alone: the tiles in a puzzle's slots (PegSlot...), held ones, the mod's own.
    //  - The host moves them: a home is networked (ServerSetPinned), and so is a loose prop's place.
    internal static class PegTileShuffler
    {
        private const string Tag = "[" + nameof(PegTileShuffler) + "]";

        private static bool _enabled;
        private static string _seed = string.Empty;
        private static float _at = -1f;
        private static float _nextScan;

        // Sets already drawn in this world, by key, and the tiles they moved: a tile set loose may
        // leave its rack in the scene, and must not be drawn a second time from wherever it went.
        private static readonly HashSet<string> _done = new();
        private static readonly HashSet<int> _moved = new();

        // A few seconds after the world is ready, so its tiles have been pinned by the game first.
        private const float Delay = 5f;

        // How often the tiles are looked for again: a far part of the island comes in later.
        private const float ScanSeconds = 10f;

        // How near a player must be for a set to be drawn: close enough for its ground to be
        // there, far enough for the swaps to happen out of sight (60 m: the player saw the tiles change in front of
        // them, 2026-10-20).
        private const float NearMetres = 150f;

        internal static void Configure(string seed)
        {
            // A reconnection to the same seed changes nothing.
            if ((seed ?? string.Empty) != _seed)
            {
                _done.Clear();
                _moved.Clear();
            }

            _enabled = true;
            _seed = seed ?? string.Empty;
        }

        // Every couple of seconds (TeleportButtonRunner).
        internal static void Tick()
        {
            if (!_enabled || !Net.ApRuntime.ShufflesPegTiles || !NetworkServer.active)
                return;

            if (_at < 0f)
            {
                _at = Time.unscaledTime + Delay;
                return;
            }

            if (Time.unscaledTime < _at || Time.unscaledTime < _nextScan)
                return;

            _nextScan = Time.unscaledTime + ScanSeconds;
            Shuffle();
            ReleaseNear();
        }

        // A world went away: the next one is shuffled again, from its own starting places.
        internal static void ForgetWorld()
        {
            _done.Clear();
            _moved.Clear();
            Frozen.Clear();
            _at = -1f;
            _nextScan = 0f;
        }

        // Where a tile is: the home it is pinned to, or, for a tile simply lying there (most of
        // them: the first version swapped homes and found none, 2026-10-05), its place.
        private struct Spot
        {
            internal PropHome Home;
            internal Vector3 Position;
            internal Vector3 Local;
        }

        private sealed class Set
        {
            internal Transform Root;
            internal readonly List<Prop> Tiles = new();
        }

        private static void Shuffle()
        {
            var sets = new Dictionary<string, Set>();
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsSortMode.None))
            {
                if (prop == null || prop.propGroups == null || !prop.propGroups.Contains(PropGroup.PegTile)
                    || prop.name.Contains("(AP", StringComparison.Ordinal) || _moved.Contains(prop.GetInstanceID())
                    || IsHeld(prop))
                    continue;

                // A tile in a puzzle's own slot is that puzzle's display or answer: left alone.
                // Nor one stowed in something carried (a backpack, a carton).
                var home = prop.currentHome;
                if (home != null && (home.name.StartsWith("PegSlot", StringComparison.Ordinal)
                                     || home.GetComponentInParent<Prop>() != null))
                    continue;

                var root = SetRoot(prop.transform);
                if (root == null)
                    continue;

                var key = Path(root) + "|" + FamilyOf(prop) + (prop.name.Contains("Speaker", StringComparison.Ordinal) ? "|speaker" : string.Empty);
                if (_done.Contains(key))
                    continue;

                if (!sets.TryGetValue(key, out var set))
                    sets[key] = set = new Set { Root = root };
                set.Tiles.Add(prop);
            }

            foreach (var pair in sets)
            {
                if (pair.Value.Tiles.Count < 2)
                {
                    _done.Add(pair.Key);
                    continue;
                }

                _done.Add(pair.Key);
                Draw(pair.Key, pair.Value);
            }
        }

        private static void Draw(string key, Set set)
        {
            var tiles = set.Tiles;
            tiles.Sort((a, b) => string.CompareOrdinal(IdOf(a), IdOf(b)));

            var spots = new List<Spot>();
            foreach (var tile in tiles)
            {
                spots.Add(new Spot
                {
                    Home = tile.currentHome,
                    Position = tile.transform.position,
                    Local = set.Root.InverseTransformPoint(tile.transform.position),
                });
            }

            // The spots in an order that does not depend on which tile stands on each.
            spots.Sort(CompareSpots);

            var random = new System.Random(StableHash(_seed + "|" + key));
            for (var i = spots.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (spots[i], spots[j]) = (spots[j], spots[i]);
            }

            // Out of every home first, so no home is asked to hold two.
            foreach (var tile in tiles)
            {
                _moved.Add(tile.GetInstanceID());
                if (tile.currentHome != null)
                    tile.ServerSetUnpinned();
            }

            var moved = 0;
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

                        // Held still until a player is near enough for the ground to be there (ReleaseNear).
                        if (tile.rb != null)
                        {
                            tile.rb.isKinematic = true;
                            Frozen.Add(tile);
                        }
                    }

                    // A tile hidden by its puzzle need has no collider: set loose, it would fall through the
                    // ground before the hider holds it again. Held until shown (PuzzleNeedHider.SoftShow).
                    if (PuzzleNeedHider.IsSoftHidden(tile.gameObject) && tile.rb != null)
                        tile.rb.isKinematic = true;

                    moved++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} '{tiles[i].name}' could not be placed: {ex.Message}");
                }
            }

            Plugin.Log.LogInfo($"{Tag} {moved} tile(s) swapped about in {key}.");
        }

        // The rack or set a tile is laid out in: its nearest parent named PegTileRack... or PegTileSet...,
        // else its own parent (a tile alone in a room's dressing).
        private static Transform SetRoot(Transform tile)
        {
            for (var parent = tile.parent; parent != null; parent = parent.parent)
            {
                var name = parent.name;
                if (name.StartsWith("PegTileRack", StringComparison.Ordinal)
                    || name.StartsWith("PegTileSet", StringComparison.Ordinal))
                    return parent;
            }

            return tile.parent;
        }

        // The symbols' family (PegTileA, PegTilePriestB...): a tile only ever swaps with its own kind.
        private static string FamilyOf(Prop prop)
        {
            foreach (var group in prop.propGroups)
            {
                if (group != PropGroup.PegTile && group.ToString().StartsWith("PegTile", StringComparison.Ordinal))
                    return PegTileCloner.Family(group);
            }

            return "PegTile";
        }

        private static string IdOf(Prop prop)
        {
            return string.IsNullOrEmpty(prop.savablePropGuid) ? Path(prop.transform) : prop.savablePropGuid;
        }

        // By the home's place in the scene, else by the spot's place in the set, to the decimetre.
        private static int CompareSpots(Spot a, Spot b)
        {
            if (a.Home != null || b.Home != null)
            {
                if (a.Home == null)
                    return 1;
                if (b.Home == null)
                    return -1;
                return string.CompareOrdinal(Path(a.Home.transform), Path(b.Home.transform));
            }

            var byX = Mathf.RoundToInt(a.Local.x * 10f).CompareTo(Mathf.RoundToInt(b.Local.x * 10f));
            if (byX != 0)
                return byX;
            var byZ = Mathf.RoundToInt(a.Local.z * 10f).CompareTo(Mathf.RoundToInt(b.Local.z * 10f));
            if (byZ != 0)
                return byZ;
            return Mathf.RoundToInt(a.Local.y * 10f).CompareTo(Mathf.RoundToInt(b.Local.y * 10f));
        }

        // The tiles held still, by what Draw placed loose.
        private static readonly List<Prop> Frozen = new();

        private static void ReleaseNear()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null || Frozen.Count == 0)
                return;

            for (var i = Frozen.Count - 1; i >= 0; i--)
            {
                var tile = Frozen[i];
                if (tile == null)
                {
                    Frozen.RemoveAt(i);
                    continue;
                }

                var near = false;
                foreach (var pc in players)
                {
                    if (pc != null && (pc.transform.position - tile.transform.position).sqrMagnitude < NearMetres * NearMetres)
                    {
                        near = true;
                        break;
                    }
                }

                if (!near)
                    continue;

                Frozen.RemoveAt(i);
                // A tile its puzzle need hides stays held (PuzzleNeedHider.SoftShow lets it go).
                if (tile.rb != null && !PuzzleNeedHider.IsSoftHidden(tile.gameObject))
                    tile.rb.isKinematic = false;
            }
        }

        private static bool PlayerNear(List<Prop> tiles)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return false;

            var centre = Vector3.zero;
            foreach (var tile in tiles)
                centre += tile.transform.position;
            centre /= tiles.Count;

            foreach (var pc in players)
            {
                if (pc != null && (pc.transform.position - centre).sqrMagnitude < NearMetres * NearMetres)
                    return true;
            }

            return false;
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
