using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // `lock_endings` (ROADMAP G5, player 2026-10-09): the secret room's printer, the entry of Big
    // Game, does not print while the other objectives are not done. The game's own queue calls
    // OnQueuePeck for each print; refusing it leaves nothing printed and the room as it was, and
    // the print is asked for again once the lock is lifted.
    //
    // Checked on whichever machine runs the method: the host says what is shut (ModChannel), and
    // a guest follows. A flag first, since this runs on the game's own call and nothing else.
    [HarmonyPatch(typeof(GoodbyePrinter), "OnQueuePeck")]
    internal static class GoodbyePrinterHoldPatch
    {
        // What was refused, kept to be asked again once the lock is lifted (the player, 2026-10-12: the print was lost, the
        // request cancelled by the first one).
        private static GoodbyePrinter _refusedPrinter;
        private static PeckContext _refusedContext;

        private static bool Prefix(GoodbyePrinter __instance, PeckContext __0)
        {
            if (!ObjectiveBoard.HoldsTheLastStairway || !ObjectiveBoard.Locked("big_game"))
                return true;

            Plugin.Log.LogInfo($"[{nameof(GoodbyePrinterHoldPatch)}] The printer stays shut: the objectives are not done.");
            ObjectiveBoard.SayEndingIsShut("big_game");
            _refusedPrinter = __instance;
            _refusedContext = __0;
            return false;
        }

        // Every couple of seconds (ObjectiveBoard.Sync), on the host: once the lock is off, the request that was refused is made again.
        internal static void ReplayIfUnlocked()
        {
            if (_refusedPrinter == null || !Mirror.NetworkServer.active)
                return;

            if (ObjectiveBoard.HoldsTheLastStairway && ObjectiveBoard.Locked("big_game"))
                return;

            var printer = _refusedPrinter;
            var context = _refusedContext;
            _refusedPrinter = null;
            _refusedContext = null;
            try
            {
                Plugin.Log.LogInfo($"[{nameof(GoodbyePrinterHoldPatch)}] The lock is off: the refused print is asked again.");
                ObjectiveBoard.Say("Big Game: the printer works again");
                printer.OnQueuePeck(context);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(GoodbyePrinterHoldPatch)}] The refused print could not be asked again: {ex.Message}");
            }
        }
    }
}
