using System;
using BigWalkArchipelago.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace BigWalkArchipelago.Patches
{
    // Adds an AP host:port field to the hosting screen (HostMenuConfirm) —
    // a prerequisite for the real AP network client (not yet written),
    // which will read Core.ApSessionConfig.HostAndPort when connecting.
    // Layout confirmed via Debug/DebugMenuLookup.cs (M key) on 2026-09-15:
    // GameSlotCard_Editable positions everything by hand (no LayoutGroup),
    // with GameName full-width (900) on its own row, and Password/LastPlayed
    // side by side below it (399.42 wide each, offset by 489.4 between the
    // two). We reproduce this exact layout for the GameName row: shrunk to
    // 399.42, with the new field cloned to its right (offset 489.4, same Y)
    // — player decision (2026-09-15) to place the field "facing the save
    // name".
    //
    // Cloning rather than building from scratch (same philosophy as
    // ReceivedItemSpawner for cosmetic gourds): GameNameInput carries
    // MultiPlatformInputField (a subclass of TMP_InputField, confirmed in
    // il2cpp.cs) with all keyboard/gamepad navigation and styling already
    // wired up — much safer than assembling a TMP_InputField by hand.
    [HarmonyPatch(typeof(HostMenuConfirm), nameof(HostMenuConfirm.OnEnable))]
    internal static class HostMenuConfirmPatch
    {
        private const string HostPortRowName = "ApHostPortRow";
        private const float HalfRowWidth = 399.42f;
        private const float ColumnOffsetX = 489.4f; // Password -> LastPlayed, measured via DebugMenuLookup

        private static void Postfix(HostMenuConfirm __instance)
        {
            // Ghidra decompilation from 2026-09-15 (HouseAuthenticator.
            // OnPasswordResponseMessage, HostMenuConfirm.RequiredInputsHaveValues/
            // IsReadyToContinue/ActionStart): passwordRequired is a plain
            // serialized boolean that gates validation of the "Continue"
            // button, nothing on the Mirror/HouseAuthenticator side requires
            // a non-empty password (ordinary string comparison, empty ==
            // empty passes). Setting it to false just makes the field
            // optional: if it's still filled in, its value is still
            // written/transmitted normally (ActionStart doesn't condition
            // the write on anything else).
            __instance.passwordRequired = false;

            try
            {
                InjectHostPortField(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(HostMenuConfirmPatch)}] Failed to inject the host:port field, ignored: {ex.Message}");
            }
        }

        private static void InjectHostPortField(HostMenuConfirm menu)
        {
            var gameNameInput = menu.gameNameField;
            var gameNameRow = gameNameInput != null ? gameNameInput.transform.parent : null;
            var parent = gameNameRow != null ? gameNameRow.parent : null;
            if (gameNameRow == null || parent == null || gameNameRow.name != "GameName")
                return;

            // OnEnable can be called back multiple times on the same instance
            // (the player leaves/reopens the screen): don't re-clone every time.
            if (parent.Find(HostPortRowName) != null)
                return;

            var gameNameRect = gameNameRow.GetComponent<RectTransform>();
            var originalWidth = gameNameRect.sizeDelta.x;
            var scale = HalfRowWidth / originalWidth;

            ScaleRowWidth(gameNameRow, scale, HalfRowWidth);

            var clone = UnityEngine.Object.Instantiate(gameNameRow.gameObject, parent);
            clone.name = HostPortRowName;

            var cloneRect = clone.transform.GetComponent<RectTransform>();
            cloneRect.anchoredPosition = new Vector2(
                gameNameRect.anchoredPosition.x + ColumnOffsetX,
                gameNameRect.anchoredPosition.y);

            RetitleClone(clone.transform);
            RelabelOriginalFields(menu, gameNameRow);

            Plugin.Log.LogInfo($"[{nameof(HostMenuConfirmPatch)}] host:port field injected on '{menu.gameObject.name}'.");
        }

        // The 2 existing fields are repurposed as AP identifiers (slot
        // name / AP password), not as the local session's name/password —
        // player decision (2026-09-15). Only renames the displayed labels:
        // the logic (ActionStart, SaveData.slotName/password,
        // NetworkMinder.SetServerPassword) stays unchanged; it's the real AP
        // network client (not yet written) that will read these same fields
        // differently when the time comes.
        private static void RelabelOriginalFields(HostMenuConfirm menu, Transform gameNameRow)
        {
            SetStaticLabel(gameNameRow.Find("GameNameTitle"), "SLOT NAME :");

            var passwordRow = menu.passwordField != null ? menu.passwordField.transform.parent : null;
            if (passwordRow != null && passwordRow.name == "Password")
                SetStaticLabel(passwordRow.Find("PasswordTitle"), "ARCHIPELAGO PASSWORD :");
        }

        // Shrinks the GameName row AND its two direct children
        // (GameNameTitle/GameNameInput) proportionally — no LayoutGroup on
        // this panel, everything uses a fixed sizeDelta, so resizing the
        // parent alone wouldn't affect the children.
        private static void ScaleRowWidth(Transform row, float scale, float newRowWidth)
        {
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(newRowWidth, rowRect.sizeDelta.y);

            for (var i = 0; i < row.childCount; i++)
            {
                var childRect = row.GetChild(i).GetComponent<RectTransform>();
                if (childRect == null)
                    continue;

                childRect.sizeDelta = new Vector2(childRect.sizeDelta.x * scale, childRect.sizeDelta.y);
            }
        }

        private static void RetitleClone(Transform clone)
        {
            var title = clone.Find("GameNameTitle");
            SetStaticLabel(title, "HÔTE ARCHIPELAGO :");

            var input = clone.Find("GameNameInput");
            if (input == null)
                return;

            var placeholder = input.Find("Text Area/Placeholder");
            SetStaticLabel(placeholder, "hôte:port");

            var tmpInput = input.GetComponent<TMP_InputField>();
            if (tmpInput == null)
                return;

            // Cloned from GameNameInput, which limits the length of a save
            // name (inherited characterLimit, too short for an address like
            // "archipelago.gg:38281" or a custom hostname) — removed, no
            // reason to limit the length of a host:port (reported by the
            // player, 2026-09-15: unable to type enough characters).
            tmpInput.characterLimit = 0;

            // Pre-filled with the last entered value (persisted in the
            // BepInEx .cfg, ModConfig.ArchipelagoHostPort — "archipelago.gg:"
            // by default on the very first launch) rather than a fixed value
            // every time — player request (2026-09-15) to avoid retyping it
            // every session.
            var lastValue = ModConfig.ArchipelagoHostPort.Value;
            tmpInput.text = lastValue;
            ApSessionConfig.HostAndPort = lastValue;
            tmpInput.onValueChanged.AddListener((UnityAction<string>)(value =>
            {
                ApSessionConfig.HostAndPort = value;
                ModConfig.ArchipelagoHostPort.Value = value;
            }));
        }

        // The labels (GameNameTitle, Placeholder) carry a LocalizedText that
        // rewrites the text from a translation key — without disabling it,
        // our text would be overwritten on the next language refresh (or
        // even immediately, since LocalizedText can reapply its translation
        // in its own OnEnable, which gets replayed by Instantiate).
        private static void SetStaticLabel(Transform target, string text)
        {
            if (target == null)
                return;

            var localized = target.GetComponent<LocalizedText>();
            if (localized != null)
                localized.enabled = false;

            var label = target.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = text;
        }
    }
}
