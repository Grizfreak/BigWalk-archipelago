using BigWalkArchipelago.Debug;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // PlayerArms.UpdateScreenSpaceArms() jette une NullReferenceException en
    // boucle (chaque frame) pendant que DebugFlightTool.Toggle() a détaché la
    // caméra (CameraCheatMover.Detach() la prive apparemment d'une référence
    // qu'elle attend). Sans conséquence sur le jeu (juste du bruit dans les
    // logs), donc on saute simplement l'appel pendant que la caméra libre de
    // debug est active plutôt que de creuser la cause exacte côté jeu.
    [HarmonyPatch(typeof(PlayerArms), "UpdateScreenSpaceArms")]
    internal static class PlayerArmsScreenSpacePatch
    {
        private static bool Prefix() => !DebugFlightTool.IsActive;
    }
}
