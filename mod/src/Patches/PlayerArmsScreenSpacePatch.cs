using BigWalkArchipelago.Debug;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // PlayerArms.UpdateScreenSpaceArms() throws a NullReferenceException in a
    // loop (every frame) while DebugFlightTool.Toggle() has detached the
    // camera (CameraCheatMover.Detach() apparently deprives it of a
    // reference it expects). No effect on the game (just log noise), so we
    // simply skip the call while the debug free camera is active rather than
    // digging into the exact cause on the game side.
    [HarmonyPatch(typeof(PlayerArms), "UpdateScreenSpaceArms")]
    internal static class PlayerArmsScreenSpacePatch
    {
        private static bool Prefix() => !DebugFlightTool.IsActive;
    }
}
