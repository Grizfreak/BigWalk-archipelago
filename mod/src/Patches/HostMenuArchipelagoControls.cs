using System;
using BigWalkArchipelago.Core;
using BigWalkArchipelago.Core.Net;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BigWalkArchipelago.Patches
{
    // The two buttons the hosting screen grew on 2026-09-22, at the player's
    // request: a switch for whether this session talks to Archipelago at
    // all, and a way to find out whether the details are right BEFORE
    // committing to a save.
    //
    // Both existed already, invisibly. The switch was `Archipelago/Enabled`
    // in the BepInEx .cfg — a file nobody opens mid-session — and the test
    // was run by HostMenuConfirmStartPatch on the first press of Continue,
    // reporting a failure by flashing a field and writing the reason to a
    // log. Neither was discoverable, which is the whole problem being fixed
    // here.
    //
    // THE RULE, unchanged and deliberately so: a failed test must never stop
    // anyone hosting (player decision, 2026-09-22). The button informs; it
    // does not gate. An Archipelago server being down is not a reason to be
    // unable to launch the game, and a bug in this file must not become one
    // either — hence the try/catch around every injection, each of which
    // degrades to "that button is not there" rather than to a dead menu.
    //
    // Cloned from ContinueButton rather than built from scratch, the same
    // reasoning as the host:port field being cloned from GameName: the
    // button on this screen already carries the game's styling, its hover
    // handling and its ManagedButton wiring. Positions are relative to that
    // button, never absolute — the one measurement this file does not have
    // is where anything is, and an offset from a widget we can see survives
    // a layout it cannot.
    internal static class HostMenuArchipelagoControls
    {
        private const string ToggleName = "ApArchipelagoToggle";
        private const string TestName = "ApTestButton";
        private const string ResultName = "ApTestResult";

        // MEASURED on screen 2026-09-22, with the M dump (DebugMenuLookup) on
        // the live hosting screen, after a first attempt stacked the buttons
        // straight up from Continue and put them on top of the two fields.
        //
        // GameSlotCard_Editable is 1139.59 x 468.76 and lays its children out
        // by hand in two columns: GameName (154.30, -68.50) and Password
        // (154.30, -215.05) on the left, ApHostPortRow (643.70, -68.50) and
        // LastPlayed (643.70, -215.05) on the right. So the columns are
        // 489.40 apart and the rows 146.55 — and those are the numbers, not
        // an estimate.
        //
        // Continue (PlayButton, 300 x 103.57) does NOT share their anchor,
        // which is why its anchoredPosition of (154.30, -124.30) reads as
        // sitting between two rows while it renders below both. Its clones
        // inherit its anchor, so they are placed by offset FROM it and never
        // in the card's coordinates. One step up from Continue lands on the
        // Password/LastPlayed line: measured, not derived.
        private const float ColumnOffsetX = 489.40f;
        private const float SpacingFactor = 1.25f;

        // The toggle does not get the column, because the game already put
        // DeleteButton ("SUPPRIMER") on that line. Where exactly took three
        // attempts and an instrumented dump to establish, and the reason is
        // worth keeping: **DeleteButton's pivot is (1, 0.5)**. Its
        // anchoredPosition.x of 473 is its RIGHT edge, not its centre, so it
        // lives at the far right of the card and not in the middle, which is
        // where every calculation from anchoredPosition alone put it. Moving
        // the toggle rightwards to dodge it moved the toggle into it.
        //
        // MEASURED in world units on 2026-09-22 (DebugMenuLookup now prints
        // a world span per element precisely so this stops being guesswork):
        //
        //     Continuer      1076 .. 1476
        //     free           1476 .. 1861      <- 385 world units
        //     SUPPRIMER      1861 .. 2261
        //
        // The canvas runs at 4/3, so 385 world units are 288 anchored ones:
        // a 300-wide button genuinely does not fit, which is the other half
        // of why the first attempt overlapped. 270 leaves about 12 world
        // units of air either side, and sits centred in the gap.
        // Centred in that gap it still read as crowding Continue (player,
        // 2026-09-22: "better, too close to Continuer, but better"), and
        // there are only 25 world units of slack to redistribute. So the
        // button narrows again to buy room and is pushed right, leaving
        // about 45 world units of air on the Continue side and 20 on the
        // SUPPRIMER side.
        //
        // The font comes down with it, and that matters more than the
        // rectangle: the label is cloned from Continue's, which is large and
        // serif, and TMP draws it past the edges of its box. Narrowing the
        // box alone would have moved nothing the eye can see.
        // Nudged right once more on sight (player, 2026-09-22), which is
        // about as far as it goes: this leaves ~53 world units of air on the
        // Continue side and ~11 on the SUPPRIMER side, and the label draws
        // past its own box, so the remaining gap is smaller than the numbers
        // suggest.
        private const float ToggleOffsetX = 340.00f;
        private const float ToggleWidth = 240f;
        private const float ToggleFontScale = 0.62f;

        // Below TEST CONNECTION and above SUPPRIMER, in the 88 world units
        // between them — it is the test button's answer, so it belongs under
        // it rather than under the toggle.
        private const float ResultOffsetFactor = 0.34f;

        internal enum ProbeOwner
        {
            None,
            Continue,
            TestButton,
        }

        // Who asked for the probe currently in flight. There is one probe and
        // two callers, and they want opposite things from the same
        // TestStatus.Ok: Continue wants the session started, the test button
        // wants a line of text and nothing else.
        //
        // This was a bool, and the bool was wrong. Tick() runs at the top of
        // the same Update postfix that acts on the result, so by the time the
        // switch below it was reached the flag had already been cleared for
        // this frame and the Continue path treated a test-button probe as its
        // own. Caught in the log on 2026-09-22 — a failed test flashed the
        // fields and set the host-anyway bypass without anyone pressing
        // Continue, and a successful one was one `_autoContinuePending` away
        // from launching the game from the test button. Ownership has to
        // outlive the frame that consumes the result, which a tri-state does
        // and a bool cannot.
        internal static ProbeOwner Owner { get; set; } = ProbeOwner.None;

        private static string _testedSlotName = string.Empty;

        internal static void Inject(HostMenuConfirm menu)
        {
            var continueButton = menu.ContinueButton;
            var template = continueButton != null ? continueButton.gameObject : null;
            if (template == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(HostMenuArchipelagoControls)}] No ContinueButton on this build of the game: "
                    + "the Archipelago toggle and test button are not added.");
                return;
            }

            var parent = template.transform.parent;
            var templateRect = template.GetComponent<RectTransform>();
            if (parent == null || templateRect == null)
                return;

            // OnEnable can be called back several times on the same instance
            // (the player leaves and reopens the screen): do not clone again.
            if (parent.Find(ToggleName) != null)
            {
                // Reopened, not rebuilt. The controls survived, but the
                // state they display is read from the config at draw time
                // and has to be put back on them.
                RefreshToggleLabel(menu);
                ApplyEnabledState(menu);
                SetResult(menu, string.Empty);
                return;
            }

            var step = templateRect.sizeDelta.y * SpacingFactor;
            var origin = templateRect.anchoredPosition;

            // Facing Continue, in the right-hand column — player decision,
            // 2026-09-22 ("Archipelago: on should be opposite Continue at
            // the bottom of the menu"). Same y, one column across.
            TryBuild(() => BuildButton(template, parent, ToggleName, origin + new Vector2(ToggleOffsetX, 0f),
                                       null, () => OnToggle(menu), ToggleWidth, ToggleFontScale), ToggleName);

            // One row up, in the same column: the slot LastPlayed occupies,
            // which HideLastPlayed empties just below. The label is set here,
            // at build time — leaving it to a later refresh is what shipped a
            // button reading "Continuer" over the slot name field, seen on
            // screen 2026-09-22: only the toggle had a refresh of its own, so
            // only the toggle was ever relabelled.
            TryBuild(() => BuildButton(template, parent, TestName, origin + new Vector2(ColumnOffsetX, step),
                                       "TEST CONNECTION", () => OnTest(menu)), TestName);

            // Under the toggle, in the band between the last row and the
            // card's bottom divider — the only space on this card that
            // nothing else uses.
            TryBuild(() => BuildResultLabel(template, parent,
                                            origin + new Vector2(ColumnOffsetX, step * ResultOffsetFactor)),
                     ResultName);

            TryBuild(() => HideLastPlayed(parent), "LastPlayed");

            RefreshToggleLabel(menu);
            ApplyEnabledState(menu);

            Plugin.Log.LogInfo(
                $"[{nameof(HostMenuArchipelagoControls)}] Archipelago toggle and test button injected on "
                + $"'{menu.gameObject.name}' (step {step:0.#}, column {ColumnOffsetX:0.#} from Continue at {origin}).");
        }

        // Each control is built on its own so that one failing leaves the
        // others standing. The toggle is worth more than the test button,
        // and the test button is worth more than the label it writes into.
        private static void TryBuild(Action build, string what)
        {
            try
            {
                build();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(HostMenuArchipelagoControls)}] Could not add '{what}', ignored: {ex.Message}");
            }
        }

        private static GameObject BuildButton(GameObject template, Transform parent, string name,
                                              Vector2 anchoredPosition, string label, Action onClick,
                                              float width = 0f, float fontScale = 0f)
        {
            var clone = UnityEngine.Object.Instantiate(template, parent);
            clone.name = name;

            var rect = clone.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;

            if (width > 0f)
                Narrow(rect, width);

            if (label != null)
                SetLabel(clone, label);

            if (fontScale > 0f)
            {
                var text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text != null)
                {
                    // Auto-sizing would undo this on the next layout pass and
                    // put the label straight back where it was.
                    text.enableAutoSizing = false;
                    text.fontSize *= fontScale;
                }
            }

            // The circled arrow is the template's own Image, and it reads as
            // "go" — wrong on a switch, wrong on a test. Made transparent
            // rather than disabled: a disabled Graphic stops being a raycast
            // target, and the button would no longer be clickable at all.
            var arrow = clone.GetComponent<Image>();
            if (arrow != null)
            {
                var c = arrow.color;
                arrow.color = new Color(c.r, c.g, c.b, 0f);
            }

            var button = clone.GetComponent<Button>();
            if (button != null)
            {
                // Instantiate carries the template's PERSISTENT listeners
                // across, and ContinueButton's persistent listener is
                // ActionStart — so a clone of it would start the session on
                // click. RemoveAllListeners does not touch persistent calls;
                // replacing the whole event does.
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)(() => onClick()));
            }

            return clone;
        }

        // Same shape as HostMenuConfirmPatch.ScaleRowWidth, and for the same
        // reason: this panel has no LayoutGroup, every child carries a fixed
        // sizeDelta, so resizing the parent alone moves nothing inside it. A
        // child left at zero stays at zero — that is a stretched anchor, not
        // a size, and scaling it would break it.
        private static void Narrow(RectTransform rect, float width)
        {
            var scale = width / rect.sizeDelta.x;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

            for (var i = 0; i < rect.childCount; i++)
            {
                var child = rect.GetChild(i).GetComponent<RectTransform>();
                if (child == null || Mathf.Approximately(child.sizeDelta.x, 0f))
                    continue;

                child.sizeDelta = new Vector2(child.sizeDelta.x * scale, child.sizeDelta.y);
            }
        }

        // "Last played" is the one field on this card with nothing to say
        // here: you have already chosen the save, and the row it occupies is
        // the only place in the right column the test button can go without
        // landing on the host field. Deactivated rather than moved, so
        // putting it back is one line.
        private static void HideLastPlayed(Transform card)
        {
            var lastPlayed = card.Find("LastPlayed");
            if (lastPlayed == null)
                return;

            lastPlayed.gameObject.SetActive(false);
            Plugin.Log.LogInfo(
                $"[{nameof(HostMenuArchipelagoControls)}] 'LastPlayed' hidden to make room for the test button.");
        }

        private static GameObject BuildResultLabel(GameObject template, Transform parent, Vector2 anchoredPosition)
        {
            var clone = UnityEngine.Object.Instantiate(template, parent);
            clone.name = ResultName;
            clone.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;

            // A button stripped down to its text: it is only ever read, so
            // anything that would let it be clicked, hovered or selected is
            // removed rather than left inert.
            var button = clone.GetComponent<Button>();
            if (button != null)
                UnityEngine.Object.Destroy(button);

            var managed = clone.GetComponent<ManagedButton>();
            if (managed != null)
                UnityEngine.Object.Destroy(managed);

            foreach (var image in clone.GetComponentsInChildren<Image>(true))
                image.enabled = false;

            SetLabel(clone, string.Empty);
            return clone;
        }

        private static void OnToggle(HostMenuConfirm menu)
        {
            var next = !ModConfig.ArchipelagoEnabled.Value;
            ModConfig.ArchipelagoEnabled.Value = next;

            // A test result belongs to the settings that produced it.
            ApConnectionTest.Reset();
            SetResult(menu, string.Empty);

            HostMenuConfirmPatch.ApplyFieldLabels(menu, next);
            RefreshToggleLabel(menu);
            ApplyEnabledState(menu);

            Plugin.Log.LogInfo(
                $"[{nameof(HostMenuArchipelagoControls)}] Archipelago switched {(next ? "on" : "off")} from the hosting screen.");
        }

        private static void OnTest(HostMenuConfirm menu)
        {
            if (!ModConfig.ArchipelagoEnabled.Value)
            {
                SetResult(menu, "Archipelago is off for this session");
                return;
            }

            var slotName = menu.gameNameField != null ? menu.gameNameField.text : string.Empty;
            var password = menu.passwordField != null ? menu.passwordField.text : string.Empty;

            if (!ApEndpoint.TryResolveFromFields(ApEndpoint.CurrentHostAndPort(), slotName, password,
                                                 out var endpoint, out var problem))
            {
                SetResult(menu, string.IsNullOrEmpty(problem) ? "Fill in the host and the slot name" : problem);
                return;
            }

            _testedSlotName = endpoint.SlotName;
            Owner = ProbeOwner.TestButton;
            ApConnectionTest.Reset();
            ApConnectionTest.Start(endpoint);

            SetResult(menu, $"Testing {endpoint.Host}:{endpoint.Port}...");
            Plugin.Log.LogInfo(
                $"[{nameof(HostMenuArchipelagoControls)}] Testing the Archipelago connection to "
                + $"{endpoint.Host}:{endpoint.Port} as '{endpoint.SlotName}' (test button).");
        }

        // Driven from the Update postfix in HostMenuConfirmStartPatch, since
        // the probe answers on its own thread and there is nothing to hook.
        internal static void Tick(HostMenuConfirm menu)
        {
            if (Owner != ProbeOwner.TestButton)
                return;

            switch (ApConnectionTest.Status)
            {
                case ApConnectionTest.TestStatus.Ok:
                    Owner = ProbeOwner.None;
                    SetResult(menu, $"Connected as '{_testedSlotName}'");
                    Plugin.Log.LogInfo(
                        $"[{nameof(HostMenuArchipelagoControls)}] Archipelago test succeeded as '{_testedSlotName}'.");
                    break;

                case ApConnectionTest.TestStatus.Failed:
                    Owner = ProbeOwner.None;
                    SetResult(menu, $"Failed: {Explain(ApConnectionTest.LastError)}");
                    Plugin.Log.LogWarning(
                        $"[{nameof(HostMenuArchipelagoControls)}] Archipelago test failed: {ApConnectionTest.LastError}");
                    break;
            }
        }

        // The server's own wording is accurate and unhelpful: "InvalidSlot"
        // and a socket exception look equally like "it did not work". These
        // are the three mistakes actually worth telling apart on this
        // screen, and anything unrecognized is passed through rather than
        // flattened into a fourth, vaguer sentence.
        private static string Explain(string error)
        {
            if (string.IsNullOrEmpty(error))
                return "no reason given";

            // WHAT IS ACTUALLY MATCHED HERE, measured on 2026-09-22, and the
            // two layers are not the same string.
            //
            // On the wire the server answers with codes — ['InvalidSlot'],
            // ['InvalidPassword'], and ['InvalidPassword', 'InvalidSlot']
            // when both are wrong (confirmed by probing a real server
            // directly). But Archipelago.MultiClient.Net 6.7.1 does not hand
            // those through: LoginFailure.Errors carries its own sentences,
            // seen in game as
            //
            //     "The password is invalid. / The slot name did not match
            //      any slot on the server."
            //
            // The substring tests below work on both, which is luck rather
            // than design: the library's wording happens to contain "slot"
            // and "password". If a future version writes "the name did not
            // match", this stops recognising anything and silently falls
            // through to printing the raw string. That is the failure to
            // expect, and it is why this comment names both layers.
            //
            // Reporting them together matters because the first version
            // flattened the both-wrong case to the slot alone: fix the name,
            // test again, and only then hear about the password.
            var badSlot = error.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0;
            var badPassword = error.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0;

            if (badSlot && badPassword)
                return $"no slot called '{_testedSlotName}' here, and the password is wrong too";

            if (badSlot)
                return $"the server does not know a slot called '{_testedSlotName}'";

            if (badPassword)
                return "wrong Archipelago password";

            // "no answer" is this mod's own 15s giveup; "timed out" and
            // "refused" come from the socket. All three mean the same thing
            // to the player and none of them mean the slot name is wrong,
            // which is what makes them worth separating. Observed on screen
            // 2026-09-22: a mistyped port reported "Connection timed out."
            // verbatim, which reads like a server problem rather than a typo
            // in the field right above the message.
            if (error.IndexOf("no answer", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("refused", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("unreachable", StringComparison.OrdinalIgnoreCase) >= 0)
                return "no server answered - check the host and the port";

            return error;
        }

        private static void RefreshToggleLabel(HostMenuConfirm menu)
        {
            var toggle = Find(menu, ToggleName);
            if (toggle != null)
                // Short because the button is: 210 units between SUPPRIMER
                // and the edge of the card is not enough for the word
                // "ARCHIPELAGO", and a clipped label says less than an
                // abbreviated one.
                SetLabel(toggle, ModConfig.ArchipelagoEnabled.Value ? "AP : ON" : "AP : OFF");
        }

        // With Archipelago off, the host:port field and the test button have
        // nothing to act on, so they are greyed rather than left looking
        // live. The two repurposed fields stay usable: they are the game's
        // own save name and password again, and ApplyFieldLabels has already
        // said so.
        private static void ApplyEnabledState(HostMenuConfirm menu)
        {
            var on = ModConfig.ArchipelagoEnabled.Value;

            var hostRow = HostMenuConfirmPatch.FindHostPortRow(menu);
            if (hostRow != null)
            {
                var input = hostRow.Find("GameNameInput");
                var field = input != null ? input.GetComponent<TMP_InputField>() : null;
                if (field != null)
                    field.interactable = on;

                // Faded rather than hidden, and through TMP's own alpha
                // rather than a CanvasGroup: CanvasGroup is not in the
                // interop assemblies this project references, and the row is
                // nothing but text anyway.
                foreach (var label in hostRow.GetComponentsInChildren<TextMeshProUGUI>(true))
                    label.alpha = on ? 1f : 0.4f;
            }

            var test = Find(menu, TestName);
            var button = test != null ? test.GetComponent<Button>() : null;
            if (button != null)
                button.interactable = on;

            ApplySlotNameLock(menu, on);
        }

        // The slot name field is the game's own save-name field, relabelled
        // (2026-09-15, to avoid a third field on this screen). The two being
        // one thing has a cost the player found on 2026-09-22: re-hosting an
        // existing save lets you edit the name, and editing the name changes
        // which Archipelago slot this save connects to.
        //
        // That is not a cosmetic mistake. At best the server answers "no
        // slot called that"; at worst it connects to a REAL other slot, and
        // `ap_reported_*` is not scoped per seed, so the save then resends
        // checks earned somewhere else entirely.
        //
        // So once a save exists, its name is its slot and the field is
        // locked. Switching Archipelago off unlocks it again, which is both
        // the escape hatch for a genuine rename and the truth: with nothing
        // connecting, the field really is just a save name.
        private static void ApplySlotNameLock(HostMenuConfirm menu, bool archipelagoOn)
        {
            var field = menu.gameNameField;
            if (field == null)
                return;

            var save = menu.saveData;
            var uid = save != null ? save.filenameUid : null;
            var alreadyExists = !string.IsNullOrEmpty(uid);

            var locked = archipelagoOn && alreadyExists;
            if (field.interactable == !locked)
                return;

            field.interactable = !locked;

            // Both signals logged, not just the one acted on: "this save has
            // a filenameUid" is the reading being trusted here, and the
            // delete button is the one that was actually observed to appear
            // only for a save that already exists. If they ever disagree,
            // this line is where it shows.
            var deleteShown = menu.deleteButton != null && menu.deleteButton.gameObject.activeInHierarchy;
            Plugin.Log.LogInfo(
                $"[{nameof(HostMenuArchipelagoControls)}] Slot name field {(locked ? "locked" : "editable")} "
                + $"(archipelago={archipelagoOn}, filenameUid={(alreadyExists ? "set" : "empty")}, "
                + $"deleteButton={(deleteShown ? "shown" : "hidden")}).");
        }

        private static void SetResult(HostMenuConfirm menu, string text)
        {
            var label = Find(menu, ResultName);
            if (label != null)
                SetLabel(label, text);
        }

        private static GameObject Find(HostMenuConfirm menu, string name)
        {
            var parent = menu.ContinueButton != null ? menu.ContinueButton.transform.parent : null;
            var found = parent != null ? parent.Find(name) : null;
            return found != null ? found.gameObject : null;
        }

        private static void SetLabel(GameObject target, string text)
        {
            var label = target.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                HostMenuConfirmPatch.SetStaticLabel(label.transform, text);
        }
    }
}
