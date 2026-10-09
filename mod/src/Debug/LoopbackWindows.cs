using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BigWalkArchipelago.Debug
{
    // Hands the keyboard to the next instance of the game on this PC — the
    // loopback guests and the host in turn — so one person can play every side
    // without reaching for the mouse.
    //
    // Windows only lets the process that has the foreground give it away,
    // which is exactly the case here: the key is read by the window that
    // has the focus. Both instances keep running while unfocused: the game
    // already sets Application.runInBackground (NetworkManager.runInBackground
    // is ticked, measured with Ctrl+N), so Kcp never times out on the one
    // left behind.
    internal static class LoopbackWindows
    {
        private const string Tag = "[" + nameof(LoopbackWindows) + "]";
        private const int SwRestore = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        internal static void FocusOtherInstance()
        {
            using var self = Process.GetCurrentProcess();

            // With up to four instances, the next one in process order after this one, round
            // and round: pressing it again and again visits every window.
            var windows = new List<Process>();
            foreach (var candidate in Process.GetProcessesByName(self.ProcessName))
            {
                if (candidate.Id != self.Id && candidate.MainWindowHandle != IntPtr.Zero)
                    windows.Add(candidate);
                else
                    candidate.Dispose();
            }

            windows.Sort((x, y) => x.Id.CompareTo(y.Id));
            var other = windows.Find(p => p.Id > self.Id) ?? (windows.Count > 0 ? windows[0] : null);
            foreach (var window in windows)
            {
                if (window != other)
                    window.Dispose();
            }

            if (other == null)
            {
                Plugin.Log.LogInfo($"{Tag} No other instance of the game with a window; nothing to switch to.");
                return;
            }

            using (other)
            {
                var window = other.MainWindowHandle;
                if (IsIconic(window))
                    ShowWindow(window, SwRestore);

                var switched = SetForegroundWindow(window);
                Plugin.Log.LogInfo(switched
                    ? $"{Tag} Focus handed to process {other.Id}."
                    : $"{Tag} Windows refused to hand the focus to process {other.Id}.");
            }
        }
    }
}
