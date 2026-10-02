using System;
using System.Globalization;
using System.IO;
using BepInEx;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // One line per thing that happened, in a file that outlives the log: what
    // came in, what was checked, which need opened, where the player stood.
    // Written 2026-10-02 for the first long session on a real multiworld, where
    // the question afterwards is "in what order did things happen, and where
    // was I when they did" — which LogOutput.log answers only by hunting.
    //
    // Tab-separated, appended, and flushed on every line so a crash loses
    // nothing. Every machine writes its own (the loopback guest too: it has the
    // same folder, so its lines carry its role to tell them apart).
    //
    // Several sessions, several launches, two instances at once:
    //   - LogOutput.log is overwritten by BepInEx on every launch, which is
    //     why this file exists at all: it is appended to, never replaced;
    //   - each line carries the id of the launch that wrote it (the moment the
    //     process started), and a `launch` line opens every run, so sessions
    //     are told apart even though the file is one;
    //   - the file is opened for sharing and closed after each line, so the
    //     host and the loopback guest can write at the same moment;
    //   - past MaxBytes it is moved to session-journal.old.tsv (replacing the
    //     previous one), so the folder never holds more than two generations.
    //
    // Never throws: a journal that breaks the game is worse than none.
    internal static class SessionJournal
    {
        private const string FileName = "session-journal.tsv";
        private const string OldFileName = "session-journal.old.tsv";
        private const long MaxBytes = 4 * 1024 * 1024;
        private const string Header = "launch	time	uptime_s	role	event	detail	x	y	z	nearest_puzzle	distance";

        private static string _path;
        private static string _launchId;
        private static bool _opened;

        internal static void Write(string kind, string detail, bool withPosition = true)
        {
            try
            {
                if (!_opened)
                    Open();

                var role = NetworkServer.active ? "host" : NetworkClient.active ? "guest" : "solo";
                var where = withPosition ? Position() : "				";
                AppendLine(string.Join("	", new[]
                {
                    _launchId,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    Time.realtimeSinceStartup.ToString("F1", CultureInfo.InvariantCulture),
                    role,
                    kind,
                    detail ?? string.Empty,
                    where,
                }));
            }
            catch (Exception)
            {
            }
        }

        // Once per launch: names the launch, rotates a file that has grown too
        // big, and writes the header into a file that is new.
        private static void Open()
        {
            _opened = true;
            _path = Path.Combine(Paths.BepInExRootPath, FileName);

            // The moment the process started: the same for every line it
            // writes, different for every launch and for the guest's own.
            var started = DateTime.Now.AddSeconds(-Time.realtimeSinceStartup);
            _launchId = started.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                    File.Move(_path, Path.Combine(Paths.BepInExRootPath, OldFileName), overwrite: true);
            }
            catch (Exception)
            {
                // The other instance got there first, or the file is held:
                // keep appending, and rotate at the next launch.
            }

            if (!File.Exists(_path))
                AppendLine(Header);

            AppendLine(string.Join("	", new[]
            {
                _launchId, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), "0",
                "-", "launch", $"{Plugin.PluginName} v{Plugin.PluginVersion}", "", "", "", "", "",
            }));
        }

        private static void AppendLine(string line)
        {
            using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(line);
        }

        // x, y, z, nearest puzzle and its distance, tab-separated; empty
        // columns when there is no player yet (the menu, a loading screen).
        private static string Position()
        {
            const string none = "\t\t\t\t";
            try
            {
                var player = BigWalkArchipelago.Debug.DebugPlayerLookup.FindLocalPlayer();
                if (player == null)
                    return none;

                var at = player.transform.position;
                var nearest = "<none>";
                var best = float.MaxValue;
                var gourds = UnityEngine.Object.FindObjectsByType<RewardGourd>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (gourds != null)
                {
                    foreach (var gourd in gourds)
                    {
                        var prop = gourd != null ? gourd.prop : null;
                        if (prop == null || !GourdRegistry.TryGetLocationId(prop.saveablePropName, out _))
                            continue;

                        var distance = Vector3.Distance(at, prop.transform.position);
                        if (distance < best)
                        {
                            best = distance;
                            nearest = prop.saveablePropName.ToString();
                        }
                    }
                }

                return string.Join("\t", new[]
                {
                    at.x.ToString("F1", CultureInfo.InvariantCulture),
                    at.y.ToString("F1", CultureInfo.InvariantCulture),
                    at.z.ToString("F1", CultureInfo.InvariantCulture),
                    nearest,
                    best == float.MaxValue ? string.Empty : best.ToString("F1", CultureInfo.InvariantCulture),
                });
            }
            catch (Exception)
            {
                return none;
            }
        }
    }
}
