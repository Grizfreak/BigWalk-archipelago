using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Tile Thief made easier (`tile_thief`, player's wish 2026-10-05): a button beside the puzzle
    // that puts new peg tiles down in front of it (PegTileCloner, copies shared with every player).
    //
    //  - easy: the tiles the validator expects, one per slot (its generator's `sequence`), the one
    //    its board does not show as a speaker, since that one is heard.
    //  - chaos: one tile of every kind of each slot's set, speakers for the heard slot's, so the
    //    answer still has to be worked out.
    //
    // Nothing of the island is taken (moving its tiles could rob another puzzle). Each press
    // replaces the tiles of the last one. With `lock_puzzle_needs`, the button does nothing until
    // the parts Tile Thief is built from have arrived: the tiles are those parts.
    internal static class TileThiefHelper
    {
        private const string PuzzlePath = "/TileThief/";
        private static readonly Vector3 ButtonPoint = new Vector3(658.53f, 20.16f, -157.20f);
        private static readonly Vector3 ButtonNormal = new Vector3(0f, 0f, 1f);
        private const int ButtonSlot = 26;
        private const float Ahead = 1.0f;
        private const float Scatter = 0.6f;

        private static string _mode = "vanilla";
        private static string _mirroredMode;

        internal static void Configure(string mode)
        {
            _mode = string.IsNullOrEmpty(mode) ? "vanilla" : mode;
            Plugin.Log.LogInfo($"[{nameof(TileThiefHelper)}] Tile Thief: {_mode}.");
        }

        // For the snapshot: 0 vanilla, 1 easy, 2 chaos.
        internal static int HostMode => _mode == "easy" ? 1 : _mode == "chaos" ? 2 : 0;

        internal static void ApplyFromHost(int mode) => _mirroredMode = mode == 1 ? "easy" : mode == 2 ? "chaos" : "vanilla";

        internal static void ForgetMirror() => _mirroredMode = null;

        private static string Mode => NetworkClient.active && !NetworkServer.active ? _mirroredMode ?? "vanilla" : _mode;

        // Every couple of seconds (TeleportButtonRunner).
        internal static void Sync()
        {
            if (Mode == "vanilla")
            {
                WorldButtons.Remove(ButtonSlot);
                return;
            }

            if (WorldButtons.Has(ButtonSlot))
                return;

            WorldButtons.Add(ButtonSlot, "Tile Thief tiles", ButtonPoint - ButtonNormal * 0.25f,
                Quaternion.LookRotation(ButtonNormal, Vector3.up), presser => Fetch(),
                icon: null, tint: Mode == "chaos" ? "black" : "hub", hostSide: true);
        }

        // Tile Thief's needs (apworld data.PUZZLE_NEEDS): its tiles are icon, drawing, pose and
        // sound panels.
        private static readonly string[] Needs = { "icon_panels", "drawing_panels", "pose_panels", "sound_panels" };

        // The tiles of the last press, so the next one replaces them.
        private static readonly List<Prop> Spawned = new();

        // Runs on the host, whoever pressed.
        private static void Fetch()
        {
            if (!NetworkServer.active)
                return;

            foreach (var need in Needs)
            {
                if (PuzzleNeeds.IsLocked(need))
                {
                    Plugin.Log.LogInfo($"[{nameof(TileThiefHelper)}] Tile Thief's tiles are still locked ({need}).");
                    ApNotices.Post("Tile Thief's tiles have not all arrived yet", warning: true);
                    return;
                }
            }

            var validator = FindValidator();
            if (validator == null)
            {
                Plugin.Log.LogInfo($"[{nameof(TileThiefHelper)}] Tile Thief's validator is not loaded.");
                return;
            }

            foreach (var old in Spawned)
            {
                if (old != null)
                    NetworkServer.Destroy(old.gameObject);
            }

            Spawned.Clear();

            var wanted = Mode == "chaos" ? EveryKind(validator) : Expected(validator);
            var near = ButtonPoint + ButtonNormal * Ahead;
            var used = new Dictionary<(PropGroup, bool), int>();
            foreach (var (kind, speaker) in wanted)
            {
                used.TryGetValue((kind, speaker), out var slot);
                while (slot < PegTileCloner.Slots && !PegTileCloner.SlotIsFree(kind, speaker, slot))
                    slot++;

                if (slot >= PegTileCloner.Slots)
                    continue;

                used[(kind, speaker)] = slot + 1;
                var at = near + new Vector3(UnityEngine.Random.Range(-Scatter, Scatter), 0.3f,
                    UnityEngine.Random.Range(-Scatter, Scatter));
                var tile = PegTileCloner.Spawn(kind, speaker, slot, at);
                if (tile != null)
                    Spawned.Add(tile);
            }

            Plugin.Log.LogInfo($"[{nameof(TileThiefHelper)}] {Mode}: {Spawned.Count} tile(s) made of {wanted.Count} wanted.");
        }

        private static PegTileValidator FindValidator()
        {
            foreach (var validator in UnityEngine.Object.FindObjectsByType<PegTileValidator>(FindObjectsSortMode.None))
            {
                if (validator == null)
                    continue;

                var path = string.Empty;
                for (var t = validator.transform; t != null; t = t.parent)
                    path = "/" + t.name + path;

                if (path.Contains(PuzzlePath, StringComparison.Ordinal))
                    return validator;
            }

            return null;
        }

        // The slot that is heard rather than seen: Tile Thief's board shows all but one of the tiles
        // it expects, and the missing one is played by a speaker tile (player, 2026-10-05). It is
        // the expected kind the board's display does not draw.
        private static int SoundSlot(List<PropGroup> sequence)
        {
            var shown = new List<PropGroup>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<PegTileRenderer>(FindObjectsSortMode.None))
            {
                if (renderer == null)
                    continue;

                var path = string.Empty;
                for (var t = renderer.transform; t != null; t = t.parent)
                    path = "/" + t.name + path;

                if (path.Contains(PuzzlePath, StringComparison.Ordinal) && path.Contains("MasterSequence", StringComparison.Ordinal))
                    shown.Add(renderer.propGroup);
            }

            for (var i = 0; i < sequence.Count; i++)
            {
                if (!shown.Contains(sequence[i]))
                    return i;
            }

            return -1;
        }

        private static List<PropGroup> Sequence(PegTileValidator validator)
        {
            var kinds = new List<PropGroup>();
            var generator = validator.sequenceGenerator;
            if (generator != null && generator.sequence != null)
            {
                foreach (var kind in generator.sequence)
                    kinds.Add(kind);
            }

            return kinds;
        }

        // easy: the expected tiles, the heard one as a speaker.
        private static List<(PropGroup, bool)> Expected(PegTileValidator validator)
        {
            var sequence = Sequence(validator);
            var sound = SoundSlot(sequence);
            var tiles = new List<(PropGroup, bool)>();
            for (var i = 0; i < sequence.Count; i++)
                tiles.Add((sequence[i], i == sound));
            return tiles;
        }

        // chaos: every kind of every slot's set, as speakers for the heard slot's set.
        private static List<(PropGroup, bool)> EveryKind(PegTileValidator validator)
        {
            var generator = validator.sequenceGenerator;
            if (generator == null || generator.pegTileDataSet == null || generator.setsPerSlot == null)
                return Expected(validator);

            var sound = SoundSlot(Sequence(validator));
            var tiles = new List<(PropGroup, bool)>();
            var slot = 0;
            foreach (var set in generator.setsPerSlot)
            {
                var speaker = slot++ == sound;
                var collection = generator.pegTileDataSet.GetCollection(set);
                if (collection.propGroups == null)
                    continue;

                foreach (var kind in collection.propGroups)
                {
                    if (!tiles.Contains((kind, speaker)))
                        tiles.Add((kind, speaker));
                }
            }

            return tiles;
        }
    }
}
