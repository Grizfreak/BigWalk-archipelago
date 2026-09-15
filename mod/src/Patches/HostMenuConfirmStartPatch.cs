using BigWalkArchipelago.Core.Net;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // Checks the Archipelago details on the hosting screen before the
    // session starts, instead of letting the host find out from a log line
    // twenty minutes later that they mistyped the slot name. Idea noted on
    // 2026-09-15, unblocked once the network client existed.
    //
    // THE RULE THIS IS BUILT AROUND: it must never be able to stop someone
    // hosting. A failed probe flashes the offending field, logs why, and
    // then gets out of the way — pressing Continue again hosts regardless.
    // An Archipelago server being unreachable is not a reason to be unable
    // to launch the game, and a bug in this patch must not be either.
    //
    // First press runs the probe and holds the screen for the second or so
    // it takes; HostMenuConfirm.Update then continues automatically, so from
    // the outside it just looks like Continue took a moment.
    internal static class HostMenuConfirmStartPatch
    {
        private static bool _bypass;
        private static bool _autoContinuePending;

        [HarmonyPatch(typeof(HostMenuConfirm), nameof(HostMenuConfirm.ActionStart))]
        internal static class ActionStart
        {
            private static bool Prefix(HostMenuConfirm __instance)
            {
                if (_bypass || !ModConfig.ArchipelagoEnabled.Value)
                    return true;

                var slotName = __instance.gameNameField != null ? __instance.gameNameField.text : string.Empty;
                var password = __instance.passwordField != null ? __instance.passwordField.text : string.Empty;

                // Nothing usable to test yet (no address typed, no slot
                // name): say nothing and let them host. ApRuntime will
                // explain itself in the log once the session is up.
                if (!ApEndpoint.TryResolveFromFields(ApEndpoint.CurrentHostAndPort(), slotName, password,
                                                     out var endpoint, out _))
                    return true;

                switch (ApConnectionTest.Status)
                {
                    case ApConnectionTest.TestStatus.Ok:
                        return true;

                    case ApConnectionTest.TestStatus.Testing:
                        // Already in flight: swallow the press rather than
                        // starting a second probe.
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
                        Plugin.Log.LogInfo(
                            $"[{nameof(HostMenuConfirmStartPatch)}] Testing the Archipelago connection to "
                            + $"{endpoint.Host}:{endpoint.Port} as '{endpoint.SlotName}'...");
                        ApConnectionTest.Start(endpoint);
                        return false;
                }
            }
        }

        [HarmonyPatch(typeof(HostMenuConfirm), "Update")]
        internal static class Update
        {
            private static void Postfix(HostMenuConfirm __instance)
            {
                if (_bypass || !ModConfig.ArchipelagoEnabled.Value)
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
                        Plugin.Log.LogWarning(
                            $"[{nameof(HostMenuConfirmStartPatch)}] Archipelago connection failed: {ApConnectionTest.LastError}. "
                            + "Check the address, the slot name and the password — or press Continue again to host anyway.");
                        Flash(__instance);
                        break;
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
