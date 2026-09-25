using System;

namespace BigWalkArchipelago.Debug
{
    // This process's part in a loopback co-op test: two instances of the game
    // on one PC, the second joining the first as a real Mirror client over
    // KcpTransport on 127.0.0.1, so that everything a guest sees and does can
    // be tested without a second machine. tools/launch-guest.ps1 starts the
    // guest; the host is the ordinary instance, launched through Steam.
    //
    // The role comes from the command line and never from the .cfg: both
    // instances run from the same install and read the same config file, so
    // a setting there could not tell them apart.
    //
    // Nothing here does anything unless Debug.Enabled is on: a player who
    // happens to launch the game with this argument gets the vanilla game.
    internal static class LoopbackGuest
    {
        internal const string GuestArgument = "--bwap-guest";

        internal static bool IsGuest { get; private set; }

        internal static string RoleName => IsGuest ? "loopback guest" : "host or solo";

        // Called once from Plugin.Load, inside the debug block. Says what it
        // decided either way: a role that is only logged when it is "guest"
        // cannot tell a missing argument from a probe that never ran.
        internal static void DetectRole()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, GuestArgument, StringComparison.OrdinalIgnoreCase))
                {
                    IsGuest = true;
                    break;
                }
            }

            Plugin.Log.LogInfo(IsGuest
                ? $"[{nameof(LoopbackGuest)}] Role: loopback guest ({GuestArgument} is on the command line)."
                : $"[{nameof(LoopbackGuest)}] Role: host or solo (no {GuestArgument} on the command line).");
        }
    }
}
