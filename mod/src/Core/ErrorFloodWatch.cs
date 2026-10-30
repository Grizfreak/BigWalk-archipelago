using System;
using System.Text;
using BepInEx.Logging;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Unity's stackless NullReferenceException many times a second (184k in one host log): once, a report.
    internal sealed class ErrorFloodWatch : ILogListener
    {
        private const string Tag = "[" + nameof(ErrorFloodWatch) + "]";
        private const int PerSecond = 20;
        private const int Seconds = 3;

        private static long _second;
        private static int _count;
        private static int _streak;
        private static volatile bool _pending;
        private static bool _reported;

        internal static void Install() => BepInEx.Logging.Logger.Listeners.Add(new ErrorFloodWatch());

        public LogLevel LogLevelFilter => LogLevel.Error;

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (_reported || _pending || eventArgs.Level != LogLevel.Error || eventArgs.Source?.SourceName != "Unity")
                return;
            if (eventArgs.Data?.ToString().StartsWith("NullReferenceException", StringComparison.Ordinal) != true)
                return;

            var second = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            if (second != _second)
            {
                _streak = second == _second + 1 && _count >= PerSecond ? _streak + 1 : 0;
                _second = second;
                _count = 0;
            }

            if (++_count == PerSecond && _streak + 1 >= Seconds)
                _pending = true;
        }

        public void Dispose()
        {
        }

        // Every couple of seconds (TeleportButtonRunner): the report is written from the main thread.
        internal static void Tick()
        {
            if (!_pending || _reported)
                return;
            _reported = true;

            var text = new StringBuilder();
            text.AppendLine($"{Tag} Unity logs NullReferenceException {PerSecond}+ times a second; "
                            + "what is broken now (the lines just before the flood say what came first):");
            Section(text, "network", Network);
            Section(text, "players", Players);
            Section(text, "homes", Homes);
            Plugin.Log.LogWarning(text.ToString());
        }

        private static void Section(StringBuilder text, string name, Action<StringBuilder> fill)
        {
            var before = text.Length;
            try
            {
                fill(text);
            }
            catch (Exception ex)
            {
                text.AppendLine($"  {name}: could not be read ({ex.Message})");
            }

            if (text.Length == before)
                text.AppendLine($"  {name}: nothing broken");
        }

        private static void Network(StringBuilder text)
        {
            if (!NetworkServer.active)
                return;

            foreach (var pair in NetworkServer.spawned)
            {
                if (pair.Value == null)
                    text.AppendLine($"  spawned netId {pair.Key}: its object is gone");
            }
        }

        private static void Players(StringBuilder text)
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return;

            foreach (var player in players)
            {
                if (player == null)
                {
                    text.AppendLine("  a player character is gone but still listed");
                    continue;
                }

                var hands = player.hands;
                if (hands != null && !ReferenceEquals(hands.heldProp, null) && hands.heldProp == null)
                    text.AppendLine($"  {player.gameObject.name}: its hands hold an object that is gone");

                var held = player.playerNetworking != null ? player.playerNetworking.playerHeldInformation : null;
                if (held != null && held.heldType != PlayerHeldInformation.HeldType.Nothing && held.identity == null)
                    text.AppendLine($"  {player.gameObject.name}: holds a {held.heldType} that is gone, as the network says");
            }
        }

        private static void Homes(StringBuilder text)
        {
            foreach (var home in UnityEngine.Object.FindObjectsByType<PropHome>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (home != null && !ReferenceEquals(home.pinnedProp, null) && home.pinnedProp == null)
                    text.AppendLine($"  home {home.saveableHomeName} ({home.gameObject.name}) pins an object that is gone");
            }
        }
    }
}
