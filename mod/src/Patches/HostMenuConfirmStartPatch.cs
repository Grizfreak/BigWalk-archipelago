using System;
using BigWalkArchipelago.Core.Net;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Checks the Archipelago details on the hosting screen before the
    // session starts, instead of letting the host find out from a log line
    // twenty minutes later that they mistyped the slot name. Idea noted on
    // 2026-09-15, unblocked once the network client existed.
    //
    // THE RULE THIS IS BUILT AROUND, AND ITS ONE EXCEPTION.
    //
    // It must never be able to stop someone PLAYING. A failed probe flashes
    // the offending field, logs why, and then gets out of the way — pressing
    // Continue again hosts regardless. An Archipelago server being
    // unreachable is not a reason to be unable to launch the game.
    //
    // Except when the save does not exist yet (player request, 2026-09-25).
    // Creating a NEW Archipelago save offline binds it to a slot nobody has
    // confirmed exists — its name is locked the moment it is written — and
    // the first sign of a typo is a whole session later. So a new save with
    // Archipelago on starts only once the connection has been verified,
    // and every press of Continue tests again. Playing is still never
    // blocked: an existing save hosts on the second press as before, and
    // switching AP : OFF on the same screen starts any game at all.
    //
    // First press runs the probe and holds the screen for the second or so
    // it takes; HostMenuConfirm.Update then continues automatically, so from
    // the outside it just looks like Continue took a moment.
    internal static class HostMenuConfirmStartPatch
    {
        private static bool _bypass;
        private static bool _autoContinuePending;

        // THE SETTINGS ALREADY VERIFIED, remembered across OnEnable
        // (2026-09-25, found in the first test of the packaged build).
        //
        // The player-count screen's Play button stuck on a new save whose
        // Continue had just been verified. The log showed HostMenuConfirm's
        // OnEnable running again right after that Continue, and OnEnable
        // wipes the test result on the reasoning that coming back to this
        // screen means the fields may have changed. So when the flow reached
        // this gate a second time the verdict was gone, a fresh probe was
        // started, and its result was waited on by an Update that no longer
        // runs once this screen is out of sight — Play waited forever.
        //
        // What a verdict belongs to is the settings, not the screen: the
        // same host, port, slot and password verified a moment ago are still
        // verified. So they pass at once, and anything changed is tested
        // again. Kept for a few minutes only, so that a server that has since
        // gone down is not vouched for indefinitely.
        private static string _verifiedKey;
        private static DateTime _verifiedAt;
        private static readonly TimeSpan VerifiedFor = TimeSpan.FromMinutes(10);

        private static string KeyOf(ApEndpoint endpoint)
        {
            return $"{endpoint.Host}:{endpoint.Port}|{endpoint.SlotName}|{endpoint.Password}";
        }

        private static bool RecentlyVerified(string key)
        {
            return _verifiedKey != null
                && (key == null || key == _verifiedKey)
                && DateTime.UtcNow - _verifiedAt < VerifiedFor;
        }

        internal static void RememberVerified(ApEndpoint endpoint)
        {
            _verifiedKey = KeyOf(endpoint);
            _verifiedAt = DateTime.UtcNow;
        }

        [HarmonyPatch(typeof(HostMenuConfirm), nameof(HostMenuConfirm.ActionStart))]
        internal static class ActionStart
        {
            private static bool Prefix(HostMenuConfirm __instance)
            {
                if (_bypass || !ModConfig.ArchipelagoEnabled.Value)
                    return true;

                var slotName = __instance.gameNameField != null ? __instance.gameNameField.text : string.Empty;
                var password = __instance.passwordField != null ? __instance.passwordField.text : string.Empty;
                var isNewSave = IsNewSave(__instance);

                // One line per press, because the order in which this screen
                // and the player-count screen call in here was never written
                // down anywhere, and the Play bug above was found by reading
                // exactly that order out of a log.
                Plugin.Log.LogInfo(
                    $"[{nameof(HostMenuConfirmStartPatch)}] Start requested (new save: {isNewSave}, "
                    + $"test: {ApConnectionTest.Status}).");

                if (!ApEndpoint.TryResolveFromFields(ApEndpoint.CurrentHostAndPort(), slotName, password,
                                                     out var endpoint, out var problem))
                {
                    // An existing save: say nothing and let them host.
                    // ApRuntime will explain itself once the session is up.
                    if (!isNewSave)
                        return true;

                    // Fields no longer readable, but this very flow was just
                    // verified: the second pass through here, not a new game
                    // typed without an address.
                    if (RecentlyVerified(null))
                        return true;

                    HostMenuArchipelagoControls.ShowResult(__instance,
                        $"A new Archipelago game needs the host and the slot name ({problem}) - or switch AP : OFF");
                    Update.FlashFields(__instance);
                    return false;
                }

                if (RecentlyVerified(KeyOf(endpoint)))
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(HostMenuConfirmStartPatch)}] These settings were verified moments ago; no second test.");
                    return true;
                }

                switch (ApConnectionTest.Status)
                {
                    case ApConnectionTest.TestStatus.Ok:
                        RememberVerified(endpoint);
                        return true;

                    case ApConnectionTest.TestStatus.Testing:
                        // Already in flight: swallow the press rather than
                        // starting a second probe.
                        return false;

                    case ApConnectionTest.TestStatus.Failed when isNewSave:
                        // No consent to host blind for a save that does not
                        // exist yet: test again, since the fields may well
                        // have been corrected since.
                        StartProbe(endpoint);
                        return false;

                    case ApConnectionTest.TestStatus.Failed:
                        // They have seen the flash and pressed again: that is
                        // consent to host anyway.
                        Plugin.Log.LogWarning(
                            $"[{nameof(HostMenuConfirmStartPatch)}] Hosting without a verified Archipelago connection "
                            + $"({ApConnectionTest.LastError}). The mod will keep retrying in the background.");
                        _bypass = true;
                        return true;

                    default:
                        StartProbe(endpoint);
                        return false;
                }
            }
        }

        private static void StartProbe(ApEndpoint endpoint)
        {
            Plugin.Log.LogInfo(
                $"[{nameof(HostMenuConfirmStartPatch)}] Testing the Archipelago connection to "
                + $"{endpoint.Host}:{endpoint.Port} as '{endpoint.SlotName}'...");

            // This probe belongs to Continue, so the Update postfix is
            // allowed to act on its result.
            _autoContinuePending = false;
            HostMenuArchipelagoControls.NoteTested(endpoint.SlotName);
            HostMenuArchipelagoControls.Owner = HostMenuArchipelagoControls.ProbeOwner.Continue;
            ApConnectionTest.Start(endpoint);
        }

        // The same test the slot-name lock uses: a save the game has already
        // written carries a file uid, a new one does not.
        private static bool IsNewSave(HostMenuConfirm menu)
        {
            var save = menu.saveData;
            return save == null || string.IsNullOrEmpty(save.filenameUid);
        }

        [HarmonyPatch(typeof(HostMenuConfirm), "Update")]
        internal static class Update
        {
            private static void Postfix(HostMenuConfirm __instance)
            {
                // Ahead of the early returns: the test button's own result
                // has to keep updating whether or not Archipelago is on and
                // whether or not someone has already consented to host
                // without it.
                HostMenuArchipelagoControls.Tick(__instance);

                if (_bypass || !ModConfig.ArchipelagoEnabled.Value)
                    return;

                // Only a probe THIS path started may be acted on. A probe the
                // player ran from the test button must never launch the
                // session, flash a field or set the host-anyway bypass —
                // they asked a question, they did not press Continue.
                if (HostMenuArchipelagoControls.Owner
                    != HostMenuArchipelagoControls.ProbeOwner.Continue)
                    return;

                switch (ApConnectionTest.Status)
                {
                    case ApConnectionTest.TestStatus.Ok when !_autoContinuePending:
                        _autoContinuePending = true;
                        Plugin.Log.LogInfo(
                            $"[{nameof(HostMenuConfirmStartPatch)}] Archipelago connection verified; starting the session.");
                        __instance.ActionStart();
                        break;

                    case ApConnectionTest.TestStatus.Failed when !_autoContinuePending:
                        _autoContinuePending = true;
                        if (IsNewSave(__instance))
                        {
                            Plugin.Log.LogWarning(
                                $"[{nameof(HostMenuConfirmStartPatch)}] Archipelago connection failed: {ApConnectionTest.LastError}. "
                                + "A new Archipelago game waits for a working connection: fix the fields and press Continue "
                                + "again, or switch AP : OFF to play without Archipelago.");
                        }
                        else
                        {
                            Plugin.Log.LogWarning(
                                $"[{nameof(HostMenuConfirmStartPatch)}] Archipelago connection failed: {ApConnectionTest.LastError}. "
                                + "Check the address, the slot name and the password — or press Continue again to host anyway.");
                        }

                        HostMenuArchipelagoControls.ShowLastFailure(__instance);
                        TryFlash(__instance);
                        break;
                }
            }

            // Flash() is kept in its own method and called through here so
            // that its failure stays a missing animation rather than an
            // exception escaping this Update postfix — i.e. once per frame,
            // exactly when a connection has just failed and the player needs
            // the log to be readable.
            //
            // It fails outright on older builds of the game: gameNameFlasher
            // and the InputWarningFlasher type are both absent on 1.48
            // (verified against its interop assembly, 2026-09-15), so the
            // JIT cannot even compile Flash there. The warning in the log
            // above says everything this animation says.
            internal static void FlashFields(HostMenuConfirm menu)
            {
                TryFlash(menu);
            }

            private static void TryFlash(HostMenuConfirm menu)
            {
                try
                {
                    Flash(menu);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(HostMenuConfirmStartPatch)}] No warning flash on this build of the game: {ex.Message}");
                }
            }

            // Reuses the game's own warning animation on the fields, the one
            // already used for an empty name or password, rather than
            // inventing a second visual language for the same idea.
            private static void Flash(HostMenuConfirm menu)
            {
                if (menu.gameNameFlasher != null)
                    menu.gameNameFlasher.Flash();

                // The host:port row is the clone HostMenuConfirmPatch put
                // next to the GameName row, so it is a sibling of that row,
                // not a child of the menu object.
                var gameNameRow = menu.gameNameField != null ? menu.gameNameField.transform.parent : null;
                var rows = gameNameRow != null ? gameNameRow.parent : null;
                var hostPortRow = rows != null ? rows.Find(HostMenuConfirmPatch.HostPortRowName) : null;
                if (hostPortRow == null)
                    return;

                var flasher = hostPortRow.GetComponentInChildren<InputWarningFlasher>();
                if (flasher != null)
                    flasher.Flash();
            }
        }

        [HarmonyPatch(typeof(HostMenuConfirm), nameof(HostMenuConfirm.OnEnable))]
        internal static class OnEnable
        {
            private static void Postfix()
            {
                // Coming back to this screen means the details may have
                // changed, so the previous verdict is worthless — including
                // a previous bypass, which was consent for that attempt
                // only.
                _bypass = false;
                _autoContinuePending = false;
                ApConnectionTest.Reset();
            }
        }
    }
}
