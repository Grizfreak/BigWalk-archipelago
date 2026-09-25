using System;
using kcp2k;
using Mirror;
using Mirror.Authenticators;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // What this process's Mirror setup actually is. Read-only: it calls
    // getters and nothing else.
    //
    // Written for the loopback guest — a second instance of the game joining
    // the first over 127.0.0.1 (docs/NEXT-SESSION.md, "Why two local
    // instances do not work"). The decompiled NetworkMinder.StartHost says a
    // host started while Steam is up runs a MultiplexTransport over
    // [KcpTransport, EosTransport] — that it ALREADY listens on UDP besides
    // EOS. No log to date can confirm it: KcpTransport says
    // "KcpTransport initialized!" in Awake and then nothing, because its Info
    // log is a no-op unless its debugLog is ticked, so a Kcp server starting
    // is silent. This dump says it from inside the game (serverActive on the
    // Kcp line), and Get-NetUDPEndpoint says it from outside.
    //
    // It also names the two things a second instance of the same Steam
    // account would share with the first: the player identifier sent to the
    // host (masked — it is a SteamID), which the host uses to hand a
    // returning player their Corpse, and the command line, which is where the
    // guest's role is meant to come from rather than from the shared .cfg.
    internal static class DebugNetworkLookup
    {
        private const string Tag = "[" + nameof(DebugNetworkLookup) + "]";

        internal static void Dump()
        {
            Plugin.Log.LogInfo($"{Tag} === network dump (process {Environment.ProcessId}) ===");

            // One section at a time, each on its own try: a getter that
            // throws must not hide the lines after it, and "the dump stopped
            // halfway" has to read differently from "there was nothing".
            Section("process", DumpProcess);
            Section("manager", DumpManager);
            Section("connections", DumpConnections);
            Section("input", DumpInput);

            Plugin.Log.LogInfo($"{Tag} === end of network dump ===");
        }

        private static void Section(string name, Action dump)
        {
            try
            {
                dump();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} The {name} section threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void DumpProcess()
        {
            Plugin.Log.LogInfo($"{Tag} Loopback role: {LoopbackGuest.RoleName}");
            Plugin.Log.LogInfo($"{Tag} Command line: {string.Join(" ", Environment.GetCommandLineArgs())}");
            Plugin.Log.LogInfo(
                $"{Tag} Application.runInBackground={Application.runInBackground}, isFocused={Application.isFocused}");
            Plugin.Log.LogInfo($"{Tag} SteamManager.Initialized={SteamManager.Initialized}");

            // The same call HouseAuthenticator.OnClientAuthenticate makes to
            // fill InitialialAuthRequestMessage.playerIdentifier; when it
            // fails, the game sends SystemInfo.deviceName instead.
            var found = HouseSteamManager.TryGetLocalUserIdentifier(out var identifier);
            Plugin.Log.LogInfo(
                $"{Tag} Player identifier this machine would send a host: "
                + (found ? Mask(identifier) : $"<none; the game falls back to the machine name, {Mask(SystemInfo.deviceName)}>"));
        }

        private static void DumpManager()
        {
            var manager = NetworkManager.singleton;
            if (manager == null)
            {
                Plugin.Log.LogInfo($"{Tag} NetworkManager.singleton is null.");
                return;
            }

            Plugin.Log.LogInfo(
                $"{Tag} NetworkManager.singleton: {manager.GetIl2CppType().FullName} on '{manager.gameObject.name}', "
                + $"mode={manager.mode}, isNetworkActive={manager.isNetworkActive}, "
                + $"networkAddress='{manager.networkAddress}', maxConnections={manager.maxConnections}, "
                + $"runInBackground={manager.runInBackground}");

            var authenticator = manager.authenticator;
            if (authenticator == null)
            {
                Plugin.Log.LogInfo($"{Tag} Authenticator: <none>");
            }
            else
            {
                // Whether a password is set, never the password itself.
                var house = authenticator.TryCast<HouseAuthenticator>();
                var password = house != null ? $", password set={!string.IsNullOrEmpty(house.password)}" : "";
                Plugin.Log.LogInfo($"{Tag} Authenticator: {authenticator.GetIl2CppType().FullName}{password}");
            }

            Plugin.Log.LogInfo($"{Tag} Transport.active: {Describe(Transport.active)}");
            Plugin.Log.LogInfo($"{Tag} NetworkManager.transport: {Describe(manager.transport)}");

            // Every transport under the manager, active or not: the question
            // is what the game COULD switch to, not only what it is using.
            var transports = manager.GetComponentsInChildren<Transport>(true);
            Plugin.Log.LogInfo($"{Tag} {transports.Length} Transport component(s) under '{manager.gameObject.name}':");
            foreach (var transport in transports)
                Plugin.Log.LogInfo($"{Tag}   {Describe(transport)}");
        }

        private static void DumpConnections()
        {
            Plugin.Log.LogInfo(
                $"{Tag} NetworkServer.active={NetworkServer.active}, NetworkClient.active={NetworkClient.active}, "
                + $"NetworkClient.isConnected={NetworkClient.isConnected}");

            if (!NetworkServer.active)
                return;

            Plugin.Log.LogInfo($"{Tag} {NetworkServer.connections.Count} server connection(s):");
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection == null)
                    continue;

                // The netId, not the object's name: the game names a player
                // "PlayerCharacter <identifier>-<n>", which would print in
                // full the SteamID the identifier column masks (measured
                // 2026-09-25).
                var player = connection.identity != null ? $"netId {connection.identity.netId}" : "<no player yet>";
                Plugin.Log.LogInfo(
                    $"{Tag}   #{connection.connectionId} address='{Safe(() => connection.address)}' "
                    + $"authenticated={connection.isAuthenticated} ready={connection.isReady} player={player} "
                    + $"identifier={Safe(() => Identifier(connection))}");
            }
        }

        // Which controllers this instance listens to, and whether it listens
        // at all while unfocused. The game reads its input through Rewired
        // (no Unity Input System in the build); this is what deciding
        // "keyboard for the host, gamepad for the guest" needs measured.
        private static void DumpInput()
        {
            if (!Rewired.ReInput.isReady)
            {
                Plugin.Log.LogInfo($"{Tag} Rewired is not ready.");
                return;
            }

            Plugin.Log.LogInfo(
                $"{Tag} Rewired: ignoreInputWhenAppNotInFocus={Rewired.ReInput.configuration.ignoreInputWhenAppNotInFocus}, "
                + $"joysticks connected={Rewired.ReInput.controllers.joystickCount}, "
                + $"players={Rewired.ReInput.players.playerCount}");

            for (var i = 0; i < Rewired.ReInput.players.playerCount; i++)
            {
                var player = Rewired.ReInput.players.GetPlayer(i);
                if (player == null)
                    continue;
                Plugin.Log.LogInfo(
                    $"{Tag}   player {player.id} '{player.name}' playing={player.isPlaying} "
                    + $"keyboard={player.controllers.hasKeyboard} mouse={player.controllers.hasMouse} "
                    + $"joysticks={player.controllers.joystickCount}");
            }
        }

        // The identifier the host received for this connection: what
        // Corpse.FindMatch is keyed on when a player comes back.
        private static string Identifier(NetworkConnection connection)
        {
            var data = connection.authenticationData;
            if (data == null)
                return "<none>";

            var request = data.TryCast<HouseAuthenticator.InitialialAuthRequestMessage>();
            return request != null
                ? Mask(request.playerIdentifier)
                : $"<{data.GetIl2CppType().FullName}>";
        }

        private static string Describe(Transport transport)
        {
            if (transport == null)
                return "<null>";

            var line =
                $"{transport.GetIl2CppType().FullName} on '{transport.gameObject.name}' enabled={transport.enabled} "
                + $"available={Safe(() => transport.Available())} serverActive={Safe(() => transport.ServerActive())} "
                + $"clientConnected={Safe(() => transport.ClientConnected())}";

            var kcp = transport.TryCast<KcpTransport>();
            if (kcp != null)
            {
                line += $" | port={kcp.port} dualMode={kcp.DualMode} noDelay={kcp.NoDelay} interval={kcp.Interval}ms "
                    + $"timeout={kcp.Timeout}ms maxRetransmit={kcp.MaxRetransmit} debugLog={kcp.debugLog}";
            }

            var multiplex = transport.TryCast<MultiplexTransport>();
            if (multiplex != null)
            {
                var inner = multiplex.transports;
                var names = new string[inner != null ? inner.Length : 0];
                for (var i = 0; i < names.Length; i++)
                    names[i] = inner[i] != null ? inner[i].GetIl2CppType().Name : "<null>";
                line += $" | transports=[{string.Join(", ", names)}]";
            }

            return line;
        }

        private static string Safe<T>(Func<T> read)
        {
            try
            {
                return read()?.ToString() ?? "<null>";
            }
            catch (Exception ex)
            {
                return $"<threw {ex.GetType().Name}>";
            }
        }

        // Enough to tell two identifiers apart in a log that gets pasted
        // around, not enough to be one.
        private static string Mask(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return "<empty>";
            return identifier.Length <= 4
                ? $"'{identifier}'"
                : $"'...{identifier.Substring(identifier.Length - 4)}' ({identifier.Length} chars)";
        }
    }
}
