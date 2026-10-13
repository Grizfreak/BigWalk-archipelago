using System;
using BigWalkArchipelago.Core.Net;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Testing without the clicks (player, 2026-10-13: "it would save me a lot of time"): with Debug.AutoHostSave set
    // to a save's name, the game goes from its title menu straight into hosting that save, the way a player would
    // (the microphone check's Continue, TitleMenu.GoToHostMenu, the save's card, HostMenuConfirm.ActionStart, PlayerCountMenu.ActionProceed); the
    // Archipelago slot is the save's own name and the server the .cfg's HostPort, as when a player hosts it. With
    // Debug.AutoHostGuest, the loopback guest is started once the world is up (it joins on its own). Once per launch;
    // the guest's own instance only continues past its microphone check.
    internal class AutoHost : MonoBehaviour
    {
        private const string Tag = "[" + nameof(AutoHost) + "]";
        private const float StepSeconds = 0.5f;
        private const float RetrySeconds = 3f;

        private float _stepAt;
        private float _confirmAt;
        private float _proceedAt;
        private bool _micDone;
        private bool _countChosen;
        private bool _hostAsked;
        private bool _slotPicked;
        private bool _done;
        private bool _missingLogged;

        public AutoHost(IntPtr ptr) : base(ptr)
        {
        }

        private void Update()
        {
            if (_done || Time.unscaledTime < _stepAt)
                return;
            _stepAt = Time.unscaledTime + StepSeconds;

            var save = ModConfig.AutoHostSave.Value?.Trim();
            if (string.IsNullOrEmpty(save))
            {
                _done = true;
                return;
            }

            try
            {
                if (WorldManager.isReadyForEffects)
                {
                    _done = true;
                    Plugin.Log.LogInfo($"{Tag} Hosting '{save}'.");
                    if (ModConfig.AutoHostGuest.Value && !LoopbackGuest.IsGuest)
                    {
                        Plugin.Log.LogInfo($"{Tag} Starting the loopback guest.");
                        LoopbackLauncher.LaunchGuest(withoutTheMod: false);
                    }
                    return;
                }

                var menus = MainMenuManager.instance;
                if (menus == null)
                    return;

                // The guest's own instance only gets past the microphone check; it joins on its own (LoopbackGuest).
                if (LoopbackGuest.IsGuest && _micDone)
                {
                    _done = true;
                    return;
                }

                // The microphone check at launch (player, 2026-10-13): continued as the player would.
                if (!_micDone && menus.micCheckMenu != null && menus.micCheckMenu.gameObject.activeInHierarchy)
                {
                    _micDone = true;
                    Plugin.Log.LogInfo($"{Tag} Microphone check: continue.");
                    menus.micCheckMenu.ActionContinue();
                    return;
                }

                if (LoopbackGuest.IsGuest)
                    return;

                if (!_hostAsked && menus.titleMenu != null && menus.titleMenu.gameObject.activeInHierarchy)
                {
                    _hostAsked = true;
                    Plugin.Log.LogInfo($"{Tag} Title menu: to the host menu.");
                    menus.titleMenu.GoToHostMenu();
                    return;
                }

                var select = menus.hostMenuSelect;
                if (!_slotPicked && select != null && select.gameObject.activeInHierarchy && select.cards != null && select.cards.Count > 0)
                {
                    foreach (var card in select.cards)
                    {
                        if (card == null || card.saveData == null || card.saveData.slotName != save)
                            continue;
                        _slotPicked = true;
                        Plugin.Log.LogInfo($"{Tag} The save '{save}': picked.");
                        card.PlaySlot();
                        return;
                    }
                    if (!_missingLogged)
                    {
                        _missingLogged = true;
                        Plugin.Log.LogWarning($"{Tag} No save called '{save}' on the host menu: create it once by hand.");
                    }
                    return;
                }

                var confirm = menus.hostMenuConfirm;
                if (confirm != null && confirm.gameObject.activeInHierarchy && Time.unscaledTime >= _confirmAt)
                {
                    _confirmAt = Time.unscaledTime + RetrySeconds;
                    Plugin.Log.LogInfo($"{Tag} Confirm screen: start.");
                    confirm.ActionStart();
                    return;
                }

                var count = menus.playerCountMenu;
                if (count != null && count.gameObject.activeInHierarchy && Time.unscaledTime >= _proceedAt)
                {
                    // Two players chosen first, as a player clicks the first choice of the row (player, 2026-10-13:
                    // Play waits for a count; the menu's own setters did not choose it), then Play a moment later.
                    if (!_countChosen && count.settingsRow != null)
                    {
                        _countChosen = true;
                        _proceedAt = Time.unscaledTime + 1f;
                        Plugin.Log.LogInfo($"{Tag} Player count: two.");
                        count.settingsRow.ActionSelect0();
                        return;
                    }
                    _proceedAt = Time.unscaledTime + RetrySeconds;
                    Plugin.Log.LogInfo($"{Tag} Player count: play.");
                    count.ActionProceed();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} {ex.Message}");
            }
        }
    }
}
