using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core.Net;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The in-world teleport buttons (ROADMAP U4/U10): in the hub one for each of six
    // destinations, and in each destination one that leads back to the hub. Which of them exist
    // is the slot's `teleport_buttons` option:
    //
    //   off          no buttons.
    //   free         every button, from the start.
    //   with_towers  a tower's hub button once its door has been opened by the button at its foot
    //                (the game's LookoutLight flag); the Black Tower's once the two buttons at its
    //                top have been held together; the Gauntlet's once the chapel has been opened
    //                (its two buttons held together).
    //   items        each hub button needs its own Teleporter item.
    //
    // The way back to the hub is there whenever the option is not off. A teleport can skip a
    // locked door, which the logic does not count on, so none of this is ever logic.
    //
    // Every machine builds the same buttons from the same table (a button is a native game
    // button with a fixed ticket per slot, see WorldButtons), so nothing is networked but the
    // set of buttons that exist: the host works it out and sends it to the guests in the
    // snapshot as a bit mask (ModChannel).
    internal static class TeleportButtons
    {
        private const string LedgerPrefix = "ap_tp_";

        // Bit 0..5 of the mask: the hub's buttons, in `Destinations` order. This bit: the
        // way back, in every destination.
        private const int ReturnBit = 1 << 6;

        // The resync and gather buttons: there when the host runs Archipelago, which a guest
        // does not, so its own config cannot say.
        private const int ToolsBit = 1 << 7;

        private sealed class Destination
        {
            internal string Key;
            internal string Label;

            // Where the hub's button for this destination is: a point on a wall and the way the
            // wall faces. The return button inside the destination is `ReturnSpot`.
            internal Spot HubSpot;
            internal Spot ReturnSpot;
        }

        private struct Spot
        {
            internal Vector3 Point;
            internal Vector3 Normal;

            internal Spot(float x, float y, float z, float nx, float nz)
            {
                Point = new Vector3(x, y, z);
                Normal = new Vector3(nx, 0f, nz).normalized;
            }
        }

        // The order is the apworld's `TELEPORT_DESTINATIONS`, which is also the order of the
        // Teleporter item ids. The hub's spots are the Black Tower's mark of 2026-10-04 (Keypad 8) with the others
        // lined up beside it, 1.2 m apart, left to right as the player faces the wall.
        // The return spots are the marks of the same night, one inside each destination.
        private static readonly Destination[] Destinations =
        {
            new Destination { Key = "red", Label = "Red Funnel Tower",
                HubSpot = new Spot(-229.25f, 33.65f, -506.16f, 0.75f, -0.66f),
                ReturnSpot = new Spot(-88.75f, 87.11f, -608.67f, 0.24f, -0.97f) },
            new Destination { Key = "green", Label = "Green Cup Tower",
                HubSpot = new Spot(-228.46f, 33.65f, -505.26f, 0.75f, -0.66f),
                ReturnSpot = new Spot(454.01f, 102.51f, -447.57f, 0.76f, 0.64f) },
            new Destination { Key = "blue", Label = "Blue Castle Tower",
                HubSpot = new Spot(-227.67f, 33.65f, -504.36f, 0.75f, -0.66f),
                ReturnSpot = new Spot(139.55f, 102.38f, -201.39f, -0.28f, -0.96f) },
            new Destination { Key = "yellow", Label = "Yellow Twist Tower",
                HubSpot = new Spot(-226.87f, 33.65f, -503.46f, 0.75f, -0.66f),
                ReturnSpot = new Spot(-229.89f, 121.50f, -239.87f, 0.55f, 0.84f) },
            new Destination { Key = "black", Label = "Black Tower",
                HubSpot = new Spot(-226.08f, 33.65f, -502.56f, 0.75f, -0.66f),
                ReturnSpot = new Spot(-33.52f, 118.31f, -287.99f, 0.64f, 0.77f) },
            new Destination { Key = "gauntlet", Label = "Silent Gauntlet",
                HubSpot = new Spot(-225.29f, 33.65f, -501.66f, 0.75f, -0.66f),
                ReturnSpot = new Spot(-737.37f, 33.05f, 678.84f, 0.47f, 0.88f) },
        };

        // The slots of the buttons in WorldButtons: 0..5 the hub's, 6..11 the returns.
        private const int ReturnSlotBase = 6;

        // How far in front of a button a player lands.
        private const float ArrivalDistance = 1.6f;

        private static string _mode = "off";
        private static int _idOffset = ApLocationIds.DefaultTeleportOffset;
        private static string[] _keys = Array.Empty<string>();

        // On a guest: the mask the host last sent.
        private static int _mirroredMask;
        private static bool _mirrored;

        internal static void Configure(string mode, int idOffset, string[] keys)
        {
            _mode = string.IsNullOrEmpty(mode) ? "off" : mode;
            _idOffset = idOffset;
            _keys = keys ?? Array.Empty<string>();
        }

        internal static bool TryResolveItem(long itemId, out int destination)
        {
            destination = -1;
            var value = itemId - ApLocationIds.Base - _idOffset;
            if (value < 0 || value >= _keys.Length)
                return false;

            destination = Array.IndexOf(Keys(), _keys[(int)value]);
            return destination >= 0;
        }

        internal static bool Grant(int destination)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"[{nameof(TeleportButtons)}] Ignored: teleporter received while not host.");
                return false;
            }

            SaveManager.SetIntValue(LedgerPrefix + Destinations[destination].Key, 1);
            Plugin.Log.LogInfo($"[{nameof(TeleportButtons)}] Teleporter to {Destinations[destination].Label} granted.");
            return true;
        }

        // A save rebound to another seed replays every item from scratch.
        internal static void ClearLedger()
        {
            foreach (var destination in Destinations)
                SaveManager.SetIntValue(LedgerPrefix + destination.Key, 0);
        }

        private static string[] Keys()
        {
            var keys = new string[Destinations.Length];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = Destinations[i].Key;
            return keys;
        }

        // Which buttons are there, as the host sees it. On a guest, as the host last said.
        internal static int Mask()
        {
            if (NetworkClient.active && !NetworkServer.active)
                return _mirrored ? _mirroredMask : 0;

            return HostMask();
        }

        internal static int HostMask()
        {
            var mask = ModConfig.ArchipelagoEnabled.Value ? ToolsBit : 0;
            if (_mode == "off")
                return mask;

            mask |= ReturnBit;
            for (var i = 0; i < Destinations.Length; i++)
            {
                if (IsOpen(Destinations[i].Key))
                    mask |= 1 << i;
            }

            return mask;
        }

        internal static void ApplyFromHost(int mask)
        {
            _mirrored = true;
            _mirroredMask = mask;
        }

        internal static void ForgetMirror()
        {
            _mirrored = false;
            _mirroredMask = 0;
        }

        private static bool IsOpen(string key)
        {
            switch (_mode)
            {
                case "free":
                    return true;
                case "items":
                    return SaveManager.GetIntValue(LedgerPrefix + key, 0, false) != 0;
                case "with_towers":
                    switch (key)
                    {
                        case "red":
                            return SaveManager.GetIntValue("LookoutLightRed", 0, false) != 0;
                        case "green":
                            return SaveManager.GetIntValue("LookoutLightGreen", 0, false) != 0;
                        case "blue":
                            return SaveManager.GetIntValue("LookoutLightBlue", 0, false) != 0;
                        case "yellow":
                            return SaveManager.GetIntValue("LookoutLightYellow", 0, false) != 0;
                        case "black":
                            // Like the other towers, the tower's own button: the two at the top held
                            // together write BlackTowerInteriorDoor (measured 2026-10-05). The door at
                            // its foot only opens with the five monuments, so being up there says it.
                            return SaveManager.GetIntValue("BlackTowerInteriorDoor", 0, false) != 0;
                        default:
                            // The Silent Gauntlet lies behind the chapel. The game writes EndingGate = 2
                            // when its two buttons have been held together (measured 2026-10-05), and the
                            // mod keeps its own latch of that (ApGoalFlags).
                            return SaveManager.GetIntValue("EndingGate", 0, false) >= 2
                                   || SaveManager.GetIntValue("ap_flag_EndingGate", 0, false) != 0;
                    }
                default:
                    return false;
            }
        }

        // The resync button, which is not a teleport but stands among them: the hub's, beside the
        // map room, in the slot after the last return button. Always there while Archipelago is on.
        // Sunk well into the wall: a plain button on its own looks bolted on.
        private const int ResyncSlot = 12;
        private static readonly Spot ResyncSpot = new Spot(-211.81f, 33.09f, -526.06f, 0.66f, 0.75f);

        // The gather buttons, one in each tower beside its way back (marks of 2026-10-04): a press
        // does the resync, but the items land in front of the button, to be picked up on the
        // spot. Slots 13..17, in the order of the first five destinations.
        private const int GatherSlotBase = 13;
        private const float GatherAhead = 1.2f;
        private static readonly Spot[] GatherSpots =
        {
            new Spot(-89.84f, 97.64f, -604.06f, 0.20f, -0.93f),
            new Spot(449.78f, 113.32f, -451.33f, 0.72f, 0.67f),
            new Spot(140.39f, 112.84f, -198.54f, -0.32f, -0.95f),
            new Spot(-231.31f, 132.27f, -242.06f, 0.45f, 0.76f),
            new Spot(-28.48f, 118.34f, -282.17f, -0.64f, -0.77f),
        };

        private static void SyncGather(bool wanted)
        {
            for (var i = 0; i < GatherSpots.Length; i++)
            {
                var slot = GatherSlotBase + i;
                if (!wanted)
                {
                    WorldButtons.Remove(slot);
                    continue;
                }

                if (WorldButtons.Has(slot))
                    continue;

                var spot = GatherSpots[i];
                var where = spot.Point + spot.Normal * GatherAhead;
                WorldButtons.Add(slot, "Gather items in the " + Destinations[i].Label, spot.Point - spot.Normal * 0.25f,
                    Quaternion.LookRotation(spot.Normal, Vector3.up), presser => ApRuntime.ResyncAll(where),
                    icon: null, tint: Destinations[i].Key, hostSide: true);
            }
        }

        private static void SyncResync(bool wanted)
        {
            if (!wanted)
            {
                WorldButtons.Remove(ResyncSlot);
                return;
            }

            if (WorldButtons.Has(ResyncSlot))
                return;

            WorldButtons.Add(ResyncSlot, "Resync", ResyncSpot.Point - ResyncSpot.Normal * 0.25f,
                Quaternion.LookRotation(ResyncSpot.Normal, Vector3.up), presser => ApRuntime.ResyncAll(),
                hostSide: true);
        }

        // Puts into the world the buttons the mask says are there and takes out the others.
        // Cheap enough to run every couple of seconds, which is how a button whose world
        // loaded late (or was destroyed by a reload) comes back.
        internal static void Sync()
        {
            var mask = Mask();
            SyncResync((mask & ToolsBit) != 0);
            SyncGather((mask & ToolsBit) != 0);
            for (var i = 0; i < Destinations.Length; i++)
            {
                SyncSlot(i, (mask & (1 << i)) != 0, Destinations[i].HubSpot, Destinations[i].Label,
                    Destinations[i].Key, null, destination => ArrivalOf(destination), i);
                SyncSlot(ReturnSlotBase + i, (mask & ReturnBit) != 0 && Destinations[i].ReturnSpot.Normal != Vector3.zero,
                    Destinations[i].ReturnSpot, "Back to the hub from " + Destinations[i].Label, "hub", Destinations[i].Key, destination => HubArrivalOf(destination), i);
            }
        }

        private static void SyncSlot(int slot, bool wanted, Spot spot, string label, string icon, string tint, Func<int, Vector3?> arrival, int destination)
        {
            if (!wanted || spot.Normal == Vector3.zero)
            {
                WorldButtons.Remove(slot);
                return;
            }

            if (WorldButtons.Has(slot))
                return;

            // A destination whose landing has not been marked yet still gets its button; it
            // does nothing but say so in the log.
            WorldButtons.Add(slot, label, spot.Point + spot.Normal * 0.02f, Quaternion.LookRotation(spot.Normal, Vector3.up),
                presser =>
                {
                    // Worked out now, not when the button was made: the floor of the other
                    // place is only there to be found once the player is near it.
                    var landing = arrival(destination);
                    if (landing == null)
                        Plugin.Log.LogInfo($"[{nameof(TeleportButtons)}] '{label}': no landing marked yet.");
                    else
                        WorldButtons.TeleportTo(presser, landing.Value, Quaternion.identity);
                }, icon, tint);
        }

        // Where the hub's button for a destination sends a player: in front of that
        // destination's return button; nowhere until that button is marked.
        private static Vector3? ArrivalOf(int destination)
        {
            var spot = Destinations[destination].ReturnSpot;
            if (spot.Normal != Vector3.zero)
                return InFrontOf(spot);

            return null;
        }

        // Where a destination's return button sends a player: in front of the hub's button
        // for that destination.
        private static Vector3? HubArrivalOf(int destination)
        {
            return InFrontOf(Destinations[destination].HubSpot);
        }

        // The floor in front of a button, found by looking down from the height of the mark: a
        // mark made higher or lower than a hand would otherwise put a player in the floor or
        // in the air.
        private static Vector3 InFrontOf(Spot spot)
        {
            var ahead = spot.Point + spot.Normal * ArrivalDistance;
            if (Physics.Raycast(ahead, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.8f;

            // Nothing to find (the other place is not loaded yet): land at the height of the mark
            // and fall the last metre, which is always above the floor, where a guess at the
            // height of a hand could be inside it.
            return ahead + Vector3.up * 0.2f;
        }
    }

    // Keeps the world's buttons what TeleportButtons says they should be.
    internal sealed class TeleportButtonRunner : MonoBehaviour
    {
        private const float Period = 2f;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next)
                return;

            _next = Time.unscaledTime + Period;
            if (!WorldManager.isReadyForEffects)
                return;

            try
            {
                TeleportButtons.Sync();
                CabinFeverWaits.Tick();
                BlackTowerDoor.Enforce();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TeleportButtonRunner)}] {ex.Message}");
            }
        }
    }
}
