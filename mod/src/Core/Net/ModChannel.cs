using System;
using System.Collections.Generic;
using System.Linq;
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
        private const byte ProtocolVersion = 12;

        private const byte KindHello = 1;
        private const byte KindSnapshot = 2;

        // Debug only: the host calling the loopback guest to its side (see
        // Debug/LoopbackGuest.cs). Only ever sent to a connection from this
        // same PC that said hello, so no remote player receives it; a guest
        // that does not know it ignores it, like any kind but a snapshot.
        private const byte KindSummon = 3;

        // The host tells a guest that the guest pressed one of the mod's world buttons: the press
        // reaches the host's copy of the button (the game is host-authoritative), but what the
        // button does, a teleport, has to happen on the machine of the player it moves.
        private const byte KindPress = 4;

        // The host tells a guest something for its feed (a press of its refused on the host).
        private const byte KindNotice = 5;

        // The host tells a guest to play a trap or a bonus on its own player (Core/TrapEffects),
        // with where to go for Big Trip and Big Meeting.
        private const byte KindEffect = 6;

        // A guest tells the host whether a puzzle's mask is on its screen, which Big Trip and Big
        // Meeting read (Core/Traps). The only kind a guest sends besides hello.
        private const byte KindStatus = 7;

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

        // ------------------------------------------------------------------
        // Builds that do not match (0.4.0, player 2026-10-09). A guest on another build of the mod
        // ignores its host, and the host its guest, and until now only the log said so: the screen
        // stayed empty. These lines are drawn by the overlay (ApStatusOverlay), in warning colour.
        // ------------------------------------------------------------------

        private const float SilentGuestSeconds = 60f;
        private const float UnansweredHelloSeconds = 25f;

        // Host: guests whose hello named another protocol, and guests with a player that never said
        // hello at all (no mod, or a build too old to).
        private static readonly Dictionary<int, byte> MismatchedGuests = new();
        private static readonly Dictionary<int, float> FirstSeen = new();
        private static readonly HashSet<int> SilentGuests = new();

        // Guest: the host spoke another protocol, or never answered our hello.
        private static bool _hostIsAnotherBuild;
        private static float _helloAt = -1f;

        internal static List<string> VersionWarnings()
        {
            var lines = new List<string>();
            if (NetworkServer.active)
            {
                foreach (var id in MismatchedGuests.Keys)
                    lines.Add($"{PlayerLabel(id)} runs another build of the mod: they see none of this session's overlay, traps or colors. Everyone needs the same build.");
                foreach (var id in SilentGuests)
                    lines.Add($"{PlayerLabel(id)} has not said hello after {SilentGuestSeconds:0}s: no mod, or an old build. Everyone needs the same build.");
                return lines;
            }

            var unanswered = _hostHasMod && _helloSent && _receivedAt < 0f && _helloAt >= 0f
                             && Time.unscaledTime - _helloAt > UnansweredHelloSeconds;
            if (_hostIsAnotherBuild || unanswered)
                lines.Add("The host runs another build of the mod: update yours so both match, or you will see none of its overlay, traps or colors.");
            return lines;
        }

        // A guest as the host names them on screen: the player's own name (PlayerNetworking.
        // moderationNameSanitized, their Steam or Epic pseudo; `username` is only an internal id,
        // "127.0.0.1-1" on a loopback guest, measured 2026-10-09), else the connection's number.
        // Never the platform id.
        internal static string PlayerLabel(int connectionId)
        {
            try
            {
                if (NetworkServer.connections.TryGetValue(connectionId, out var connection) && connection?.identity != null)
                {
                    var networking = connection.identity.GetComponentInChildren<PlayerNetworking>();
                    var name = networking != null ? networking.moderationNameSanitized : null;
                    if (string.IsNullOrWhiteSpace(name) && networking != null)
                        name = networking.moderationName;
                    if (!string.IsNullOrWhiteSpace(name))
                        return name.Length > 24 ? name.Substring(0, 24) : name.Trim();
                }
            }
            catch (Exception)
            {
                // The label is a courtesy: the number does.
            }

            return $"Player #{connectionId}";
        }

        // Host, about once a second: who is on this session and has not said hello.
        private static float _nextGuestCheck;

        private static void TrackGuests()
        {
            if (Time.unscaledTime < _nextGuestCheck)
                return;
            _nextGuestCheck = Time.unscaledTime + 1f;

            var live = new HashSet<int>();
            var local = NetworkServer.localConnection != null ? NetworkServer.localConnection.connectionId : 0;
            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection == null || connection.connectionId == local)
                    continue;
                live.Add(connection.connectionId);

                // Only a connection with a player in the world counts: one still loading is not silent.
                if (connection.identity == null || Announced.Contains(connection.connectionId)
                    || MismatchedGuests.ContainsKey(connection.connectionId))
                    continue;
                if (!FirstSeen.TryGetValue(connection.connectionId, out var since))
                    FirstSeen[connection.connectionId] = Time.unscaledTime;
                else if (Time.unscaledTime - since >= SilentGuestSeconds && SilentGuests.Add(connection.connectionId))
                    Plugin.Log.LogWarning($"[{nameof(ModChannel)}] {PlayerLabel(connection.connectionId)} (connection #{connection.connectionId}) has not said hello after {SilentGuestSeconds:0}s: no mod, or an old build.");
            }

            foreach (var id in new List<int>(MismatchedGuests.Keys))
            {
                if (!live.Contains(id))
                    MismatchedGuests.Remove(id);
            }
            foreach (var id in new List<int>(FirstSeen.Keys))
            {
                if (!live.Contains(id))
                    FirstSeen.Remove(id);
            }
            SilentGuests.RemoveWhere(id => !live.Contains(id) || Announced.Contains(id));
        }

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
            TrackGuests();
            TickRepublish();

            if (Time.unscaledTime < _nextSnapshot || Announced.Count == 0)
                return;

            _nextSnapshot = Time.unscaledTime + SnapshotIntervalSeconds;
            SendSnapshotToAnnounced();
        }

        private static void ResetHost()
        {
            Announced.Clear();
            MismatchedGuests.Clear();
            FirstSeen.Clear();
            SilentGuests.Clear();
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

        // After a guest says hello, the holds are said again twice, a few seconds apart: the first
        // time its world is usually still filling in.
        private static readonly float[] RepublishDelays = { 3f, 8f };
        private static readonly List<float> RepublishAt = new();

        private static void TickRepublish()
        {
            for (var i = RepublishAt.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < RepublishAt[i])
                    continue;

                RepublishAt.RemoveAt(i);
                ReceivedItemSpawner.RepublishHolds();
            }
        }

        // Runs inside Mirror's dispatch: anything thrown here is a failed
        // message, and a failed message disconnects the sender. Nothing may
        // escape it.
        private static void OnServerMessage(NetworkConnection connection, NetworkReader reader, int channelId)
        {
            try
            {
                var kind = reader.ReadByte();
                if (kind == KindStatus)
                {
                    if (reader.ReadByte() == ProtocolVersion && Announced.Contains(connection.connectionId))
                        Traps.NoteGuestMasked(connection.connectionId, NetworkReaderExtensions.ReadBool(reader));
                    return;
                }

                if (kind != KindHello)
                    return;

                var version = reader.ReadByte();
                if (version != ProtocolVersion)
                {
                    MismatchedGuests[connection.connectionId] = version;
                    Plugin.Log.LogWarning(
                        $"[{nameof(ModChannel)}] Guest #{connection.connectionId} runs protocol v{version}, this host "
                        + $"v{ProtocolVersion}; not mirroring to it. Both players need the same build of the mod.");
                    return;
                }

                MismatchedGuests.Remove(connection.connectionId);
                SilentGuests.Remove(connection.connectionId);
                if (Announced.Add(connection.connectionId))
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(ModChannel)}] Guest #{connection.connectionId} runs the mod; mirroring the overlay "
                        + "and the radio to it.");
                    SendSnapshot(connection);
                    Traps.OnGuestJoined(connection);
                    foreach (var delay in RepublishDelays)
                        RepublishAt.Add(Time.unscaledTime + delay);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Unreadable message from a guest, ignored: {ex.Message}");
            }
        }

        // Sends the snapshot now rather than at the next tick, for a change a guest should see at
        // once (a resync station's sign).
        internal static void SendSnapshotNow()
        {
            if (NetworkServer.active)
                SendSnapshotToAnnounced();
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

                NetworkWriterExtensions.WriteBool(writer, PuzzleNeeds.Enabled);
                var lockedNeeds = PuzzleNeeds.LockedNeeds();
                writer.WriteByte((byte)lockedNeeds.Count);
                foreach (var need in lockedNeeds)
                    NetworkWriterExtensions.WriteString(writer, need);

                NetworkWriterExtensions.WriteBool(writer, GauntletStairways.Enabled);
                NetworkWriterExtensions.WriteBool(writer, GauntletStairways.PartsHidden);
                writer.WriteByte((byte)TeleportButtons.HostMask());
                NetworkWriterExtensions.WriteUShort(writer, (ushort)CabinFeverWaits.Seconds(0));
                NetworkWriterExtensions.WriteUShort(writer, (ushort)CabinFeverWaits.Seconds(1));
                writer.WriteByte((byte)((CabinFeverWaits.Help(0) ? 1 : 0) | (CabinFeverWaits.Help(1) ? 2 : 0)
                                        | (TileThiefHelper.HostMode << 2)));
                writer.WriteByte(ResyncStations.HostState());
                NetworkWriterExtensions.WriteString(writer, GourdNames.Current);
                writer.WriteByte(PackChecks.HostMode);
                var collected = new List<string>(PackChecks.HostCollected);
                writer.WriteByte((byte)Math.Min(collected.Count, byte.MaxValue));
                for (var i = 0; i < collected.Count && i < byte.MaxValue; i++)
                    NetworkWriterExtensions.WriteString(writer, collected[i]);
                var colours = Palette.HostColours;
                writer.WriteByte((byte)Math.Min(colours.Length, byte.MaxValue));
                for (var i = 0; i < colours.Length && i < byte.MaxValue; i++)
                    writer.WriteByte((byte)colours[i]);

                connection.Send(writer.ToArraySegment(), 0);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ModChannel)}] Could not send a snapshot to guest #{connection.connectionId}: {ex.Message}");
            }
        }

        // Tells the guest on this connection that its player pressed the button in this slot.
        internal static void SendPress(NetworkConnection connection, int slot)
        {
            if (connection == null || !Announced.Contains(connection.connectionId))
                return;

            try
            {
                var writer = new NetworkWriter();
                NetworkWriterExtensions.WriteUShort(writer, MessageId);
                writer.WriteByte(KindPress);
                writer.WriteByte(ProtocolVersion);
                writer.WriteByte((byte)slot);
                connection.Send(writer.ToArraySegment(), 0);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ModChannel)}] Could not send a button press to guest #{connection.connectionId}: {ex.Message}");
            }
        }

        internal static void SendEffect(NetworkConnection connection, string key, int seconds, Vector3? destination, float yaw)
        {
            if (connection == null || !Announced.Contains(connection.connectionId))
                return;

            try
            {
                var writer = new NetworkWriter();
                NetworkWriterExtensions.WriteUShort(writer, MessageId);
                writer.WriteByte(KindEffect);
                writer.WriteByte(ProtocolVersion);
                NetworkWriterExtensions.WriteString(writer, key);
                NetworkWriterExtensions.WriteUShort(writer, (ushort)Math.Clamp(seconds, 0, ushort.MaxValue));
                NetworkWriterExtensions.WriteBool(writer, destination != null);
                var point = destination ?? Vector3.zero;
                NetworkWriterExtensions.WriteFloat(writer, point.x);
                NetworkWriterExtensions.WriteFloat(writer, point.y);
                NetworkWriterExtensions.WriteFloat(writer, point.z);
                NetworkWriterExtensions.WriteFloat(writer, yaw);
                connection.Send(writer.ToArraySegment(), 0);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ModChannel)}] Could not send {key} to guest #{connection.connectionId}: {ex.Message}");
            }
        }

        // Guest side; false when there is no host to tell yet.
        internal static bool SendStatus(bool masked)
        {
            if (!_helloSent || !_hostHasMod)
                return false;

            try
            {
                var connection = NetworkClient.connection;
                if (connection == null)
                    return false;

                var writer = new NetworkWriter();
                NetworkWriterExtensions.WriteUShort(writer, MessageId);
                writer.WriteByte(KindStatus);
                writer.WriteByte(ProtocolVersion);
                NetworkWriterExtensions.WriteBool(writer, masked);
                connection.Send(writer.ToArraySegment(), 0);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ModChannel)}] Could not tell the host about the mask: {ex.Message}");
                return false;
            }
        }

        internal static void SendNotice(NetworkConnection connection, string text, bool warning)
        {
            if (connection == null || !Announced.Contains(connection.connectionId))
                return;

            try
            {
                var writer = new NetworkWriter();
                NetworkWriterExtensions.WriteUShort(writer, MessageId);
                writer.WriteByte(KindNotice);
                writer.WriteByte(ProtocolVersion);
                NetworkWriterExtensions.WriteString(writer, text);
                NetworkWriterExtensions.WriteBool(writer, warning);
                connection.Send(writer.ToArraySegment(), 0);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ModChannel)}] Could not send a notice to guest #{connection.connectionId}: {ex.Message}");
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
                    // With several guests, each a step further back, so they do not land in one another.
                    var spot = position + rotation * Vector3.back * (1.5f * sent);
                    var writer = new NetworkWriter();
                    NetworkWriterExtensions.WriteUShort(writer, MessageId);
                    writer.WriteByte(KindSummon);
                    writer.WriteByte(ProtocolVersion);
                    NetworkWriterExtensions.WriteFloat(writer, spot.x);
                    NetworkWriterExtensions.WriteFloat(writer, spot.y);
                    NetworkWriterExtensions.WriteFloat(writer, spot.z);
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
            {
                RadioStations.ForgetMirror();
                PuzzleNeeds.ForgetMirror();
                GauntletStairways.ForgetMirror();
                TeleportButtons.ForgetMirror();
                CabinFeverWaits.ForgetMirror();
                TileThiefHelper.ForgetMirror();
                ResyncStations.ForgetMirror();
                PackChecks.ForgetMirror();
                Palette.ForgetMirror();
                GourdNames.ForgetMirror();
            }

            _clientHandlerRegistered = false;
            _beaconHandlerRegistered = false;
            _hostHasMod = false;
            _helloSent = false;
            _versionMismatchLogged = false;
            _hostIsAnotherBuild = false;
            _helloAt = -1f;
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
                _helloAt = Time.unscaledTime;
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

                if (kind == KindPress)
                {
                    if (reader.ReadByte() == ProtocolVersion)
                        WorldButtons.RunPress(reader.ReadByte());
                    return;
                }

                if (kind == KindEffect)
                {
                    if (reader.ReadByte() != ProtocolVersion)
                        return;

                    var key = NetworkReaderExtensions.ReadString(reader);
                    var seconds = NetworkReaderExtensions.ReadUShort(reader);
                    var hasDestination = NetworkReaderExtensions.ReadBool(reader);
                    var point = new Vector3(
                        NetworkReaderExtensions.ReadFloat(reader),
                        NetworkReaderExtensions.ReadFloat(reader),
                        NetworkReaderExtensions.ReadFloat(reader));
                    var yaw = NetworkReaderExtensions.ReadFloat(reader);
                    TrapEffects.Play(key, seconds, hasDestination ? point : null, yaw);
                    return;
                }

                if (kind == KindNotice)
                {
                    if (reader.ReadByte() == ProtocolVersion)
                    {
                        var text = NetworkReaderExtensions.ReadString(reader);
                        ApNotices.Post(text, warning: NetworkReaderExtensions.ReadBool(reader));
                    }

                    return;
                }

                if (kind != KindSnapshot)
                    return;

                var version = reader.ReadByte();
                if (version != ProtocolVersion)
                {
                    _hostIsAnotherBuild = true;
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

                var needsEnabled = NetworkReaderExtensions.ReadBool(reader);
                var lockedNeeds = new List<string>();
                var lockedCount = reader.ReadByte();
                for (var i = 0; i < lockedCount; i++)
                    lockedNeeds.Add(NetworkReaderExtensions.ReadString(reader));

                var gauntletLocked = NetworkReaderExtensions.ReadBool(reader);
                var gauntletPartsLocked = NetworkReaderExtensions.ReadBool(reader);
                var teleportMask = reader.ReadByte();
                var cabinFever = NetworkReaderExtensions.ReadUShort(reader);
                var cabinFeverLong = NetworkReaderExtensions.ReadUShort(reader);
                var cabinFeverHelp = reader.ReadByte();
                var resyncStations = reader.ReadByte();
                var gourdName = NetworkReaderExtensions.ReadString(reader);
                var packMode = reader.ReadByte();
                var collectedPacks = new List<string>();
                var collectedCount = reader.ReadByte();
                for (var i = 0; i < collectedCount; i++)
                    collectedPacks.Add(NetworkReaderExtensions.ReadString(reader));
                var colours = new List<byte>();
                var colourCount = reader.ReadByte();
                for (var i = 0; i < colourCount; i++)
                    colours.Add(reader.ReadByte());

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
                PuzzleNeeds.ApplyFromHost(needsEnabled, lockedNeeds);
                GauntletStairways.ApplyFromHost(gauntletLocked, gauntletPartsLocked);
                TeleportButtons.ApplyFromHost(teleportMask);
                CabinFeverWaits.ApplyFromHost(cabinFever, cabinFeverLong, (cabinFeverHelp & 1) != 0, (cabinFeverHelp & 2) != 0);
                TileThiefHelper.ApplyFromHost((cabinFeverHelp >> 2) & 3);
                ResyncStations.ApplyFromHost(resyncStations);
                PackChecks.ApplyFromHost(packMode, collectedPacks);
                Palette.ApplyFromHost(colours);
                GourdNames.ApplyFromHost(gourdName);
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
