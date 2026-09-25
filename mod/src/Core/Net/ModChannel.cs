using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core.Net
{
    // The host telling its guests what only the host can know.
    //
    // Only the host runs an Archipelago client, so only the host ever learns
    // that the slot is connected, what it is playing for, what was just sent
    // or received — and which radio stations have been granted. Measured in
    // co-op on 2026-09-23: a guest saw none of the overlay, and never heard a
    // note of radio music, before or after the item arrived. Both need the
    // host to say something, which is what this carries.
    //
    // HOW, and whose recipe it is. A custom Mirror message was long thought
    // closed to this mod, because Il2CppInterop cannot marshal a delegate
    // over a non-blittable struct and Mirror's typed messages are exactly
    // that. The low-level route is not typed: NetworkClient.handlers and
    // NetworkServer.handlers map a ushort to a NetworkMessageDelegate taking
    // (NetworkConnection, NetworkReader, int), and a raw NetworkWriter goes
    // out through NetworkConnection.Send. The Big TV mod does precisely this
    // on the very same game build, which is where the recipe was read from
    // (its member references, 2026-09-23), not guessed at.
    //
    // THE ONE RULE THAT SHAPES EVERYTHING BELOW. This build of Mirror
    // disconnects a peer that receives a message id it has no handler for —
    // "NetworkClient: failed to unpack and invoke message. Disconnecting."
    // and the server's twin, both read out of global-metadata.dat. So neither
    // side may ever send first to a peer that has not proven it runs this
    // mod, or a player who kept the mod installed would be thrown out of
    // every vanilla game they joined. Hence the handshake:
    //
    //   1. The host spawns a BEACON — an empty networked object under an
    //      asset id of ours. An unknown spawn is not fatal (a guest without a
    //      handler logs "Could not spawn assetId" and carries on, observed
    //      2026-09-22), and ForceShown puts it past any interest management.
    //   2. A guest whose mod builds the beacon now KNOWS the host runs the
    //      mod, and only then says Hello.
    //   3. The host only ever writes to a connection that said Hello.
    //
    // What goes across is a whole snapshot, once a second, rather than a
    // stream of changes: a guest joining late, or one that missed a message,
    // is caught up by the next one, and there is no state to fall out of
    // step. It is a few hundred bytes.
    internal class ModChannel : MonoBehaviour
    {
        // Constructor required by Il2CppInterop for any type injected into IL2CPP.
        public ModChannel(IntPtr ptr) : base(ptr)
        {
        }

        // Clear of Big TV's 0xB17B, and checked against whatever else is
        // registered before being claimed.
        internal const ushort MessageId = 0xB1A9;

        // In the mod's own asset id block: clear of the cosmetic gourd
        // (0xB16_9A00), the retired per-kind gadget ids (0xB16_9B01 to 0xB16_9B11)
        // and the per-instance ones (0xB16_9C00 to 0xB16_9E1F).
        internal const uint BeaconAssetId = 0xB16_9BFE;

        // Both ends must speak the same version; a mismatch is logged once
        // and ignored rather than half-read.
        private const byte ProtocolVersion = 1;

        private const byte KindHello = 1;
        private const byte KindSnapshot = 2;

        // Debug only: the host calling the loopback guest to its side (see
        // Debug/LoopbackGuest.cs). Only ever sent to a connection from this
        // same PC that said hello, so no remote player receives it; a guest
        // that does not know it ignores it, like any kind but a snapshot.
        private const byte KindSummon = 3;

        private const float SnapshotIntervalSeconds = 1f;

        // A guest stops showing the host's overlay once the host has been
        // silent this long: whatever it last said is no longer true.
        private const float SnapshotStaleSeconds = 5f;

        // --- host state ---
        private static readonly HashSet<int> Announced = new();
        private static GameObject _beacon;
        private static bool _serverHandlerRegistered;
        private float _nextSnapshot;

        // --- guest state ---
        private static bool _clientHandlerRegistered;
        private static bool _beaconHandlerRegistered;
        private static bool _hostHasMod;
        private static bool _helloSent;
        private static bool _versionMismatchLogged;

        // --- what a guest was last told ---
        internal sealed class MirroredLine
        {
            internal string Text;
            internal bool Warning;
        }

        internal static string MirroredStatus { get; private set; }
        internal static bool MirroredStatusIsWarning { get; private set; }
        internal static string MirroredGoal { get; private set; }
        internal static bool MirroredGoalIsWarning { get; private set; }
        internal static readonly List<MirroredLine> MirroredLines = new();
        private static float _receivedAt = -1f;

        // True on a guest that has heard from its host recently enough to
        // believe it. Never true on the host, which has the real thing.
        internal static bool HasFreshSnapshot =>
            !NetworkServer.active
            && _receivedAt >= 0f
            && Time.unscaledTime - _receivedAt < SnapshotStaleSeconds;

        private void Update()
        {
            try
            {
                if (NetworkServer.active)
                    TickHost();
                else
                    ResetHost();

                if (NetworkClient.active && !NetworkServer.active)
                    TickGuest();
                else
                    ResetGuest();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Tick failed, ignored: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Host
        // ------------------------------------------------------------------

        [HideFromIl2Cpp]
        private void TickHost()
        {
            if (!_serverHandlerRegistered || !NetworkServer.handlers.ContainsKey(MessageId))
                RegisterServerHandler();

            if (WorldManager.isReadyForEffects && _beacon == null)
                SpawnBeacon();

            PruneAnnounced();

            if (Time.unscaledTime < _nextSnapshot || Announced.Count == 0)
                return;

            _nextSnapshot = Time.unscaledTime + SnapshotIntervalSeconds;
            SendSnapshotToAnnounced();
        }

        private static void ResetHost()
        {
            Announced.Clear();
            _beacon = null;
            _serverHandlerRegistered = false;
        }

        private static void RegisterServerHandler()
        {
            var handlers = NetworkServer.handlers;
            if (handlers == null)
                return;

            if (handlers.ContainsKey(MessageId))
            {
                // Ours from a previous session is fine to replace; anybody
                // else's is not ours to take, and taking it would break
                // whatever owns it.
                if (_serverHandlerRegistered)
                    handlers.Remove(MessageId);
                else
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(ModChannel)}] Message id 0x{MessageId:X4} is already taken on the server; "
                        + "guests will not be told anything this session.");
                    _serverHandlerRegistered = true;
                    return;
                }
            }

            NetworkMessageDelegate handler = (Action<NetworkConnection, NetworkReader, int>)OnServerMessage;
            handlers[MessageId] = handler;
            _serverHandlerRegistered = true;
            Plugin.Log.LogInfo($"[{nameof(ModChannel)}] Listening for guests on 0x{MessageId:X4}.");
        }

        private static void SpawnBeacon()
        {
            try
            {
                var beacon = new GameObject("Big Walk Archipelago beacon (AP)");
                var identity = beacon.AddComponent<NetworkIdentity>();

                // Past any interest management: a guest standing far from
                // wherever this lands must still see it, or it would never
                // learn the host runs the mod.
                identity.visible = Visibility.ForceShown;

                NetworkServer.Spawn(beacon, BeaconAssetId);
                _beacon = beacon;
                Plugin.Log.LogInfo(
                    $"[{nameof(ModChannel)}] Beacon spawned (netId {identity.netId}); guests running the mod will say hello.");
            }
            catch (Exception ex)
            {
                // Not retried every frame: a beacon that cannot spawn will
                // not spawn a frame later either.
                _beacon = new GameObject("Big Walk Archipelago beacon (failed)");
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Could not spawn the beacon: {ex.Message}");
            }
        }

        private static void PruneAnnounced()
        {
            if (Announced.Count == 0)
                return;

            var live = new HashSet<int>();
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection != null)
                    live.Add(connection.connectionId);
            }

            Announced.RemoveWhere(id => !live.Contains(id));
        }

        // Runs inside Mirror's dispatch: anything thrown here is a failed
        // message, and a failed message disconnects the sender. Nothing may
        // escape it.
        private static void OnServerMessage(NetworkConnection connection, NetworkReader reader, int channelId)
        {
            try
            {
                var kind = reader.ReadByte();
                if (kind != KindHello)
                    return;

                var version = reader.ReadByte();
                if (version != ProtocolVersion)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(ModChannel)}] Guest #{connection.connectionId} runs protocol v{version}, this host "
                        + $"v{ProtocolVersion}; not mirroring to it. Both players need the same build of the mod.");
                    return;
                }

                if (Announced.Add(connection.connectionId))
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(ModChannel)}] Guest #{connection.connectionId} runs the mod; mirroring the overlay "
                        + "and the radio to it.");
                    SendSnapshot(connection);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Unreadable message from a guest, ignored: {ex.Message}");
            }
        }

        private static void SendSnapshotToAnnounced()
        {
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection != null && Announced.Contains(connection.connectionId))
                    SendSnapshot(connection);
            }
        }

        private static void SendSnapshot(NetworkConnection connection)
        {
            try
            {
                var writer = new NetworkWriter();
                NetworkWriterExtensions.WriteUShort(writer, MessageId);
                writer.WriteByte(KindSnapshot);
                writer.WriteByte(ProtocolVersion);

                NetworkWriterExtensions.WriteString(writer, ApRuntime.StatusMessage ?? string.Empty);
                NetworkWriterExtensions.WriteBool(writer, ApRuntime.StatusIsWarning);
                NetworkWriterExtensions.WriteString(writer, ApRuntime.GoalLine ?? string.Empty);
                NetworkWriterExtensions.WriteBool(writer, ApRuntime.GoalLineIsWarning);

                var lines = ApNotices.Lines;
                var count = Math.Min(lines.Count, byte.MaxValue);
                writer.WriteByte((byte)count);
                for (var i = 0; i < count; i++)
                {
                    NetworkWriterExtensions.WriteString(writer, lines[i].Display ?? string.Empty);
                    NetworkWriterExtensions.WriteBool(writer, lines[i].Warning);
                }

                NetworkWriterExtensions.WriteBool(writer, RadioStations.ItemsInPlay);
                var granted = new List<SavableSystem>();
                foreach (var system in RadioStations.All)
                {
                    if (RadioStations.IsGranted(system))
                        granted.Add(system);
                }

                writer.WriteByte((byte)granted.Count);
                foreach (var system in granted)
                    writer.WriteByte((byte)(int)system);

                connection.Send(writer.ToArraySegment(), 0);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ModChannel)}] Could not send a snapshot to guest #{connection.connectionId}: {ex.Message}");
            }
        }

        // Sends the summon to every loopback guest that said hello; returns
        // how many got it.
        internal static int SendSummon(Vector3 position, Quaternion rotation)
        {
            var sent = 0;
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection == null || !Announced.Contains(connection.connectionId) || !IsFromThisMachine(connection))
                    continue;

                try
                {
                    var writer = new NetworkWriter();
                    NetworkWriterExtensions.WriteUShort(writer, MessageId);
                    writer.WriteByte(KindSummon);
                    writer.WriteByte(ProtocolVersion);
                    NetworkWriterExtensions.WriteFloat(writer, position.x);
                    NetworkWriterExtensions.WriteFloat(writer, position.y);
                    NetworkWriterExtensions.WriteFloat(writer, position.z);
                    NetworkWriterExtensions.WriteFloat(writer, rotation.eulerAngles.y);
                    connection.Send(writer.ToArraySegment(), 0);
                    sent++;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning(
                        $"[{nameof(ModChannel)}] Could not send the summon to guest #{connection.connectionId}: {ex.Message}");
                }
            }

            return sent;
        }

        // A guest on this same PC: connection 0 is the host's own local
        // client, and every other connection's address is the remote end
        // (measured 2026-09-26: '127.0.0.1' for the loopback guest).
        private static bool IsFromThisMachine(NetworkConnectionToClient connection)
        {
            if (connection.connectionId == NetworkConnection.LocalConnectionId)
                return false;
            if (!System.Net.IPAddress.TryParse(connection.address, out var address))
                return false;
            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();
            return System.Net.IPAddress.IsLoopback(address);
        }

        // ------------------------------------------------------------------
        // Guest
        // ------------------------------------------------------------------

        [HideFromIl2Cpp]
        private static void TickGuest()
        {
            if (!_beaconHandlerRegistered || !NetworkClient.spawnHandlers.ContainsKey(BeaconAssetId))
                RegisterBeaconHandler();

            if (!_clientHandlerRegistered || !NetworkClient.handlers.ContainsKey(MessageId))
                RegisterClientHandler();

            if (!WorldManager.isReadyForEffects)
                RadioStations.ForgetMirroredWorld();

            if (_hostHasMod && !_helloSent && _clientHandlerRegistered)
                SendHello();
        }

        private static void ResetGuest()
        {
            if (_hostHasMod || _receivedAt >= 0f)
                RadioStations.ForgetMirror();

            _clientHandlerRegistered = false;
            _beaconHandlerRegistered = false;
            _hostHasMod = false;
            _helloSent = false;
            _versionMismatchLogged = false;
            _receivedAt = -1f;
            MirroredStatus = null;
            MirroredGoal = null;
            MirroredLines.Clear();
        }

        private static void RegisterBeaconHandler()
        {
            NetworkClient.RegisterSpawnHandler(BeaconAssetId, (SpawnDelegate)BuildBeacon, (UnSpawnDelegate)DestroyBeacon);
            _beaconHandlerRegistered = true;
        }

        private static void RegisterClientHandler()
        {
            var handlers = NetworkClient.handlers;
            if (handlers == null)
                return;

            if (handlers.ContainsKey(MessageId) && !_clientHandlerRegistered)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ModChannel)}] Message id 0x{MessageId:X4} is already taken on this client; "
                    + "the host's overlay and radio will not reach this player.");
                _clientHandlerRegistered = true;
                return;
            }

            NetworkMessageDelegate handler = (Action<NetworkConnection, NetworkReader, int>)OnClientMessage;
            handlers[MessageId] = handler;
            _clientHandlerRegistered = true;
        }

        // Same rule as every spawn handler in this mod: never null.
        private static GameObject BuildBeacon(Vector3 position, uint assetId)
        {
            var beacon = new GameObject("Big Walk Archipelago beacon (AP)");
            beacon.AddComponent<NetworkIdentity>();
            NoteHostHasMod("its beacon arrived");
            return beacon;
        }

        private static void DestroyBeacon(GameObject beacon)
        {
            if (beacon != null)
                UnityEngine.Object.Destroy(beacon);
        }

        // Also called by the cosmetic spawn handlers: an asset id of ours can
        // only come from a host running this mod, so any of them is proof
        // enough, and a second route in case the beacon was ever missed.
        internal static void NoteHostHasMod(string evidence)
        {
            if (_hostHasMod || NetworkServer.active)
                return;

            _hostHasMod = true;
            Plugin.Log.LogInfo($"[{nameof(ModChannel)}] The host runs the mod ({evidence}); saying hello.");
        }

        private static void SendHello()
        {
            try
            {
                var connection = NetworkClient.connection;
                if (connection == null)
                    return;

                var writer = new NetworkWriter();
                NetworkWriterExtensions.WriteUShort(writer, MessageId);
                writer.WriteByte(KindHello);
                writer.WriteByte(ProtocolVersion);
                connection.Send(writer.ToArraySegment(), 0);
                _helloSent = true;
            }
            catch (Exception ex)
            {
                _helloSent = true;
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Could not say hello to the host: {ex.Message}");
            }
        }

        // Same rule as OnServerMessage: nothing may escape it.
        private static void OnClientMessage(NetworkConnection connection, NetworkReader reader, int channelId)
        {
            try
            {
                var kind = reader.ReadByte();
                if (kind == KindSummon)
                {
                    OnSummon(reader);
                    return;
                }

                if (kind != KindSnapshot)
                    return;

                var version = reader.ReadByte();
                if (version != ProtocolVersion)
                {
                    if (!_versionMismatchLogged)
                    {
                        _versionMismatchLogged = true;
                        Plugin.Log.LogWarning(
                            $"[{nameof(ModChannel)}] The host speaks protocol v{version}, this build v{ProtocolVersion}; "
                            + "ignoring it. Both players need the same build of the mod.");
                    }
                    return;
                }

                var status = NetworkReaderExtensions.ReadString(reader);
                var statusWarning = NetworkReaderExtensions.ReadBool(reader);
                var goal = NetworkReaderExtensions.ReadString(reader);
                var goalWarning = NetworkReaderExtensions.ReadBool(reader);

                var lines = new List<MirroredLine>();
                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    lines.Add(new MirroredLine
                    {
                        Text = NetworkReaderExtensions.ReadString(reader),
                        Warning = NetworkReaderExtensions.ReadBool(reader),
                    });
                }

                var radioItemsInPlay = NetworkReaderExtensions.ReadBool(reader);
                var granted = new List<SavableSystem>();
                var grantedCount = reader.ReadByte();
                for (var i = 0; i < grantedCount; i++)
                    granted.Add((SavableSystem)reader.ReadByte());

                var first = _receivedAt < 0f;

                MirroredStatus = string.IsNullOrEmpty(status) ? null : status;
                MirroredStatusIsWarning = statusWarning;
                MirroredGoal = string.IsNullOrEmpty(goal) ? null : goal;
                MirroredGoalIsWarning = goalWarning;
                MirroredLines.Clear();
                MirroredLines.AddRange(lines);
                _receivedAt = Time.unscaledTime;

                if (first)
                    Plugin.Log.LogInfo(
                        $"[{nameof(ModChannel)}] First word from the host: '{MirroredStatus}'. Showing the overlay here too.");

                RadioStations.ApplyFromHost(radioItemsInPlay, granted);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Unreadable snapshot from the host, ignored: {ex.Message}");
            }
        }

        // Called from OnClientMessage's try, so nothing escapes it either.
        private static void OnSummon(NetworkReader reader)
        {
            if (reader.ReadByte() != ProtocolVersion)
                return;

            var position = new Vector3(
                NetworkReaderExtensions.ReadFloat(reader),
                NetworkReaderExtensions.ReadFloat(reader),
                NetworkReaderExtensions.ReadFloat(reader));
            var yaw = NetworkReaderExtensions.ReadFloat(reader);

            BigWalkArchipelago.Debug.LoopbackGuest.OnSummoned(position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
