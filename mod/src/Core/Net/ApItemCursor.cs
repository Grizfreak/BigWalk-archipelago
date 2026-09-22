using System;

namespace BigWalkArchipelago.Core.Net
{
    // How many items of the slot's ordered received-items list have already
    // been materialized into THIS save. The single most important piece of
    // state in the client (apworld/protocol.md §5).
    //
    // Why it has to exist: the server resends every item the slot has ever
    // received on every connection. Re-applying a big key is harmless — it
    // rewrites the same SaveManager key and re-pins the same prop — but
    // re-applying a gourd spawns another physical prop at the hub. Without a
    // cursor, one reconnection duplicates every gourd the slot ever got, and
    // the monument economy the whole world is balanced around falls apart.
    //
    // It lives in the save file rather than in the config, and that is what
    // makes the agreed recovery path work for free: a player who is
    // physically stuck starts a NEW save and reconnects to the same slot,
    // the fresh save has no cursor, so everything replays from zero and the
    // run is rebuilt (decision of 2026-09-15, see
    // apworld/design-decisions.md). Known cost, accepted at the time:
    // replayed gourds land at the hub, not back in the monuments they were
    // deposited in, so the players redeposit them by hand.
    internal static class ApItemCursor
    {
        private const string CountKey = "ap_items_received";
        private const string GourdCountKey = "ap_gourds_received";
        private const string GadgetCountKeyPrefix = "ap_gadgets_received_";
        private const string SeedKey = "ap_seed_name";
        // Internal, not private: the hosting screen reads it straight
        // off SaveData to warn when a save has been renamed away from
        // the slot it was bound to (HostMenuArchipelagoControls).
        internal const string SlotKey = "ap_slot_name";

        // Binds this save to a (seed, slot) pair and returns how many items
        // are already applied to it. A save that has never seen this pair
        // starts at zero, which is exactly the new-save recovery above — and
        // also protects against the opposite accident, connecting an old
        // save to an unrelated seed and silently skipping its first items.
        internal static int SyncTo(string seedName, string slotName)
        {
            var knownSeed = SaveManager.GetStringValue(SeedKey);
            var knownSlot = SaveManager.GetStringValue(SlotKey);

            if (knownSeed == seedName && knownSlot == slotName)
                return SaveManager.GetIntValue(CountKey, 0, false);

            if (!string.IsNullOrEmpty(knownSeed) || !string.IsNullOrEmpty(knownSlot))
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(ApItemCursor)}] This save was bound to seed '{knownSeed}' / slot '{knownSlot}', "
                    + $"now connecting as seed '{seedName}' / slot '{slotName}'. Replaying every received item from scratch.");
            }

            SaveManager.SetStringValue(SeedKey, seedName ?? string.Empty);
            SaveManager.SetStringValue(SlotKey, slotName ?? string.Empty);
            SaveManager.SetIntValue(CountKey, 0);
            SaveManager.SetIntValue(GourdCountKey, 0);

            foreach (GadgetKind kind in Enum.GetValues(typeof(GadgetKind)))
                SaveManager.SetIntValue(GadgetCountKeyPrefix + kind, 0);

            return 0;
        }

        internal static void Set(int appliedCount)
        {
            SaveManager.SetIntValue(CountKey, appliedCount);
        }

        // How many Gourd items this save has ever been given, which is not
        // the same question as how many gourds physically exist right now.
        //
        // A cosmetic gourd has no save identity on purpose (that is what
        // stops it colliding with a real check), so the game persists it
        // only once it is pinned in a monument. One lying at the hub, or
        // carried, is simply gone after a restart. Before the item cursor
        // existed that was invisible, because the server's replay respawned
        // everything on every connection; suppressing the replay made it
        // visible and permanent — and losing a single gourd puts the last
        // big key out of reach, since the item pool holds exactly one per
        // monument slot.
        //
        // So the mod stops trying to remember individual props and remembers
        // the count instead: gourds are fungible, and Core/Net/ApRuntime
        // rebuilds "received minus deposited" loose gourds at every session
        // start. Self-healing by construction — it also recovers gourds lost
        // by a version of the mod that had this bug.
        internal static int GourdsReceived => SaveManager.GetIntValue(GourdCountKey, 0, false);

        internal static void CountGourdReceived()
        {
            SaveManager.SetIntValue(GourdCountKey, GourdsReceived + 1);
        }

        // Adopts a count recomputed from the server's replay. The server's
        // item history is the real authority, so this repairs a save whose
        // ledger is wrong or — the case that matters — was never written at
        // all, by a build of the mod that predates this counter.
        internal static void SetGourdsReceived(int count)
        {
            SaveManager.SetIntValue(GourdCountKey, count);
        }

        // Same shape as GourdsReceived/CountGourdReceived, one counter per
        // GadgetKind: a filler gadget has no deposit sink at all (there is
        // nowhere to "spend" a megaphone), so every one ever received is
        // permanently owed a loose copy somewhere in the world — the count
        // never decreases. ApRuntime.RestoreLooseGadgets rebuilds
        // "received minus spawned this session" at every world load, the
        // same reasoning RestoreLooseGourds already uses for "received
        // minus deposited".
        internal static int GadgetsReceived(GadgetKind kind) =>
            SaveManager.GetIntValue(GadgetCountKeyPrefix + kind, 0, false);

        internal static void CountGadgetReceived(GadgetKind kind)
        {
            SaveManager.SetIntValue(GadgetCountKeyPrefix + kind, GadgetsReceived(kind) + 1);
        }
    }
}
