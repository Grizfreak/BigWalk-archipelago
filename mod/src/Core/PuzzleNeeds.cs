using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;

namespace BigWalkArchipelago.Core
{
    // Which puzzle needs are locked on this machine — the mod's side of the
    // apworld's `lock_puzzle_needs` (apworld/bigwalk/data.py, PUZZLE_NEEDS).
    // The keys are the apworld's own. A need is global to the map, not per
    // player (2026-10-02): locked means every object of that need is hidden,
    // and receiving the item shows them again (PuzzleNeedHider).
    //
    // Who knows what. Only the host runs the Archipelago client, so only the
    // host learns slot_data and receives the items; it keeps the ledger in the
    // save (`ap_need_<key>`, because items already applied are not replayed
    // on the next connection — ApItemCursor) and tells the guests which needs
    // are locked in its snapshot (ModChannel). Every machine then hides its
    // own copies of the objects. Items the player starts with arrive in the
    // server's list like any other, so nothing here treats them specially.
    //
    // Nothing is locked by default, so nothing changes until slot_data says
    // `lock_puzzle_needs` — or the debug keys lock something by hand.
    internal static class PuzzleNeeds
    {
        internal const string Buttons = "buttons";
        internal const string SyncButtons = "sync_buttons";
        internal const string IconPanels = "icon_panels";
        internal const string DrawingPanels = "drawing_panels";
        internal const string PosePanels = "pose_panels";
        internal const string SoundPanels = "sound_panels";
        internal const string PointPanels = "point_panels";
        internal const string Lights = "lights";
        internal const string Speakers = "speakers";
        internal const string Teapots = "teapots";
        internal const string Counter = "counter";
        internal const string CoordinatesComputer = "coordinates_computer";
        internal const string Eggs = "eggs";
        internal const string InkViewer = "ink_viewer";
        internal const string BigHead = "big_head";
        internal const string GolfBall = "golf_ball";
        internal const string TimedTomato = "timed_tomato";

        // The needs PuzzleNeedHider knows the objects of.
        internal static readonly string[] All =
        {
            Buttons, SyncButtons, IconPanels, DrawingPanels, PosePanels, SoundPanels, PointPanels, Lights,
            Speakers, Teapots, Counter, CoordinatesComputer, Eggs, InkViewer, BigHead, GolfBall, TimedTomato,
        };

        private const string KeyPrefix = "ap_need_";

        private static readonly HashSet<string> Locked = new HashSet<string>();

        // From slot_data. The keys are in the order of their item ids: an
        // item's id is the base plus the offset plus its position here.
        private static string[] _keys = Array.Empty<string>();
        private static long _itemIdBase;
        private static bool _mirrored;

        // Bumped on every change, so the hider can tell without comparing sets.
        internal static int Version { get; private set; }

        internal static bool Enabled { get; private set; }

        // True wherever the option is on: the host reads it from slot_data, a
        // guest is told by its host. What the overlay shows the line for.
        internal static bool Active => Enabled || _mirrored;

        internal static bool IsLocked(string need) => Locked.Contains(need);

        internal static List<string> LockedNeeds() => Locked.ToList();

        internal static void SetLocked(string need, bool locked)
        {
            var changed = locked ? Locked.Add(need) : Locked.Remove(need);
            if (changed)
                Version++;
        }

        // Host: slot_data has arrived. Every need the ledger has not granted
        // is locked, which is all of them for a save with no items yet.
        internal static void Configure(bool enabled, string[] keysInIdOrder, long itemIdBase)
        {
            Enabled = enabled;
            _keys = keysInIdOrder ?? Array.Empty<string>();
            _itemIdBase = itemIdBase;

            Locked.Clear();
            if (enabled)
            {
                foreach (var key in _keys)
                {
                    if (!All.Contains(key))
                        Plugin.Log.LogWarning($"[{nameof(PuzzleNeeds)}] '{key}' is a need this build does not know; ignored.");
                    else if (!IsGranted(key))
                        Locked.Add(key);
                }
            }

            Version++;
            Plugin.Log.LogInfo(
                $"[{nameof(PuzzleNeeds)}] lock_puzzle_needs {(enabled ? "on" : "off")}; "
                + (Locked.Count == 0 ? "nothing locked." : $"locked until their item arrives: {string.Join(", ", Locked)}."));
        }

        // A world came up on a connection that was already there (the host
        // went to the menu and loaded the save again): what is locked is what
        // the ledger says, not whatever was toggled by hand in the last one.
        internal static void Rearm()
        {
            if (!Enabled)
                return;

            Locked.Clear();
            foreach (var key in _keys)
            {
                if (All.Contains(key) && !IsGranted(key))
                    Locked.Add(key);
            }

            Version++;
        }

        // The item id is the apworld's: base, then the need's position.
        internal static bool TryResolveItem(long itemId, out string need)
        {
            need = null;
            var index = itemId - _itemIdBase;
            if (!Enabled || index < 0 || index >= _keys.Length)
                return false;

            need = _keys[index];
            return true;
        }

        internal static bool Grant(string need)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"[{nameof(PuzzleNeeds)}] Ignored: {need} received while not host.");
                return false;
            }

            SaveManager.SetIntValue(KeyPrefix + need, 1);
            SetLocked(need, false);
            Plugin.Log.LogInfo($"[{nameof(PuzzleNeeds)}] {PuzzleNeedHider.Stamp()} {need} granted.");
            SessionJournal.Write("need", $"{need} granted");
            return true;
        }

        private static bool IsGranted(string need) => SaveManager.GetIntValue(KeyPrefix + need, 0, false) != 0;

        // A save rebound to another seed replays every item from scratch, so
        // needs granted under the old binding go with it.
        internal static void ClearLedger()
        {
            // Only what was granted: writing a zero for every need would fill
            // a save that never used the option with seventeen keys.
            foreach (var key in All)
            {
                if (IsGranted(key))
                    SaveManager.SetIntValue(KeyPrefix + key, 0);
            }
        }

        // The world went away: its slot's list goes with it, and so does
        // whatever was locked by hand.
        internal static void Forget()
        {
            Enabled = false;
            _keys = Array.Empty<string>();
            _mirrored = false;
            if (Locked.Count == 0)
                return;

            Locked.Clear();
            Version++;
        }

        // Guest: the host's snapshot says which needs are locked right now.
        internal static void ApplyFromHost(bool enabled, List<string> locked)
        {
            if (!enabled && !_mirrored)
                return;

            _mirrored = enabled;
            if (Locked.SetEquals(locked))
                return;

            Locked.Clear();
            foreach (var need in locked)
                Locked.Add(need);

            Version++;
            Plugin.Log.LogInfo(
                $"[{nameof(PuzzleNeeds)}] {PuzzleNeedHider.Stamp()} The host says locked: "
                + $"{(Locked.Count == 0 ? "nothing" : string.Join(", ", Locked))}.");
        }

        // The guest left the host: whatever it was told stops being true.
        internal static void ForgetMirror()
        {
            if (!_mirrored)
                return;

            Forget();
        }
    }
}
