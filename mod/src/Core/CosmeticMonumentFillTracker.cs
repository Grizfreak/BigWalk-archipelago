using System;
using System.Collections.Generic;
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
    // Détection du pin — confirmé en test le 2026-09-11 : PropHome.
    // onAnyChangeServer (event STATIQUE global) ne se déclenche JAMAIS pour
    // un pin fait via Prop.ServerSetPinned — le vrai signal est PropHome.
    // onChangeServer, la version PAR-INSTANCE du même type d'event, à
    // laquelle il faut s'abonner individuellement sur CHAQUE PropHome.
    //
    // Abonnement par instance (2ème correction, même session) : un premier
    // essai scannait `PropHome.allPropHomes` UNE SEULE FOIS au démarrage
    // (poll sur WorldManager.isReadyForEffects, cf. historique git) — a
    // fonctionné pour un monument proche du hub mais PAS pour un monument
    // loin (Black Tower) : le jeu charge manifestement certains PropHome à
    // la volée, pas tous simultanément. Remplacé par Patches/
    // PropHomeEnablePatch.cs (postfix sur PropHome.OnEnable(), le seul
    // point de passage garanti pour CHAQUE instance qu'elle existe dès le
    // départ ou apparaisse plus tard) qui appelle RegisterHome ci-dessous
    // pour chaque PropHome, au fil de l'eau.
    //
    // PropHome est aussi utilisé pour des emplacements portés par le joueur
    // (ex. une "ceinture" d'inventaire, saveableHomeName == notSavable,
    // toujours à distance ~0 puisqu'attachée au personnage) — pas seulement
    // les monuments. Filtré via ReceivedItemSpawner.IsMonumentHome
    // (préfixe "monoument") pour ne jamais persister/restaurer un pin dans
    // un emplacement de ce type.
    internal class CosmeticMonumentFillTracker : MonoBehaviour
    {
        // Constructeur requis par Il2CppInterop pour tout type injecté en IL2CPP.
        public CosmeticMonumentFillTracker(IntPtr ptr) : base(ptr)
        {
        }

        private const string SaveKeyPrefix = "ap_home_";

        // File d'attente alimentée par PropHomeEnablePatch, au fil de l'eau
        // (chargement initial ET streaming ultérieur confondus) — vidée en
        // continu dans Update() une fois le monde prêt pour les effets, pas
        // en un seul passage figé au démarrage.
        private static readonly Queue<PropHome> PendingRestoreCheck = new Queue<PropHome>();

        // Tous les PropHome de monument connus (remplis ou non), pour
        // GetFilledMonumentCount ci-dessous — Option A tranchée le
        // 2026-09-15 (cf. big-walk-archipelago-notes.md) : comptage global
        // agrégé, pas de location par tour, pour éliminer le risque de
        // softlock identifié le 2026-09-11 (rien n'empêche un joueur de tout
        // déposer dans un seul monument). RegisterHome est garanti appelé
        // une seule fois par instance (cf. Patches/PropHomeEnablePatch),
        // donc pas de doublon possible ici.
        private static readonly List<PropHome> MonumentHomes = new List<PropHome>();

        // Appelé par Patches/PropHomeEnablePatch pour CHAQUE PropHome, dès
        // qu'il devient actif. L'abonnement lui-même (juste un +=) ne
        // dépend d'aucune condition de timing et se fait immédiatement ;
        // la vérification de restauration est différée en revanche (cf.
        // Update ci-dessous) — spawner/épingler un clone trop tôt (avant
        // que le "peck manager" du jeu existe) peut échouer silencieusement,
        // même piège déjà rencontré pour ArchDoorUnlocker.
        internal static void RegisterHome(PropHome home)
        {
            if (home == null)
                return;

            home.onChangeServer += (Action<PropHome, Prop, Prop>)OnHomeChanged;
            PendingRestoreCheck.Enqueue(home);

            if (ReceivedItemSpawner.IsMonumentHome(home))
                MonumentHomes.Add(home);
        }

        // Total agrégé, tous monuments confondus (Option A) : le nombre de
        // PropHome de monument actuellement occupés par un gourd cosmétique.
        // Requête à la volée sur la donnée déjà persistée (`ap_home_<slot>`)
        // plutôt qu'un compteur en cache — évite tout risque de désync avec
        // OnHomeChanged/TryRestoreHome (deux chemins d'écriture distincts).
        internal static int GetFilledMonumentCount()
        {
            var count = 0;
            foreach (var home in MonumentHomes)
            {
                if (home != null && SaveManager.GetIntValue(SaveKeyPrefix + home.saveableHomeName, 0, false) == 1)
                    count++;
            }

            return count;
        }

        private void Update()
        {
            if (!NetworkServer.active || !WorldManager.isReadyForEffects || PendingRestoreCheck.Count == 0)
                return;

            var restoredCount = 0;
            while (PendingRestoreCheck.Count > 0)
            {
                if (TryRestoreHome(PendingRestoreCheck.Dequeue()))
                    restoredCount++;
            }

            if (restoredCount > 0)
                Plugin.Log.LogInfo($"[{nameof(CosmeticMonumentFillTracker)}] {restoredCount} gourd(s) cosmétique(s) restauré(s) dans leurs monuments.");
        }

        private static bool TryRestoreHome(PropHome home)
        {
            // Ne jamais écraser un home déjà occupé (par un vrai gourd
            // normalement restauré par Prop.Start(), ou déjà par un clone
            // restauré juste avant dans cette même passe).
            if (home == null || home.pinnedProp != null || !ReceivedItemSpawner.IsMonumentHome(home))
                return false;

            var key = SaveKeyPrefix + home.saveableHomeName;
            if (SaveManager.GetIntValue(key, 0, false) == 0)
                return false;

            return ReceivedItemSpawner.SpawnCosmeticPickupPinnedTo(home) != null;
        }

        private static void OnHomeChanged(PropHome propHome, Prop propBefore, Prop propAfter)
        {
            if (propHome == null || !ReceivedItemSpawner.IsMonumentHome(propHome))
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
    }
}
