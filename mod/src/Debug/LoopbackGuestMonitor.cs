using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Runs in the loopback guest only (added by Plugin.Load when
    // LoopbackGuest.IsGuest). Two jobs:
    //
    // - joins on its own once the title menu is up, when the launcher passed
    //   --bwap-join (it does when the host was already listening);
    // - says every step of the connection as it happens, failure included:
    //   connecting, connected, authenticated, ready, player spawned,
    //   disconnected. "Nothing happened" and "it stopped at step three" have
    //   to read differently in the guest's log.
    internal class LoopbackGuestMonitor : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public LoopbackGuestMonitor(IntPtr ptr) : base(ptr)
        {
        }

        private const string Tag = "[" + nameof(LoopbackGuestMonitor) + "]";

        private bool _autoJoinDone;
        private string _lastState;
        private float _stateSince;
        private bool _errorLogged;

        private void Update()
        {
            try
            {
                if (LoopbackGuest.AutoJoin && !_autoJoinDone && TitleMenuIsUp())
                {
                    _autoJoinDone = true;
                    Plugin.Log.LogInfo($"{Tag} Title menu is up; joining on my own ({LoopbackGuest.JoinArgument}).");
                    LoopbackGuest.Join();
                }

                var state = CurrentState();
                if (state != _lastState)
                {
                    var now = Time.realtimeSinceStartup;
                    var after = _lastState == null ? "" : $" (after {now - _stateSince:0.0}s as '{_lastState}')";
                    Plugin.Log.LogInfo($"{Tag} Connection: {state}{after}.");
                    _lastState = state;
                    _stateSince = now;
                }
            }
            catch (Exception ex)
            {
                // Once: an Update that throws does so every frame.
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Log.LogWarning($"{Tag} Update threw (logged once): {ex.Message}");
                }
            }
        }

        private static bool TitleMenuIsUp()
        {
            var menus = MainMenuManager.instance;
            return menus != null && menus.titleMenu != null && menus.titleMenu.gameObject.activeInHierarchy;
        }

        private static string CurrentState()
        {
            if (NetworkServer.active)
                return "hosting (this is not what a loopback guest is for)";
            if (!NetworkClient.active)
                return "offline";
            if (!NetworkClient.isConnected)
                return "connecting";

            var connection = NetworkClient.connection;
            if (connection == null || !connection.isAuthenticated)
                return "connected, not authenticated yet";
            if (!NetworkClient.ready)
                return "authenticated, not ready yet";
            if (NetworkClient.localPlayer == null)
                return "ready, waiting for the player object";
            return $"in the world (local player netId {NetworkClient.localPlayer.netId})";
        }
    }
}
