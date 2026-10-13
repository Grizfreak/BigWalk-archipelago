using System;
using System.Collections.Generic;
using BigWalkArchipelago.Core.Net;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // What the trackers show (U2 the Map Room's map, player 2026-10-13), worked out on the host every few seconds: the
    // locations of this slot checked, those in logic now (the seed's logic, Net/ApLogic, with the items received), and
    // those an open hint points to. Empty when not connected or the seed sends no logic.
    internal static class TrackerState
    {
        private const float RecountSeconds = 3f;

        internal static HashSet<long> Checked { get; private set; } = new();
        internal static HashSet<long> InLogic { get; private set; } = new();
        internal static HashSet<long> Hinted { get; private set; } = new();
        internal static bool Ready { get; private set; }

        private static float _recountAt;

        internal static void Tick()
        {
            if (Time.unscaledTime < _recountAt)
                return;
            _recountAt = Time.unscaledTime + RecountSeconds;

            var connection = ApRuntime.Current;
            var logic = connection != null && connection.Status == ApConnection.ConnectionStatus.Connected
                ? connection.SlotData?.Logic
                : null;
            if (logic == null)
            {
                Ready = false;
                return;
            }

            try
            {
                Checked = connection.CheckedLocations();
                var inLogic = new HashSet<long>();
                foreach (var (id, _) in logic.InLogic(connection.ReceivedByName()))
                    inLogic.Add(id);
                InLogic = inLogic;
                Hinted = connection.HintedLocations();
                Ready = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(TrackerState)}] {ex.Message}");
            }
        }
    }
}
