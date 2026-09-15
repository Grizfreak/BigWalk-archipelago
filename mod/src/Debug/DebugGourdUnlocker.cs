using BigWalkArchipelago.Core;

namespace BigWalkArchipelago.Debug
{
    // Débloque de force un gourd/big key sans résoudre l'énigme, pour tester
    // GourdStatePatch/SaveValuePatch sans dépendre de puzzles (souvent
    // multijoueur) ni d'un vrai déroulé de jeu.
    internal static class DebugGourdUnlocker
    {
        internal static void UnlockNext()
        {
            var closest = DebugGourdLookup.FindNearestLocked();
            if (closest == null)
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(DebugGourdUnlocker)}] Plus aucun gourd/big key verrouillé dans la zone actuelle.");
                return;
            }

            var prop = closest.prop;
            string label = prop != null ? prop.saveablePropName.ToString() : "<prop inconnu>";
            Plugin.Log.LogInfo($"[{nameof(DebugGourdUnlocker)}] Déblocage forcé (le plus proche) : {label}");

            // Important : on pin le prop dans son home AVANT de passer le
            // gourd à Loose. GourdStatePatch réagit lui aussi à ce passage
            // (même méthode patchée) et fait NetworkServer.UnSpawn + SetActive
            // (false) sur ce même GameObject (prop et gourd partagent la même
            // NetworkIdentity, confirmé en jeu le 2026-09-07). Si on pin après
            // coup, l'objet est déjà déspawné (netIdentity.isServer devient
            // false) et Prop.LocallySetPinned n'écrit jamais dans la save. En
            // pinant avant, on simule le vrai flux du jeu (dépôt au couffin
            // avant que la vice ne masque le gourd).
            if (prop != null)
            {
                PropHome home = null;
                if (GourdRegistry.TryGetHomeName(prop.saveablePropName, out var homeName))
                {
                    home = PropHome.GetSaveableHome(homeName);
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Home cible {homeName} : {(home != null ? "trouvé" : "introuvable")}");
                }

                if (home == null)
                {
                    home = prop.startHome;
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Repli sur startHome : {(home != null ? "présent" : "absent")}");
                }

                if (home != null)
                {
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Pin sur saveableHomeName={home.saveableHomeName} avant transition Loose.");
                    prop.ServerSetPinned(home);
                }
            }

            closest.ServerSetGourdState(GourdFlag.GourdState.Loose);
        }
    }
}
