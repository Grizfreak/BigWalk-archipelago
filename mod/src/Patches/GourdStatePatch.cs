using BigWalkArchipelago.Core;
using HarmonyLib;
using Mirror;

namespace BigWalkArchipelago.Patches
{
    // Postfix sur RewardGourd.ServerSetGourdState : la transition vers Loose
    // (le gourd vient de sortir de l'étau, énigme résolue) vaut check validé.
    //
    // Choix revu en session de test le 2026-09-03 : architecture-mod.md visait
    // initialement Stashed (dépôt au home), mais un test en jeu a révélé que
    // stasher un gourd nécessite DEUX joueurs (l'un ouvre le slot, l'autre y
    // dépose le gourd) — donc pas un bon point de détection de check (rarement
    // atteint, pas testable en solo). Loose correspond au vrai moment de
    // résolution de l'énigme, comme dans GourdInterceptPOC.cs. Stashed reste
    // pertinent plus tard pour le sens inverse : quand un item Archipelago est
    // reçu à distance, on voudra forcer un gourd home à se remplir sans passer
    // par la coordination à deux joueurs (cf. big-walk-archipelago-notes.md,
    // recommandation n°2).
    //
    // On lit newGourdState (le paramètre reçu par l'appel intercepté), pas
    // __instance.gourdState, pour réagir au bon événement même si la SyncVar a
    // déjà changé plusieurs fois entre-temps.
    //
    // Une fois le check reporté, le gourd physique ne doit pas rester
    // récupérable localement : le "vrai" don passera par le réseau
    // Archipelago (item reçu), pas par le pickup local. Le rappel à
    // ServerSetGourdState(Hidden) réentre ce postfix, mais la garde du dessus
    // (newGourdState != Loose) empêche toute boucle infinie. Uniquement pour
    // les gourds du registre : les gourds de test (hors registre) gardent
    // leur comportement vanilla.
    //
    // Confirmé en test le 2026-09-07 : ServerSetGourdState(Loose) est aussi
    // rappelé pour de vrai (pas juste Prop.Start()) au chargement d'une zone
    // pour un gourd déjà résolu lors d'une session précédente — une
    // restauration légitime, pas une re-résolution par le joueur. D'où
    // l'importance que CheckTracker.TryMarkReported soit persistant (cf.
    // Core/CheckTracker.cs) : sans ça, ce rappel redéclencherait le check à
    // chaque redémarrage du jeu. Le reste du postfix (Hidden/UnSpawn/
    // SetActive) doit lui continuer à s'exécuter à chaque fois : chaque
    // session recrée une instance fraîche du GameObject, donc le re-masquage
    // est nécessaire à chaque fois, seul le *report* du check doit être
    // dédoublonné.
    //
    // Testé en jeu le 2026-09-03 : Hidden seul ne fait que rafraîchir l'icône
    // sur la carte (confirmé — GourdMap.refreshFlag, cf. notes.md), le modèle
    // 3D reste visible et ramassable. Un simple GameObject.SetActive(false)
    // serait purement local (Mirror ne réplique pas ça automatiquement aux
    // autres clients) et créerait une désync visuelle en co-op. On passe donc
    // par NetworkServer.UnSpawn (API Mirror correcte pour retirer un objet
    // réseau de la vue de TOUS les clients sans le détruire définitivement,
    // contrairement à NetworkServer.Destroy) puis on désactive le GameObject
    // localement côté host. Pas encore vérifié : est-ce que le GameObject de
    // RewardGourd correspond exactement au modèle visuel du gourd, ou à une
    // hiérarchie plus large partagée avec le mécanisme de l'étau — à surveiller.
    [HarmonyPatch(typeof(RewardGourd), nameof(RewardGourd.ServerSetGourdState))]
    internal static class GourdStatePatch
    {
        private static void Postfix(RewardGourd __instance, GourdFlag.GourdState newGourdState)
        {
            if (newGourdState != GourdFlag.GourdState.Loose)
                return;

            var prop = __instance.prop;
            if (prop == null)
            {
                Plugin.Log.LogWarning(
                    $"[{nameof(GourdStatePatch)}] RewardGourd sans prop associé, transition Loose ignorée.");
                return;
            }

            if (!GourdRegistry.TryGetLocationId(prop.saveablePropName, out var locationId))
            {
                Plugin.Log.LogInfo(
                    $"[{nameof(GourdStatePatch)}] '{prop.saveablePropName}' hors registre (test/notSavable), ignoré.");
                return;
            }

            if (CheckTracker.TryMarkReported(locationId))
                Plugin.Reporter.ReportCheck(locationId);

            __instance.ServerSetGourdState(GourdFlag.GourdState.Hidden);

            NetworkServer.UnSpawn(__instance.gameObject);
            __instance.gameObject.SetActive(false);
        }
    }
}
