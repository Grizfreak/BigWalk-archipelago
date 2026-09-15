using System;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;

namespace BigWalkArchipelago.Core.Net
{
    // A throwaway login used to tell the host, on the hosting screen, whether
    // their Archipelago details actually work — before they commit to a
    // session and discover the mistake from a log line nobody reads.
    //
    // It is a real login rather than a reachability check because the errors
    // worth catching are a mistyped slot name and a wrong password, and only
    // the server can judge those. The session is closed again immediately:
    // ApRuntime opens its own once the game is hosting, and leaving this one
    // alive would put two clients on the slot for no reason.
    //
    // Same threading rules as ApConnection: the probe runs on a Task, touches
    // nothing but managed types, and the caller polls Status from the main
    // thread.
    internal static class ApConnectionTest
    {
        internal enum TestStatus
        {
            Idle,
            Testing,
            Ok,
            Failed,
        }

        // A login that never answers must not leave the hosting screen stuck:
        // past this, the probe is declared failed and the player can host
        // anyway.
        private const double TimeoutSeconds = 15;

        private static volatile TestStatus _status = TestStatus.Idle;
        private static DateTime _startedAt;

        internal static string LastError { get; private set; } = string.Empty;

        internal static TestStatus Status
        {
            get
            {
                if (_status == TestStatus.Testing && (DateTime.UtcNow - _startedAt).TotalSeconds > TimeoutSeconds)
                {
                    LastError = $"no answer after {TimeoutSeconds:0}s";
                    _status = TestStatus.Failed;
                }

                return _status;
            }
        }

        internal static void Reset()
        {
            _status = TestStatus.Idle;
            LastError = string.Empty;
        }

        internal static void Start(ApEndpoint endpoint)
        {
            if (_status == TestStatus.Testing)
                return;

            _status = TestStatus.Testing;
            _startedAt = DateTime.UtcNow;
            LastError = string.Empty;

            Task.Run(() => Probe(endpoint));
        }

        private static void Probe(ApEndpoint endpoint)
        {
            try
            {
                var session = ArchipelagoSessionFactory.CreateSession(endpoint.Host, endpoint.Port);

                // NoItems, and no slot_data: this connection is about to be
                // thrown away, so it must not consume the slot's item stream
                // or make the server think a real client is playing.
                var result = session.TryConnectAndLogin(
                    "Big Walk",
                    endpoint.SlotName,
                    ItemsHandlingFlags.NoItems,
                    new Version(0, 6, 0),
                    Array.Empty<string>(),
                    endpoint.Uuid + "-test",
                    endpoint.Password,
                    false);

                try
                {
                    session.Socket.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    // Closing the probe is best effort; the server drops it
                    // on its own soon enough either way.
                    Plugin.Log.LogInfo($"[{nameof(ApConnectionTest)}] Probe cleanup: {ex.Message}");
                }

                if (result.Successful)
                {
                    _status = TestStatus.Ok;
                    return;
                }

                LastError = result is LoginFailure failure && failure.Errors is { Length: > 0 }
                    ? string.Join(" / ", failure.Errors)
                    : "login refused";
                _status = TestStatus.Failed;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _status = TestStatus.Failed;
            }
        }
    }
}
