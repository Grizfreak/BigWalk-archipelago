using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // What a trap or a bonus does, on THIS machine's own player (ROADMAP step 3). The host
    // decides what fires and where a player goes (Core/Traps.cs); every machine, the host
    // included, then plays the effect on its own player, because a player's hands, movement and
    // position belong to that player's machine (the game is owner-authoritative for players).
    //
    // Everything here goes through the game's own code paths, read with Ghidra on 2026-10-05:
    //
    //   drop     PlayerActions.ActionDropPropOrPlayer, what letting go of the drop button calls.
    //            It throws with the wind-up the arms show; ClearWindUp first makes it a plain drop.
    //   throw    PlayerDecisions.SetWindUpStartTime(NetworkTime.time), what pressing the button
    //            does; put back every frame if the player cancels it, and one second past a full
    //            wind-up (PlayerTunings.maxWindUpDuration) ActionDropPropOrPlayer throws.
    //   speed    PlayerTunings, a per-player copy: the forward speeds, scaled for a while.
    //   jump     PlayerTunings.jumpForce (read by PlayerJumper.LocalFixedUpdate) and the cap on
    //            upward velocity, scaled for a while.
    //   sleep    asleep two seconds out of seven for a while (player, 2026-10-05): the game's own
    //            sleep (PlayerSleeper.forceSleeping), and the same speeds and jump at 0 meanwhile.
    //   night,   SkyManager.SetFixedTime, as DebugDaylight, stepped every frame: the clock
    //   day      fast-forwards to midnight (noon), then ClearFixedTime and it runs on from there.
    //   trip,    PlayerGrease.Teleport, the game's own player teleport, as the world buttons.
    //   meeting
    internal static class TrapEffects
    {
        internal const string Drop = "drop";
        internal const string Throw = "throw";
        internal const string Trip = "trip";
        internal const string Meeting = "meeting";
        internal const string Night = "night";
        internal const string Day = "day";
        internal const string Speed = "speed";
        internal const string Jump = "jump";
        internal const string Sleep = "sleep";
        internal const string Load = "load";

        // A DeathLink received (`death_link_effect: knockout`, players 2026-10-07): what is held
        // dropped, the packs off (the host's half, Core/Traps), and dazed as after a big fall.
        internal const string Knockout = "knockout";

        // The blindfold helmet on every unmasked player for the trap's duration (host only, Core/Traps).
        internal const string Mask = "mask";

        // A flare in front of every player, in the Archipelago colours (Core/BigFlare).
        internal const string Flare = "flare";

        // A clock in every player's hands, on the real time of their own computer (host only, Core/Traps).
        internal const string BigTime = "time";

        // Every player muted for a while (R9 "No Comms", player 2026-10-13: the player muted, not their radios). Each
        // machine mutes its own player the way the game's mute does: the game's own state (PlayerNetworking.CmdSetMute,
        // which shows the mute icon to everyone) and the voice chat's microphone (DissonanceComms.IsMuted), held every
        // frame so the mute key cannot lift it, then put back as the player had it.
        internal const string Silence = "silence";
        private static float _silenceUntil = -1f;
        private static bool _silenceWasMuted;
        private static bool _silenceCommsWasMuted;
        private static Dissonance.DissonanceComms _comms;

        // The ground slippery for a while. Each fixed step the game sets the velocity the controls ask for
        // (PlayerMover.localControlsVelocity, in LocalFixedUpdate, decompiled 2026-10-13); while this lasts the
        // velocity only drifts towards it (Patches/BigSlidePatch), so starting, stopping and turning slide. Each
        // machine for its own player. (PlayerMover.defaultSmoothTime, tried first, changed nothing in game.)
        internal const string Slide = "slide";
        private static float _slideUntil = -1f;

        // How much of the asked-for velocity is taken each fixed step (50 a second); 0.04 was a little weak (player, 2026-10-13).
        internal const float SlideGrip = 0.025f;

        internal static bool SlideActive => _slideUntil >= 0f;

        // Every player white for a while, as in the Silent Gauntlet, but only the colour: not its silence (R9, player
        // 2026-10-07). The Gauntlet's white is `PlayerLooks.SetSpeechlessness`, which `PlayerSpeechless.Update` sets
        // again every frame; while this lasts it is set to the full after it (Patches/BigGhostPatch), for every player
        // on this machine, so everyone sees everyone white.
        internal const string Ghost = "ghost";
        private static float _ghostUntil = -1f;

        internal static bool GhostActive => _ghostUntil >= 0f;

        // Big Paint (T5's colour chaos, the trap table: "two players swap colours"): each player takes the next one's
        // colours (head, torso, legs), in the players' network order, so every machine makes the same swap with nothing
        // sent; two players swap, more go round. Set once, then swapped in whenever the game sets a colour
        // (Patches/BigPaintPatch); each one's own put back at the end. Alone, nothing.
        internal const string Paint = "paint";
        private static float _paintUntil = -1f;
        private static readonly List<(PlayerCharacter target, PlayerLookSet.LookColor head, PlayerLookSet.LookColor torso, PlayerLookSet.LookColor legs)> Painted = new();
        private static readonly List<(PlayerCharacter target, PlayerLookSet.LookColor head, PlayerLookSet.LookColor torso, PlayerLookSet.LookColor legs)> OwnColours = new();

        private static void StartPaint(int seconds)
        {
            var players = new List<PlayerCharacter>();
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player != null && player.looks != null)
                    players.Add(player);
            }
            if (players.Count < 2)
            {
                Plugin.Log.LogInfo($"{Tag} Big Paint: alone, nothing to swap.");
                return;
            }
            players.Sort((a, b) => a.netId.CompareTo(b.netId));

            EndPaint();
            for (var i = 0; i < players.Count; i++)
            {
                var source = players[(i + 1) % players.Count].looks;
                Painted.Add((players[i], source.headColor, source.torsoColor, source.legsColor));
                var own = players[i].looks;
                OwnColours.Add((players[i], own.headColor, own.torsoColor, own.legsColor));
            }
            _paintUntil = Time.unscaledTime + seconds;
            HoldPaint();
        }

        internal static bool PaintActive => _paintUntil >= 0f;

        // Big Goggles (the trap table: the field of view shrinks): the camera reads the player's setting from
        // `PlayerCameraMinder.fovFromSettings`; while this lasts it is GogglesFactor of it, then the player's own back.
        // Each machine for its own camera.
        // Big Pixels (the trap table: the graphics lowered a while, then the player's own back): the game's URP asset
        // renders at PixelsScale of the screen (0.1, the least it takes: player, 2026-10-13) and is blown up by its nearest pixel (UpscalingFilterSelection.Point),
        // so the island turns to big square pixels; its own scale and filter back at the end. Each machine for its own
        // screen; set again should the game's settings put theirs back meanwhile.
        internal const string Pixels = "pixels";
        private const float PixelsScale = 0.1f;
        private static float _pixelsUntil = -1f;
        private static float _pixelsOwnScale;
        private static UnityEngine.Rendering.Universal.UpscalingFilterSelection _pixelsOwnFilter;

        private static UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset UrpAsset()
        {
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            return asset != null ? asset.TryCast<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>() : null;
        }

        private static void StartPixels(int seconds)
        {
            if (Gentle(Pixels))
                return;
            var asset = UrpAsset();
            if (asset == null)
            {
                Plugin.Log.LogWarning($"{Tag} Big Pixels: no URP asset.");
                return;
            }
            if (_pixelsUntil < 0f)
            {
                _pixelsOwnScale = asset.renderScale;
                _pixelsOwnFilter = asset.upscalingFilter;
            }
            _pixelsUntil = Mathf.Max(_pixelsUntil, Time.unscaledTime + seconds);
            HoldPixels(asset);
            Plugin.Log.LogInfo($"{Tag} Big Pixels: render scale {_pixelsOwnScale:F2} -> {PixelsScale:F2}, filter {_pixelsOwnFilter} -> Point.");
        }

        private static void HoldPixels(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset asset)
        {
            if (asset == null)
                return;
            if (asset.renderScale != PixelsScale)
                asset.renderScale = PixelsScale;
            if (asset.upscalingFilter != UnityEngine.Rendering.Universal.UpscalingFilterSelection.Point)
                asset.upscalingFilter = UnityEngine.Rendering.Universal.UpscalingFilterSelection.Point;
        }

        private static void EndPixels()
        {
            if (_pixelsUntil < 0f)
                return;
            _pixelsUntil = -1f;
            try
            {
                var asset = UrpAsset();
                if (asset != null)
                {
                    asset.renderScale = _pixelsOwnScale;
                    asset.upscalingFilter = _pixelsOwnFilter;
                }
                Plugin.Log.LogInfo($"{Tag} Big Pixels over.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Pixels end: {ex.Message}");
            }
        }

        // Big Head (the trap table: the players' heads grow): on each machine the other players' heads (the common parent
        // of their head's renderers: the head and the animated nose) grow HeadFactor times, eased in and out; the own
        // head is left alone (it is not seen in first person, and the camera may hang from it).
        internal const string Head = "head";
        private const float HeadFactor = 2.5f;
        private const float HeadEase = 1f;
        private static float _headStart = -1f;
        private static float _headUntil = -1f;
        private static readonly Dictionary<int, (Transform bone, Vector3 own, Vector3 ownPosition)> Heads = new();

        // Raised as it grows, so it sits on the body rather than sinking into it (player, 2026-10-13): metres per unit of growth.
        private const float HeadLift = 0.15f;

        private static Transform HeadBone(PlayerCharacter player)
        {
            var renderers = player.looks != null ? player.looks.headRenderers : null;
            if (renderers == null)
                return null;
            Transform common = null;
            foreach (var part in renderers)
            {
                var t = part != null && part.renderer != null ? part.renderer.transform : null;
                if (t == null)
                    continue;
                if (common == null)
                {
                    common = t.parent;
                    continue;
                }
                while (common != null && !t.IsChildOf(common))
                    common = common.parent;
            }
            return common != null && common != player.transform ? common : null;
        }

        private static void StartHead(int seconds)
        {
            var now = Time.unscaledTime;
            if (_headUntil < 0f)
                _headStart = now;
            _headUntil = Mathf.Max(_headUntil, now + seconds);
        }

        private static void StepHead(float now)
        {
            float amount;
            if (now < _headStart + HeadEase)
                amount = (now - _headStart) / HeadEase;
            else if (now < _headUntil)
                amount = 1f;
            else
                amount = 1f - (now - _headUntil) / HeadEase;
            if (amount <= 0f && now >= _headUntil)
            {
                EndHead();
                return;
            }
            var factor = Mathf.Lerp(1f, HeadFactor, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount)));
            _headFactor = factor;
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player == null || player.isLocalPlayer)
                    continue;
                var id = player.GetInstanceID();
                if (!Heads.TryGetValue(id, out var head) || head.bone == null)
                {
                    var bone = HeadBone(player);
                    if (bone == null)
                        continue;
                    head = (bone, bone.localScale, bone.localPosition);
                    Heads[id] = head;
                }
                head.bone.localScale = head.own * factor;
            }
        }

        private static float _headFactor = 1f;

        // Every frame after the animation (TrapRunner.LateUpdate), which places the head again each frame: the lift,
        // from where the animation put it.
        internal static void LateTick()
        {
            LateBrain();
            LateArms();
            if (_headUntil < 0f || _headFactor <= 1f)
                return;
            var lift = Vector3.up * (_headFactor - 1f) * HeadLift;
            foreach (var head in Heads.Values)
            {
                if (head.bone != null)
                    head.bone.position += lift;
            }
        }

        private static void EndHead()
        {
            if (_headUntil < 0f)
                return;
            _headUntil = -1f;
            _headStart = -1f;
            _headFactor = 1f;
            foreach (var head in Heads.Values)
            {
                if (head.bone == null)
                    continue;
                head.bone.localScale = head.own;
            }
            Heads.Clear();
            Plugin.Log.LogInfo($"{Tag} Big Head over.");
        }

        // Big Brain (the trap table: the players' heads become a buoy light and glow): on each machine the other players'
        // heads are hidden and a copy of the island's lamp (its body and its glowing halo, lit, with a light of its own)
        // hangs where the head is (the head's bone, as Big Head's), BrainSize across; the own head is left alone. Kept
        // hidden after the animation every frame (LateTick); the heads back and the lamps gone at the end.
        internal const string Brain = "brain";
        private const float BrainSize = 0.8f;
        // A little higher than the head's centre (player, 2026-10-13).
        private const float BrainLift = 0.1f;
        private const float BrainShine = 1.8f;
        private static readonly Color BrainGlow = new(1f, 0.8f, 0.45f, 1f);
        private static float _brainUntil = -1f;
        private static readonly Dictionary<int, (PlayerCharacter player, GameObject lamp)> Brains = new();

        private static void StartBrain(int seconds)
        {
            _brainUntil = Mathf.Max(_brainUntil, Time.unscaledTime + seconds);
            Plugin.Log.LogInfo($"{Tag} Big Brain for {seconds}s.");
        }

        // A lamp of the island to copy, a lit one first (player, 2026-10-13: lit makes more sense), its colours then.
        private static GameObject LampTemplate()
        {
            GameObject any = null;
            foreach (var prop in UnityEngine.Object.FindObjectsByType<Prop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop == null || !prop.gameObject.name.StartsWith("BuoyProp", StringComparison.Ordinal) || ReceivedItemSpawner.IsCosmeticClone(prop))
                    continue;
                foreach (var light in prop.GetComponentsInChildren<Light>(false))
                {
                    if (light != null && light.enabled && light.name == "bouyLight")
                        return prop.gameObject;
                }
                any ??= prop.gameObject;
            }
            return any;
        }

        // A copy of the lamp's meshes (the body and the halo, even when the lamp is off), centred, BrainSize across.
        private static GameObject MakeBrain(Transform bone)
        {
            var template = LampTemplate();
            if (template == null)
                return null;
            var source = template.transform;
            var lamp = new GameObject("AP Big Brain");
            var model = new GameObject("model");
            model.transform.SetParent(lamp.transform, false);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var parts = new List<Transform>();
            foreach (var renderer in template.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null || (renderer.name != "buoy" && renderer.name != "LobbyFlareMesh"))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    continue;
                var part = new GameObject(renderer.name);
                part.transform.SetParent(model.transform, false);
                var position = source.InverseTransformPoint(renderer.transform.position);
                var rotation = Quaternion.Inverse(source.rotation) * renderer.transform.rotation;
                var lossy = renderer.transform.lossyScale;
                var rootScale = source.lossyScale;
                var scale = new Vector3(lossy.x / rootScale.x, lossy.y / rootScale.y, lossy.z / rootScale.z);
                part.transform.localPosition = position;
                part.transform.localRotation = rotation;
                part.transform.localScale = scale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = part.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                // The halo glowing whatever the template's state.
                if (renderer.name == "LobbyFlareMesh")
                    block.SetColor("_MainColor", BrainGlow);
                // The body lit: its tint pushed past white so it shines (a lit lamp of the island could not be read,
                // 2026-10-13: the copies stayed the dull unlit colour).
                else
                    block.SetColor("_TintColor", block.GetColor("_TintColor") * BrainShine);
                copy.SetPropertyBlock(block);
                var bounds = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = position + rotation * Vector3.Scale(corner, scale);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                parts.Add(part.transform);
            }
            if (parts.Count == 0)
            {
                UnityEngine.Object.Destroy(lamp);
                return null;
            }
            var centre = (min + max) / 2f;
            foreach (var part in parts)
                part.localPosition -= centre;
            var extent = max - min;
            var size = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            model.transform.localScale = Vector3.one * (size > 0.0001f ? BrainSize / size : 1f);

            // Gentle effects (ModConfig.GentleEffects, player 2026-10-14): the lamp without its light on this screen.
            if (!ModConfig.GentleEffects.Value)
            {
                var light = lamp.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 6f;
                light.intensity = 2f;
                light.color = BrainGlow;
            }

            lamp.transform.SetParent(bone, false);
            lamp.transform.position = bone.position + Vector3.up * BrainLift;
            lamp.transform.rotation = Quaternion.identity;
            var boneScale = bone.lossyScale;
            lamp.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, boneScale.x), 1f / Mathf.Max(0.0001f, boneScale.y), 1f / Mathf.Max(0.0001f, boneScale.z));
            return lamp;
        }

        private static void SetHeadShown(PlayerCharacter player, bool shown)
        {
            var renderers = player != null && player.looks != null ? player.looks.headRenderers : null;
            if (renderers == null)
                return;
            foreach (var part in renderers)
            {
                if (part?.renderer != null && part.renderer.enabled != shown)
                    part.renderer.enabled = shown;
            }
        }

        private static void LateBrain()
        {
            if (_brainUntil < 0f)
                return;
            if (Time.unscaledTime >= _brainUntil)
            {
                EndBrain();
                return;
            }
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player == null || player.isLocalPlayer)
                    continue;
                var id = player.GetInstanceID();
                if (!Brains.TryGetValue(id, out var brain) || brain.lamp == null)
                {
                    var bone = HeadBone(player);
                    var lamp = bone != null ? MakeBrain(bone) : null;
                    if (lamp == null)
                        continue;
                    brain = (player, lamp);
                    Brains[id] = brain;
                }
                SetHeadShown(player, false);
            }
        }

        private static void EndBrain()
        {
            if (_brainUntil < 0f)
                return;
            _brainUntil = -1f;
            foreach (var brain in Brains.Values)
            {
                if (brain.lamp != null)
                    UnityEngine.Object.Destroy(brain.lamp);
                SetHeadShown(brain.player, true);
            }
            Brains.Clear();
            Plugin.Log.LogInfo($"{Tag} Big Brain over.");
        }

        // Big Arms (player, 2026-10-13: the ends of the arms, the hands, grow): every player's hands (the parts whose name
        // says hand) grow ArmsFactor times, eased in and out. Seen on the other players only: the first-person hands are
        // drawn in a way their size does not reach (tried in LateUpdate and after PlayerArms.UpdateScreenSpaceArms,
        // 2026-10-13; the player: leave it so).
        internal const string Arms = "arms";
        private const float ArmsFactor = 2.5f;
        private const float ArmsEase = 1f;
        private static float _armsStart = -1f;
        private static float _armsUntil = -1f;
        private static readonly Dictionary<int, List<(Transform hand, Vector3 own)>> Hands = new();
        private static bool _handsLogged;

        private static List<(Transform hand, Vector3 own)> HandsOf(PlayerCharacter player)
        {
            var hands = new List<(Transform, Vector3)>();
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                hands.Add((renderer.transform, renderer.transform.localScale));
            }
            if (!_handsLogged)
            {
                _handsLogged = true;
                var names = new List<string>();
                foreach (var (hand, _) in hands)
                    names.Add(hand.name);
                Plugin.Log.LogInfo($"{Tag} Big Arms: hands of {player.gameObject.name}: {(names.Count == 0 ? "none found" : string.Join(", ", names))}.");
            }
            return hands;
        }

        private static void StartArms(int seconds)
        {
            var now = Time.unscaledTime;
            if (_armsUntil < 0f)
                _armsStart = now;
            _armsUntil = Mathf.Max(_armsUntil, now + seconds);
        }

        private static void StepArms(float now)
        {
            float amount;
            if (now < _armsStart + ArmsEase)
                amount = (now - _armsStart) / ArmsEase;
            else if (now < _armsUntil)
                amount = 1f;
            else
                amount = 1f - (now - _armsUntil) / ArmsEase;
            if (amount <= 0f && now >= _armsUntil)
            {
                EndArms();
                return;
            }
            _armsFactor = Mathf.Lerp(1f, ArmsFactor, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount)));
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player == null)
                    continue;
                var id = player.GetInstanceID();
                if (!Hands.ContainsKey(id))
                    Hands[id] = HandsOf(player);
            }
        }

        private static float _armsFactor = 1f;

        // After the game has placed the arms for the frame (the first-person ones are set again every frame, 2026-10-13:
        // the own hands stayed small): the hands' size.
        private static void LateArms()
        {
            if (_armsUntil < 0f)
                return;
            foreach (var hands in Hands.Values)
                foreach (var (hand, own) in hands)
                {
                    if (hand != null)
                        hand.localScale = own * _armsFactor;
                }
        }

        private static void EndArms()
        {
            if (_armsUntil < 0f)
                return;
            _armsUntil = -1f;
            _armsStart = -1f;
            _armsFactor = 1f;
            foreach (var hands in Hands.Values)
                foreach (var (hand, own) in hands)
                {
                    if (hand != null)
                        hand.localScale = own;
                }
            Hands.Clear();
            Plugin.Log.LogInfo($"{Tag} Big Arms over.");
        }

        // Big Smell (the trap table; player, 2026-10-13: the others pushed away, the gas greenish): every player gives off
        // a green cloud, and on each machine the own player is pushed away from any other player nearer than SmellRadius
        // (each machine moves only its own player, so everyone is pushed apart). The clouds gone at the end.
        internal const string Smell = "smell";
        private const float SmellRadius = 5f;
        private const float SmellPush = 9f;
        // Added to the velocity the controls ask for (Patches/BigSmellPatch), so the game's own movement keeps the
        // player out of the walls (player, 2026-10-14: moving the body itself went through them).
        internal static Vector3 SmellVelocity;
        private static float _smellUntil = -1f;
        private static readonly Dictionary<int, GameObject> Clouds = new();

        private static void StartSmell(int seconds)
        {
            _smellUntil = Mathf.Max(_smellUntil, Time.unscaledTime + seconds);
            Plugin.Log.LogInfo($"{Tag} Big Smell for {seconds}s.");
        }

        // The cloud (player, 2026-10-13: the flare as the particle system): the look of a flare gun shot's smoke.
        private static bool _smellLogged;

        private static ParticleSystem SmellTemplate()
        {
            foreach (var effect in UnityEngine.Object.FindObjectsByType<PeckEffectParticleNetworked>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (effect != null && effect.name == "FlareGunFlare" && effect.targetParticleSystem != null)
                    return effect.targetParticleSystem;
            }
            return null;
        }

        // A copy of the shot kept still, its own playing off: its smoke system (the smoke's look) only gets the puffs
        // the mod emits itself, green, all around the player (2026-10-14: the shot's smoke comes from its flight, a
        // copy hung on a player gave next to none).
        private static GameObject MakeCloud(PlayerCharacter player)
        {
            var template = SmellTemplate();
            if (template == null)
                return null;
            var cloud = UnityEngine.Object.Instantiate(template.gameObject);
            cloud.name = "AP Big Smell";
            cloud.transform.SetParent(player.transform, false);
            cloud.transform.localPosition = Vector3.zero;
            var names = new List<string>();
            foreach (var system in cloud.GetComponentsInChildren<ParticleSystem>(true))
            {
                names.Add(system.name);
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            foreach (var light in cloud.GetComponentsInChildren<Light>(true))
                light.enabled = false;
            if (!_smellLogged)
            {
                _smellLogged = true;
                Plugin.Log.LogInfo($"{Tag} Big Smell: shot parts {string.Join(", ", names)}.");
            }
            return cloud;
        }

        private static ParticleSystem SmokeOf(GameObject cloud)
        {
            foreach (var system in cloud.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system.name == "SmokeParticleSystem")
                    return system;
            }
            return null;
        }

        private const float SmellPuffsPerSecond = 40f;
        private static readonly Dictionary<int, float> PuffDebt = new();

        private static void AnimateCloud(GameObject cloud, float now)
        {
            if (cloud == null)
                return;
            var smoke = SmokeOf(cloud);
            if (smoke == null)
                return;
            var id = cloud.GetInstanceID();
            PuffDebt.TryGetValue(id, out var debt);
            debt += SmellPuffsPerSecond * Time.deltaTime;
            var centre = cloud.transform.position + Vector3.up * 1f;
            while (debt >= 1f)
            {
                debt -= 1f;
                var offset = UnityEngine.Random.insideUnitSphere;
                offset.y *= 0.8f;
                // The interop keeps only the raw fields of EmitParams and Particle.
                var life = UnityEngine.Random.Range(1.5f, 2.5f);
                var particle = new ParticleSystem.Particle();
                particle.m_Position = centre + offset * 1.2f;
                particle.m_Velocity = offset * 0.4f + Vector3.up * 0.25f;
                particle.m_StartSize = Vector3.one * UnityEngine.Random.Range(1f, 2f);
                particle.m_Lifetime = life;
                particle.m_StartLifetime = life;
                particle.m_StartColor = Color.Lerp(new Color(0.35f, 0.6f, 0.1f, 0.6f), new Color(0.6f, 0.8f, 0.3f, 0.5f), UnityEngine.Random.value);
                var emit = new ParticleSystem.EmitParams();
                emit.m_Particle = particle;
                emit.m_PositionSet = emit.m_VelocitySet = emit.m_StartSizeSet = emit.m_StartLifetimeSet = emit.m_StartColorSet = true;
                emit.m_ApplyShapeToPosition = false;
                smoke.Emit(emit, 1);
            }
            PuffDebt[id] = debt;
        }

        private static void StepSmell(float now)
        {
            if (now >= _smellUntil)
            {
                EndSmell();
                return;
            }
            PlayerCharacter own = null;
            var players = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player == null)
                    continue;
                if (player.isLocalPlayer)
                    own = player;
                var id = player.GetInstanceID();
                // Gentle effects: no gas on this screen, the push stays.
                if (ModConfig.GentleEffects.Value)
                    continue;
                if (!Clouds.TryGetValue(id, out var cloud) || cloud == null)
                    Clouds[id] = cloud = MakeCloud(player);
                AnimateCloud(cloud, now);
            }
            SmellVelocity = Vector3.zero;
            if (own == null)
                return;
            var push = Vector3.zero;
            foreach (var player in players)
            {
                if (player == null || player == own)
                    continue;
                // The bodies, not the characters' roots, which do not follow them (2026-10-14: the players drew together).
                var away = BodyPosition(own) - BodyPosition(player);
                away.y = 0f;
                var distance = away.magnitude;
                if (distance >= SmellRadius)
                    continue;
                var direction = distance > 0.01f ? away / distance : Vector3.forward;
                push += direction * (1f - distance / SmellRadius);
            }
            push = Vector3.ClampMagnitude(push, 1f) * SmellPush;
            // The controls' velocity is in the player's own frame, the way the camera faces (2026-10-14: facing the
            // other player, the push drew in).
            var camera = Camera.main;
            if (camera != null)
                push = Quaternion.Inverse(Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f)) * push;
            SmellVelocity = push;
        }

        private static Vector3 BodyPosition(PlayerCharacter player)
        {
            var body = player.GetComponentInChildren<Rigidbody>();
            return body != null ? body.position : player.transform.position;
        }

        private static void EndSmell()
        {
            if (_smellUntil < 0f)
                return;
            _smellUntil = -1f;
            SmellVelocity = Vector3.zero;
            foreach (var cloud in Clouds.Values)
            {
                if (cloud != null)
                    UnityEngine.Object.Destroy(cloud);
            }
            Clouds.Clear();
            PuffDebt.Clear();
            Plugin.Log.LogInfo($"{Tag} Big Smell over.");
        }

        // Big Brother (player, 2026-10-13: every player small for a while, for real: body, camera and collisions; 2026-10-14:
        // with a transition): on each machine every player's body (the transform of its Rigidbody, which carries the
        // collider, the model and the head the camera hangs from) shrinks to BrotherFactor, eased in and out.
        internal const string Brother = "brother";
        private const float BrotherFactor = 0.4f;
        private const float BrotherEase = 2f;
        // The voices pitched up as the bodies shrink (player, 2026-10-14), through the game's own voice pitch
        // (Patches/BigBrotherVoicePatch).
        private const float BrotherVoiceRaise = 1.5f;
        internal static float BrotherVoicePitch { get; private set; } = 1f;
        private static float _brotherStart = -1f;
        private static float _brotherUntil = -1f;
        private static readonly Dictionary<int, (Transform body, Vector3 own)> Brothers = new();

        private static void StartBrother(int seconds)
        {
            var now = Time.unscaledTime;
            if (_brotherUntil < 0f)
                _brotherStart = now;
            _brotherUntil = Mathf.Max(_brotherUntil, now + seconds);
            Plugin.Log.LogInfo($"{Tag} Big Brother for {seconds}s.");
        }

        private static void StepBrother(float now)
        {
            float amount;
            if (now < _brotherStart + BrotherEase)
                amount = (now - _brotherStart) / BrotherEase;
            else if (now < _brotherUntil)
                amount = 1f;
            else
                amount = 1f - (now - _brotherUntil) / BrotherEase;
            if (amount <= 0f && now >= _brotherUntil)
            {
                EndBrother();
                return;
            }
            var eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
            var factor = Mathf.Lerp(1f, BrotherFactor, eased);
            BrotherVoicePitch = Mathf.Lerp(1f, BrotherVoiceRaise, eased);
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player == null)
                    continue;
                var id = player.GetInstanceID();
                if (!Brothers.TryGetValue(id, out var brother) || brother.body == null)
                {
                    var body = player.GetComponentInChildren<Rigidbody>();
                    if (body == null)
                        continue;
                    brother = (body.transform, body.transform.localScale);
                    Brothers[id] = brother;
                }
                brother.body.localScale = brother.own * factor;
            }
        }

        private static void EndBrother()
        {
            if (_brotherUntil < 0f)
                return;
            _brotherUntil = -1f;
            _brotherStart = -1f;
            BrotherVoicePitch = 1f;
            foreach (var brother in Brothers.Values)
            {
                if (brother.body != null)
                    brother.body.localScale = brother.own;
            }
            Brothers.Clear();
            Plugin.Log.LogInfo($"{Tag} Big Brother over.");
        }

        // Big Reset (the trap table's Big Slam, renamed by the player 2026-10-14): the nearest puzzle still holding its
        // gourd put back as it starts, host only (Core/Traps.FireReset).
        internal const string Reset = "reset";

        // Big Break (player, 2026-10-14: chairs spawned and the players sat in them): the host puts a folding chair
        // before every player (Core/Traps); each machine then sits its own player on the chair nearest the spot it
        // was told, as soon as that chair has reached it (a guest gets the spawn a moment later).
        internal const string Break = "break";
        private const float BreakWait = 5f;
        private static Vector3 _breakAt;
        private static float _breakUntil = -1f;

        private static void StartBreak(Vector3? chair)
        {
            if (chair == null)
                return;
            _breakAt = chair.Value;
            _breakUntil = Time.unscaledTime + BreakWait;
            Plugin.Log.LogInfo($"{Tag} Big Break: a chair at {_breakAt}.");
        }

        // The trap's chairs (clones, not the island's own) and their seats.
        private static List<PlayerPose> BreakSeats()
        {
            var seats = new List<PlayerPose>();
            foreach (var pose in UnityEngine.Object.FindObjectsByType<PlayerPose>(FindObjectsSortMode.None))
            {
                var prop = pose != null ? pose.parentProp : null;
                if (prop != null && prop.gameObject.name.StartsWith("foldingChair", StringComparison.Ordinal) && ReceivedItemSpawner.IsCosmeticClone(prop))
                    seats.Add(pose);
            }
            return seats;
        }

        private static PlayerPose NearestSeat(List<PlayerPose> seats, Vector3 at, float within)
        {
            PlayerPose seat = null;
            foreach (var pose in seats)
            {
                var distance = Vector3.Distance(pose.transform.position, at);
                if (distance < within)
                {
                    within = distance;
                    seat = pose;
                }
            }
            return seat;
        }

        // Every machine sits its own player on its chair, and also the other players on theirs, as it sees them: the
        // host sits everyone through the server (Core/Traps), but a guest did not show the others sat (2026-10-14).
        private static readonly HashSet<int> BreakSat = new();

        private static void StepBreak(float now)
        {
            if (now >= _breakUntil)
            {
                _breakUntil = -1f;
                BreakSat.Clear();
                return;
            }
            var seats = BreakSeats();
            if (seats.Count == 0)
                return;
            foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (player == null || BreakSat.Contains(player.GetInstanceID()))
                    continue;
                var poser = player.poser;
                if (poser == null)
                    continue;
                var seat = player.isLocalPlayer ? NearestSeat(seats, _breakAt, 3f) : NearestSeat(seats, BodyPosition(player), 3f);
                if (seat == null)
                    continue;
                BreakSat.Add(player.GetInstanceID());
                if (poser.currentPose != null && poser.currentPose.Pointer == seat.Pointer)
                    continue;
                poser.EnterPose(seat);
                Plugin.Log.LogInfo($"{Tag} Big Break: {player.gameObject.name} sat here on ticket {seat.ticket}.");
            }
        }

        internal const string Goggles = "goggles";
        private const float GogglesFactor = 0.45f;
        // Eased in and out over this long (player, 2026-10-13).
        private const float GogglesEase = 1.5f;
        private static float _gogglesStart = -1f;
        private static float _gogglesUntil = -1f;
        private static float _gogglesOwnFov;
        private static float _gogglesApplied;

        private static void StartGoggles(int seconds)
        {
            if (Gentle(Goggles))
                return;
            var now = Time.unscaledTime;
            if (_gogglesUntil < 0f)
            {
                _gogglesOwnFov = PlayerCameraMinder.fovFromSettings;
                _gogglesApplied = _gogglesOwnFov;
                _gogglesStart = now;
            }
            _gogglesUntil = Mathf.Max(_gogglesUntil, now + seconds);
            Plugin.Log.LogInfo($"{Tag} Big Goggles: field of view {_gogglesOwnFov:F0} towards {_gogglesOwnFov * GogglesFactor:F0}.");
        }

        // Every frame while it lasts: narrowing, held, then widening back; the player's own when it is over.
        private static void StepGoggles(float now)
        {
            // The player changed the setting meanwhile: that becomes their own.
            if (PlayerCameraMinder.fovFromSettings != _gogglesApplied)
                _gogglesOwnFov = PlayerCameraMinder.fovFromSettings / Mathf.Max(0.01f, _gogglesApplied / _gogglesOwnFov);

            float amount;
            if (now < _gogglesStart + GogglesEase)
                amount = (now - _gogglesStart) / GogglesEase;
            else if (now < _gogglesUntil)
                amount = 1f;
            else
                amount = 1f - (now - _gogglesUntil) / GogglesEase;
            if (amount <= 0f && now >= _gogglesUntil)
            {
                EndGoggles();
                return;
            }
            amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
            _gogglesApplied = _gogglesOwnFov * Mathf.Lerp(1f, GogglesFactor, amount);
            PlayerCameraMinder.fovFromSettings = _gogglesApplied;
        }

        private static void EndGoggles()
        {
            if (_gogglesUntil < 0f)
                return;
            _gogglesUntil = -1f;
            _gogglesStart = -1f;
            PlayerCameraMinder.fovFromSettings = _gogglesOwnFov;
            Plugin.Log.LogInfo($"{Tag} Big Goggles over: field of view {_gogglesOwnFov:F0}.");
        }

        // Big Air (a player's idea, 2026-10-13): a timed tomato in the hands, as for Big Trip, and when it pops the
        // player is thrown high up, their body pushed straight up for a while (HoldLaunch). Each machine
        // for its own player; the host keeps DeathLink quiet for the fall (Core/Traps).
        internal const string Air = "air";
        // Much higher (player, 2026-10-13): a push held ThrustSeconds at ThrustSpeed (the game caps a body near 60 m/s: the player asked for 4-5 s); never into a ceiling.
        private const float ThrustSpeed = 60f;
        private const float ThrustSeconds = 5f;
        private const float CeilingRoom = 2f;
        private static bool _countdownLaunch;

        // The colour Big Paint gives this part of this player, if any (Patches/BigPaintPatch swaps it in as the game
        // sets it: held from outside every frame, the mouth flickered as the game set it again, 2026-10-13).

        internal static bool TryPaintColour(PlayerLooks looks, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PlayerLooks.LooksRenderer> part, out PlayerLookSet.LookColor colour)
        {
            foreach (var (target, head, torso, legs) in Painted)
            {
                if (target == null || target.looks == null || part == null || target.looks.Pointer != looks.Pointer)
                    continue;
                if (looks.headRenderers != null && part.Pointer == looks.headRenderers.Pointer) { colour = head; return true; }
                if (looks.torsoRenderers != null && part.Pointer == looks.torsoRenderers.Pointer) { colour = torso; return true; }
                if (looks.legsRenderers != null && part.Pointer == looks.legsRenderers.Pointer) { colour = legs; return true; }
            }
            colour = default;
            return false;
        }

        private static void HoldPaint()
        {
            foreach (var (target, head, torso, legs) in Painted)
            {
                var looks = target != null ? target.looks : null;
                if (looks == null)
                    continue;
                looks.SetColor(looks.headRenderers, head);
                looks.SetColor(looks.torsoRenderers, torso);
                looks.SetColor(looks.legsRenderers, legs);
            }
        }

        private static void EndPaint()
        {
            if (Painted.Count == 0)
            {
                _paintUntil = -1f;
                return;
            }
            // Off first: the patch would swap the own colours put back below.
            _paintUntil = -1f;
            // Each player's own colours back, as they were before (player, 2026-10-13), then the game's own refresh.
            foreach (var (target, head, torso, legs) in OwnColours)
            {
                try
                {
                    var looks = target != null ? target.looks : null;
                    if (looks == null)
                        continue;
                    looks.SetColor(looks.headRenderers, head);
                    looks.SetColor(looks.torsoRenderers, torso);
                    looks.SetColor(looks.legsRenderers, legs);
                    looks.RefreshLook();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"{Tag} Big Paint end: {ex.Message}");
                }
            }
            Painted.Clear();
            OwnColours.Clear();
            _paintUntil = -1f;
            Plugin.Log.LogInfo($"{Tag} Big Paint over.");
        }

        // The game writes a player's whiteness only when it changes (2026-10-13: the white stayed): each player is
        // given back their own (0 outside the Gauntlet) at the end.
        private static void EndGhost()
        {
            if (_ghostUntil < 0f)
                return;
            _ghostUntil = -1f;
            try
            {
                foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
                {
                    if (player != null && player.looks != null)
                        player.looks.SetSpeechlessness(player.speechless != null ? player.speechless.speechlessness : 0f);
                }
                Plugin.Log.LogInfo($"{Tag} Big Ghost over.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Ghost end: {ex.Message}");
            }
        }

        // The three that are a boost, not a trap: they get the item sound, not the alert of a trap.
        internal static bool IsBonus(string key) => key == Day || key == Speed || key == Jump;

        // Every effect this build plays, so a key from a newer apworld is refused by name.
        internal static readonly string[] Known = { Drop, Throw, Trip, Meeting, Night, Sleep, Load, Mask, Flare, BigTime, Silence, Slide, Ghost, Paint, Air, Goggles, Pixels, Head, Brain, Arms, Smell, Brother, Reset, Break, Day, Speed, Jump, Knockout };

        // Gentle effects (ModConfig.GentleEffects, the accessibility setting; player 2026-10-14): the traps that are hard
        // to look at (Big Goggles' narrowing view, Big Pixels) are not shown on this machine's screen.
        private static bool Gentle(string key)
        {
            if (!ModConfig.GentleEffects.Value)
                return false;
            Plugin.Log.LogInfo($"{Tag} Gentle effects on this machine: {DisplayName(key)} is not shown.");
            return true;
        }

        internal static string DisplayName(string key)
        {
            if (key == Knockout)
                return "Knock-out";
            return string.IsNullOrEmpty(key) ? "?" : "Big " + char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        // How long past a full wind-up the throw goes: a second at most (player, 2026-10-05).
        private const float ThrowHoldSeconds = 1f;
        private const float SpeedFactor = 1.75f;
        private const float JumpFactor = 1.8f;
        private const float MidnightHour = 0f;
        private const float NoonHour = 12f;

        private const string Tag = "[" + nameof(TrapEffects) + "]";

        // --- throw ---
        private static PlayerCharacter _throwPlayer;
        private static float _throwAt = -1f;
        private static double _throwWindUpStart;
        private static bool _cancelRefusedLogged;

        // --- Big Speed, Big Jump, Big Sleep: the tunings, the player's own values, until when ---
        private static PlayerTunings _moveTunings;
        private static float[] _moveBase;
        private static float _speedUntil = -1f;
        private static float _jumpUntil = -1f;
        private static float _sleepUntil = -1f;
        private static float _sleepStart;
        private static PlayerCharacter _sleeper;

        // Big Sleep's rhythm (player, 2026-10-05): asleep and still this long, then awake this
        // long, again and again for `trap_duration`.
        private const float AsleepSeconds = 2f;
        private const float AwakeSeconds = 5f;

        // Lasting effects received before this machine's player existed, and until when.
        private static readonly Dictionary<string, float> PendingMovement = new();

        // --- night and day ---
        private const float FastForwardSeconds = 4f;
        private static bool _skyRunning;
        private static float _skyFrom;
        private static float _skyTo;
        private static float _skySpan;
        private static float _skyStart;

        // --- the masks some puzzles put on the screen (PeckEffectMask: binoculars, telescope,
        // blindfold), by instance, while on. Big Meeting and Big Trip leave a masked player alone:
        // they may be in that puzzle, and moving them out or others in could strand someone. ---
        private static readonly HashSet<int> ActiveMasks = new();

        internal static bool LocalMasked => ActiveMasks.Count > 0;

        internal static void NoteMask(PeckEffectMask mask, bool active)
        {
            if (mask == null)
                return;

            var id = mask.GetInstanceID();
            var changed = active ? ActiveMasks.Add(id) : ActiveMasks.Remove(id);
            if (changed)
                Plugin.Log.LogInfo($"{Tag} Screen mask {mask.maskType} {(active ? "on" : "off")} ('{mask.gameObject.name}'); {ActiveMasks.Count} on.");
        }

        // Plays an effect on this machine's player. `destination` is for trip and meeting.
        internal static void Play(string key, int seconds, Vector3? destination, float yaw)
        {
            var player = Debug.DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
            {
                // A guest told on joining, before its player exists: kept for when it does.
                if (key is Speed or Jump or Sleep)
                {
                    PendingMovement[key] = Time.unscaledTime + seconds;
                    Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: no local player yet; kept for {seconds}s.");
                }
                else
                {
                    Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: no local player, nothing played.");
                }

                return;
            }

            try
            {
                switch (key)
                {
                    case Drop:
                        DropHeld(player);
                        break;
                    case Slide:
                        StartSlide(seconds);
                        break;
                    case Ghost:
                        _ghostUntil = Time.unscaledTime + seconds;
                        break;
                    case Paint:
                        StartPaint(seconds);
                        break;
                    case Goggles:
                        StartGoggles(seconds);
                        break;
                    case Pixels:
                        StartPixels(seconds);
                        break;
                    case Head:
                        StartHead(seconds);
                        break;
                    case Brain:
                        StartBrain(seconds);
                        break;
                    case Arms:
                        StartArms(seconds);
                        break;
                    case Smell:
                        StartSmell(seconds);
                        break;
                    case Brother:
                        StartBrother(seconds);
                        break;
                    case Break:
                        StartBreak(destination);
                        break;
                    case Air:
                        StartCountdown(key, player, Vector3.zero, Quaternion.identity);
                        _countdownLaunch = true;
                        break;
                    case Silence:
                        StartSilence(player, seconds);
                        break;
                    case Flare:
                        BigFlare.Play();
                        break;
                    case Knockout:
                        DropHeld(player);
                        if (player.faller != null)
                            player.faller.TriggerFall();
                        break;
                    case Throw:
                        StartThrow(player);
                        break;
                    case Trip:
                    case Meeting:
                        if (destination != null)
                            StartCountdown(key, player, destination.Value, Quaternion.Euler(0f, yaw, 0f));
                        else
                            Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: no destination for this player (spared).");
                        break;
                    case Night:
                        StartSky(MidnightHour); // the same hour for everyone (player, 2026-10-07: no softer dusk)
                        break;
                    case Day:
                        StartSky(NoonHour);
                        break;
                    case Speed:
                    case Jump:
                    case Sleep:
                        StartMovement(player, seconds, key);
                        break;
                    default:
                        Plugin.Log.LogWarning($"{Tag} Unknown effect '{key}', ignored (a newer apworld than this mod?).");
                        return;
                }

                Plugin.Log.LogInfo($"{Tag} {DisplayName(key)} played ({seconds}s).");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {DisplayName(key)} failed: {ex.Message}");
            }
        }

        // Every frame: the throw that is due, and whatever has run its time.
        internal static void Tick()
        {
            var now = Time.unscaledTime;

            if (_throwAt >= 0f && now >= _throwAt)
                FinishThrow();
            else if (_throwAt >= 0f)
                HoldWindUp();

            if (PendingMovement.Count > 0)
                PlayPendingMovement(now);

            TickMovement(now);

            if (_slideUntil >= 0f && now >= _slideUntil)
                EndSlide();

            // The game puts each player's own colour back on its next frame.
            if (_ghostUntil >= 0f && now >= _ghostUntil)
                EndGhost();

            if (_paintUntil >= 0f && now >= _paintUntil)
                EndPaint();

            if (_gogglesUntil >= 0f)
                StepGoggles(now);

            if (_headUntil >= 0f)
                StepHead(now);

            if (_armsUntil >= 0f)
                StepArms(now);

            if (_smellUntil >= 0f)
                StepSmell(now);

            if (_brotherUntil >= 0f)
                StepBrother(now);

            if (_breakUntil >= 0f)
                StepBreak(now);

            if (_pixelsUntil >= 0f)
            {
                if (now >= _pixelsUntil)
                    EndPixels();
                else
                    HoldPixels(UrpAsset());
            }

            if (_silenceUntil >= 0f)
            {
                if (now >= _silenceUntil)
                    EndSilence();
                else
                    HoldSilence();
            }

            if (_skyRunning)
                StepSky();

            if (_countdownEnd >= 0f)
                StepCountdown();

            if (_launchUntil >= 0f)
                HoldLaunch();
        }

        // The world is going away: everything put back now, while the objects still exist.
        internal static void EndAll()
        {
            _throwAt = -1f;
            _throwPlayer = null;
            _speedUntil = _jumpUntil = _sleepUntil = -1f;
            SetAsleep(false);
            RestoreMovement();
            EndSlide();
            EndSilence();
            EndGhost();
            EndPaint();
            EndGoggles();
            EndPixels();
            EndHead();
            EndBrain();
            EndArms();
            EndSmell();
            EndBrother();
            _countdownEnd = -1f;
            _countdownPlayer = null;

            if (_skyRunning)
            {
                // Never leave the clock frozen part way.
                _skyRunning = false;
                try { SkyManager.ClearFixedTime(); }
                catch (Exception) { }
            }

            ActiveMasks.Clear();
        }

        internal static string Describe()
        {
            var now = Time.unscaledTime;
            return $"masked={LocalMasked} ({ActiveMasks.Count}), "
                   + $"throw in {(_throwAt >= 0f ? (_throwAt - now).ToString("F1") + "s" : "-")}, "
                   + $"speed {(_speedUntil >= 0f ? (_speedUntil - now).ToString("F0") + "s left" : "off")}, "
                   + $"jump {(_jumpUntil >= 0f ? (_jumpUntil - now).ToString("F0") + "s left" : "off")}, "
                   + $"sleep {(_sleepUntil >= 0f ? (_sleepUntil - now).ToString("F0") + "s left" : "off")}, "
                   + $"sky {(_skyRunning ? $"fast forward to {_skyTo:F2}h" : "free")}, "
                   + $"hour {SafeHour():F2}";
        }

        private static float SafeHour()
        {
            try
            {
                return SkyManager.GetCurrentTime();
            }
            catch (Exception)
            {
                return -1f;
            }
        }

        // ------------------------------------------------------------------
        // The wait before a teleport (player, 2026-10-05): three seconds, then the teleport. No
        // text on screen and no sound (player: a beep made in Unity was not heard in the game).
        // ------------------------------------------------------------------

        // The tomato is set going 0.3 s after it is handed over and pops 3 s later
        // (GadgetItemSpawner.TomatoSeconds); the teleport comes a second after the pop, so the pop
        // is heard where the player stood (player, 2026-10-06).
        private const float CountdownSeconds = 0.3f + GadgetItemSpawner.TomatoSeconds + 1f;
        private static float _countdownEnd = -1f;
        private static string _countdownName;
        private static PlayerCharacter _countdownPlayer;
        private static Vector3 _countdownDestination;
        private static Quaternion _countdownRotation;

        private static void StartCountdown(string key, PlayerCharacter player, Vector3 destination, Quaternion rotation)
        {
            _countdownName = DisplayName(key);
            _countdownPlayer = player;
            _countdownDestination = destination;
            _countdownRotation = rotation;
            _countdownEnd = Time.unscaledTime + CountdownSeconds;
            _countdownLaunch = false;
        }

        private static void StepCountdown()
        {
            if (Time.unscaledTime < _countdownEnd)
                return;

            _countdownEnd = -1f;
            var player = _countdownPlayer;
            _countdownPlayer = null;
            if (_countdownLaunch)
            {
                _countdownLaunch = false;
                Launch(player);
                return;
            }
            try
            {
                WorldButtons.TeleportTo(player, _countdownDestination, _countdownRotation);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {_countdownName}: the teleport failed: {ex.Message}");
            }
        }

        // The game holds a body's climb back (2026-10-13: 110 m/s went barely higher than 70), so the push is kept up a
        // while, as a rocket: the speed set again every frame for up to ThrustSeconds, the height its time, and it
        // stops CeilingRoom under a ceiling.
        private static Rigidbody _launchBody;
        private static float _launchUntil = -1f;

        private static void Launch(PlayerCharacter player)
        {
            try
            {
                var body = player != null ? player.GetComponentInChildren<Rigidbody>() : null;
                if (body == null)
                {
                    Plugin.Log.LogWarning($"{Tag} Big Air: no body to throw.");
                    return;
                }
                var reach = ThrustSpeed * ThrustSeconds;
                var from = body.position + Vector3.up * 1.5f;
                foreach (var hit in Physics.RaycastAll(new Ray(from, Vector3.up), reach + CeilingRoom))
                {
                    if (hit.collider == null || hit.collider.attachedRigidbody == body || hit.collider.transform.IsChildOf(player.transform))
                        continue;
                    reach = Mathf.Min(reach, Mathf.Max(0f, hit.distance - CeilingRoom));
                }
                _launchBody = body;
                _launchUntil = Time.unscaledTime + reach / ThrustSpeed;
                Plugin.Log.LogInfo($"{Tag} Big Air: up {reach:F0} m ({(reach < ThrustSpeed * ThrustSeconds ? "under a ceiling" : "open sky")}).");
                HoldLaunch();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Air failed: {ex.Message}");
            }
        }

        private static void HoldLaunch()
        {
            if (_launchBody == null || Time.unscaledTime >= _launchUntil)
            {
                _launchBody = null;
                _launchUntil = -1f;
                return;
            }
            var velocity = _launchBody.velocity;
            _launchBody.velocity = new Vector3(velocity.x, ThrustSpeed, velocity.z);
        }

        // ------------------------------------------------------------------
        // Hands
        // ------------------------------------------------------------------

        private static void StartSilence(PlayerCharacter player, int seconds)
        {
            if (_silenceUntil < 0f)
            {
                var networking = player.GetComponent<PlayerNetworking>();
                _silenceWasMuted = networking != null && networking.isMuted;
                _comms = UnityEngine.Object.FindObjectOfType<Dissonance.DissonanceComms>();
                _silenceCommsWasMuted = _comms != null && _comms.IsMuted;
            }

            _silenceUntil = Mathf.Max(_silenceUntil, Time.unscaledTime + seconds);
            HoldSilence();
            Plugin.Log.LogInfo($"{Tag} Big Silence: muted for {seconds}s (was muted: {_silenceWasMuted}, mic {_silenceCommsWasMuted}).");
        }

        private static void HoldSilence()
        {
            try
            {
                var player = Debug.DebugPlayerLookup.FindLocalPlayer();
                var networking = player != null ? player.GetComponent<PlayerNetworking>() : null;
                if (networking != null && !networking.isMuted)
                    networking.CmdSetMute(true);
                if (_comms == null)
                    _comms = UnityEngine.Object.FindObjectOfType<Dissonance.DissonanceComms>();
                if (_comms != null && !_comms.IsMuted)
                    _comms.IsMuted = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Silence: {ex.Message}");
            }
        }

        private static void EndSilence()
        {
            if (_silenceUntil < 0f)
                return;
            _silenceUntil = -1f;
            try
            {
                var player = Debug.DebugPlayerLookup.FindLocalPlayer();
                var networking = player != null ? player.GetComponent<PlayerNetworking>() : null;
                if (networking != null && networking.isMuted != _silenceWasMuted)
                    networking.CmdSetMute(_silenceWasMuted);
                if (_comms != null)
                    _comms.IsMuted = _silenceCommsWasMuted;
                Plugin.Log.LogInfo($"{Tag} Big Silence over.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Silence end: {ex.Message}");
            }
            _comms = null;
        }

        private static void StartSlide(int seconds)
        {
            _slideUntil = Mathf.Max(_slideUntil, Time.unscaledTime + seconds);
            Plugin.Log.LogInfo($"{Tag} Big Slide: the ground slips for {seconds}s (grip {SlideGrip}).");
        }

        private static void EndSlide()
        {
            if (_slideUntil < 0f)
                return;
            _slideUntil = -1f;
            Plugin.Log.LogInfo($"{Tag} Big Slide over.");
        }

        private static void DropHeld(PlayerCharacter player)
        {
            if (player.hands == null || !player.hands.isHoldingSomething)
            {
                Plugin.Log.LogInfo($"{Tag} Big Drop: hands already empty.");
                return;
            }

            player.decisions?.ClearWindUp();
            player.actions.ActionDropPropOrPlayer();
        }

        private static void StartThrow(PlayerCharacter player)
        {
            if (player.hands == null || !player.hands.isHoldingSomething)
            {
                Plugin.Log.LogInfo($"{Tag} Big Throw: nothing in the hands to throw.");
                return;
            }

            _throwWindUpStart = NetworkTime.time;
            TrapIsThrowing = true;
            try { player.decisions.SetWindUpStartTime(_throwWindUpStart); }
            finally { TrapIsThrowing = false; }
            _throwPlayer = player;
            _cancelRefusedLogged = false;
            var windUp = player.tunings != null ? Mathf.Max(0f, player.tunings.maxWindUpDuration) : 1f;
            _throwAt = Time.unscaledTime + windUp + ThrowHoldSeconds;
        }

        // The throw is forced (player, 2026-10-05): the game's cancel (right click) clears the
        // wind-up, and every frame until the throw it is put back as it was.
        private static void HoldWindUp()
        {
            var player = _throwPlayer;
            try
            {
                if (player == null || player.decisions == null || player.hands == null || !player.hands.isHoldingSomething)
                    return;

                if (player.decisions.localWindUpStartTime == _throwWindUpStart)
                    return;

                TrapIsThrowing = true;
                try { player.decisions.SetWindUpStartTime(_throwWindUpStart); }
                finally { TrapIsThrowing = false; }
                if (!_cancelRefusedLogged)
                {
                    _cancelRefusedLogged = true;
                    Plugin.Log.LogInfo($"{Tag} Big Throw: the wind-up was cancelled or changed; put back.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"{Tag} Big Throw: could not hold the wind-up ({ex.Message}).");
            }
        }

        // Big Throw works whatever lock_abilities says (player, 2026-10-13): the Throwing lock lets the trap's own
        // wind-up and throw through while this is set (Patches/AbilityLockPatches).
        internal static bool TrapIsThrowing { get; private set; }

        private static void FinishThrow()
        {
            var player = _throwPlayer;
            _throwAt = -1f;
            _throwPlayer = null;
            TrapIsThrowing = true;

            try
            {
                if (player == null || player.hands == null)
                    return;

                // Let go before the five seconds (or never picked up): the wind-up goes.
                if (!player.hands.isHoldingSomething)
                {
                    player.decisions?.ClearWindUp();
                    Plugin.Log.LogInfo($"{Tag} Big Throw: the hands were empty by the end of the wind-up.");
                    return;
                }

                player.actions.ActionDropPropOrPlayer();
                Plugin.Log.LogInfo($"{Tag} Big Throw: thrown.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Big Throw failed at the end of the wind-up: {ex.Message}");
            }
            finally
            {
                TrapIsThrowing = false;
            }
        }

        // ------------------------------------------------------------------
        // Movement: Big Speed, Big Jump, Big Sleep
        // ------------------------------------------------------------------

        // All three change the same PlayerTunings, so none of them scales and puts back on its
        // own: the player's own values are read once, and every frame while any is running the
        // tunings are written as those values times what is running (asleep beats faster). An
        // effect ending can then never leave a player faster, slower or stuck for good, whatever
        // order they come and go in. A second one of a kind lengthens it, never stacks.
        private static void StartMovement(PlayerCharacter player, int seconds, string key)
        {
            var tunings = player.tunings;
            if (tunings == null)
            {
                Plugin.Log.LogInfo($"{Tag} {DisplayName(key)}: the player has no tunings.");
                return;
            }

            if (_moveTunings != null && _moveTunings.Pointer != tunings.Pointer)
                RestoreMovement();

            if (_moveTunings == null)
            {
                _moveTunings = tunings;
                _moveBase = new[]
                {
                    tunings.forwardSpeed, tunings.forwardSprintSpeed, tunings.crouchForwardSpeed,
                    tunings.crouchForwardSprintSpeed, tunings.swimForwardSpeed, tunings.swimForwardSprintSpeed,
                    tunings.forwardSprintGhostSpeed, tunings.jumpForce, tunings.maxUpwardsVelocity,
                    tunings.mouseLookSpeed, tunings.stickLookSpeed,
                };
            }

            var until = Time.unscaledTime + seconds;
            switch (key)
            {
                case Speed:
                    _speedUntil = until;
                    break;
                case Jump:
                    _jumpUntil = until;
                    break;
                case Sleep:
                    if (_sleepUntil < 0f)
                        _sleepStart = Time.unscaledTime;
                    _sleepUntil = until;
                    _sleeper = player;
                    break;
            }

            ApplyMovement();
        }

        private static void PlayPendingMovement(float now)
        {
            var player = Debug.DebugPlayerLookup.FindLocalPlayer();
            if (player == null)
                return;

            foreach (var pair in new List<KeyValuePair<string, float>>(PendingMovement))
            {
                var left = Mathf.CeilToInt(pair.Value - now);
                if (left > 0)
                {
                    StartMovement(player, left, pair.Key);
                    Plugin.Log.LogInfo($"{Tag} {DisplayName(pair.Key)} played now that the player is here ({left}s left).");
                }
            }

            PendingMovement.Clear();
        }

        private static void TickMovement(float now)
        {
            if (_speedUntil >= 0f && now >= _speedUntil)
            {
                _speedUntil = -1f;
                Plugin.Log.LogInfo($"{Tag} Big Speed over.");
            }

            if (_jumpUntil >= 0f && now >= _jumpUntil)
            {
                _jumpUntil = -1f;
                Plugin.Log.LogInfo($"{Tag} Big Jump over.");
            }

            if (_sleepUntil >= 0f && now >= _sleepUntil)
            {
                _sleepUntil = -1f;
                SetAsleep(false);
                Plugin.Log.LogInfo($"{Tag} Big Sleep over.");
            }

            if (_moveTunings == null)
                return;

            if (_speedUntil < 0f && _jumpUntil < 0f && _sleepUntil < 0f)
                RestoreMovement();
            else
                ApplyMovement();
        }

        private static void ApplyMovement()
        {
            var t = _moveTunings;
            var b = _moveBase;
            if (t == null || b == null)
                return;

            try
            {
                // Asleep for AsleepSeconds out of every AsleepSeconds + AwakeSeconds of Big Sleep.
                var frozen = _sleepUntil >= 0f
                             && (Time.unscaledTime - _sleepStart) % (AsleepSeconds + AwakeSeconds) < AsleepSeconds;
                SetAsleep(frozen);
                var move = frozen ? 0f : _speedUntil >= 0f ? SpeedFactor : 1f;
                var jump = frozen ? 0f : _jumpUntil >= 0f ? JumpFactor : 1f;
                t.forwardSpeed = b[0] * move;
                t.forwardSprintSpeed = b[1] * move;
                t.crouchForwardSpeed = b[2] * move;
                t.crouchForwardSprintSpeed = b[3] * move;
                t.swimForwardSpeed = b[4] * move;
                t.swimForwardSprintSpeed = b[5] * move;
                t.forwardSprintGhostSpeed = b[6] * move;
                t.jumpForce = b[7] * jump;
                t.maxUpwardsVelocity = frozen ? b[8] : b[8] * jump;

                // Asleep, the camera does not move either (player, 2026-10-05: looking round woke
                // the walker up).
                t.mouseLookSpeed = frozen ? 0f : b[9];
                t.stickLookSpeed = frozen ? 0f : b[10];
            }
            catch (Exception ex)
            {
                // The player object went with its world.
                Plugin.Log.LogInfo($"{Tag} Movement effects dropped (the player is gone: {ex.Message}).");
                _moveTunings = null;
                _moveBase = null;
            }
        }

        // The game's own sleep (PlayerSleeper.forceSleeping): the walker nods off, as when left
        // alone, for the asleep part of Big Sleep.
        private static bool _asleep;

        // Whether Big Sleep has this machine's walker asleep right now (Patches/TrapPatches: no
        // crouching meanwhile either).
        internal static bool Asleep => _asleep;

        private static void SetAsleep(bool asleep)
        {
            if (_asleep == asleep)
                return;

            _asleep = asleep;
            try
            {
                if (_sleeper != null && _sleeper.sleeper != null)
                {
                    _sleeper.sleeper.forceSleeping = asleep;

                    // Nearly asleep at once, not after the game's slow nod-off, so the picture and
                    // the stillness come together (player, 2026-10-05). Not all the way: the game
                    // only moves the animation when the sleepiness changes from one frame to the
                    // next, so set to its target it played the sound and never the animation.
                    if (asleep)
                        _sleeper.sleeper.smoothSleepiness = Mathf.Max(_sleeper.sleeper.smoothSleepiness, 0.85f);
                    else
                        // forceSleeping zeroed the idle clock: without a fresh action the game
                        // would keep the walker asleep until a button is pressed.
                        _sleeper.sleeper.RecordAction();
                }
            }
            catch (Exception)
            {
                // The player went with its world.
            }

            if (!asleep && _sleepUntil < 0f)
                _sleeper = null;
        }

        private static void RestoreMovement()
        {
            var t = _moveTunings;
            var b = _moveBase;
            _moveTunings = null;
            _moveBase = null;
            if (t == null || b == null)
                return;

            try
            {
                t.forwardSpeed = b[0];
                t.forwardSprintSpeed = b[1];
                t.crouchForwardSpeed = b[2];
                t.crouchForwardSprintSpeed = b[3];
                t.swimForwardSpeed = b[4];
                t.swimForwardSprintSpeed = b[5];
                t.forwardSprintGhostSpeed = b[6];
                t.jumpForce = b[7];
                t.maxUpwardsVelocity = b[8];
                t.mouseLookSpeed = b[9];
                t.stickLookSpeed = b[10];
            }
            catch (Exception)
            {
                // The player object went with its world: nothing left to put back.
            }
        }

        // ------------------------------------------------------------------
        // Sky
        // ------------------------------------------------------------------

        // The clock fast-forwards to the hour (never backwards: it goes round to it), then runs on
        // from there. Nothing is put back afterwards: the day has moved (player, 2026-10-05).
        // A second one during the first starts again from wherever the sky has got to.
        private static void StartSky(float hour)
        {
            _skyFrom = SkyManager.GetCurrentTime();
            _skyTo = hour;
            _skySpan = Mathf.Repeat(hour - _skyFrom, 24f);
            _skyStart = Time.unscaledTime;
            _skyRunning = true;
            Plugin.Log.LogInfo($"{Tag} Sky: fast forward from {_skyFrom:F2}h to {hour:F2}h.");
        }

        private static void StepSky()
        {
            var t = Mathf.Clamp01((Time.unscaledTime - _skyStart) / FastForwardSeconds);
            try
            {
                // Eased, so the sun speeds up and settles rather than jerking.
                var eased = t * t * (3f - 2f * t);
                SkyManager.SetFixedTime(Mathf.Repeat(_skyFrom + _skySpan * eased, 24f));
                if (t < 1f)
                    return;

                _skyRunning = false;
                SkyManager.ClearFixedTime();
                Plugin.Log.LogInfo($"{Tag} Sky at {_skyTo:F2}h; the clock runs on from there.");
            }
            catch (Exception ex)
            {
                _skyRunning = false;
                Plugin.Log.LogInfo($"{Tag} Sky fast forward stopped (no sky now: {ex.Message}).");
            }
        }
    }
}
