using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The seed's colours (ROADMAP C1, `random_colours`, player 2026-10-06/07). The mod holds ten
    // colours; the apworld draws between five and ten of them for the seed (`colour_palette`,
    // indices into Colours, in a drawn order), and the buoys and the flare guns are painted from
    // that draw (Core/ColourPainter). The host has it from the slot; a guest from the host, in
    // the snapshot (ModChannel), so every screen paints the same.
    //
    // Each colour carries two tones, tuned by eye on 2026-10-06 (Debug: Ctrl+F6): the buoy's body
    // multiplies its warm painted vertex colours by the tint (househouse/VertexColors,
    // _TintColor), so a cool colour needs more blue than it shows; the halo, the light and a
    // flare's shot take the colour as it is.
    internal static class Palette
    {
        private const string Tag = "[" + nameof(Palette) + "]";

        internal static readonly (string name, Color body, Color halo)[] Colours =
        {
            ("Red",    new Color(1.00f, 0.15f, 0.15f), new Color(1.00f, 0.10f, 0.05f)),
            ("Orange", new Color(1.00f, 0.50f, 0.20f), new Color(1.00f, 0.40f, 0.00f)),
            ("Yellow", new Color(1.00f, 0.95f, 0.30f), new Color(1.00f, 0.85f, 0.10f)),
            ("Lime",   new Color(0.60f, 1.00f, 0.25f), new Color(0.55f, 1.00f, 0.10f)),
            ("Green",  new Color(0.15f, 0.85f, 0.35f), new Color(0.05f, 0.90f, 0.30f)),
            ("Cyan",   new Color(0.00f, 0.85f, 1.60f), new Color(0.00f, 0.85f, 1.00f)),
            ("Blue",   new Color(0.15f, 0.35f, 1.60f), new Color(0.10f, 0.30f, 1.00f)),
            ("Purple", new Color(0.55f, 0.20f, 1.40f), new Color(0.55f, 0.15f, 1.00f)),
            ("Pink",   new Color(1.00f, 0.30f, 0.85f), new Color(1.00f, 0.25f, 0.75f)),
            ("White",  new Color(1.00f, 1.15f, 1.40f), new Color(1.00f, 1.00f, 1.00f)),
        };

        // The rainbow gun's body (white, a touch warm, so it reads as painted and not lit).
        internal static readonly Color RainbowBody = new(0.95f, 0.95f, 0.92f);

        private static int[] _seed = Array.Empty<int>();
        private static int[] _mirrored;

        internal static void Configure(ApSlotData slot)
        {
            var drawn = new List<int>();
            if (slot.RandomColours)
            {
                foreach (var index in slot.ColourPalette)
                {
                    if (index >= 0 && index < Colours.Length && !drawn.Contains(index))
                        drawn.Add(index);
                }
            }

            _seed = drawn.ToArray();
            Plugin.Log.LogInfo(_seed.Length == 0
                ? $"{Tag} The game's own colours."
                : $"{Tag} The seed's colours: {string.Join(", ", Array.ConvertAll(_seed, i => Colours[i].name))}.");
            ColourPainter.Repaint();
        }

        internal static void Forget()
        {
            _seed = Array.Empty<int>();
        }

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        // The colours in force on this machine: the seed's on the host, the host's on a guest.
        internal static int[] Current => IsGuest ? _mirrored ?? Array.Empty<int>() : _seed;

        internal static bool Active => Current.Length > 0;

        internal static int[] HostColours => _seed;

        internal static void ApplyFromHost(List<byte> indices)
        {
            var valid = new List<int>();
            foreach (var index in indices)
            {
                if (index < Colours.Length)
                    valid.Add(index);
            }

            var changed = _mirrored == null || !SameAs(_mirrored, valid);
            _mirrored = valid.ToArray();
            if (changed)
                ColourPainter.Repaint();
        }

        internal static void ForgetMirror()
        {
            _mirrored = null;
        }

        private static bool SameAs(int[] a, List<int> b)
        {
            if (a.Length != b.Count)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        // The colour at a position of the seed's draw, wrapping round.
        internal static (string name, Color body, Color halo) At(int position)
        {
            var current = Current;
            return Colours[current[((position % current.Length) + current.Length) % current.Length]];
        }

        // A colour for something known by a stable key (a buoy's guid): the same on every machine.
        internal static (string name, Color body, Color halo) For(string key)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in key ?? string.Empty)
                    hash = (hash ^ c) * 16777619u;
                return At((int)(hash & int.MaxValue));
            }
        }

        // The rainbow gun's colour now: Mirror's network clock, which every machine shares, so a
        // shot looks the same everywhere (but at the very turn of a step). Fast and out of order
        // (player, 2026-10-07: "more chaotic"), never the same twice in a row. The whole palette,
        // the seed's draw or not.
        internal const double RainbowStep = 0.4;

        internal static Color RainbowNow()
        {
            var step = (long)Math.Floor(NetworkTime.time / RainbowStep);
            int Pick(long n)
            {
                unchecked
                {
                    var h = (ulong)n * 0x9E3779B97F4A7C15UL;
                    h ^= h >> 29;
                    return (int)(h % (ulong)Colours.Length);
                }
            }

            var index = Pick(step);
            if (index == Pick(step - 1))
                index = (index + 1 + Pick(step + 7919) % (Colours.Length - 1)) % Colours.Length;
            return Colours[index].halo;
        }

        // An item's name in the feed: the island's flare guns go by the colour they are painted.
        internal static string Display(string itemName)
        {
            if (!Active)
                return itemName;
            var slot = Array.IndexOf(FlareGunItemNames, itemName);
            return slot < 0 ? itemName : $"{At(slot).name} Flare Gun";
        }

        // The four flare guns' items in the apworld's order, which is also the order of their
        // colours in the seed's draw (Core/ColourPainter: FlareGunSlot).
        internal static readonly string[] FlareGunItemNames = { "Flare Gun", "Blue Flare Gun", "Green Flare Gun", "Yellow Flare Gun" };
    }
}
