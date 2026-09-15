using System.Collections.Generic;
using BigWalkArchipelago.Core;
using HarmonyLib;

namespace BigWalkArchipelago.Patches
{
    // CosmeticMonumentFillTracker a besoin de connaître TOUS les PropHome du
    // jeu, pas seulement ceux déjà chargés au moment d'un unique passage "au
    // démarrage de la session" — confirmé en test le 2026-09-11 : un
    // monument proche du hub (monoumentIntroSlot0) a fonctionné du premier
    // coup, mais un monument loin (Black Tower, monoumentFinalSlot4) n'a
    // reçu AUCUN abonnement onChangeServer (silence total dans les logs
    // après un pin pourtant confirmé réussi par ailleurs) — le jeu charge
    // manifestement certains PropHome à la volée (au moins en partie), pas
    // tous simultanément au démarrage comme l'hypothèse non vérifiée le
    // supposait.
    //
    // PropHome.OnEnable() est le seul point de passage garanti pour CHAQUE
    // instance, qu'elle existe dès le chargement initial ou apparaisse plus
    // tard. Remplace l'ancien passage unique (PropHome.allPropHomes scanné
    // une fois dans CosmeticMonumentFillTracker.Update()) par un abonnement
    // fait au fil de l'eau, instance par instance, dès son activation.
    [HarmonyPatch(typeof(PropHome), nameof(PropHome.OnEnable))]
    internal static class PropHomeEnablePatch
    {
        // Garde contre un double abonnement si OnEnable est rappelé plus
        // d'une fois sur la même instance (désactivation/réactivation).
        private static readonly HashSet<PropHome> RegisteredHomes = new HashSet<PropHome>();

        private static void Postfix(PropHome __instance)
        {
            if (__instance == null || !RegisteredHomes.Add(__instance))
                return;

            CosmeticMonumentFillTracker.RegisterHome(__instance);
        }
    }
}
