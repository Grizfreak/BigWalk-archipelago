using System;
using BigWalkArchipelago.Core.Net;
using Mirror;
using TMPro;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The resync stations (player's design, 2026-10-05): one in the hub and one in each of the five
    // towers. Each is a button, a toggle beside it and a sign above them.
    //
    //  - The button takes back every gourd, key and (when the toggle says so) gadget of the slot
    //    that is not where it counts, and puts them back in front of itself (ApRuntime.ResyncAll).
    //  - The toggle says whether the gadgets come too; the sign says what the button does, and
    //    changes with the toggle.
    //  - One resync at a time on the whole map: while one is running every button refuses.
    //  - Only the host can press them to any effect: the items live in its world. A guest's
    //    press is told so.
    //
    // This replaces Ctrl+R. Every machine builds the same stations; the host's toggles and
    // whether a resync is running reach the guests in the snapshot, for their signs.
    internal static class ResyncStations
    {
        private sealed class Station
        {
            internal string Name;
            internal string Tint;
            internal Vector3 Point;
            internal Vector3 Normal;

            // Where the sign stands, when it is not right above the button: the hub's is where the
            // player marked it (2026-10-05).
            internal Vector3? SignPoint;

            // How deep the button sits in the wall, and the sign: the towers' walls lean, so what
            // stands well on a straight one sticks out of a leaning one (player, 2026-10-05).
            internal float ButtonSink = 0.25f;
            internal float SignSink;

            // How deep the sign goes behind its face: deep where the wall leans away, thin where
            // the wall is thin (the Black Tower's, through which it showed outside).
            internal float SignDepth = 0.3f;

            // The switch under the button rather than beside it: in the towers, where the wall
            // beside the button curves away (player, 2026-10-05).
            internal bool SwitchBelow;
            internal float SwitchDrop = 0.45f;
            internal float SwitchSink = 0.02f;

            internal int ButtonSlot;
            internal int ToggleSlot;

            internal GameObject Sign;
            internal TextMeshPro Text;
            internal string ShownText;
            internal bool? BuiltToggleOn;
        }

        // Marks (Keypad 8, 2026-10-04): the hub's beside the map room, the others in each tower
        // beside its way back to the hub. The hub's sign was marked on 2026-10-05.
        private static readonly Station[] Stations =
        {
            Make("the hub", "hub", -211.81f, 33.09f, -526.06f, 0.66f, 0.75f, 12, new Vector3(-212.09f, 33.53f, -526.02f),
                switchSink: 0.10f),
            Make("the Red Funnel Tower", "red", -89.84f, 97.64f, -604.06f, 0.20f, -0.93f, 13, buttonSink: 0.2f, signSink: 0.22f, switchDrop: 0.32f),
            Make("the Green Cup Tower", "green", 449.78f, 113.32f, -451.33f, 0.72f, 0.67f, 14),
            Make("the Blue Castle Tower", "blue", 140.39f, 112.84f, -198.54f, -0.32f, -0.95f, 15, switchSink: 0.06f),
            Make("the Yellow Twist Tower", "yellow", -231.31f, 132.27f, -242.06f, 0.45f, 0.76f, 16, switchDrop: 0.32f, switchSink: -0.03f),
            Make("the Black Tower", "black", -28.48f, 118.34f, -282.17f, -0.64f, -0.77f, 17, signSink: 0.03f, signDepth: 0.06f),
        };

        private static Station Make(string name, string tint, float x, float y, float z, float nx, float nz, int slot,
            Vector3? sign = null, float buttonSink = 0.25f, float signSink = 0f, float switchDrop = 0.45f,
            float switchSink = 0.02f, float signDepth = 0.3f)
        {
            return new Station
            {
                Name = name, Tint = tint, Point = new Vector3(x, y, z), Normal = new Vector3(nx, 0f, nz).normalized,
                ButtonSlot = slot, ToggleSlot = slot + 8, SignPoint = sign,
                ButtonSink = buttonSink, SignSink = signSink, SwitchBelow = sign == null,
                SwitchDrop = switchDrop, SwitchSink = switchSink, SignDepth = signDepth,
            };
        }

        private const string GadgetsKeyPrefix = "ap_resync_gadgets_";
        private const float SwitchBeside = 0.5f;

        // A switch beside its button sits a little higher, so the two line up: the button's
        // copy stands on its root, the switch hangs a little below its own.
        private const float SwitchLift = 0.03f;
        private const float GatherAhead = 1.2f;
        private const float SignAbove = 0.8f;
        // The face of a sign; its depth is the station's.
        private static readonly Vector3 SignSize = new Vector3(1.3f, 0.5f, 0f);

        // On a guest: what the host last said (bits 0..5: gadgets per station; bit 6: running;
        // bit 7: the towers' stations are there).
        private static int _mirrored = -1;

        // From the slot (host): the towers' stations, and whether guests may use the stations.
        private static bool _towers;
        private static bool _guests;

        internal static void Configure(bool towers, bool guests)
        {
            _towers = towers;
            _guests = guests;
            Plugin.Log.LogInfo(
                $"[{nameof(ResyncStations)}] Stations in the towers {(towers ? "on" : "off")}, guests {(guests ? "may" : "may not")} use them.");
        }

        private static bool TowersOn => IsGuest ? _mirrored >= 0 && (_mirrored & (1 << 7)) != 0 : _towers;

        // For the snapshot.
        internal static byte HostState()
        {
            var state = 0;
            for (var i = 0; i < Stations.Length; i++)
            {
                if (GadgetsOn(i))
                    state |= 1 << i;
            }

            if (ApRuntime.ResyncRunning)
                state |= 1 << 6;

            if (_towers)
                state |= 1 << 7;

            return (byte)state;
        }

        // On a guest: the signs follow at once, not at the next sync.
        internal static void ApplyFromHost(byte state)
        {
            if (_mirrored == state)
                return;

            _mirrored = state;
            for (var i = 0; i < Stations.Length; i++)
                Write(Stations[i], i);
        }

        // On the host: what was last sent, to send a change at once.
        private static int _sent = -1;

        private static void SendIfChanged()
        {
            int state = HostState();
            if (state == _sent)
                return;

            _sent = state;
            ModChannel.SendSnapshotNow();
        }

        internal static void ForgetMirror() => _mirrored = -1;

        private static bool IsGuest => NetworkClient.active && !NetworkServer.active;

        private static bool GadgetsOn(int station)
        {
            if (IsGuest)
                return _mirrored < 0 || (_mirrored & (1 << station)) != 0;

            return SaveManager.GetIntValue(GadgetsKeyPrefix + station, 1, false) != 0;
        }

        private static bool Running => IsGuest ? _mirrored >= 0 && (_mirrored & (1 << 6)) != 0 : ApRuntime.ResyncRunning;

        // Every couple of seconds (TeleportButtonRunner).
        internal static void Sync(bool wanted)
        {
            if (NetworkServer.active)
                SendIfChanged();

            for (var i = 0; i < Stations.Length; i++)
            {
                var station = Stations[i];
                if (!wanted || (i > 0 && !TowersOn))
                {
                    Take(station);
                    continue;
                }

                Build(station, i);
                Write(station, i);
            }
        }

        private static void Take(Station station)
        {
            WorldButtons.Remove(station.ButtonSlot);
            WorldButtons.Remove(station.ToggleSlot);
            if (station.Sign != null)
                UnityEngine.Object.Destroy(station.Sign);

            station.Sign = null;
            station.Text = null;
            station.ShownText = null;
            station.BuiltToggleOn = null;
        }

        private static Vector3 Right(Station station) => new Vector3(-station.Normal.z, 0f, station.Normal.x);

        private static Vector3 SwitchPoint(Station station) => station.SwitchBelow
            ? station.Point - Vector3.up * station.SwitchDrop
            : station.Point + Right(station) * SwitchBeside + Vector3.up * SwitchLift;

        private static void Build(Station station, int index)
        {
            var facing = Quaternion.LookRotation(station.Normal, Vector3.up);
            if (!WorldButtons.Has(station.ButtonSlot))
            {
                // The hub's restocks where the game itself puts a new player's items, its
                // inventory spawn (player, 2026-10-05); a tower's in front of its button.
                Vector3? where = station.SignPoint != null ? null : station.Point + station.Normal * GatherAhead;
                WorldButtons.Add(station.ButtonSlot, "Resync in " + station.Name, station.Point - station.Normal * station.ButtonSink,
                    facing, presser => Press(index, where, presser), icon: null, tint: station.Tint, hostSide: true);
            }

            // The toggle is a copy of the game's light switch (player's choice).
            var on = GadgetsOn(index);
            if (!WorldButtons.Has(station.ToggleSlot))
            {
                WorldButtons.Add(station.ToggleSlot, "Gadgets in " + station.Name,
                    SwitchPoint(station) - station.Normal * station.SwitchSink, facing,
                    presser => Toggle(index, presser), template: WorldButtons.LightSwitch, hostSide: true);
                station.BuiltToggleOn = null;
            }

            // The host keeps the switch where its saved choice says (up with the gadgets); the
            // switch is networked, so the guests see it move.
            if (NetworkServer.active && station.BuiltToggleOn != on)
            {
                var state = WorldButtons.StateOf(station.ToggleSlot);
                if (state != null)
                {
                    if ((state.currentPeckContext.state >= 1) != on)
                        state.SetState(on ? 1 : 0);
                    station.BuiltToggleOn = on;
                }
            }

            if (station.Sign == null)
                MakeSign(station);
        }

        private static void MakeSign(Station station)
        {
            var point = station.SignPoint ?? station.Point + Vector3.up * SignAbove;
            var face = point + station.Normal * (0.02f - station.SignSink);
            var size = new Vector3(SignSize.x, SignSize.y, station.SignDepth);
            var sign = WorldButtons.MakeSlab("AP resync sign", face - station.Normal * (size.z / 2f),
                Quaternion.LookRotation(station.Normal, Vector3.up), size, new Color(0.93f, 0.9f, 0.82f));

            // The text faces the reader: a TextMeshPro reads from its back, so it looks into the wall.
            var holder = new GameObject("AP resync text");
            holder.transform.SetParent(sign.transform, false);
            holder.transform.position = face + station.Normal * 0.01f;
            holder.transform.rotation = Quaternion.LookRotation(-station.Normal, Vector3.up);
            holder.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);

            try
            {
                var text = holder.AddComponent<TextMeshPro>();
                var font = FindFont();
                if (font != null)
                    text.font = font;

                text.rectTransform.sizeDelta = new Vector2(SignSize.x - 0.1f, SignSize.y - 0.06f);
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.05f;
                text.fontSizeMax = 1.5f;
                text.color = new Color(0.12f, 0.12f, 0.14f);
                station.Text = text;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ResyncStations)}] No text on the sign in {station.Name}: {ex.Message}");
            }

            station.Sign = sign;
            station.ShownText = null;
        }

        private static TMP_FontAsset _font;

        // The game's own font, from any text it shows.
        private static TMP_FontAsset FindFont()
        {
            if (_font != null)
                return _font;

            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text != null && text.font != null)
                {
                    _font = text.font;
                    Plugin.Log.LogInfo($"[{nameof(ResyncStations)}] Signs use the font '{_font.name}'.");
                    break;
                }
            }

            return _font;
        }

        private static void Write(Station station, int index)
        {
            if (station.Text == null)
                return;

            var what = GadgetsOn(index) ? "gourds, gadgets and keys" : "gourds and keys";
            var text = Running
                ? "A resync is running.\nWait for it to finish."
                : $"Press this button to recover your {what} in here.\n<size=70%>Flip the switch {(GadgetsOn(index) ? "to leave the gadgets out" : "to bring the gadgets too")}.</size>";
            if (text == station.ShownText)
                return;

            station.Text.text = text;
            station.ShownText = text;
        }

        // The controls run on the host, whoever pressed them (hostSide): a guest's press is
        // refused there, and the guest is told so.
        private static bool Refused(PlayerCharacter presser, string why)
        {
            if (!NetworkServer.active)
                return true;

            if (presser == null || presser.isLocalPlayer || _guests)
                return false;

            ModChannel.SendNotice(presser.connectionToClient, why, warning: true);
            Plugin.Log.LogInfo($"[{nameof(ResyncStations)}] '{presser.name}' is not the host: {why}.");
            return true;
        }

        private static void Press(int index, Vector3? where, PlayerCharacter presser)
        {
            if (Refused(presser, "Only the host can resync"))
                return;

            if (ApRuntime.ResyncAll(where, GadgetsOn(index)))
                Plugin.Log.LogInfo($"[{nameof(ResyncStations)}] Resync in {Stations[index].Name}, gadgets {(GadgetsOn(index) ? "in" : "out")}.");

            WriteAll();
            SendIfChanged();
        }

        private static void Toggle(int index, PlayerCharacter presser)
        {
            if (Refused(presser, "Only the host can change this"))
            {
                // The guest's press flipped the switch all the same: put back where the choice is
                // at the next sync, not from inside the press itself.
                Stations[index].BuiltToggleOn = null;
                return;
            }

            var on = !GadgetsOn(index);
            SaveManager.SetIntValue(GadgetsKeyPrefix + index, on ? 1 : 0);
            Plugin.Log.LogInfo($"[{nameof(ResyncStations)}] Gadgets {(on ? "in" : "out")} for the resync in {Stations[index].Name}.");
            Build(Stations[index], index);
            Write(Stations[index], index);
            SendIfChanged();
        }

        // Every sign says "a resync is running" while one is, wherever it was started.
        private static void WriteAll()
        {
            for (var i = 0; i < Stations.Length; i++)
                Write(Stations[i], i);
        }
    }
}
