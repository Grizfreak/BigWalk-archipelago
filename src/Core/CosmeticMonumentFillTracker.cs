using System;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Persiste et restaure le remplissage des monuments par des gourds
    // cosmétiques (ReceivedItemSpawner) — décision du joueur (2026-09-11) :
    // les puzzles ne donnent plus de gourd exploitable directement (le vrai
    // don passe par le réseau AP), donc les clones cosmétiques doivent
    // devenir le SEUL moyen de remplir un monument, et ce remplissage doit
    // survivre à un rechargement.
    //
    // Pourquoi pas le mécanisme vanilla (saveablePropName -> Prop.Start()) :
    // ce mécanisme est fondamentalement par-IDENTITÉ (chaque prop retrouve
    // sa place au chargement en relisant SA PROPRE clé SaveManager). Un
    // clone cosmétique n'a pas d'identité stable réutilisable sans risquer
    // une collision avec un vrai check (cf. ReceivedItemSpawner, points 3
    // et 4) — la seule identité "libre" du jeu (gourdTesting00-39, ~40
    // valeurs jamais utilisées en jeu normal) serait trop petite face aux
    // ~45 emplacements de monuments réels du jeu (comptage F6, cf.
    // big-walk-archipelago-notes.md) si les gourds cosmétiques doivent
    // pouvoir remplir N'IMPORTE lequel. D'où ce mécanisme entièrement
    // séparé, indexé par PropHome (pas par Prop) : une clé SaveManager par
    // PLACE (`saveableHomeName`, l'identité stable du PropHome lui-même,
    // pas du prop qui l'occupe), préfixée pour ne jamais collisionner avec
    // les clés `SaveablePropName`/`SaveableHomeName` que le jeu écrit pour
    // de vrai.
    //
    // Détection du pin : PropHome.onAnyChangeServer est un event STATIQUE
    // GLOBAL du jeu (pas un Harmony patch — le jeu expose déjà ce point
    // d'extension), déclenché pour CHAQUE changement de pin, dans tous les
    // PropHome du jeu. Un clone cosmétique est reconnu sans ambiguïté par
    // saveablePropName == notSavable (jamais vrai pour un prop normal du
    // jeu) + le suffixe de nom (double vérification, peu coûteuse).
    //
    // Restauration au chargement : timing calqué sur ArchDoorUnlocker
    // (poll sur WorldManager.isReadyForEffects), UNE SEULE FOIS par
    // session. Hypothèse non vérifiée (à confirmer en jeu) : tous les
    // PropHome du jeu sont chargés simultanément (monde ouvert sans
    // streaming de zones à proprement parler) — si ça s'avère faux (des
    // PropHome distants ne sont chargés qu'à l'approche du joueur), cette
    // restauration "une fois au démarrage" manquerait les monuments hors
    // de la zone initiale et il faudrait la redéclencher à chaque
    // (re)chargement de zone plutôt qu'une seule fois par session.
    internal class CosmeticMonumentFillTracker : MonoBehaviour
    {
        // Constructeur requis par Il2CppInterop pour tout type injecté en IL2CPP.
        public CosmeticMonumentFillTracker(IntPtr ptr) : base(ptr)
        {
        }

        private const string SaveKeyPrefix = "ap_home_";

        private static bool _subscribed;
        private bool _restored;

        private void Update()
        {
            if (!NetworkServer.active || !WorldManager.isReadyForEffects)
                return;

            // L'abonnement à l'event global ne dépend d'aucune zone
            // chargée : fait une seule fois pour toute la durée du process
            // (pas de désabonnement nécessaire, ce composant vit aussi
            // longtemps que la session).
            if (!_subscribed)
            {
                PropHome.onAnyChangeServer += (Action<PropHome, Prop, Prop>)OnAnyPropHomeChanged;
                _subscribed = true;
            }

            if (_restored)
                return;
            _restored = true;

            RestoreFilledHomes();
        }

        private static void OnAnyPropHomeChanged(PropHome propHome, Prop propBefore, Prop propAfter)
        {
            if (propHome == null)
                return;

            // Règle unique qui couvre pin/dépin/remplacement : la clé
            // reflète simplement "un clone cosmétique occupe CE propHome
            // MAINTENANT", peu importe ce qui s'y trouvait avant.
            var isCosmeticNow = ReceivedItemSpawner.IsCosmeticClone(propAfter);
            var key = SaveKeyPrefix + propHome.saveableHomeName;
            var newValue = isCosmeticNow ? 1 : 0;

            if (SaveManager.GetIntValue(key, 0, false) == newValue)
                return;

            SaveManager.SetIntValue(key, newValue);
            Plugin.Log.LogInfo(
                $"[{nameof(CosmeticMonumentFillTracker)}] {key} = {newValue} (gourd cosmétique {(isCosmeticNow ? "déposé" : "retiré")}).");
        }

        private static void RestoreFilledHomes()
        {
            var homes = PropHome.allPropHomes;
            if (homes == null || homes.Count == 0)
            {
                Plugin.Log.LogInfo($"[{nameof(CosmeticMonumentFillTracker)}] Aucun PropHome chargé, restauration ignorée.");
                return;
            }

            var restoredCount = 0;
            foreach (var home in homes)
            {
                // Ne jamais écraser un home déjà occupé (par un vrai gourd
                // normalement restauré par Prop.Start(), ou déjà par un
                // clone restauré plus tôt dans cette même passe).
                if (home == null || home.pinnedProp != null)
                    continue;

                var key = SaveKeyPrefix + home.saveableHomeName;
                if (SaveManager.GetIntValue(key, 0, false) == 0)
                    continue;

                if (ReceivedItemSpawner.SpawnCosmeticPickupPinnedTo(home) != null)
                    restoredCount++;
            }

            if (restoredCount > 0)
                Plugin.Log.LogInfo($"[{nameof(CosmeticMonumentFillTracker)}] {restoredCount} gourd(s) cosmétique(s) restauré(s) dans leurs monuments.");
        }
    }
}
