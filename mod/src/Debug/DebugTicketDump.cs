using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Every object carrying a ticket, switched off ones too, and who the office gives each ticket to.
    internal static class DebugTicketDump
    {
        private static float _nextRequestCheck;

        // On a file BepInEx/ticket-dump.request, once the world is ready; written to ticket-dump.txt.
        internal static void DumpOnRequest()
        {
            if (Time.unscaledTime < _nextRequestCheck)
                return;
            _nextRequestCheck = Time.unscaledTime + 2f;
            var request = System.IO.Path.Combine(Paths.BepInExRootPath, "ticket-dump.request");
            if (!WorldManager.isReadyForEffects || !File.Exists(request))
                return;
            File.Delete(request);
            Dump();
        }

        private static void Dump()
        {
            var rows = new List<(ushort Ticket, string Line, IntPtr Pointer)>();
            Collect<PeckSwitch>(rows, item => item.ticket);
            Collect<TrackedPeckState>(rows, item => item.ticket);
            Collect<PropHome>(rows, item => item.ticket);
            Collect<PlayerPose>(rows, item => item.ticket);
            Collect<StickyPlatform>(rows, item => item.ticket);

            var office = LobbyNetworking.TicketOffice.instance;
            var text = new StringBuilder();
            foreach (var group in rows.GroupBy(row => row.Ticket).OrderBy(group => group.Key))
            {
                var holder = "-";
                if (office != null && office.tickets.ContainsKey(group.Key))
                {
                    var registered = office.tickets[group.Key];
                    holder = registered == null ? "null" : registered.Pointer.ToString("X");
                }

                var mark = group.Count() > 1 ? "DUPLICATE " : "";
                foreach (var row in group)
                    text.AppendLine($"{mark}{group.Key}\t{row.Line}\t{(row.Pointer.ToString("X") == holder ? "registered" : "office:" + holder)}");
            }

            var path = System.IO.Path.Combine(Paths.BepInExRootPath, "ticket-dump.txt");
            File.WriteAllText(path, text.ToString());
            Plugin.Log.LogInfo($"[{nameof(DebugTicketDump)}] {rows.Count} ticketed object(s) written to {path}.");
        }

        private static void Collect<T>(List<(ushort, string, IntPtr)> rows, Func<T, ushort> ticketOf) where T : Component
        {
            foreach (var item in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item == null)
                    continue;
                var ticket = ticketOf(item);
                if (ticket == 0)
                    continue;
                var identity = item.GetComponentInParent<Mirror.NetworkIdentity>(true);
                var scene = identity == null ? "no identity" : $"sceneId {identity.sceneId:X}";
                rows.Add((ticket, $"{typeof(T).Name}\t{(item.gameObject.activeInHierarchy ? "on" : "off")}\t{scene}\t{Path(item.transform)}", item.Pointer));
            }
        }

        private static string Path(Transform transform)
        {
            var parts = new List<string>();
            for (var at = transform; at != null; at = at.parent)
                parts.Add(at.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
