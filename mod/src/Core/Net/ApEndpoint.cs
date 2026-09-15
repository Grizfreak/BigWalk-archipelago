using System;

namespace BigWalkArchipelago.Core.Net
{
    // Everything needed to open a session, gathered from where the player
    // actually typed it (apworld/protocol.md §1).
    //
    // Two of the three values are read back out of the save rather than
    // captured from the UI, and that is deliberate: the hosting screen
    // already writes the slot name and the password into SaveData
    // (HostMenuConfirmPatch relabelled those two fields as Archipelago
    // identifiers on 2026-09-15), so reading them from there also covers the
    // session where the player loads an existing save instead of filling the
    // form in again. Only the host:port has nowhere to live in the save, so
    // it persists in the BepInEx config.
    //
    // Reads SaveManager, so main thread only.
    internal readonly struct ApEndpoint
    {
        // The port Archipelago rooms use unless told otherwise; players
        // routinely paste just a hostname.
        private const int DefaultPort = 38281;

        internal string Host { get; }
        internal int Port { get; }
        internal string SlotName { get; }
        internal string Password { get; }
        internal string Uuid { get; }

        private ApEndpoint(string host, int port, string slotName, string password, string uuid)
        {
            Host = host;
            Port = port;
            SlotName = slotName;
            Password = password;
            Uuid = uuid;
        }

        internal static bool TryResolve(out ApEndpoint endpoint, out string problem)
        {
            endpoint = default;

            var save = SaveManager.instance != null ? SaveManager.instance.currentData : null;
            if (save == null)
            {
                problem = "no save loaded yet";
                return false;
            }

            // filenameUid is the game's own per-save identifier, so the same
            // save reconnects under the same uuid and the server recognises
            // it as the same client rather than a new one each time.
            var uuid = !string.IsNullOrWhiteSpace(save.filenameUid) ? save.filenameUid : save.slotName;

            return TryBuild(CurrentHostAndPort(), save.slotName, save.password, uuid, out endpoint, out problem);
        }

        // Used from the hosting screen, where the player is still typing and
        // the save being configured is not the loaded one: the values come
        // straight from the three input fields rather than from SaveData.
        internal static bool TryResolveFromFields(string rawHostPort, string slotName, string password,
                                                  out ApEndpoint endpoint, out string problem)
        {
            return TryBuild(rawHostPort, slotName, password, slotName, out endpoint, out problem);
        }

        internal static string CurrentHostAndPort()
        {
            var raw = ApSessionConfig.HostAndPort;
            return string.IsNullOrWhiteSpace(raw) ? ModConfig.ArchipelagoHostPort.Value : raw;
        }

        private static bool TryBuild(string rawHostPort, string slotName, string password, string uuid,
                                     out ApEndpoint endpoint, out string problem)
        {
            endpoint = default;

            if (!TryParseHostPort(rawHostPort, out var host, out var port))
            {
                problem = $"no usable Archipelago address (got '{rawHostPort}')";
                return false;
            }

            if (string.IsNullOrWhiteSpace(slotName))
            {
                problem = "the slot name (the save's name field on the hosting screen) is empty";
                return false;
            }

            endpoint = new ApEndpoint(host, port, slotName.Trim(), password ?? string.Empty,
                                      string.IsNullOrWhiteSpace(uuid) ? slotName.Trim() : uuid);
            problem = string.Empty;
            return true;
        }

        // Accepts what players actually paste: "archipelago.gg:38281",
        // "archipelago.gg", "wss://host:1234", a bare IPv4. Anything the
        // library can be handed as (hostname, port).
        private static bool TryParseHostPort(string raw, out string host, out int port)
        {
            host = string.Empty;
            port = DefaultPort;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var value = raw.Trim();

            foreach (var scheme in new[] { "wss://", "ws://" })
            {
                if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(scheme.Length);
                    break;
                }
            }

            value = value.TrimEnd('/');

            var separator = value.LastIndexOf(':');
            if (separator >= 0)
            {
                var portText = value.Substring(separator + 1);
                host = value.Substring(0, separator);

                // "archipelago.gg:" is the value the field is prefilled with
                // on a first launch — a host with no port yet, not an error
                // worth refusing, so it just falls back to the default port.
                if (portText.Length > 0 && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
                    return false;

                if (portText.Length == 0)
                    port = DefaultPort;
            }
            else
            {
                host = value;
            }

            return host.Length > 0;
        }
    }
}
