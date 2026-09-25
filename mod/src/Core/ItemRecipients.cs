using System.Collections.Generic;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Who a received item goes to (2026-09-25, from the first alpha: "the host
    // should not get every item — other players would like to receive items
    // too").
    //
    // The rule is the players', word for word: someone whose hands are free,
    // and if nobody's are, anyone — who then drops what they hold. Among
    // those, whoever has been given the fewest items this session, ties
    // broken at random. The count lives in memory only, as asked: it is about
    // taking turns during a session, not a record to keep.
    //
    // Host only: it reads every player's hands, which only the server knows
    // truthfully, and it is the host that spawns and hands over.
    internal static class ItemRecipients
    {
        private static readonly Dictionary<uint, int> GivenThisSession = new();

        // Players already promised an item that has not reached their hands
        // yet: not free, whatever their hands say this frame, so a burst of
        // arrivals spreads out instead of piling onto the same pair of hands.
        private static readonly HashSet<uint> Promised = new();

        internal static PlayerCharacter Choose()
        {
            var players = PlayerCharacter.allPlayerCharacters;
            if (players == null)
                return null;

            var everyone = new List<PlayerCharacter>();
            var free = new List<PlayerCharacter>();
            foreach (var pc in players)
            {
                if (pc == null || pc.hands == null)
                    continue;

                everyone.Add(pc);
                if (!pc.hands.isHoldingSomething && !Promised.Contains(pc.netId))
                    free.Add(pc);
            }

            var pool = free.Count > 0 ? free : everyone;
            if (pool.Count == 0)
                return null;

            var fewest = int.MaxValue;
            foreach (var pc in pool)
                fewest = Mathf.Min(fewest, Given(pc));

            var tied = pool.FindAll(pc => Given(pc) == fewest);
            var chosen = tied[UnityEngine.Random.Range(0, tied.Count)];

            GivenThisSession[chosen.netId] = Given(chosen) + 1;
            Promised.Add(chosen.netId);
            return chosen;
        }

        // The item has reached their hands, or never will.
        internal static void Settled(PlayerCharacter pc)
        {
            if (pc != null)
                Promised.Remove(pc.netId);
        }

        internal static void Forget()
        {
            GivenThisSession.Clear();
            Promised.Clear();
        }

        private static int Given(PlayerCharacter pc)
        {
            return GivenThisSession.TryGetValue(pc.netId, out var count) ? count : 0;
        }
    }
}
