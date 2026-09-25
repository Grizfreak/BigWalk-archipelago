using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Starts the loopback guest from inside the host, on a key: a co-op test
    // becomes "host a world, press Ctrl+L", with no terminal involved. It is
    // the same launch as tools/launch-guest.ps1 — keep the two in step — and
    // for the same reasons, which that script's header gives in full:
    //
    // - SteamAppId / SteamGameId set, or SteamManager.Awake's
    //   SteamAPI_RestartAppIfNecessary quits the guest and asks Steam, which
    //   refuses a second launch;
    // - no DOORSTOP_* variables passed on: this process carries Doorstop's own,
    //   and a child that inherits them can skip loading BepInEx;
    // - windowed, with the Screenmanager values in PlayerPrefs (which both
    //   instances share) put back by a hidden watcher once the guest exits.
    //   The watcher is its own process so that it still runs if the host is
    //   closed first.
    //
    // Started through the shell rather than with redirected handles, so the
    // guest's own boot output does not land in anything of the host's; the
    // environment is changed in this process for the length of the call.
    internal static class LoopbackLauncher
    {
        private const string Tag = "[" + nameof(LoopbackLauncher) + "]";
        private const string AppId = "1478500";
        private const int Width = 1280;
        private const int Height = 720;
        private const string PrefsKey = @"Software\House House\Big Walk";

        internal static void LaunchGuest()
        {
            using var self = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(self.ProcessName);
            var running = 0;
            foreach (var process in others)
            {
                if (process.Id != self.Id)
                    running++;
                process.Dispose();
            }

            if (running > 0)
            {
                Plugin.Log.LogInfo($"{Tag} Another instance of the game is already running; not starting a guest.");
                return;
            }

            var exe = Environment.ProcessPath;
            var gameDir = Path.GetDirectoryName(exe);
            var hosting = NetworkServer.active;
            var unityLog = Path.Combine(Application.persistentDataPath, "Player-guest.log");
            var arguments = LoopbackGuest.GuestArgument
                + (hosting ? " " + LoopbackGuest.JoinArgument : "")
                + $" -logFile \"{unityLog}\" -screen-fullscreen 0 -screen-width {Width} -screen-height {Height}";

            var snapshot = SnapshotScreenSettings();

            Process guest;
            var previous = new Dictionary<string, string>();
            try
            {
                foreach (var name in new[] { "SteamAppId", "SteamGameId" })
                {
                    previous[name] = Environment.GetEnvironmentVariable(name);
                    Environment.SetEnvironmentVariable(name, AppId);
                }

                foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
                {
                    var name = (string)variable.Key;
                    if (name.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase))
                    {
                        previous[name] = (string)variable.Value;
                        Environment.SetEnvironmentVariable(name, null);
                    }
                }

                guest = Process.Start(new ProcessStartInfo(exe)
                {
                    WorkingDirectory = gameDir,
                    Arguments = arguments,
                    UseShellExecute = true,
                });
            }
            finally
            {
                foreach (var pair in previous)
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            if (guest == null)
            {
                Plugin.Log.LogWarning($"{Tag} The guest did not start.");
                return;
            }

            Plugin.Log.LogInfo(
                $"{Tag} Guest started: process {guest.Id}, {Width}x{Height} windowed, log BepInEx\\LogOutput.1.log "
                + (hosting
                    ? "(joins on its own once its title menu is up)."
                    : "(not hosting yet: host a world, then press Ctrl+L in the guest's window)."));

            if (snapshot != null)
                StartRestoreWatcher(guest.Id, snapshot);
        }

        // Written as the JSON tools/launch-guest.ps1 writes, so the watcher
        // below restores it the way that script's own watcher, measured on
        // 2026-09-26, does.
        private static string SnapshotScreenSettings()
        {
            // The game ships for Windows only; the guard is what tells the
            // compiler so (CA1416 on every registry call otherwise).
            if (!OperatingSystem.IsWindows())
                return null;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PrefsKey);
                if (key == null)
                    return null;

                var entries = new List<Dictionary<string, object>>();
                foreach (var name in key.GetValueNames())
                {
                    if (!name.StartsWith("Screenmanager ", StringComparison.Ordinal))
                        continue;

                    var value = key.GetValue(name);
                    if (value is byte[] bytes)
                    {
                        var numbers = new int[bytes.Length];
                        for (var i = 0; i < bytes.Length; i++)
                            numbers[i] = bytes[i];
                        value = numbers;
                    }

                    entries.Add(new Dictionary<string, object>
                    {
                        ["Name"] = name,
                        ["Kind"] = key.GetValueKind(name).ToString(),
                        ["Value"] = value,
                    });
                }

                var file = Path.Combine(Path.GetTempPath(), "bwap-guest-screen-prefs.json");
                File.WriteAllText(file, JsonSerializer.Serialize(entries));
                return file;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not save the screen settings; the guest may leave them windowed: {ex.Message}");
                return null;
            }
        }

        private static void StartRestoreWatcher(int guestId, string snapshotFile)
        {
            var file = snapshotFile.Replace("'", "''");
            var script =
                $"$p = Get-Process -Id {guestId} -ErrorAction SilentlyContinue; if ($p) {{ $p.WaitForExit() }}\n"
                + "$k = 'HKCU:\\Software\\House House\\Big Walk'\n"
                + $"$entries = Get-Content '{file}' -Raw | ConvertFrom-Json\n"
                + "$names = @()\n"
                + "foreach ($e in $entries) { $v = $e.Value; if ($e.Kind -eq 'Binary') { $v = [byte[]]$v }; "
                + "Set-ItemProperty -Path $k -Name $e.Name -Value $v -Type $e.Kind; $names += $e.Name }\n"
                + "foreach ($n in (Get-Item $k).Property | Where-Object { $_ -like 'Screenmanager *' -and $names -notcontains $_ }) "
                + "{ Remove-ItemProperty -Path $k -Name $n }\n"
                + $"Remove-Item '{file}'\n";

            try
            {
                Process.Start(new ProcessStartInfo("powershell.exe")
                {
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand "
                        + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })?.Dispose();
                Plugin.Log.LogInfo($"{Tag} Screen settings saved; they are put back when the guest exits.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"{Tag} Could not start the screen-settings watcher: {ex.Message}");
            }
        }
    }
}
