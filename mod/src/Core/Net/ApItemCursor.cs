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
        private const string SeedKey = "ap_seed_name";
        private const string SlotKey = "ap_slot_name";

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
            return 0;
        }

        internal static void Set(int appliedCount)
        {
            SaveManager.SetIntValue(CountKey, appliedCount);
        }
    }
}
