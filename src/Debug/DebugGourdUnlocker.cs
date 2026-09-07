using System;
using UnityEngine;

namespace BigWalkArchipelago.Debug
{
    // Débloque de force un gourd/big key sans résoudre l'énigme, pour tester
    // GourdStatePatch/SaveValuePatch sans dépendre de puzzles (souvent
    // multijoueur) ni d'un vrai déroulé de jeu.
    internal static class DebugGourdUnlocker
    {
        internal static void UnlockNext()
        {
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(DebugGourdUnlocker)}] Aucun RewardGourd trouvé dans la zone actuelle.");
                return;
            }

            // Débloque le plus proche du joueur local (plutôt qu'un ordre
            // arbitraire) pour que l'effet soit visible immédiatement pendant
            // les tests, sans avoir à parcourir toute la zone.
            var playerPosition = FindLocalPlayerPosition();

            RewardGourd closest = null;
            float closestSqrDistance = float.MaxValue;

            foreach (var gourd in all)
            {
                if (gourd == null || gourd.gourdState != GourdFlag.GourdState.Locked)
                    continue;

                if (playerPosition == null)
                {
                    closest = gourd;
                    break;
                }

                float sqrDistance = (gourd.transform.position - playerPosition.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = gourd;
                }
            }

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
            //
            // Le vrai home utilisé par PeckEffectSavableHome (le "vice launch
            // switch") suit une convention de nommage confirmée en jeu :
            // gourdXxx (SaveablePropName) ↔ valetXxx (SaveableHomeName), ex.
            // gourdTellerWindow ↔ valetTellerWindow. On la réutilise pour
            // retrouver le bon home via PropHome.GetSaveableHome ; à défaut on
            // retombe sur startHome (qui n'a pas de saveableHomeName, donc pas
            // persistant — mieux que rien visuellement).
            if (prop != null)
            {
                PropHome home = null;
                string propName = prop.saveablePropName.ToString();
                if (propName.StartsWith("gourd", StringComparison.Ordinal) &&
                    Enum.TryParse("valet" + propName.Substring("gourd".Length), out SaveableHomeName homeName))
                {
                    home = PropHome.GetSaveableHome(homeName);
                    Plugin.Log.LogInfo(
                        $"[{nameof(DebugGourdUnlocker)}] Home cible valet{propName.Substring(5)} : {(home != null ? "trouvé" : "introuvable")}");
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

        private static Vector3? FindLocalPlayerPosition()
        {
            var localPlayer = DebugPlayerLookup.FindLocalPlayer();
            return localPlayer != null ? localPlayer.transform.position : (Vector3?)null;
        }
    }
}
