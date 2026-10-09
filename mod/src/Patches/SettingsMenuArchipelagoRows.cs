using System;
using BigWalkArchipelago.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BigWalkArchipelago.Patches
{
    // The mod's own lines in the game's Settings, General category (ROADMAP U13, player
    // 2026-10-05): the size of the Archipelago overlay's text, each player on their own machine.
    //
    // A line is a copy of one of the game's own left / right lines (a SettingsRow with two arrow
    // buttons and a label). The copy is made from the line switched off, so the game's row script
    // never wakes on it, and that script is then taken off: a SettingsRow is tied to one of the
    // game's own settings (`settingsType`), and left on it would change that one. The arrows' click
    // events are replaced, not added to, because Instantiate carries the persistent listeners
    // across (the lesson of HostMenuArchipelagoControls). The texts lose their LocalizedText,
    // which would put the game's words back on a language change.
    [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.Start))]
    internal static class SettingsMenuArchipelagoRows
    {
        private const string Tag = "[" + nameof(SettingsMenuArchipelagoRows) + "]";
        private const string RowName = "Archipelago text size (AP)";

        // Point sizes; 22 is the .cfg's default. No "off": the overlay always shows (player,
        // 2026-10-06).
        private static readonly (string label, int size)[] Sizes =
        {
            ("Small", 16), ("Medium", 22), ("Large", 28), ("Huge", 36),
        };

        private const string CategoryName = "Archipelago category (AP)";
        private const string ButtonName = "Archipelago button (AP)";

        private static void Postfix(SettingsMenu __instance)
        {
            try
            {
                var general = __instance != null ? __instance.catagoryGeneral : null;
                if (general == null || general.rows == null)
                    return;

                SettingsRow template = null;
                foreach (var row in general.rows)
                {
                    if (row != null && row.leftButton != null && row.rightButton != null && row.arrayLabel != null)
                    {
                        template = row;
                        break;
                    }
                }

                if (template == null)
                {
                    Plugin.Log.LogWarning($"{Tag} No left/right line in Settings > General to copy; no Archipelago settings.");
                    return;
                }

                if (general.transform.parent.Find(CategoryName) != null)
                    return;

                var rows = BuildCategory(__instance, general, template);
                if (rows != null)
                {
                    _arrayRows.Clear();
                    BuildSizeRow(template, rows);
                    BuildDeathLinkRows(template, rows);
                    BuildFlareRow(template, rows);
                    _rows = rows;
                    BuildGourdNameRow(rows);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not add the Archipelago settings: {ex}");
            }
        }

        // An "Archipelago" category of its own (player, 2026-10-06): General's panel copied and
        // emptied of the game's lines, and General's button copied under the last one. The button
        // hides the category showing and shows ours, and makes ours the menu's active one, so the
        // game hides it again when another category is chosen. Returns where our lines go.
        private static Transform BuildCategory(SettingsMenu menu, SettingsCatagory general, SettingsRow template)
        {
            GameObject panel;
            var wasActive = general.gameObject.activeSelf;
            try
            {
                general.gameObject.SetActive(false);
                panel = UnityEngine.Object.Instantiate(general.gameObject, general.transform.parent);
            }
            finally
            {
                general.gameObject.SetActive(wasActive);
            }

            panel.name = CategoryName;
            var category = panel.GetComponent<SettingsCatagory>();

            // Where the lines sit, at the same place as in General; every line of General's copy goes.
            var rows = Twin(panel.transform, general.transform, template.transform.parent) ?? panel.transform;
            for (var i = rows.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(rows.GetChild(i).gameObject);
            category.rows = new SettingsRow[0];

            var source = general.catagoryButton != null ? general.catagoryButton.gameObject : null;
            if (source == null)
                return rows;

            var button = UnityEngine.Object.Instantiate(source, source.transform.parent);
            button.name = ButtonName;
            button.transform.SetAsLastSibling();
            _menu = menu;
            _button = button.transform;
            Retitle(button, source.transform, "Archipelago", null, null, null);

            var click = button.GetComponent<Button>();
            if (click != null)
            {
                click.onClick = new Button.ButtonClickedEvent();
                click.onClick.AddListener((UnityAction)(() =>
                {
                    var active = menu.activeCatagory;
                    if (active != null && active != category)
                        active.gameObject.SetActive(false);
                    panel.SetActive(true);
                    menu.activeCatagory = category;
                }));
                category.catagoryButton = click;
            }

            return rows;
        }

        // The category buttons are laid out by the game after the menu starts (at Start every one
        // sat at the same place, 2026-10-06), so ours is placed again every frame it shows: under
        // the lowest category button, as far below it as the last two are apart. Only the seven
        // categories' buttons count (the menu's button list holds others, the lobby's).
        private static SettingsMenu _menu;
        private static Transform _button;

        internal static void KeepPlaced()
        {
            CatchUpGourdNameRow();
            KeepArrayRows();
            KeepGourdNameRow();

            if (_menu == null || _button == null || !_button.gameObject.activeInHierarchy)
                return;

            var buttons = new System.Collections.Generic.List<Transform>();
            foreach (var category in new[] { _menu.catagoryGeneral, _menu.catagoryAudio, _menu.catagoryMicrophone, _menu.catagoryGraphics,
                                             _menu.catagoryDisplay, _menu.catagoryControls, _menu.catagoryRebind })
            {
                var button = category != null ? category.catagoryButton : null;
                if (button != null && button.transform != _button && button.gameObject.activeInHierarchy)
                    buttons.Add(button.transform);
            }

            if (buttons.Count < 2)
                return;

            buttons.Sort((x, y) => x.position.y.CompareTo(y.position.y));
            var step = buttons[1].position - buttons[0].position;
            if (step.sqrMagnitude < 0.0001f)
                return;
            _button.position = buttons[0].position - step;
        }

        // ------------------------------------------------------------------
        // The gourds' name (U14): a free text field, the host's to set
        // ------------------------------------------------------------------

        // The settings have no text field: the hosting screen's save name field is copied
        // (MultiPlatformInputField, keyboard and gamepad already wired), and kept, since that
        // screen only exists in the main menu and the pause menu's settings need one too.
        private static GameObject _inputTemplate;
        private static TMP_InputField _gourdField;
        private static Transform _gourdTitle;

        // Called at every SettingsMenu.Start: the main menu's hosting screen is found then.
        private static GameObject InputTemplate()
        {
            if (_inputTemplate != null)
                return _inputTemplate;

            foreach (var menu in UnityEngine.Object.FindObjectsByType<HostMenuConfirm>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var row = menu != null && menu.gameNameField != null ? menu.gameNameField.transform.parent : null;
                if (row == null)
                    continue;

                var wasActive = row.gameObject.activeSelf;
                try
                {
                    row.gameObject.SetActive(false);
                    _inputTemplate = UnityEngine.Object.Instantiate(row.gameObject);
                }
                finally
                {
                    row.gameObject.SetActive(wasActive);
                }

                _inputTemplate.name = "Archipelago text field template (AP)";
                UnityEngine.Object.DontDestroyOnLoad(_inputTemplate);
                break;
            }

            return _inputTemplate;
        }

        // A guest can join without ever opening the settings in the main menu (the loopback guest
        // does, player 2026-10-06), so its settings were built with no field to copy. The hosting
        // screen is looked for once a second until found, and a category built without the gourd
        // line gets it then.
        private static Transform _rows;
        private static float _nextTemplateLook;

        private static void CatchUpGourdNameRow()
        {
            if (_inputTemplate == null)
            {
                if (Time.unscaledTime < _nextTemplateLook)
                    return;
                _nextTemplateLook = Time.unscaledTime + 1f;
                if (InputTemplate() == null)
                    return;
            }

            if (_rows == null || _gourdRow != null)
                return;

            // Once per category: a line that failed to build is not tried again every frame.
            var rows = _rows;
            _rows = null;
            try
            {
                BuildGourdNameRow(rows);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not add the gourd name line late: {ex.Message}");
            }
        }

        private static void BuildGourdNameRow(Transform rows)
        {
            var template = InputTemplate();
            if (template == null)
            {
                Plugin.Log.LogInfo($"{Tag} No hosting screen seen yet to copy a text field from; no gourd name line this time.");
                return;
            }

            var clone = UnityEngine.Object.Instantiate(template, rows);
            clone.name = "Gourd name (AP)";
            clone.transform.SetAsLastSibling();

            _gourdTitle = clone.transform.Find("GameNameTitle");
            HostMenuConfirmPatch.SetStaticLabel(_gourdTitle, "GOURD NAME :");

            var input = clone.transform.Find("GameNameInput");
            _gourdField = input != null ? input.GetComponent<TMP_InputField>() : null;
            if (_gourdField != null)
            {
                HostMenuConfirmPatch.SetStaticLabel(input.Find("Text Area/Placeholder"), GourdNames.Default);

                // The save name's own listeners came across with the copy: replaced, not added to.
                _gourdField.onValueChanged = new TMP_InputField.OnChangeEvent();
                _gourdField.onEndEdit = new TMP_InputField.SubmitEvent();
                _gourdField.onSubmit = new TMP_InputField.SubmitEvent();
                _gourdField.characterLimit = GourdNames.MaxLength;
                _gourdField.text = GourdNames.Current;

                // Enter, or leaving the field (the menu closing among others), applies it.
                _gourdField.onEndEdit.AddListener((UnityAction<string>)(value =>
                {
                    if (GourdNames.IsGuest)
                        return;
                    GourdNames.Set(value);
                    _gourdField.text = GourdNames.Current;
                    Plugin.Log.LogInfo($"{Tag} The gourds are now called '{GourdNames.Current}'.");
                }));
            }

            _gourdRow = clone.GetComponent<RectTransform>();

            // The text size line's title takes this title's font: the game's own caps font,
            // which the copied settings line never managed to keep (it fell back to a fixed-width one).
            var model = _gourdTitle != null ? _gourdTitle.GetComponent<TMP_Text>() : null;
            if (model != null)
            {
                foreach (var row in _arrayRows)
                {
                    var text = row.Title;
                    if (text == null)
                        continue;
                    text.font = model.font;
                    text.fontSharedMaterial = model.fontSharedMaterial;
                    text.fontStyle = model.fontStyle;
                    text.text = row.Caps;
                }

                _sizeTitle = _arrayRows.Count > 0 && _arrayRows[0].Title != null ? _arrayRows[0].Title.rectTransform : null;
                _arraysAsGuest = null;
            }

            clone.SetActive(true);
            Plugin.Log.LogInfo($"{Tag} Gourd name line added to Settings > Archipelago.");
        }

        // Every frame: on a guest the field is the host's name, greyed, and says so.
        private static bool _shownAsGuest;

        private static RectTransform _gourdRow;
        private static RectTransform _sizeRow;
        private static RectTransform _sizeTitle;

        // A text's left edge on screen, from its rect (GetWorldCorners reads zeros under IL2CPP).
        private static float LeftEdge(RectTransform r)
        {
            var text = r.GetComponent<TMP_Text>();
            var left = text != null ? text.textBounds.min.x : r.rect.xMin;
            return r.TransformPoint(new Vector3(left, 0f, 0f)).x;
        }
        private static readonly Vector3[] Corners = new Vector3[4];

        // The panel places its lines by hand, no layout (a LayoutElement changed nothing,
        // 2026-10-06): the gourd name line is put under the text size line every frame it shows,
        // its left edge on that line's, with a gap of 40 % of that line's height.
        private static void PlaceGourdRow()
        {
            var below = _arrayRows.Count > 0 ? _arrayRows[_arrayRows.Count - 1].Row : _sizeRow;
            if (_gourdRow == null || below == null)
                return;

            // The category is a VerticalLayoutGroup; the gourd line stays out of it and is set
            // in the category's own space, under the size line (GetWorldCorners reads back zeros
            // under IL2CPP, 2026-10-06 log).
            var element = _gourdRow.GetComponent<LayoutElement>() ?? _gourdRow.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            var sizeTop = below.anchoredPosition.y + below.sizeDelta.y * (1f - below.pivot.y);
            var sizeBottom = sizeTop - below.sizeDelta.y;
            var sizeLeft = below.anchoredPosition.x - below.sizeDelta.x * below.pivot.x;
            _gourdRow.anchorMin = _gourdRow.anchorMax = below.anchorMin;
            _gourdRow.pivot = new Vector2(0f, 1f);
            _gourdRow.sizeDelta = new Vector2(below.sizeDelta.x, _gourdRow.sizeDelta.y);
            _gourdRow.anchoredPosition = new Vector2(sizeLeft, sizeBottom - 20f);

            // Both titles start at the same x, whatever indent each line's own layout gives them.
            var gourdTitle = _gourdTitle != null ? _gourdTitle.GetComponent<RectTransform>() : null;
            if (_sizeTitle != null && gourdTitle != null)
            {
                var shift = LeftEdge(_sizeTitle) - LeftEdge(gourdTitle);
                _gourdRow.position += new Vector3(shift, 0f, 0f);
                if (!_shiftLogged)
                {
                    _shiftLogged = true;
                    Plugin.Log.LogInfo($"{Tag} Title shift: size title left {LeftEdge(_sizeTitle)} (bounds {_sizeTitle.GetComponent<TMP_Text>()?.textBounds}, rect {_sizeTitle.rect}), gourd title left {LeftEdge(gourdTitle)} (rect {gourdTitle.rect}), shift {shift}.");
                }
            }
            else if (!_shiftLogged)
            {
                _shiftLogged = true;
                Plugin.Log.LogInfo($"{Tag} Title shift skipped: size title {(_sizeTitle != null ? "found" : "missing")}, gourd title {(gourdTitle != null ? "found" : "missing")}.");
            }
            if (!_layoutLogged)
            {
                _layoutLogged = true;
                LogLayout(Vector3.zero, 0f, Vector3.zero, Vector3.zero);
            }
        }

        private static bool _layoutLogged;
        private static bool _shiftLogged;

        // Once: how the panel lays its lines out, to stop guessing (2026-10-06).
        private static void LogLayout(Vector3 sizeBottomLeft, float sizeHeight, Vector3 gourdTopLeft, Vector3 target)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"{Tag} Layout: size line bottom-left {sizeBottomLeft} height {sizeHeight}; gourd line top-left was {gourdTopLeft}, moved to {target}, now {_gourdRow.position}.",
            };

            void Describe(Transform t, string indent)
            {
                var types = new System.Collections.Generic.List<string>();
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c != null)
                        types.Add(c.GetIl2CppType().Name);
                }

                var r = t.GetComponent<RectTransform>();
                lines.Add(r == null
                    ? $"{indent}{t.name}: {string.Join(",", types)}"
                    : $"{indent}{t.name} active={t.gameObject.activeSelf} pos={r.anchoredPosition} size={r.sizeDelta} anchors={r.anchorMin}-{r.anchorMax} pivot={r.pivot} world={r.position}: {string.Join(",", types)}");
            }

            var parent = _sizeRow.parent;
            for (var t = parent; t != null && lines.Count < 8; t = t.parent)
                Describe(t, "  up ");
            for (var i = 0; i < parent.childCount; i++)
                Describe(parent.GetChild(i), "  child ");

            Plugin.Log.LogInfo(string.Join(Environment.NewLine, lines));
        }

        private static bool? _arraysAsGuest;
        private static float _nextArrayRefresh;

        // Every frame the lines show: the host's lines greyed on a guest, saying whose they are,
        // and every value read again once a second (a seed connected or left changes what "On"
        // and "Seed's" say).
        private static void KeepArrayRows()
        {
            if (_arrayRows.Count == 0 || _arrayRows[0].Row == null || !_arrayRows[0].Row.gameObject.activeInHierarchy)
                return;

            var guest = GourdNames.IsGuest;
            if (guest != _arraysAsGuest)
            {
                _arraysAsGuest = guest;
                foreach (var row in _arrayRows)
                {
                    if (!row.HostOnly)
                        continue;
                    if (row.Left != null)
                        row.Left.interactable = !guest;
                    if (row.Right != null)
                        row.Right.interactable = !guest;
                    if (row.Title != null)
                        row.Title.text = row.Caps;
                }
            }

            if (Time.unscaledTime >= _nextArrayRefresh)
            {
                _nextArrayRefresh = Time.unscaledTime + 1f;
                foreach (var row in _arrayRows)
                    row.Show();
            }
        }

        private static void KeepGourdNameRow()
        {
            if (_gourdField == null || !_gourdField.gameObject.activeInHierarchy)
                return;

            PlaceGourdRow();

            var guest = GourdNames.IsGuest;
            _gourdField.interactable = !guest;
            if (guest && !_gourdField.isFocused && _gourdField.text != GourdNames.Current)
                _gourdField.text = GourdNames.Current;

            if (guest != _shownAsGuest)
            {
                _shownAsGuest = guest;
                HostMenuConfirmPatch.SetStaticLabel(_gourdTitle, guest ? "GOURD NAME (THE HOST'S IN THIS GAME) :" : "GOURD NAME :");

                // No longer a guest: the field shows this player's own name again, never the
                // host's, which leaving the field would otherwise save as theirs.
                if (!guest)
                    _gourdField.text = GourdNames.Current;
            }
        }

        // The transform at `inSource`'s place in `source`, found under `copy`.
        private static Transform Twin(Transform copy, Transform source, Transform inSource)
        {
            if (inSource == null)
                return null;

            var path = new System.Collections.Generic.List<string>();
            for (var t = inSource; t != null && t != source; t = t.parent)
                path.Insert(0, t.name);
            return path.Count == 0 ? copy : copy.Find(string.Join("/", path));
        }

        // Every translated text of `clone` but `value` and the arrows', rewritten as `text` in the
        // font the game gave the same text of `source` (see BuildSizeRow).
        private static void Retitle(GameObject clone, Transform source, string text, LocalizedText value, Button left, Button right)
        {
            foreach (var localized in clone.GetComponentsInChildren<LocalizedText>(true))
            {
                if (localized == value || Under(localized.transform, left) || Under(localized.transform, right))
                    continue;

                var element = localized.textElement != null ? localized.textElement : localized.GetComponent<TMP_Text>();
                var place = element != null ? Twin(source, clone.transform, element.transform) : null;
                var original = place != null ? place.GetComponent<TMP_Text>() : null;
                UnityEngine.Object.DestroyImmediate(localized);
                if (element == null)
                    continue;

                if (original != null)
                {
                    element.font = original.font;
                    element.fontSharedMaterial = original.fontSharedMaterial;
                    element.fontStyle = original.fontStyle;
                    element.fontSize = original.fontSize;
                    element.characterSpacing = original.characterSpacing;
                }

                element.text = text;
            }
        }

        // A left / right line of ours: its title as typed until the gourd line lends it the game's
        // caps font (BuildGourdNameRow), its value from `show`, the arrows stepping through `step`.
        private sealed class ArrayRow
        {
            internal RectTransform Row;
            internal TMP_Text Title;
            internal string Caps;
            internal bool HostOnly;
            internal Button Left;
            internal Button Right;
            internal Action Show;
        }

        private static readonly System.Collections.Generic.List<ArrayRow> _arrayRows = new();

        private static ArrayRow BuildArrayRow(SettingsRow template, Transform parent, string title, string caps,
                                              bool hostOnly, Func<string> show, Action<int> step)
        {
            GameObject clone;
            var wasActive = template.gameObject.activeSelf;
            try
            {
                template.gameObject.SetActive(false);
                clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
            }
            finally
            {
                template.gameObject.SetActive(wasActive);
            }

            clone.name = $"{title} (AP)";
            clone.transform.SetAsLastSibling();

            var settingsRow = clone.GetComponent<SettingsRow>();
            var value = settingsRow.arrayLabel;
            var left = settingsRow.leftButton;
            var right = settingsRow.rightButton;

            UnityEngine.Object.DestroyImmediate(settingsRow);
            var identifier = clone.GetComponent<SettingItemIdentifier>();
            if (identifier != null)
                UnityEngine.Object.DestroyImmediate(identifier);
            // The title: every translated text but the value and the arrows' (the arrows are
            // translated glyphs too), in the font the game gave the copied line's own title. The
            // translation comes off, or it puts "Language" back; left with a key of ours it showed
            // "[Archipelago text]" in a fallback font (2026-10-06).
            Retitle(clone, template.transform, title, value, left, right);

            var row = new ArrayRow
            {
                Row = clone.GetComponent<RectTransform>(),
                Caps = caps,
                HostOnly = hostOnly,
                Left = left,
                Right = right,
            };
            row.Show = () =>
            {
                // A guest's own .cfg says nothing of the host's DeathLink: no value to show.
                if (value != null)
                    SetRaw(value, hostOnly && GourdNames.IsGuest ? "Set by the host" : show());
            };

            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text == title)
                    row.Title = text;
            }

            if (left != null)
            {
                left.onClick = new Button.ButtonClickedEvent();
                left.onClick.AddListener((UnityAction)(() => { step(-1); row.Show(); }));
            }

            if (right != null)
            {
                right.onClick = new Button.ButtonClickedEvent();
                right.onClick.AddListener((UnityAction)(() => { step(+1); row.Show(); }));
            }

            row.Show();
            clone.SetActive(true);
            _arrayRows.Add(row);
            return row;
        }

        private static void BuildSizeRow(SettingsRow template, Transform parent)
        {
            var row = BuildArrayRow(template, parent, "Archipelago text", "ARCHIPELAGO TEXT :", hostOnly: false,
                () => $"{Sizes[Current()].label} ({Sizes[Current()].size})",
                delta =>
                {
                    var next = (Current() + delta + Sizes.Length) % Sizes.Length;
                    ModConfig.ShowConnectionStatus.Value = true;
                    ModConfig.StatusFontSize.Value = Sizes[next].size;
                });
            _sizeRow = row.Row;
            Plugin.Log.LogInfo($"{Tag} Archipelago text size line added (copied from '{template.gameObject.name}').");
        }

        // The host's DeathLink (player, 2026-10-06): on or off, sending and receiving together,
        // and the amnesty, the seed's or a number of its own. Greyed on a guest, which has no
        // connection of its own.
        private static readonly int[] Amnesties = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Each player's own, the accessibility setting (player, 2026-10-07): the traps' flashes
        // (Big Flare) not shown on this screen.
        private static void BuildFlareRow(SettingsRow template, Transform parent)
        {
            BuildArrayRow(template, parent, "Gentle effects", "GENTLE EFFECTS :", hostOnly: false,
                () => ModConfig.GentleEffects.Value ? "On" : "Off",
                _ => ModConfig.GentleEffects.Value = !ModConfig.GentleEffects.Value);
        }

        private static void BuildDeathLinkRows(SettingsRow template, Transform parent)
        {
            BuildArrayRow(template, parent, "DeathLink", "DEATHLINK :", hostOnly: true,
                () => !ModConfig.DeathLinkEnabled.Value ? "Off"
                    : Traps.SeedHasDeathLink || !Core.Net.ApRuntime.ShufflesPegTiles ? "On" : "On (not in this seed)",
                _ =>
                {
                    ModConfig.DeathLinkEnabled.Value = !ModConfig.DeathLinkEnabled.Value;
                    Core.Net.ApRuntime.ApplyDeathLinkSetting();
                });

            BuildArrayRow(template, parent, "DeathLink amnesty", "DEATHLINK AMNESTY :", hostOnly: true,
                () => ModConfig.DeathLinkAmnesty.Value < 0
                    ? Core.Net.ApRuntime.ShufflesPegTiles ? $"Seed's ({Traps.SeedAmnesty})" : "Seed's"
                    : ModConfig.DeathLinkAmnesty.Value.ToString(),
                delta =>
                {
                    var index = Math.Max(0, Array.IndexOf(Amnesties, ModConfig.DeathLinkAmnesty.Value));
                    ModConfig.DeathLinkAmnesty.Value = Amnesties[(index + delta + Amnesties.Length) % Amnesties.Length];
                    Plugin.Log.LogInfo($"{Tag} DeathLink amnesty: {(ModConfig.DeathLinkAmnesty.Value < 0 ? "the seed's" : ModConfig.DeathLinkAmnesty.Value.ToString())}.");
                });
        }

        // The preset nearest the .cfg's size.
        private static int Current()
        {
            var size = ModConfig.StatusFontSize.Value;
            var best = 0;
            for (var i = 1; i < Sizes.Length; i++)
            {
                if (Math.Abs(Sizes[i].size - size) < Math.Abs(Sizes[best].size - size))
                    best = i;
            }

            return best;
        }

        private static bool Under(Transform t, Button button)
        {
            return button != null && t.IsChildOf(button.transform);
        }

        private static void TryRefresh(LocalizedText localized)
        {
            try
            {
                localized.Refresh();
            }
            catch (Exception)
            {
                // Not initialised yet: it reads its key when it wakes.
            }
        }

        private static void SetRaw(LocalizedText localized, string text)
        {
            if (localized == null)
                return;

            localized.displayType = LocalizedText.DisplayType.RawValue;
            localized.rawValue = text;
            localized.key = string.Empty;
            try
            {
                localized.Refresh();
            }
            catch (Exception)
            {
                // Not initialised yet (the line is still off): it shows the raw value when it wakes.
                var element = localized.textElement != null ? localized.textElement : localized.GetComponent<TMP_Text>();
                if (element != null)
                    element.text = text;
            }
        }
    }
}
