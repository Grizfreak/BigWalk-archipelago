using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BigWalkArchipelago.Core.Net
{
    // The world's logic, as the apworld writes it into slot_data (`logic`, apworld/bigwalk/logic_export.py), evaluated
    // with the items this slot has received: which checks are in logic now (R7, the next-check board). The same sweep
    // as the apworld's test (test_logic_export.reachable), which checks it against Archipelago's own logic: regions
    // reached from the origin through their exits, locations reached in them, an event's item collected as soon as
    // its location is reached, until nothing changes.
    internal sealed class ApLogic
    {
        private enum Kind
        {
            True,
            False,
            And,
            Or,
            Has,
            All,
            Any,
            From,
            Location,
            Region,
        }

        private sealed class Rule
        {
            internal Kind Kind;
            internal Rule[] Children;
            internal string Name;
            internal string[] Names;
            internal int Count;
        }

        private sealed class Location
        {
            internal string Name;
            internal long Id;
            internal string Region;
            internal Rule Rule;
            internal string EventItem;
        }

        private string _origin;
        private readonly List<(string from, string to, Rule rule)> _exits = new();
        private readonly List<Location> _locations = new();
        private readonly Dictionary<string, Location> _byName = new();

        internal int LocationCount => _locations.Count;

        // A check's region by its id (null if unknown).
        internal string RegionOf(long id)
        {
            foreach (var location in _locations)
            {
                if (location.Id == id)
                    return location.Region;
            }
            return null;
        }

        internal static ApLogic From(JObject raw)
        {
            try
            {
                var logic = new ApLogic { _origin = (string)raw["origin"] };
                var rules = new List<Rule>();
                foreach (var rule in (JArray)raw["rules"])
                    rules.Add(Parse((JArray)rule));
                foreach (JArray exit in (JArray)raw["exits"])
                    logic._exits.Add(((string)exit[0], (string)exit[1], rules[(int)exit[2]]));
                foreach (JArray entry in (JArray)raw["locations"])
                {
                    var location = new Location
                    {
                        Name = (string)entry[0],
                        Id = (long)entry[1],
                        Region = (string)entry[2],
                        Rule = rules[(int)entry[3]],
                        EventItem = (string)entry[4],
                    };
                    logic._locations.Add(location);
                    logic._byName[location.Name] = location;
                }

                Plugin.Log.LogInfo($"[{nameof(ApLogic)}] Logic read: {rules.Count} rules, {logic._exits.Count} exits, {logic._locations.Count} locations.");
                return logic;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ApLogic)}] The seed's logic could not be read, no next-check board: {ex.Message}");
                return null;
            }
        }

        private static Rule Parse(JArray raw)
        {
            var kind = (string)raw[0];
            switch (kind)
            {
                case "true":
                    return new Rule { Kind = Kind.True };
                case "false":
                    return new Rule { Kind = Kind.False };
                case "and":
                case "or":
                {
                    var children = new List<Rule>();
                    foreach (var child in (JArray)raw[1])
                        children.Add(Parse((JArray)child));
                    return new Rule { Kind = kind == "and" ? Kind.And : Kind.Or, Children = children.ToArray() };
                }
                case "has":
                    return new Rule { Kind = Kind.Has, Name = (string)raw[1], Count = (int)raw[2] };
                case "all":
                    return new Rule { Kind = Kind.All, Names = raw[1].ToObject<string[]>() };
                case "any":
                    return new Rule { Kind = Kind.Any, Names = raw[1].ToObject<string[]>() };
                case "from":
                    return new Rule { Kind = Kind.From, Names = raw[1].ToObject<string[]>(), Count = (int)raw[2] };
                case "loc":
                    return new Rule { Kind = Kind.Location, Name = (string)raw[1] };
                case "reg":
                    return new Rule { Kind = Kind.Region, Name = (string)raw[1] };
                default:
                    throw new FormatException($"unknown rule '{kind}'");
            }
        }

        // The real checks (not events) in logic with these items, in the apworld's order.
        internal List<(long id, string name)> InLogic(Dictionary<string, int> received)
        {
            var have = new Dictionary<string, int>(received);
            var regions = new HashSet<string> { _origin };
            var reached = new HashSet<string>();

            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var (from, to, rule) in _exits)
                {
                    if (regions.Contains(from) && !regions.Contains(to) && Holds(rule, have, regions))
                    {
                        regions.Add(to);
                        changed = true;
                    }
                }

                foreach (var location in _locations)
                {
                    if (reached.Contains(location.Name) || !regions.Contains(location.Region) || !Holds(location.Rule, have, regions))
                        continue;
                    reached.Add(location.Name);
                    changed = true;
                    if (!string.IsNullOrEmpty(location.EventItem))
                        have[location.EventItem] = Count(have, location.EventItem) + 1;
                }
            }

            var result = new List<(long, string)>();
            foreach (var location in _locations)
            {
                if (location.Id != 0 && reached.Contains(location.Name))
                    result.Add((location.Id, location.Name));
            }
            return result;
        }

        private bool Holds(Rule rule, Dictionary<string, int> have, HashSet<string> regions)
        {
            switch (rule.Kind)
            {
                case Kind.True:
                    return true;
                case Kind.False:
                    return false;
                case Kind.And:
                    foreach (var child in rule.Children)
                    {
                        if (!Holds(child, have, regions))
                            return false;
                    }
                    return true;
                case Kind.Or:
                    foreach (var child in rule.Children)
                    {
                        if (Holds(child, have, regions))
                            return true;
                    }
                    return false;
                case Kind.Has:
                    return Count(have, rule.Name) >= rule.Count;
                case Kind.All:
                    foreach (var name in rule.Names)
                    {
                        if (Count(have, name) < 1)
                            return false;
                    }
                    return true;
                case Kind.Any:
                    foreach (var name in rule.Names)
                    {
                        if (Count(have, name) >= 1)
                            return true;
                    }
                    return false;
                case Kind.From:
                {
                    var total = 0;
                    foreach (var name in rule.Names)
                        total += Count(have, name);
                    return total >= rule.Count;
                }
                case Kind.Location:
                    return _byName.TryGetValue(rule.Name, out var location)
                           && regions.Contains(location.Region) && Holds(location.Rule, have, regions);
                case Kind.Region:
                    return regions.Contains(rule.Name);
                default:
                    return false;
            }
        }

        private static int Count(Dictionary<string, int> have, string name) => have.TryGetValue(name, out var count) ? count : 0;
    }
}
