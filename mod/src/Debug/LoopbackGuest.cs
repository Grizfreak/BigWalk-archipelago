using System;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // This process's part in a loopback co-op test: two instances of the game
    // on one PC, the second joining the first as a real Mirror client over
    // KcpTransport on 127.0.0.1, so that everything a guest sees and does can
    // be tested without a second machine. tools/launch-guest.ps1 starts the
    // guest; the host is the ordinary instance, launched through Steam, and
    // runs none of this: a host started while Steam is up already listens on
    // UDP 7777 besides EOS (NetworkMinder.StartHost puts a MultiplexTransport
    // over [KcpTransport, EosTransport]; measured 2026-09-25).
    //
    // The role comes from the command line and never from the .cfg: both
    // instances run from the same install and read the same config file, so
    // a setting there could not tell them apart.
    //
    // Nothing here does anything unless Debug.Enabled is on: a player who
    // happens to launch the game with these arguments gets the vanilla game.
    internal static class LoopbackGuest
    {
        internal const string GuestArgument = "--bwap-guest";

        // Passed by the launcher when the host was already listening on UDP
        // 7777: join as soon as the title menu is up, instead of on Ctrl+L.
        internal const string JoinArgument = "--bwap-join";

        internal const string HostAddress = "127.0.0.1";

        private const string Tag = "[" + nameof(LoopbackGuest) + "]";

        internal static bool IsGuest { get; private set; }

        internal static bool AutoJoin { get; private set; }

        internal static string RoleName => IsGuest ? "loopback guest" : "host or solo";

        // Called once from Plugin.Load, inside the debug block. Says what it
        // decided either way: a role that is only logged when it is "guest"
        // cannot tell a missing argument from a probe that never ran.
        internal static void DetectRole()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, GuestArgument, StringComparison.OrdinalIgnoreCase))
                    IsGuest = true;
                else if (string.Equals(argument, JoinArgument, StringComparison.OrdinalIgnoreCase))
                    AutoJoin = true;
            }

            AutoJoin &= IsGuest;

            Plugin.Log.LogInfo(IsGuest
                ? $"{Tag} Role: loopback guest ({GuestArgument} is on the command line); "
                  + (AutoJoin ? $"joins {HostAddress} as soon as the title menu is up." : "press Ctrl+L to join.")
                : $"{Tag} Role: host or solo (no {GuestArgument} on the command line).");
        }

        // The two patches below exist for the guest only and are applied only
        // there. Each on its own try: a patch that fails to bind must not
        // take the rest of the plugin with it (see PlayerCountProceedLog).
        internal static void TryApply(Harmony harmony)
        {
            if (!IsGuest)
                return;

            TryPatch(harmony, "the player identifier",
                () => harmony.Patch(
                    AccessTools.Method(typeof(HouseSteamManager), nameof(HouseSteamManager.TryGetLocalUserIdentifier)),
                    postfix: new HarmonyMethod(typeof(LoopbackGuest), nameof(IdentifierPostfix))));

            TryPatch(harmony, "OnClientConnect",
                () => harmony.Patch(
                    AccessTools.Method(typeof(HouseNetworkManager), nameof(HouseNetworkManager.OnClientConnect)),
                    prefix: new HarmonyMethod(typeof(LoopbackGuest), nameof(OnClientConnectPrefix))));
        }

        private static void TryPatch(Harmony harmony, string what, Action patch)
        {
            try
            {
                patch();
                Plugin.Log.LogInfo($"{Tag} Patched {what}.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not patch {what}: {ex.Message}");
            }
        }

        // Both instances run on the same Steam account, so both would send the
        // host the same SteamID as their player identifier (measured with
        // Ctrl+N, 2026-09-25). The host keys things on it: Corpse.FindMatch
        // hands a returning player the pack and holster they left behind, and
        // a player object is named "PlayerCharacter <identifier>-<n>". Two
        // players with one identifier is not a situation a real session ever
        // produces, so the guest reports none — and every caller then falls
        // back on its own: HouseAuthenticator sends SystemInfo.deviceName
        // instead, as the game does whenever Steam is not up. Returning false
        // rather than writing another string keeps the out parameter out of
        // Harmony's hands.
        private static void IdentifierPostfix(ref bool __result)
        {
            __result = false;
        }

        // HouseNetworkManager.OnClientConnect, on a client that is not also
        // the host, readies the connection, asks for a player, then builds a
        // LobbyInfo from EOSLobbyManager.currentLobbyInfo and has an EOS lobby
        // created from it (decompiled 2026-09-25). That field is only filled
        // by joining through an EOS lobby; after a join by IP it is null and
        // the method throws a NullReferenceException — and Mirror disconnects
        // a connection whose message handler throws, which is where this call
        // comes from (the authenticator's response).
        //
        // So, only when that field is empty: do what the method does before
        // the lobby, verbatim, and skip the lobby, which is EOS presence and
        // no part of what a loopback guest is for. When it is set, the game's
        // own method runs untouched.
        private static bool OnClientConnectPrefix(HouseNetworkManager __instance)
        {
            try
            {
                if (NetworkServer.active)
                    return true;

                var lobbies = EOSLobbyManager.Instance;
                if (lobbies != null && lobbies.currentLobbyInfo != null)
                    return true;

                if (!__instance.clientLoadedScene)
                {
                    if (!NetworkClient.ready)
                        NetworkClient.Ready();
                    if (__instance.autoCreatePlayer)
                        NetworkClient.AddPlayer();
                }

                Plugin.Log.LogInfo(
                    $"{Tag} OnClientConnect: no EOS lobby behind this connection, so the connection was readied "
                    + "and a player asked for, and the EOS lobby part was skipped (it would have thrown).");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} OnClientConnect prefix threw, leaving it to the game: {ex.Message}");
                return true;
            }
        }

        // The game's own join, as JoinFriendCard.ActionJoin does it: the
        // transport chosen from the address (not a number, so Kcp) and the
        // client started, then the current menu swapped for the connecting
        // one. That menu matters beyond looks: the authenticator's password
        // and error paths switch it off and something else on.
        internal static void Join()
        {
            if (!IsGuest)
            {
                Plugin.Log.LogInfo($"{Tag} Only the loopback guest joins (tools/launch-guest.ps1 starts it).");
                return;
            }

            if (NetworkServer.active || NetworkClient.active)
            {
                Plugin.Log.LogInfo(
                    $"{Tag} Not joining: already {(NetworkServer.active ? "hosting" : "connected or connecting")}.");
                return;
            }

            Plugin.Log.LogInfo($"{Tag} Joining {HostAddress} over Kcp (NetworkMinder.SetTransportAndConnect)...");
            NetworkMinder.SetTransportAndConnect(HostAddress);
            ShowConnectingMenu();
        }

        // Where a summoned guest lands: beside the host, not inside them.
        private const float SummonSideOffset = 1.5f;
        private const float SummonLift = 0.5f;

        // Host side, on its key: sends the host's position to the loopback
        // guest through the ModChannel.
        internal static void SummonGuest()
        {
            if (!NetworkServer.active)
            {
                Plugin.Log.LogInfo($"{Tag} Only the host calls the guest over.");
                return;
            }

            var host = DebugPlayerLookup.FindLocalPlayer();
            if (host == null)
            {
                Plugin.Log.LogInfo($"{Tag} No local player to call the guest to.");
                return;
            }

            var sent = BigWalkArchipelago.Core.Net.ModChannel.SendSummon(host.transform.position, host.transform.rotation);
            Plugin.Log.LogInfo(sent > 0
                ? $"{Tag} Called {sent} loopback guest(s) over to {host.transform.position}."
                : $"{Tag} No loopback guest to call: none connected from this PC has said hello.");
        }

        // Guest side, from the ModChannel: the game's own player teleport,
        // the one its puzzle teleporters use (PeckEffectTeleporter.Peck ->
        // PlayerGrease.Teleport, decompiled 2026-09-26). A player's position
        // belongs to its owner (HouseNetworkTransform sends CmdMove), so a
        // teleport done here is what the host sees too.
        internal static void OnSummoned(Vector3 hostPosition, Quaternion hostRotation)
        {
            if (!IsGuest)
                return;

            var me = DebugPlayerLookup.FindLocalPlayer();
            if (me == null || me.grease == null)
            {
                Plugin.Log.LogInfo($"{Tag} Called over by the host, but there is no local player to move yet.");
                return;
            }

            var destination = hostPosition + hostRotation * Vector3.right * SummonSideOffset + Vector3.up * SummonLift;
            var marker = new GameObject("Loopback summon point");
            try
            {
                marker.transform.SetPositionAndRotation(destination, hostRotation);
                me.grease.Teleport(marker.transform);
                Plugin.Log.LogInfo($"{Tag} Called over by the host; teleported to {destination}.");
            }
            finally
            {
                UnityEngine.Object.Destroy(marker);
            }
        }

        private static void ShowConnectingMenu()
        {
            try
            {
                var menus = MainMenuManager.instance;
                if (menus == null)
                    return;
                if (menus.titleMenu != null)
                    menus.titleMenu.gameObject.SetActive(false);
                if (menus.connectingMenu != null)
                    menus.connectingMenu.gameObject.SetActive(true);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not show the connecting menu: {ex.Message}");
            }
        }
    }
}
