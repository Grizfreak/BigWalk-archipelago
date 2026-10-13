using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // R7: the hub's signs that carry the mod's text are carried as any sign, but pressing one does not open it for
    // writing when this player may not write on it (Core/HintBoards.IsLocked; every player runs the mod). The host-side Command could not be patched: the interop
    // does not expose `UserCode_CmdSendNewText__String__String` under that name (2026-10-13, the mod failed to load).
    [HarmonyPatch(typeof(PeckEffectTextInput), nameof(PeckEffectTextInput.Peck))]
    internal static class HintSignEditPatch
    {
        private static bool Prefix(PeckEffectTextInput __instance) => !HintBoards.IsLocked(__instance);
    }
}
