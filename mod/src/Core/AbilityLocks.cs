using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // The player's body behind items — the mod's side of the apworld's `lock_abilities` (ROADMAP R3,
    // 0.6; apworld/bigwalk/data.py, ABILITIES). Off by default; with it on, every ability below is
    // locked until its item arrives, for every player at once (an item is the slot's, not a player's).
    //
    // Who knows what: the same shape as PuzzleNeeds. The host reads slot_data, receives the items and
    // keeps the ledger in the save (`ap_ability_<key>`); its snapshot tells the guests which abilities
    // are locked (a mask). Every machine then blocks its own player (AbilityLockEnforcer, the patches
    // below), which is local input: every player runs the mod.
    //
    // How each one is blocked (recon 2026-10-12, docs/ROADMAP.md R3):
    //   - jumping, crouching, floor sitting, raising an arm: their Rewired actions (jump 16, crouch 20,
    //     sit 26, waveLeft 18, waveRight 19), whose element maps are switched off;
    //   - pointing (a click held, hands empty) and extending (an arm raised and a click held): the
    //     click is also pick up and drop, so the game's own decision is overridden instead
    //     (PlayerGestures.UpdateSingleArm, `isPointing`);
    //   - running: its Rewired action (sprint 15), only with `lock_running`;
    //   - throwing: PlayerHeldInformation.ThrowInfo's wind-up forced to 0, which makes a throw a drop, and
    //     the wind-up itself refused (PlayerDecisions.SetWindUpStartTime);
    //   - carrying another player: refused on the host (UserCode_CmdPickUpPlayer);
    //   - chair sitting: PlayerPoser.EnterPose refused for furniture only (IsFurniture), never for a
    //     turnstile, a telescope, a listening chair, the chairlift, the train or the ending's poses.
    internal static class AbilityLocks
    {
        internal const string Jumping = "jumping";
        internal const string Crouching = "crouching";
        internal const string ChairSitting = "chair_sitting";
        internal const string FloorSitting = "floor_sitting";
        internal const string LeftPointing = "left_pointing";
        internal const string LeftRaising = "left_raising";
        internal const string LeftExtending = "left_extending";
        internal const string RightPointing = "right_pointing";
        internal const string RightRaising = "right_raising";
        internal const string RightExtending = "right_extending";
        internal const string Throwing = "throwing";
        internal const string Carrying = "carrying";
        internal const string Running = "running";

        // What the player can be kept from doing, in the order of the snapshot's mask bits.
        internal static readonly string[] All =
        {
            Jumping, Crouching, ChairSitting, FloorSitting,
            LeftPointing, LeftRaising, LeftExtending, RightPointing, RightRaising, RightExtending,
            Throwing, Carrying, Running,
        };

        // The items (apworld data.ABILITIES): Legs and Seat grant two abilities at once; a progressive
        // arm grants its next gesture, raising, then pointing, then extending; Progressive Arms does it for
        // both arms.
        private static readonly Dictionary<string, string[]> Grants = new Dictionary<string, string[]>
        {
            { "legs", new[] { Jumping, Crouching } },
            { "seat", new[] { ChairSitting, FloorSitting } },
            { "throwing", new[] { Throwing } },
            { "carrying", new[] { Carrying } },
            { "running", new[] { Running } },
        };

        private static readonly Dictionary<string, string[][]> Progressive = new Dictionary<string, string[][]>
        {
            { "left_arm", new[] { new[] { LeftRaising }, new[] { LeftPointing }, new[] { LeftExtending } } },
            { "right_arm", new[] { new[] { RightRaising }, new[] { RightPointing }, new[] { RightExtending } } },
            { "arms", new[] { new[] { LeftRaising, RightRaising }, new[] { LeftPointing, RightPointing }, new[] { LeftExtending, RightExtending } } },
        };

        private const string KeyPrefix = "ap_ability_";

        // How many ability items this save has received (big_body counts them, a progressive arm once per copy;
        // not Running, which stays useful and which the apworld leaves out of the count).
        private const string ItemsKey = "ap_ability_items";

        internal static int ItemsReceived => SaveManager.GetIntValue(ItemsKey, 0, false);

        private static readonly HashSet<string> Locked = new HashSet<string>();

        // From slot_data: every ability item key, in the order of their ids (base + offset + position).
        private static string[] _keys = Array.Empty<string>();
        private static long _itemIdBase;
        private static bool _mirrored;

        internal static int Version { get; private set; }

        internal static bool Enabled { get; private set; }

        internal static bool Active => Enabled || _mirrored;

        internal static bool IsLocked(string ability) => Locked.Contains(ability);

        // Host: slot_data. `mode` is the option's key (disabled, handed, ambidextrous); every ability
        // the ledger has not granted is locked.
        // Running is only locked with `lock_running`: without it, the seed holds no Running item.
        private static bool _lockRunning;

        internal static void Configure(string mode, bool lockRunning, string[] keysInIdOrder, long itemIdBase)
        {
            Enabled = mode == "handed" || mode == "ambidextrous";
            _lockRunning = lockRunning;
            _keys = keysInIdOrder ?? Array.Empty<string>();
            _itemIdBase = itemIdBase;
            Rearm();
            Plugin.Log.LogInfo(
                $"[{nameof(AbilityLocks)}] lock_abilities {mode}; "
                + (Locked.Count == 0 ? "nothing locked." : $"locked until their item arrives: {string.Join(", ", Locked)}."));
        }

        internal static void Rearm()
        {
            Locked.Clear();
            if (Enabled)
            {
                foreach (var ability in All)
                {
                    if (!IsGranted(ability) && (ability != Running || _lockRunning))
                        Locked.Add(ability);
                }
            }

            Version++;
        }

        internal static bool TryResolveItem(long itemId, out string key)
        {
            key = null;
            var index = itemId - _itemIdBase;
            if (!Enabled || index < 0 || index >= _keys.Length)
                return false;

            key = _keys[index];
            return true;
        }

        internal static bool Grant(string key)
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogWarning($"[{nameof(AbilityLocks)}] Ignored: {key} received while not host.");
                return false;
            }

            foreach (var ability in AbilitiesOf(key))
            {
                if (Array.IndexOf(All, ability) < 0)
                {
                    Plugin.Log.LogWarning($"[{nameof(AbilityLocks)}] '{ability}' is an ability this build does not know; ignored.");
                    continue;
                }

                SaveManager.SetIntValue(KeyPrefix + ability, 1);
                Locked.Remove(ability);
            }

            Version++;
            if (key != "running")
                SaveManager.SetIntValue(ItemsKey, ItemsReceived + 1);
            Plugin.Log.LogInfo($"[{nameof(AbilityLocks)}] {key} granted.");
            SessionJournal.Write("ability", $"{key} granted");
            return true;
        }

        // What this copy of the item grants: a progressive one, its first tier not granted yet (the ledger
        // is in the save, so a reconnection that replays nothing still counts right).
        private static string[] AbilitiesOf(string key)
        {
            if (Grants.TryGetValue(key, out var abilities))
                return abilities;
            if (Progressive.TryGetValue(key, out var tiers))
            {
                foreach (var tier in tiers)
                {
                    if (!Array.TrueForAll(tier, IsGranted))
                        return tier;
                }

                return Array.Empty<string>();
            }

            return new[] { key };
        }

        private static bool IsGranted(string ability) => SaveManager.GetIntValue(KeyPrefix + ability, 0, false) != 0;

        internal static void ClearLedger()
        {
            if (ItemsReceived != 0)
                SaveManager.SetIntValue(ItemsKey, 0);
            foreach (var ability in All)
            {
                if (IsGranted(ability))
                    SaveManager.SetIntValue(KeyPrefix + ability, 0);
            }
        }

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

        // The snapshot: bit i set when All[i] is locked.
        internal static ushort HostMask()
        {
            ushort mask = 0;
            for (var i = 0; i < All.Length; i++)
            {
                if (Locked.Contains(All[i]))
                    mask |= (ushort)(1 << i);
            }

            return mask;
        }

        internal static void ApplyFromHost(bool enabled, ushort mask)
        {
            if (!enabled && !_mirrored)
                return;

            _mirrored = enabled;
            if (HostMask() == mask)
                return;

            Locked.Clear();
            for (var i = 0; i < All.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                    Locked.Add(All[i]);
            }

            Version++;
            Plugin.Log.LogInfo(
                $"[{nameof(AbilityLocks)}] The host says locked: {(Locked.Count == 0 ? "nothing" : string.Join(", ", Locked))}.");
        }

        internal static void ForgetMirror()
        {
            if (_mirrored)
                Forget();
        }

        // A chair, a couch, a bench, a stump: a pose that only seats the player. A pose that drives
        // something (a turnstile's switch, a telescope, a listening chair's headset) or carries the
        // player (chairlift, train) or ends the game (Goodbye) is never furniture: locking those would
        // lock puzzles and ways through.
        private static readonly string[] FurnitureWords = { "chair", "couch", "bench", "stump", "seat", "sofa", "stool" };
        private static readonly string[] NeverFurniture = { "chairlift", "trainsystem", "climbaboard", "goodbye", "turnstile", "telescope", "listening" };

        internal static bool IsFurniture(PlayerPose pose)
        {
            if (pose == null || pose.onEnterSwitch != null || pose.onExitSwitch != null || pose.directSystemControl != null)
                return false;

            var path = pose.transform.name;
            for (var parent = pose.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            path = path.ToLowerInvariant();

            foreach (var word in NeverFurniture)
            {
                if (path.Contains(word))
                    return false;
            }

            foreach (var word in FurnitureWords)
            {
                if (path.Contains(word))
                    return true;
            }

            return false;
        }
    }

    // Applies the Rewired half on the local player: the element maps of each locked action off, the
    // others back on. Only once the option has locked something, so a vanilla game is never touched;
    // re-applied every second, since the game rebuilds its maps when controls are rebound or a
    // controller is plugged in. Jump stays on while the player sits in a pose: it is the way off a
    // chair, and a player who could sit without jumping would be stuck there. Likewise after a fall
    // that leaves the player on the ground (a big fall, the DeathLink knock-out): jump is how one gets
    // up (player, 2026-10-15), so it stays on from the fall, through the daze, and until a couple of
    // seconds after the last jump pressed once the daze is over: one press that the game takes too
    // early does not lock it again with the player still on the ground, every try gets its own time.
    internal class AbilityLockEnforcer : MonoBehaviour
    {
        public AbilityLockEnforcer(IntPtr ptr) : base(ptr)
        {
        }

        private static readonly (int Action, string Ability)[] RewiredLocks =
        {
            (RewiredConsts.Action.jump, AbilityLocks.Jumping),
            (RewiredConsts.Action.crouch, AbilityLocks.Crouching),
            (RewiredConsts.Action.sit, AbilityLocks.FloorSitting),
            (RewiredConsts.Action.waveLeft, AbilityLocks.LeftRaising),
            (RewiredConsts.Action.waveRight, AbilityLocks.RightRaising),
            (RewiredConsts.Action.sprint, AbilityLocks.Running),
        };

        private bool _touched;
        private float _next;
        private int _appliedVersion = -1;
        private bool _wasJumpFree;

        // The local player has fallen and not got up yet (Patches/TrapPatches, PlayerFallPatch); once a
        // jump is pressed after the daze, when jump goes back off unless another is pressed before.
        private const float GetUpGrace = 2f;
        private static bool _down;
        private static float _upAt = -1f;

        internal static void OnLocalFall()
        {
            _down = true;
            _upAt = -1f;
        }

        private void Update()
        {
            try
            {
                if (!Rewired.ReInput.isReady)
                    return;

                if (_down && !LocalPlayerIsDazed())
                {
                    if (Rewired.ReInput.players.GetPlayer(0).GetButtonDown(RewiredConsts.Action.jump))
                        _upAt = Time.unscaledTime + GetUpGrace;
                    else if (_upAt >= 0f && Time.unscaledTime >= _upAt)
                        _down = false;
                }

                var jumpFree = _down || LocalPlayerIsPosing();
                if (_appliedVersion == AbilityLocks.Version && jumpFree == _wasJumpFree && Time.unscaledTime < _next)
                    return;

                _next = Time.unscaledTime + 1f;
                _appliedVersion = AbilityLocks.Version;
                _wasJumpFree = jumpFree;

                if (!AbilityLocks.Active && !_touched)
                    return;

                Apply(jumpFree);
                _touched = AbilityLocks.Active;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(AbilityLockEnforcer)}] {ex.Message}");
                _next = Time.unscaledTime + 5f;
            }
        }

        private static void Apply(bool jumpFree)
        {
            var player = Rewired.ReInput.players.GetPlayer(0);
            var maps = new Il2CppSystem.Collections.Generic.List<Rewired.ControllerMap>();
            player.controllers.maps.GetAllMaps(maps);
            var elements = new Il2CppSystem.Collections.Generic.List<Rewired.ActionElementMap>();
            foreach (var (action, ability) in RewiredLocks)
            {
                var enabled = !AbilityLocks.IsLocked(ability) || (action == RewiredConsts.Action.jump && jumpFree);
                for (var m = 0; m < maps.Count; m++)
                {
                    elements.Clear();
                    maps[m].GetElementMapsWithAction(action, elements);
                    for (var e = 0; e < elements.Count; e++)
                    {
                        if (elements[e].enabled != enabled)
                            elements[e].enabled = enabled;
                    }
                }
            }
        }

        private static bool LocalPlayerIsPosing()
        {
            var local = Mirror.NetworkClient.localPlayer;
            var character = local != null ? local.GetComponent<PlayerCharacter>() : null;
            return character != null && character.poser != null && character.poser.currentPose != null;
        }

        private static bool LocalPlayerIsDazed()
        {
            var local = Mirror.NetworkClient.localPlayer;
            var character = local != null ? local.GetComponent<PlayerCharacter>() : null;
            return character != null && character.faller != null && character.faller.isDazed;
        }
    }
}
