using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Hides the objects of every locked puzzle need, and shows them again
    // when the need is unlocked. Written 2026-10-02 for `lock_puzzle_needs`.
    //
    // The same shape as VanillaGadgetRemover, whose reasons apply here:
    //   - purely local, on every machine, nothing travels (every player runs
    //     the mod — guests without it are no longer supported);
    //   - Mirror calls SetActive(true) on a scene object when it spawns it,
    //     so what was hidden is checked again every second;
    //   - polled on WorldManager.isReadyForEffects, and re-armed on every
    //     world load, since the process outlives a world.
    //
    // What counts as an object of a need is decided by name, not by place:
    // the puzzles are built from prefabs whose names say what they are
    // (Ctrl+E dumps, 2026-10-02), and the same prefab appears in every
    // puzzle that uses it. The root of the prefab is hidden whole, so the
    // model and its trigger go together.
    internal class PuzzleNeedHider : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public PuzzleNeedHider(IntPtr ptr) : base(ptr)
        {
        }

        private const float SettleSeconds = 4f;
        private const float RecheckIntervalSeconds = 1f;

        // Everything the game builds puzzles under. Hiding too little only
        // lets a puzzle be solved earlier than the logic expects, which is
        // harmless; hiding too much can leave a player unable to go on. So
        // everything that is not a puzzle but shares its prefabs is left
        // alone, found by listing the folders each rule matched in
        // (puzzle-needs-dump.tsv, 2026-10-02):
        //   - the Gauntlet and the credits, whose needs the logic does not
        //     model: a goal behind an item the player has not received would
        //     be a softlock;
        //   - the ending and its bells (EndingGate, BlackTower,
        //     SecondGoodbye, GoodbyeGate, OverflowMonument): hold-together
        //     buttons that the goals need;
        //   - the hub gates, the lookouts, the music garden and the peaks:
        //     ways through the island and its radios, not puzzles;
        //   - the spawn and the intro pavilion, where the player starts;
        //   - the radio stations, wherever they stand.
        private const string PuzzleRoot = "Landmarks";
        private static readonly string[] Excluded =
        {
            "Gauntlet", "Credits",
            "EndingGate", "BlackTower", "SecondGoodbye", "GoodbyeGate", "OverflowMonument",
            "HubGate", "Lookouts", "MusicGarden", "PeakMarker", "SouthPeak", "FireworksPlatform",
            "IntroPavilion", "SpawnCourtyard",
            // A radio station of the world, with its own button: the station
            // is a check that the logic asks nothing for, and two of them
            // stand inside puzzle folders (DoofChamber, MusicalHoliday).
            "BroadcastStation",
        };

        // The Silent Gauntlet is left alone, except for the parts of its seven
        // stages when `lock_gauntlet_needs` says the logic counts on them
        // (apworld `GAUNTLET_STAGE_TAGS`). Those are collected like any
        // puzzle's, remembered in _inGauntlet, and hidden only while
        // GauntletStairways.ButtonsHidden is true. What a stage's parts are NOT:
        // the finale (`Level7`, whose buttons end the game and which the logic
        // asks no part for), the entrance, the skip aids, the stairway buttons
        // (they have their own rule) and the cliff walls.
        private const string GauntletRoot = "SilentGauntlet";
        private static readonly string[] GauntletSpared = { "SkipAid", "Entrance", "NHoldFullSet", "CliffBlockingWalls" };

        private static bool IsGauntletStagePart(string path)
        {
            if (!path.Contains(GauntletRoot) || path.Contains("/Level7") || GauntletSpared.Any(s => path.Contains(s)))
                return false;

            return Enumerable.Range(0, 7).Any(level => path.Contains($"/Level{level}/"));
        }

        // Whether a path is out of this hider's reach. "Gauntlet" is the one
        // entry that gives way, to the stages' own parts.
        private static bool IsExcluded(string path)
        {
            foreach (var excluded in Excluded)
            {
                if (!path.Contains(excluded))
                    continue;

                if (excluded == "Gauntlet" && IsGauntletStagePart(path))
                    continue;

                return true;
            }

            return false;
        }

        // One rule per need: the name a prefab root starts with, and where it
        // may sit. The highest ancestor whose name matches is hidden.
        private sealed class Rule
        {
            internal string Need;
            internal string[] Prefixes;
            internal bool InPuzzlesOnly = true;
            internal string[] PathMustContainOneOf;
            internal bool IgnoreExclusions;
        }

        private static readonly Rule[] Rules =
        {
            // BasicKnob is a door handle, not a puzzle part: hiding it could
            // shut players in. The train's own buttons are CustomPushButton.
            new Rule { Need = PuzzleNeeds.Buttons, Prefixes = new[] { "BasicPokeButton", "BasicPushButton", "ButtonNHold" } },

            // The synchronized presses: a SimPressSwitch is the button, and
            // the station it stands in goes with it where there is one.
            new Rule { Need = PuzzleNeeds.SyncButtons, Prefixes = new[] { "SimPressSwitch", "BreadcrumbStation", "MediumSimPressStation" } },

            // The red bulbs that carry information (Ring Room, Observation
            // Room, Centurion Seance), not the room switches.
            new Rule { Need = PuzzleNeeds.Lights, Prefixes = new[] { "Light_Table_Red" }, InPuzzlesOnly = false },

            // The sending half is a PeckSwitch, the listening half a bare
            // wall speaker beside it: ListeningPartner finds the pair.
            new Rule { Need = PuzzleNeeds.Speakers, Prefixes = new[] { "FixedRadio", "Intercom" } },
            new Rule { Need = PuzzleNeeds.Teapots, Prefixes = new[] { "KettleProp" } },
            new Rule { Need = PuzzleNeeds.Counter, Prefixes = new[] { "counter" } },
            new Rule { Need = PuzzleNeeds.CoordinatesComputer, Prefixes = new[] { "CoordinateTrackerProp" } },
            new Rule { Need = PuzzleNeeds.Eggs, Prefixes = new[] { "PegTileRoundProp" } },
            new Rule { Need = PuzzleNeeds.InkViewer, Prefixes = new[] { "FixedTelescope" },
                // The open-world puzzle, and the Gauntlet's own sixth stage.
                PathMustContainOneOf = new[] { "InvisibleInk", "GauntletChamberInvisi" } },
            new Rule { Need = PuzzleNeeds.BigHead, Prefixes = new[] { "BlindfoldProp" } },
            new Rule { Need = PuzzleNeeds.GolfBall, Prefixes = new[] { "cannonballProp" } },
            new Rule { Need = PuzzleNeeds.TimedTomato, Prefixes = new[] { "Pomodoro" } },

            // The Silent Gauntlet's stairway buttons, hold-together buttons of
            // every stage, which `gauntlet_mode: locked_stages` replaces with
            // an item each (GauntletStairways). The only thing in the Gauntlet
            // this hider touches, hence the exemption from Excluded. The root
            // of the set goes whole: its buttons, its combinators and its
            // success switch.
            new Rule
            {
                Need = GauntletStairways.ButtonsNeed, Prefixes = new[] { "NHoldFullSet" }, InPuzzlesOnly = false,
                PathMustContainOneOf = new[] { "GourdTower_Stairs" }, IgnoreExclusions = true,
            },
        };

        private readonly Dictionary<string, List<GameObject>> _objects = new Dictionary<string, List<GameObject>>();
        private readonly HashSet<int> _hiddenByUs = new HashSet<int>();

        // Instance ids of the objects that belong to a Gauntlet stage. They are
        // collected with the others but only hidden while the slot locks the
        // stages and counts on their parts (`lock_gauntlet_needs`), in either mode.
        private readonly HashSet<int> _inGauntlet = new HashSet<int>();

        // Objects the game replicates (a NetworkIdentity on them or below).
        // They are never switched off, only made invisible and untouchable
        // (SoftHide): Mirror sends a joining client the objects that are
        // ACTIVE on the server, so one hidden with SetActive(false) when the
        // guest arrives is never sent to it, and showing it again later does
        // not send it either (found 2026-10-02: the host unlocked the icon
        // panels and the guest, who had joined while they were hidden, never
        // saw one).
        private readonly HashSet<int> _networked = new HashSet<int>();

        private sealed class SoftState
        {
            internal Renderer[] Renderers;
            internal bool[] RendererWas;
            internal Collider[] Colliders;
            internal bool[] ColliderWas;
            internal Rigidbody[] Bodies;
            internal bool[] BodyWasKinematic;
        }

        private readonly Dictionary<int, SoftState> _soft = new Dictionary<int, SoftState>();

        private const float StatusIntervalSeconds = 60f;

        private static PuzzleNeedHider _current;

        private float _nextStatus;
        private float _readySince = -1f;
        private bool _collected;
        private int _appliedVersion = -1;
        private float _nextRecheck;

        private void Awake()
        {
            _current = this;
        }

        private void Update()
        {
            if (!WorldManager.isReadyForEffects)
            {
                // A world going away does NOT take the slot's needs with it:
                // the Archipelago connection outlives a return to the menu,
                // and hosting the same save again does not connect again, so
                // nothing would say it a second time (this forgot them on
                // 2026-10-02 and the next world came up with nothing locked).
                // A different save or slot reconnects, and Configure runs
                // afresh; a guest is told again by the next snapshot.
                if (_collected)
                    SessionJournal.Write("world", "gone", withPosition: false);

                _readySince = -1f;
                _collected = false;
                _appliedVersion = -1;
                _objects.Clear();
                _inGauntlet.Clear();
                _networked.Clear();
                _soft.Clear();

                // _hiddenByUs is NOT cleared: what this hider hid is still
                // hidden if the world is the same one (the guest's saw the
                // world flicker out of "ready" mid-session, 2026-10-02), and
                // forgetting it left those objects hidden for good. Instance
                // ids of a world that really went away are never met again.
                return;
            }

            if (_readySince < 0f)
                _readySince = Time.time;

            if (!_collected)
            {
                if (Time.time - _readySince < SettleSeconds)
                    return;

                Collect();
                _collected = true;
                SessionJournal.Write("world", "ready");

                // A save hosted with the Archipelago switch off must be
                // vanilla, whatever the previous world in this process left
                // locked.
                if (NetworkServer.active)
                {
                    if (ModConfig.ArchipelagoEnabled.Value)
                        PuzzleNeeds.Rearm();
                    else
                        PuzzleNeeds.Forget();
                }
            }

            if (_appliedVersion == PuzzleNeeds.Version && Time.time < _nextRecheck)
                return;

            _nextRecheck = Time.time + RecheckIntervalSeconds;
            Apply();
            _appliedVersion = PuzzleNeeds.Version;

            if (Time.time >= _nextStatus)
            {
                _nextStatus = Time.time + StatusIntervalSeconds;
                LogStatus(detailed: false);
            }
        }

        // Candidates are the components every part of a puzzle carries
        // somewhere in its prefab: a PeckSwitch (buttons, the radios'
        // sending half, the counter's keys), a TrackedPeckState (the
        // telescope), a HouseLight (the lamps) or a Prop (everything that is
        // carried). Walking every Transform of the world would be far
        // slower for no more objects.
        private void Collect()
        {
            _objects.Clear();
            _inGauntlet.Clear();
            var found = new Dictionary<string, Dictionary<int, GameObject>>();
            foreach (var need in Rules.Select(r => r.Need).Concat(PanelNeeds))
                found[need] = new Dictionary<int, GameObject>();

            var started = Time.realtimeSinceStartup;
            var candidates = new Dictionary<int, Transform>();
            Gather<PeckSwitch>(candidates, c => c.transform);
            Gather<SimPressSwitch>(candidates, c => c.transform);
            Gather<TrackedPeckState>(candidates, c => c.transform);
            Gather<HouseLight>(candidates, c => c.transform);
            Gather<Prop>(candidates, c => c.transform);

            foreach (var start in candidates.Values)
            {
                var chain = new List<(Transform transform, string name)>();
                for (var t = start; t != null; t = t.parent)
                    chain.Add((t, t.name));

                var path = string.Join("/", Enumerable.Reverse(chain).Select(n => n.name));
                foreach (var rule in Rules)
                {
                    if (!Allowed(rule, path))
                        continue;

                    Transform top = null;
                    foreach (var (transform, name) in chain)
                    {
                        if (rule.Prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
                            top = transform;
                    }

                    if (top == null)
                        continue;

                    found[rule.Need][top.gameObject.GetInstanceID()] = top.gameObject;
                    if (path.Contains(GauntletRoot))
                        _inGauntlet.Add(top.gameObject.GetInstanceID());

                    foreach (var extra in ListeningPartner(top))
                        found[rule.Need][extra.gameObject.GetInstanceID()] = extra.gameObject;
                }
            }

            CollectPanels(found, _inGauntlet);

            _networked.Clear();
            foreach (var group in found.Values)
            {
                foreach (var go in group.Values)
                {
                    if (go.GetComponentInChildren<NetworkIdentity>(true) != null)
                        _networked.Add(go.GetInstanceID());
                }
            }

            foreach (var need in found.Keys.ToList())
            {
                var objects = found[need].Values.ToList();
                _objects[need] = objects;
                var kinds = objects.GroupBy(o => o.name.Split(' ')[0]).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => $"{g.Key} x{g.Count()}");
                var replicated = objects.Count(o => _networked.Contains(o.GetInstanceID()));
                Plugin.Log.LogInfo(
                    $"[{nameof(PuzzleNeedHider)}] {need}: {objects.Count} object(s), {replicated} replicated - {string.Join(", ", kinds)}");
            }

            LogGauntletParts(found);

            Plugin.Log.LogInfo(
                $"[{nameof(PuzzleNeedHider)}] Collected in {(Time.realtimeSinceStartup - started):F2}s from {candidates.Count} candidate(s).");
        }

        // What each Gauntlet stage is made of according to the game, one line a
        // stage, so that it can be set against the apworld's GAUNTLET_STAGE_TAGS:
        // the logic and what is hidden must name the same parts, or a stage
        // would need an item its checks never asked for.
        private void LogGauntletParts(Dictionary<string, Dictionary<int, GameObject>> found)
        {
            var perStage = new SortedDictionary<int, SortedDictionary<string, int>>();
            foreach (var group in found)
            {
                foreach (var go in group.Value.Values)
                {
                    if (go == null || !_inGauntlet.Contains(go.GetInstanceID()))
                        continue;

                    var path = PathOf(go.transform);
                    var at = path.IndexOf("/Level", StringComparison.Ordinal);
                    if (at < 0 || at + 7 > path.Length || !int.TryParse(path.Substring(at + 6, 1), out var level))
                        continue;

                    if (!perStage.TryGetValue(level, out var needs))
                        perStage[level] = needs = new SortedDictionary<string, int>(StringComparer.Ordinal);

                    needs[group.Key] = needs.TryGetValue(group.Key, out var count) ? count + 1 : 1;
                }
            }

            foreach (var stage in perStage)
                Plugin.Log.LogInfo(
                    $"[{nameof(PuzzleNeedHider)}] Gauntlet stage {stage.Key + 1} (Level{stage.Key}) parts: "
                    + string.Join(", ", stage.Value.Select(n => $"{n.Key} x{n.Value}")));
        }

        private static readonly string[] PanelNeeds =
        {
            PuzzleNeeds.IconPanels, PuzzleNeeds.DrawingPanels, PuzzleNeeds.PosePanels, PuzzleNeeds.SoundPanels,
            PuzzleNeeds.PointPanels,
        };

        // A panel is a tile: a carried Prop that shows a glyph, and what the
        // glyph is decides the need. The family of its PropGroup says so
        // (PegTileA icons, PegTileD poses, PegTileC and PegTilePriest
        // drawings, PegTileB points), except the speaker tiles, which are
        // set A like the icons and are told apart by the rack they sit in.
        // The eggs (PegTileRound) and the blank tiles belong to no panel need.
        private static void CollectPanels(Dictionary<string, Dictionary<int, GameObject>> found, HashSet<int> gauntletIds)
        {
            var renderers = FindObjectsByType<PegTileRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (renderers == null)
                return;

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.GetComponent<Prop>() == null)
                    continue;

                var path = PathOf(renderer.transform);
                if (!path.StartsWith(PuzzleRoot, StringComparison.Ordinal) || IsExcluded(path))
                    continue;

                var need = PanelNeed(renderer.propGroup.ToString(), path);
                if (need == null)
                    continue;

                found[need][renderer.gameObject.GetInstanceID()] = renderer.gameObject;
                if (path.Contains(GauntletRoot))
                    gauntletIds.Add(renderer.gameObject.GetInstanceID());
            }
        }

        private static string PanelNeed(string group, string path)
        {
            if (path.Contains("PegTileSetSpeaker"))
                return PuzzleNeeds.SoundPanels;
            if (group.StartsWith("PegTilePriest", StringComparison.Ordinal))
                return PuzzleNeeds.DrawingPanels;

            const string stem = "PegTile";
            if (group.Length <= stem.Length + 1 || !char.IsDigit(group[stem.Length + 1]))
                return null;

            switch (group[stem.Length])
            {
                case 'A': return PuzzleNeeds.IconPanels;
                case 'B': return PuzzleNeeds.PointPanels;
                case 'C': return PuzzleNeeds.DrawingPanels;
                case 'D': return PuzzleNeeds.PosePanels;
                default: return null;
            }
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void Gather<T>(Dictionary<int, Transform> into, Func<T, Transform> transformOf)
            where T : UnityEngine.Object
        {
            var all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null)
                return;

            foreach (var item in all)
            {
                if (item == null)
                    continue;

                var transform = transformOf(item);
                into[transform.GetInstanceID()] = transform;
            }
        }

        private static bool Allowed(Rule rule, string path)
        {
            if (!rule.IgnoreExclusions && IsExcluded(path))
                return false;
            if (rule.InPuzzlesOnly && !path.StartsWith(PuzzleRoot, StringComparison.Ordinal))
                return false;
            return rule.PathMustContainOneOf == null || rule.PathMustContainOneOf.Any(path.Contains);
        }

        // A "(sending)" radio or intercom is paired with a "(listening)" one
        // of the same number beside it, with no component of its own to find.
        private static IEnumerable<Transform> ListeningPartner(Transform sending)
        {
            var name = sending.name;
            if (sending.parent == null || !name.Contains("(sending)"))
                yield break;

            var partner = sending.parent.Find(name.Replace("(sending)", "(listening)"));
            if (partner != null)
                yield return partner;
        }

        // What this machine believes right now, for the session reports: the
        // role, the locked needs, and how many objects of each are showing.
        // One line a minute without anyone asking, the full table on demand
        // (Ctrl+Z in debug), so a log sent back after a session says what
        // each machine saw and when.
        internal static void LogStatus(bool detailed)
        {
            var self = _current;
            if (self == null || !self._collected)
            {
                Plugin.Log.LogInfo($"[{nameof(PuzzleNeedHider)}] {Stamp()} status: no world collected yet.");
                return;
            }

            var role = NetworkServer.active ? "host" : NetworkClient.active ? "guest" : "solo";
            var locked = PuzzleNeeds.LockedNeeds();
            var perNeed = self._objects.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => (need: p.Key, total: p.Value.Count(o => o != null),
                    showing: p.Value.Count(o => o != null && self.IsShowing(o))))
                .ToList();

            var summary = $"{locked.Count} locked of {perNeed.Count}, "
                          + $"{perNeed.Sum(n => n.showing)} of {perNeed.Sum(n => n.total)} objects showing";
            Plugin.Log.LogInfo($"[{nameof(PuzzleNeedHider)}] {Stamp()} status ({role}): {summary}.");
            SessionJournal.Write(detailed ? "mark" : "status", $"{summary} | locked: {string.Join(",", locked)}");

            if (!detailed)
                return;

            foreach (var (need, total, showing) in perNeed)
                Plugin.Log.LogInfo(
                    $"[{nameof(PuzzleNeedHider)}]   {need}: {(IsLocked(need) ? "locked" : "open  ")} {showing}/{total} showing");
        }

        // Showing as the player sees it: a replicated object stays active and
        // is told apart by what this hider has made invisible.
        private bool IsShowing(GameObject go) =>
            go.activeSelf && !(_networked.Contains(go.GetInstanceID()) && _hiddenByUs.Contains(go.GetInstanceID()));

        internal static string Stamp() => DateTime.Now.ToString("HH:mm:ss");

        // The stairway buttons are not one of PuzzleNeeds' needs: they are gone
        // wherever the Gauntlet's stages are locked.
        private static bool IsLocked(string need) =>
            need == GauntletStairways.ButtonsNeed ? GauntletStairways.ButtonsHidden : PuzzleNeeds.IsLocked(need);

        private void Apply()
        {
            var gauntletPartsLocked = GauntletStairways.PartsHidden;
            foreach (var pair in _objects)
            {
                var needLocked = IsLocked(pair.Key);
                var locked = needLocked;
                var changed = 0;
                foreach (var go in pair.Value)
                {
                    if (go == null)
                        continue;

                    var id = go.GetInstanceID();
                    var networked = _networked.Contains(id);

                    // A Gauntlet stage's part follows its need only while the
                    // slot locks the stages; otherwise the Gauntlet is as the
                    // game has it.
                    locked = needLocked && (gauntletPartsLocked || !_inGauntlet.Contains(id));
                    if (locked)
                    {
                        if (networked)
                        {
                            if (SoftHide(go, id))
                                changed++;
                        }
                        else if (go.activeSelf)
                        {
                            _hiddenByUs.Add(id);
                            go.SetActive(false);
                            changed++;
                        }
                    }
                    else if (_hiddenByUs.Remove(id))
                    {
                        if (networked)
                            SoftShow(id);
                        else
                            go.SetActive(true);

                        changed++;
                    }
                }

                if (changed > 0)
                    Plugin.Log.LogInfo(
                        $"[{nameof(PuzzleNeedHider)}] {Stamp()} {pair.Key}: {(needLocked ? "hid" : "showed")} {changed} of {pair.Value.Count}.");
            }
        }

        // True when something changed. Called every second for what is already
        // hidden too: the game's culling and its own scripts switch renderers
        // back on, and putting that right is cheaper than finding out who does.
        private bool SoftHide(GameObject go, int id)
        {
            var first = !_soft.TryGetValue(id, out var state);
            if (first)
            {
                state = new SoftState
                {
                    Renderers = go.GetComponentsInChildren<Renderer>(true),
                    Colliders = go.GetComponentsInChildren<Collider>(true),
                    Bodies = go.GetComponentsInChildren<Rigidbody>(true),
                };
                state.RendererWas = state.Renderers.Select(r => r != null && r.enabled).ToArray();
                state.ColliderWas = state.Colliders.Select(c => c != null && c.enabled).ToArray();
                state.BodyWasKinematic = state.Bodies.Select(b => b != null && b.isKinematic).ToArray();
                _soft[id] = state;
            }

            _hiddenByUs.Add(id);
            foreach (var renderer in state.Renderers)
            {
                if (renderer != null && renderer.enabled)
                    renderer.enabled = false;
            }

            foreach (var collider in state.Colliders)
            {
                if (collider != null && collider.enabled)
                    collider.enabled = false;
            }

            // Without a collider a loose tile would fall out of the world.
            foreach (var body in state.Bodies)
            {
                if (body != null && !body.isKinematic)
                    body.isKinematic = true;
            }

            return first;
        }

        private void SoftShow(int id)
        {
            if (!_soft.TryGetValue(id, out var state))
                return;

            for (var i = 0; i < state.Renderers.Length; i++)
            {
                if (state.Renderers[i] != null)
                    state.Renderers[i].enabled = state.RendererWas[i];
            }

            for (var i = 0; i < state.Colliders.Length; i++)
            {
                if (state.Colliders[i] != null)
                    state.Colliders[i].enabled = state.ColliderWas[i];
            }

            for (var i = 0; i < state.Bodies.Length; i++)
            {
                if (state.Bodies[i] != null)
                    state.Bodies[i].isKinematic = state.BodyWasKinematic[i];
            }

            _soft.Remove(id);
        }
    }
}
