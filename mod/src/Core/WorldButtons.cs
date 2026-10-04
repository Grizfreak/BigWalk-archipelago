using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Buttons the mod puts into the world (ROADMAP U4 and U10): a small green post with a
    // button on top, that does something when the local player looks at it and clicks.
    //
    // A PLAIN COPY OF THE GAME'S BUTTON DOES NOT WORK. The first try copied a
    // `BasicPushButton` as it was: the copy kept the original's `ShellReference`, the
    // reference the game routes every press by, so pressing it reached the ORIGINAL and the
    // player's arm reached for that one.
    //
    // WHAT THE GAME DOES INSTEAD FOR WHAT IT MAKES AT RUN TIME (decompiled 2026-10-03). A
    // scene object is found by its index in its `SeaShell`'s list. Anything with `useTicket`
    // is found by a `ticket` in `TicketOffice.tickets`, a plain dictionary:
    // `PeckSwitch.OnEnable` calls `AddTicket`, which stores the object under whatever
    // `ticket` it carries and never makes one up. So a copy made while switched off, given a
    // fresh ticket, `useTicket` and an empty `shellReference`, is registered as a button of
    // its own when it is switched on. The tickets come from a fixed table, so every machine
    // gives the same one to the same button and nothing has to be sent.
    //
    // The press is heard through a Harmony patch on `SetState` (Patches/WorldButtonPatch),
    // which also says who pressed, and the effect runs on that player's own machine: a
    // teleport is the one the mod already uses to call the loopback guest over
    // (`PlayerGrease.Teleport`), and a player's position belongs to its owner.
    //
    // If no push button can be copied, the old way is kept: the mod's own mesh copy, aimed
    // at with the camera and clicked with the game's "use" input.
    internal class WorldButtons : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public WorldButtons(IntPtr ptr) : base(ptr)
        {
        }

        private const float ReachMetres = 4f;
        private const float CheckEverySeconds = 0f;

        internal sealed class Button
        {
            internal int Slot;

            // The tower whose colour the plate takes, when it is not the one of the icon.
            internal string Tint;

            // True for a button whose effect belongs to the host's world (the resync, the gather
            // buttons, the Cabin Fever help): a guest's press runs on the host, not on the guest.
            internal bool HostSide;

            // The game prop it is a copy of: a push button, or a light switch (which stays where it
            // is put, on or off, and is pressed again to go back).
            internal string Template = PushButton;
            internal int LastState;
            internal string Label;
            internal Vector3 Position;
            internal Action<PlayerCharacter> OnPress;
            internal GameObject Root;

            // Where to aim: the middle of what is drawn, and how far from it the crosshair may
            // be and still count. Measured from the renderers, not guessed, since the look
            // differs between the game-button copy and the primitive fallback.
            internal Vector3 Centre;
            internal float Radius = 0.55f;
            internal Bounds Bounds;

            // True when this is a copy the game itself treats as a button, pressed through
            // its own system; false for the fallback, which the mod aims and clicks itself.
            internal bool Native;

            // The picture above the button, which also names the colour of its plate.
            internal string Icon;
        }

        // Tickets of the buttons made here: a block clear of the game's own, which count up from
        // the bottom, and a fixed stride per button so every machine agrees on every number.
        private const ushort FirstTicket = 0xE000;
        private const int TicketsPerButton = 8;

        // The states of the native buttons, by instance id, and the button each belongs to.
        private static readonly Dictionary<int, Button> NativeStates = new Dictionary<int, Button>();

        internal static bool HasNative => NativeStates.Count > 0;

        private static readonly List<Button> Buttons = new List<Button>();

        // The buttons by slot. A slot is a fixed place in the mod's table, so the tickets of its
        // button (Core/WorldButtons, FirstTicket) are the same on every machine whatever the
        // order buttons were made in, which differs: a button may appear on one machine first.
        private static readonly Dictionary<int, Button> Slots = new Dictionary<int, Button>();

        // False for a button whose world went away with it, so it gets made again.
        internal static bool Has(int slot) => Slots.TryGetValue(slot, out var button) && button.Root != null;

        // Takes a button out of the world again, and forgets its states.
        internal static void Remove(int slot)
        {
            if (!Slots.TryGetValue(slot, out var button))
                return;

            Slots.Remove(slot);
            Buttons.Remove(button);
            if (button.Root != null)
                UnityEngine.Object.Destroy(button.Root);

            var gone = new List<int>();
            foreach (var pair in NativeStates)
            {
                if (pair.Value == button)
                    gone.Add(pair.Key);
            }

            foreach (var id in gone)
                NativeStates.Remove(id);
        }

        // A world went away with all its objects: forget the buttons without touching them.
        internal static void Forget()
        {
            Slots.Clear();
            Buttons.Clear();
            NativeStates.Clear();
            _focused = null;
        }
        private static Material _material;
        private static Material _plateMaterial;
        private static Button _focused;

        // Puts a button in the world. The look is made at once, from whichever game material
        // can be found; a world reload destroys it, and the caller adds it again.
        internal static Button Add(int slot, string label, Vector3 position, Quaternion rotation,
            Action<PlayerCharacter> onPress, string icon = null, string tint = null, bool hostSide = false,
            string template = PushButton)
        {
            if (Slots.ContainsKey(slot))
                Remove(slot);

            var button = new Button { Slot = slot, Label = label, Position = position, OnPress = onPress, Icon = icon, Tint = tint, HostSide = hostSide, Template = template };
            button.Root = Build(position, rotation, slot, button);
            Slots[slot] = button;
            Measure(button);
            if (icon != null)
                AddIcon(button, icon);
            Buttons.Add(button);
            Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] '{label}' placed at {position}.");
            return button;
        }

        // The gap between the top of a button and the bottom of its icon, how big the icon is
        // drawn next to the panel's own, and how far it is sunk into the wall.
        private const float IconGap = 0.06f;
        private const float IconScale = 0.6f;
        private const float IconSink = 0.10f;

        // The icon of a tower above a button, copied from the Black Tower's panel of them
        // (`EndingGateIndicator`): its dark bezel, its red light and the silhouette named by
        // `tower` (red, green, blue, yellow, black or white), each rebuilt as a plain object with
        // the same mesh, material, place and size and none of the game's components. The panel
        // holds every silhouette and shows one, so any tower's can be had from any of its five.
        // Nothing is wired to anything: it is a picture, as an indicator is to the eye.
        private static void AddIcon(Button button, string tower)
        {
            try
            {
                var container = FindIndicatorContainer();
                if (container == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] No tower-icon panel is loaded to copy; '{button.Label}' has no icon.");
                    return;
                }

                var icon = new GameObject("AP world button icon");
                icon.transform.SetParent(button.Root.transform, false);
                icon.transform.localPosition = Vector3.zero;
                icon.transform.localRotation = Quaternion.identity;
                icon.transform.localScale = Vector3.one * IconScale;

                var made = 0;
                // Seven pictures. The four towers by their colour. "black" is the Black Tower's own
                // dark silhouette, and "gauntlet" a black sphere, the panel having no icon for it:
                // all on the panel's light bezel (the player's choice), where black is not lost as it
                // is on the dark one. "hub" is the beige silhouette (the panel's `tower_white`, beige
                // of its own).
                var gauntlet = tower == "gauntlet";
                var beige = tower == "hub";
                var silhouette = beige ? "white" : tower;
                var bezel = "EndingGateIndicator_base_white";
                var names = gauntlet
                    ? new[] { bezel, "EndingGateIndicator_light" }
                    : new[] { bezel, "EndingGateIndicator_light", "EndingGateIndicator_tower_" + silhouette };
                Renderer bezelCopy = null;
                foreach (var name in names)
                {
                    var source = container.Find(name);
                    var filter = source != null ? source.GetComponent<MeshFilter>() : null;
                    var renderer = source != null ? source.GetComponent<MeshRenderer>() : null;
                    if (filter == null || filter.sharedMesh == null || renderer == null)
                        continue;

                    var part = new GameObject(name);
                    part.transform.SetParent(icon.transform, false);
                    part.transform.localPosition = container.InverseTransformPoint(source.position);
                    part.transform.localRotation = Quaternion.Inverse(container.rotation) * source.rotation;
                    part.transform.localScale = source.localScale;
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var copy = part.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = renderer.sharedMaterials;

                    // The silhouettes' colour is not in their material: the game sets it on the panel
                    // while it runs, so a copy comes out dark and the green and blue ones vanish on
                    // the bezel. Each gets its own, and the black tower's is the beige the panel shows.
                    if (name == bezel)
                        bezelCopy = copy;

                    if (!beige && tower != "black" && name.EndsWith("_tower_" + silhouette, StringComparison.Ordinal))
                    {
                        var block = new MaterialPropertyBlock();
                        copy.GetPropertyBlock(block);
                        var colour = IconColour(tower);
                        block.SetColor("_TintColor", colour);
                        block.SetColor("_Color", colour);
                        block.SetColor("_BaseColor", colour);
                        copy.SetPropertyBlock(block);
                    }

                    made++;
                }

                if (gauntlet && bezelCopy != null)
                    AddSphere(icon, bezelCopy.bounds, button.Root.transform.forward);

                // Lifted by what it measures, not by a fixed amount: the icon is bigger than the
                // button, and a fixed lift left the button inside the symbol. Walls are upright, so
                // up is the world's.
                var drawn = false;
                var iconBounds = new Bounds(icon.transform.position, Vector3.zero);
                foreach (var renderer in icon.GetComponentsInChildren<Renderer>(true))
                {
                    if (!drawn)
                        iconBounds = renderer.bounds;
                    else
                        iconBounds.Encapsulate(renderer.bounds);
                    drawn = true;
                }

                if (drawn)
                    icon.transform.position += Vector3.up * (button.Bounds.max.y - iconBounds.min.y + IconGap);

                // Into the wall: the root faces out of it, so back is along its forward.
                icon.transform.position -= button.Root.transform.forward * IconSink;

                Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] Icon '{tower}' for '{button.Label}': {made} of 3 part(s) copied.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(WorldButtons)}] No icon for '{button.Label}': {ex.Message}");
            }
        }

        // A black sphere in front of the bezel's middle, about two fifths of its width.
        private static void AddSphere(GameObject icon, Bounds bezel, Vector3 outward)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "gauntlet sphere";
            sphere.transform.SetParent(icon.transform, false);
            var width = Mathf.Max(bezel.size.x, bezel.size.z);
            sphere.transform.localScale = Vector3.one * (width * 0.42f / Mathf.Max(0.0001f, icon.transform.lossyScale.x));
            // A little below the middle: centred it ran into the light at the top.
            sphere.transform.position = bezel.center + outward * (width * 0.04f) + Vector3.down * (width * 0.08f);
            Paint(sphere, new Color(0.03f, 0.03f, 0.04f));
        }

        private static Color IconColour(string tower)
        {
            switch (tower)
            {
                case "red": return new Color(0.80f, 0.22f, 0.18f);
                case "green": return new Color(0.14f, 0.68f, 0.34f);
                case "blue": return new Color(0.16f, 0.50f, 0.88f);
                case "yellow": return new Color(0.88f, 0.78f, 0.16f);
                case "black": return new Color(0.74f, 0.64f, 0.44f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        // The inner object of one indicator of the panel, the one that holds the bezel, the light
        // and the silhouettes as children.
        private static Transform FindIndicatorContainer()
        {
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer != null && renderer.gameObject.name == "EndingGateIndicator_base" && renderer.transform.parent != null
                    && renderer.transform.parent.Find("EndingGateIndicator_light") != null)
                    return renderer.transform.parent;
            }

            return null;
        }

        // The aim box of a button: everything drawn but the post, which is not the button.
        private static void Measure(Button button)
        {
            var any = false;
            var bounds = new Bounds(button.Position + Vector3.up * 1f, Vector3.zero);
            foreach (var renderer in button.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.gameObject.name == "post")
                    continue;

                if (!any)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
                any = true;
            }

            button.Bounds = bounds;
            button.Centre = any ? bounds.center : button.Position + Vector3.up * 1f;
            // A hand's width of slack around the drawn box, so a button is easy to aim at.
            button.Radius = Mathf.Max(0.45f, bounds.extents.magnitude + 0.2f);
        }

        internal static void Clear()
        {
            foreach (var button in Buttons)
            {
                if (button.Root != null)
                    UnityEngine.Object.Destroy(button.Root);
            }

            Buttons.Clear();
            Slots.Clear();
            NativeStates.Clear();
            _focused = null;
        }

        // The game's own teleport, on this machine's own player.
        // A teleport from a button waits this long: the button's click plays where it stands, and
        // a player sent away at once never heard it (2026-10-05).
        private const float TeleportDelay = 0.4f;

        private static PlayerCharacter _pendingPlayer;
        private static Vector3 _pendingDestination;
        private static Quaternion _pendingRotation;
        private static float _pendingAt = -1f;

        internal static void TeleportSoon(PlayerCharacter player, Vector3 destination, Quaternion rotation)
        {
            _pendingPlayer = player;
            _pendingDestination = destination;
            _pendingRotation = rotation;
            _pendingAt = Time.unscaledTime + TeleportDelay;
        }

        private static void RunPendingTeleport()
        {
            if (_pendingAt < 0f || Time.unscaledTime < _pendingAt)
                return;

            _pendingAt = -1f;
            TeleportTo(_pendingPlayer, _pendingDestination, _pendingRotation);
            _pendingPlayer = null;
        }

        internal static void TeleportTo(PlayerCharacter player, Vector3 destination, Quaternion rotation)
        {
            if (player == null || player.grease == null)
            {
                Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] No local player to move.");
                return;
            }

            var marker = new GameObject("World button destination");
            try
            {
                marker.transform.SetPositionAndRotation(destination, rotation);
                player.grease.Teleport(marker.transform);
                Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] Teleported to {destination}.");
            }
            finally
            {
                UnityEngine.Object.Destroy(marker);
            }
        }

        private void Update()
        {
            RunPendingTeleport();
            _focused = null;
            if (Buttons.Count == 0 || !WorldManager.isReadyForEffects)
                return;

            var player = Debug.DebugPlayerLookup.FindLocalPlayer();
            var camera = Camera.main;
            if (player == null || camera == null)
                return;

            // The button the crosshair is on, if any: the nearest one along the view ray
            // whose centre passes within a hand's width of it, and not past arm's reach.
            var origin = camera.transform.position;
            var direction = camera.transform.forward;
            var bestAlong = float.MaxValue;
            foreach (var button in Buttons)
            {
                var toCentre = button.Centre - origin;
                var along = Vector3.Dot(toCentre, direction);
                if (along < 0.3f || along > ReachMetres)
                    continue;

                var miss = (toCentre - direction * along).magnitude;
                if (miss > button.Radius || along >= bestAlong)
                    continue;

                bestAlong = along;
                _focused = button;
            }

            if (_focused != null && !_focused.Native && Pressed())
            {
                try
                {
                    _focused.OnPress?.Invoke(player);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"[{nameof(WorldButtons)}] '{_focused.Label}' failed: {ex.Message}");
                }
            }
        }

        // The game's own "use": the click. Its binding is the player's, whatever it is.
        private static bool Pressed()
        {
            return Rewired.ReInput.isReady
                   && Rewired.ReInput.players.GetPlayer(0).GetButtonDown(RewiredConsts.Action.use);
        }

        // The look of one of the game's own buttons, made of nothing of the game's but its
        // meshes and materials: each renderer of a push button is rebuilt as a fresh object
        // with the same mesh, material, place and size, and none of its components, so no
        // network reference or press logic comes along. The first version used two
        // primitives and was plainly ugly. Tinted green, so it is never taken for a real one.
        // Falls back to the primitives where no push button is loaded.
        private static GameObject Build(Vector3 position, Quaternion rotation, int index, Button button)
        {
            var root = new GameObject("AP world button");
            root.transform.SetPositionAndRotation(position, rotation);

            if (TryBuildNative(root, index, button))
            {
                button.Native = true;
                return root;
            }

            if (TryBuildFromGameButton(root))
            {
                AddPost(root);
                return root;
            }

            return BuildFromPrimitives(root);
        }

        // A copy of a game push button that the game accepts as a button of its own. Made
        // inside an inactive parent so nothing of it is registered while it still holds the
        // original's references, then given a ticket of its own, then switched on.
        private static bool TryBuildNative(GameObject root, int index, Button button)
        {
            var template = GetTemplate(button.Template);
            if (template == null)
                return false;

            var holder = new GameObject("AP button holder");
            holder.SetActive(false);
            try
            {
                var clone = UnityEngine.Object.Instantiate(template, holder.transform);
                clone.name = "AP native button";

                var ticket = FreeTicketBlock(index);
                var firstTicket = ticket;
                // A switch is found by its reference: a push button is pressed by the hand touching
                // it, but a switch used from afar (the light switch) is sent to the host as its
                // `shellReference` (PlayerActions.ActionUseWorldSwitch, decompiled 2026-10-05),
                // which the host resolves by ticket when it has one. Left empty, the press went to
                // some other switch: each copy refers to itself by its own ticket.
                foreach (var peckSwitch in clone.GetComponentsInChildren<PeckSwitch>(true))
                {
                    peckSwitch.useTicket = true;
                    peckSwitch.ticket = (ushort)ticket;
                    peckSwitch.shellReference = new SeaShell.ShellReference((ushort)ticket);
                    ticket++;
                }

                // A light switch's state is saved under the original's identity (SaveIdentity): the
                // copy would answer to the original's save and network entry, so presses on it went
                // to the switch in the hub. The copy gets none, and no lamp of its own either.
                foreach (var identity in clone.GetComponentsInChildren<SaveIdentity>(true))
                    UnityEngine.Object.DestroyImmediate(identity);

                foreach (var lamp in clone.GetComponentsInChildren<Light>(true))
                    UnityEngine.Object.DestroyImmediate(lamp.gameObject);

                TrackedPeckState own = null;
                foreach (var state in clone.GetComponentsInChildren<TrackedPeckState>(true))
                {
                    state.saveIdentity = null;
                    state.savableSystem = SavableSystem.NotSavable;
                    // Its own ticket as well as a reference to it: OnEnable registers the state
                    // under `ticket`, which the copy had from the original, so the registration
                    // failed ("Duplicate ticket" in Player.log) and a press could not find it.
                    state.ticket = (ushort)ticket;
                    state.shellReference = new SeaShell.ShellReference((ushort)ticket);
                    ticket++;
                    NativeStates[state.GetInstanceID()] = button;
                    own ??= state;
                }

                // A switch that drives a state outside the prop copied (the light switch drives
                // the one beside it, not its own) would still drive the original: it is pointed at
                // the copy's own state instead.
                foreach (var peckSwitch in clone.GetComponentsInChildren<PeckSwitch>(true))
                {
                    var target = peckSwitch.trackedStateSystem;
                    if (own != null && (target == null || !target.transform.IsChildOf(clone.transform)))
                        peckSwitch.trackedStateSystem = own;
                }

                clone.transform.SetParent(root.transform, false);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                // A push button's own plate is hidden (ReplacePlate), and the effect that changes its
                // material while it is held down then failed on every press ("had error in effect"
                // in Player.log) and lit up whatever shared that material. Taken off before the copy
                // wakes up, so its state never counts it among its effects.
                if (button.Template == PushButton)
                {
                    foreach (var effect in clone.GetComponentsInChildren<PeckEffectMaterialProperty>(true))
                        UnityEngine.Object.DestroyImmediate(effect);
                }

                clone.SetActive(true);
                if (button.Template == PushButton)
                {
                    ReplacePlate(clone, PlateColour(button.Tint ?? button.Icon));

                }

                Plugin.Log.LogInfo(
                    $"[{nameof(WorldButtons)}] '{template.name}' copied as a native button, tickets {firstTicket}..{ticket - 1}.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(WorldButtons)}] The native copy failed ({ex.Message}); using the mod's own button.");
                return false;
            }
            finally
            {
                UnityEngine.Object.Destroy(holder);
            }
        }

        // The colour of a button's plate: the colour of the tower it stands for, so the button
        // says where it goes before its picture is read. The Black Tower's is a dark grey and the
        // Gauntlet's black, the hub's the beige of its silhouette; with no picture, green.
        private static Color PlateColour(string icon)
        {
            switch (icon)
            {
                case "red": return new Color(0.80f, 0.22f, 0.18f);
                case "green": return new Color(0.14f, 0.68f, 0.34f);
                case "blue": return new Color(0.16f, 0.50f, 0.88f);
                case "yellow": return new Color(0.88f, 0.78f, 0.16f);
                case "black": return new Color(0.16f, 0.16f, 0.19f);
                case "hub": return new Color(0.74f, 0.64f, 0.44f);
                case "gauntlet": return new Color(0.04f, 0.04f, 0.05f);
                default: return new Color(0.23f, 0.81f, 0.39f);
            }
        }

        // The game's plate is yellow in the mesh itself and a tint only multiplies it, so no blue
        // can be had from it (the same wall the big keys met). It is hidden and a plain slab of the
        // same size and place stands in front of where it was: nothing of the button's own works
        // through the plate, so its interaction is untouched, and the knob stays the game's.
        private static void ReplacePlate(GameObject target, Color colour)
        {
            Renderer plate = null;
            var biggest = 0f;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var size = renderer.bounds.size.sqrMagnitude;
                if (size > biggest)
                {
                    biggest = size;
                    plate = renderer;
                }
            }

            var filter = plate != null ? plate.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return;

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "AP button plate";
            slab.transform.SetParent(target.transform, false);
            slab.transform.rotation = plate.transform.rotation;
            slab.transform.position = plate.transform.TransformPoint(filter.sharedMesh.bounds.center);
            slab.transform.localScale = Vector3.Scale(filter.sharedMesh.bounds.size, plate.transform.lossyScale);
            // The game's own plate material, not whichever vertex-colour material the scene lists
            // first, which changes from run to run and was sometimes one that glows.
            // A copy of it: the game animates the button's material while it is held down, and
            // every slab sharing it (the signs too) lit up with it (2026-10-05).
            if (plate.sharedMaterial != null && _plateMaterial == null)
                _plateMaterial = new Material(plate.sharedMaterial);
            if (_plateMaterial != null)
                _material = _plateMaterial;

            Paint(slab, colour);
            plate.enabled = false;
            Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] Plate '{plate.gameObject.name}' replaced by a {colour} slab.");
        }

        // The slots a table can have, so two slots never share a block of tickets.
        private const int MaxSlots = 32;

        // The first ticket of a block of the button's own tickets that nothing in the game holds.
        // The game's scene objects carry tickets too, and one of the blocks was one of theirs:
        // a state of a button then resolved to a switch and every press threw. The next block of
        // the same slot is taken instead, which is the same on every machine because the scene
        // is, and never one of another slot's.
        private static int FreeTicketBlock(int slot)
        {
            var office = LobbyNetworking.TicketOffice.instance;
            for (var round = 0; round < 64; round++)
            {
                var first = FirstTicket + (slot + round * MaxSlots) * TicketsPerButton;
                if (first + TicketsPerButton > ushort.MaxValue)
                    break;

                var free = true;
                for (var i = 0; i < TicketsPerButton && free; i++)
                    free = office == null || !office.tickets.ContainsKey((ushort)(first + i));

                if (free)
                {
                    if (round > 0)
                        Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] Slot {slot}: tickets taken in the game, using block {round}.");
                    return first;
                }
            }

            return FirstTicket + slot * TicketsPerButton;
        }

        // Called by Patches/WorldButtonPatch whenever a TrackedPeckState is set: when it is one
        // of the native buttons', pressed, by the player of THIS machine, its effect runs here.
        // Another machine's player runs it on their own machine, where the same press arrives.
        internal static void OnState(TrackedPeckState state, PeckContext context)
        {
            if (!NativeStates.TryGetValue(state.GetInstanceID(), out var button))
                return;

            // A push button acts when it goes down; a light switch on every flip, either way.
            var flipped = context.state != button.LastState;
            button.LastState = context.state;
            if (button.Template == LightSwitch ? !flipped : context.state < 1)
                return;

            var who = context.playerIdentity;
            Plugin.Log.LogInfo(
                $"[{nameof(WorldButtons)}] '{button.Label}' pressed (state {context.state}) by "
                + $"'{(who != null ? who.name : "<nobody>")}', local={(who != null && who.isLocalPlayer)}.");
            // A guest's press reaches the host's copy of the button. What acts on the host's world
            // runs here; what moves the player (a teleport) is sent to the guest's own machine.
            if (who != null && !who.isLocalPlayer && Mirror.NetworkServer.active && button.HostSide)
            {
                RunPress(button, who.GetComponent<PlayerCharacter>());
                return;
            }

            if (who != null && !who.isLocalPlayer && Mirror.NetworkServer.active)
            {
                Net.ModChannel.SendPress(who.connectionToClient, button.Slot);
                return;
            }

            if (who == null || !who.isLocalPlayer)
                return;

            RunPress(button);
        }

        // On a guest: the host says its player pressed the button in this slot.
        internal static void RunPress(int slot)
        {
            if (Slots.TryGetValue(slot, out var button))
                RunPress(button);
        }

        private static void RunPress(Button button, PlayerCharacter presser = null)
        {
            try
            {
                button.OnPress?.Invoke(presser != null ? presser : Debug.DebugPlayerLookup.FindLocalPlayer());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(WorldButtons)}] '{button.Label}' failed: {ex.Message}");
            }
        }

        // The button sits on a post at about the height of a hand, as a mounted one does.
        private const float PostHeight = 1.0f;

        private static void AddPost(GameObject root)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, PostHeight * 0.5f - 0.1f, 0f);
            post.transform.localScale = new Vector3(0.14f, PostHeight * 0.5f - 0.05f, 0.14f);
            Paint(post, new Color(0.12f, 0.2f, 0.17f));
        }

        private static bool TryBuildFromGameButton(GameObject root)
        {
            var template = FindPushButton(root.transform.position);
            if (template == null)
                return false;

            var made = 0;
            foreach (var source in template.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = source != null ? source.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var part = new GameObject(source.gameObject.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = template.transform.InverseTransformPoint(source.transform.position)
                                               + Vector3.up * PostHeight;
                part.transform.localRotation = Quaternion.Inverse(template.transform.rotation) * source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;

                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_TintColor", new Color(0.23f, 0.81f, 0.39f));
                block.SetColor("_Color", new Color(0.23f, 0.81f, 0.39f));
                block.SetColor("_BaseColor", new Color(0.23f, 0.81f, 0.39f));
                renderer.SetPropertyBlock(block);
                made++;
            }

            return made > 0;
        }

        // Where the template is looked for: the hub, whose buttons are the plain ones. A tower's own
        // button is a different thing (its colour and parts are not the hub's), and a copy of it
        // came out wholly green or wholly wrong.
        private static readonly Vector3 TemplateNear = new Vector3(-219.29f, 33.12f, -521.17f);
        private const float TemplateReach = 40f;

        internal const string PushButton = "BasicPushButton";
        internal const string LightSwitch = "BasicLightSwitch";

        private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();

        // The state of a light switch made here, to set it from the mod (its own sets are not
        // presses: they carry no player).
        internal static TrackedPeckState StateOf(int slot)
        {
            if (!Slots.TryGetValue(slot, out var button) || button.Root == null)
                return null;

            foreach (var state in button.Root.GetComponentsInChildren<TrackedPeckState>(true))
            {
                if (state != null && NativeStates.ContainsKey(state.GetInstanceID()))
                    return state;
            }

            return null;
        }

        // The prop every native copy of a kind is made from, the same one everywhere: a
        // switched-off copy kept for the whole session, so it survives the world that held the
        // original (the hub's: a push button by the inventory zone, a light switch in its teaching
        // area). It is only kept when the original is the hub's; otherwise it is used once.
        private static GameObject GetTemplate(string kind)
        {
            if (Templates.TryGetValue(kind, out var kept) && kept != null)
                return kept;

            var found = FindPushButton(TemplateNear, kind);
            if (found == null)
                return null;

            if ((found.transform.position - TemplateNear).magnitude > TemplateReach)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(WorldButtons)}] No hub push button loaded; '{found.name}' at {found.transform.position} used once.");
                return found;
            }

            var holder = new GameObject("AP template holder");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            var template = UnityEngine.Object.Instantiate(found, holder.transform);
            template.name = "AP template";
            Templates[kind] = template;
            Plugin.Log.LogInfo(
                $"[{nameof(WorldButtons)}] Template kept: '{found.name}' at {found.transform.position}.");
            Describe(found);
            return template;
        }

        // What a prop is made of, for the log: every object under it with its components, where
        // each switch points and how, and the object above it. To learn a prop before copying it.
        private static void Describe(GameObject prop)
        {
            try
            {
                var parent = prop.transform.parent;
                Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] '{prop.name}' under '{(parent != null ? parent.name : "-")}':");
                if (parent != null)
                {
                    foreach (var component in parent.GetComponents<Component>())
                        Plugin.Log.LogInfo($"[{nameof(WorldButtons)}]   parent has {component.GetIl2CppType().Name}");
                }

                foreach (var t in prop.GetComponentsInChildren<Transform>(true))
                {
                    var names = new List<string>();
                    foreach (var component in t.GetComponents<Component>())
                        names.Add(component.GetIl2CppType().Name);
                    Plugin.Log.LogInfo($"[{nameof(WorldButtons)}]   {t.name}: {string.Join(", ", names)}");

                    var peckSwitch = t.GetComponent<PeckSwitch>();
                    if (peckSwitch != null)
                    {
                        var target = peckSwitch.trackedStateSystem;
                        Plugin.Log.LogInfo(
                            $"[{nameof(WorldButtons)}]     switch -> '{(target != null ? target.name + " under " + (target.transform.parent != null ? target.transform.parent.name : "-") : "<none>")}', "
                            + $"mode {peckSwitch.stateMode}, specific {peckSwitch.specificState}, wrap {peckSwitch.wrapTotal}, up {(peckSwitch.upSwitch != null ? peckSwitch.upSwitch.name : "-")}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(WorldButtons)}] Could not describe '{prop.name}': {ex.Message}");
            }
        }

        // The nearest loaded prop of a kind (a push button by default), by the name of its root,
        // that is switched on.
        private static GameObject FindPushButton(Vector3 near, string kind = PushButton)
        {
            GameObject best = null;
            var bestDistance = float.MaxValue;
            foreach (var peckSwitch in UnityEngine.Object.FindObjectsByType<PeckSwitch>(FindObjectsSortMode.None))
            {
                if (peckSwitch == null)
                    continue;

                GameObject root = null;
                var ours = false;
                for (var t = peckSwitch.transform; t != null; t = t.parent)
                {
                    if (t.name.StartsWith(kind, StringComparison.Ordinal))
                        root = t.gameObject;

                    // One of this mod's own buttons: its parts are named like the game's, but
                    // copying one would copy tickets already registered.
                    if (t.name.StartsWith("AP ", StringComparison.Ordinal))
                        ours = true;
                }

                if (ours || root == null || !root.activeInHierarchy)
                    continue;

                var distance = (root.transform.position - near).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = root;
                }
            }

            return best;
        }

        // Two primitives: a post and a flat green cap. No collider on either, so nothing can
        // catch on them and nothing of the game's physics is involved.
        private static GameObject BuildFromPrimitives(GameObject root)
        {

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            post.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "cap";
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            cap.transform.localScale = new Vector3(0.28f, 0.06f, 0.28f);

            Paint(post, new Color(0.16f, 0.28f, 0.24f));
            Paint(cap, new Color(0.23f, 0.81f, 0.39f));
            return root;
        }

        // Paints a button's plate again, in the colour of another tower, without making the button
        // again: a button made anew flashes and loses the press it is in the middle of.
        internal static void Recolour(int slot, string tint)
        {
            if (!Slots.TryGetValue(slot, out var button) || button.Root == null)
                return;

            button.Tint = tint;
            foreach (var renderer in button.Root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.gameObject.name == "AP button plate")
                    Paint(renderer.gameObject, PlateColour(tint));
            }
        }

        // A plain slab in a colour, in the game's own material, with nothing to collide with: the
        // signs of the resync stations (Core/ResyncStations).
        internal static GameObject MakeSlab(string name, Vector3 position, Quaternion rotation, Vector3 size, Color colour)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetPositionAndRotation(position, rotation);
            slab.transform.localScale = size;
            Paint(slab, colour);
            return slab;
        }

        private static void Paint(GameObject part, Color colour)
        {
            var collider = part.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var material = FindGameMaterial();
            if (material != null)
                renderer.sharedMaterial = material;

            // The game's vertex-colour shader multiplies this over the mesh's own colour,
            // which a primitive has none of, so the tint is the colour that shows.
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_TintColor", colour);
            block.SetColor("_Color", colour);
            block.SetColor("_BaseColor", colour);
            renderer.SetPropertyBlock(block);
        }

        // A material the game renders with, since the engine's default one may not have
        // been kept in the build and would come out pink. Found once, on any renderer that
        // uses the game's vertex-colour shader.
        private static Material FindGameMaterial()
        {
            if (_material != null)
                return _material;

            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null && material.shader != null && material.shader.name.Contains("VertexColors"))
                {
                    _material = material;
                    Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] Borrowing material '{material.name}' ({material.shader.name}).");
                    return _material;
                }
            }

            Plugin.Log.LogInfo($"[{nameof(WorldButtons)}] No game material found; the engine's default will be used.");
            return null;
        }
    }
}
