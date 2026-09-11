using System;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace BigWalkArchipelago.Core
{
    // Fait apparaître un gourd cosmétique/ramassable au hub quand un item
    // Archipelago est reçu — purement une notification visuelle ("tiens, tu
    // viens de recevoir un item"), PAS le mécanisme de délivrance lui-même
    // (ItemApplier.ApplyGourdItem fait déjà tout le travail réel par écriture
    // SaveManager, avant même l'appel à ce spawner). Décidé le 2026-09-09
    // (cf. big-walk-archipelago-notes.md, "Sur l'apparence de l'item gourd
    // reçu") : durée de vie = jusqu'au ramassage (pas de timer), visible de
    // tous les joueurs de la session (spawn réseau standard, pas de scoping
    // par connexion).
    //
    // Approche : cloner un RewardGourd déjà chargé en scène (Instantiate),
    // plutôt que d'introduire un nouveau prefab réseau (aucun enregistré pour
    // ce mod, et NetworkServer.Spawn exige un prefab déjà connu de
    // NetworkManager pour qu'un client puisse l'instancier lui-même à la
    // réception du message de spawn). Cloner une instance déjà présente
    // fonctionne car son NetworkIdentity/PrefabHash est déjà celui d'un
    // prefab que TOUS les clients connaissent déjà.
    //
    // Ce type de clonage (un objet PLACÉ EN SCÈNE, pas un vrai prefab
    // instancié dynamiquement par le jeu) s'est révélé truffé de pièges
    // Mirror/moteur, tous rencontrés et résolus en session de test le
    // 2026-09-11 — cf. commentaires inline pour le détail de chacun :
    //   1. NetworkIdentity.sceneId + hasSpawned (copiés tels quels par
    //      Instantiate) font croire à Awake() que ce clone est "le même"
    //      objet de scène déjà connu → warning "already spawned" côté moteur.
    //   2. NetworkIdentity.SpawnedFromInstantiate (mis à true par Awake())
    //      doit aussi être remis à false après coup.
    //   3. `saveablePropName` copié tel quel créerait une collision
    //      SaveManager si le clone est un jour épinglé à une vraie plinthe —
    //      neutralisé (`notSavable`/`PropSaveType.Never`) AVANT toute
    //      activation. Fait aussi APRÈS ServerSetGourdState serait trop tard
    //      : ce dernier peut déclencher un vrai check via GourdStatePatch si
    //      le nom d'origine est encore présent (confirmé en test — a
    //      déclenché un faux "gourdTellerWindow").
    //   4. `startHome` (référence sérialisée vers le casier d'origine du
    //      template) fait téléporter le clone loin du hub via le mécanisme
    //      de restauration `Prop.Start()` — neutralisé (null) pour la même
    //      raison que saveablePropName.
    //   5. `Prop.SetLoose()` doit être appelé explicitement pour le cas
    //      "posé au hub" : le SyncVar gourdState (piloté par
    //      ServerSetGourdState) ne pilote que l'état visuel/logique du
    //      puzzle, pas l'état physique du Prop lui-même (sinon le clone
    //      reste "fixé"/sans gravité comme dans son étau d'origine).
    //   6. Un MaterialPropertyBlock par-instance (pas copié par Instantiate,
    //      contrairement aux références sérialisées) est capturé puis
    //      réappliqué sur le clone par précaution.
    //
    // DÉCISION (2026-09-11, joueur) : contrairement à l'intuition initiale
    // ("empêcher tout pin dans un vrai monument, cf. propGroups.Clear() —
    // retiré depuis"), le comportement VOULU est l'inverse : les puzzles ne
    // donnent plus de gourd exploitable directement (le vrai don passe par
    // le réseau AP), donc ces clones cosmétiques doivent devenir le SEUL
    // moyen de remplir les monuments. `propGroups` du template est donc
    // conservé intact (permet un pin réel dans n'importe quel vrai
    // PropHome) — la persistance de ce pin est gérée séparément par
    // `Core/CosmeticMonumentFillTracker.cs` (clé `SaveManager` dédiée par
    // PropHome, indépendante de `saveablePropName`/`SaveableHomeName`,
    // cf. ce fichier pour le détail).
    internal static class ReceivedItemSpawner
    {
        internal const string CosmeticNameSuffix = "(AP cosmetic)";

        // Retourne le GameObject créé (ou null en cas d'échec/no-op) — utile
        // pour du diagnostic ponctuel (cf. DebugHotkeys) ; ItemApplier
        // (usage réel) ignore simplement la valeur de retour.
        internal static GameObject SpawnCosmeticPickup()
        {
            if (!NetworkServer.active)
                return null;

            try
            {
                var spawnPoint = FindSpawnPoint();
                if (spawnPoint == null)
                {
                    Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] Aucun InventorySpawn chargé (hors zone du hub ?), spawn cosmétique ignoré.");
                    return null;
                }

                var position = spawnPoint.GetNextSpawnPosition();
                var rewardGourd = CreateNeutralizedClone(position, Quaternion.identity);
                if (rewardGourd == null)
                    return null;

                rewardGourd.ServerSetGourdState(GourdFlag.GourdState.Loose);

                // Sans ça, le clone reste "fixé" (pas de gravité) comme dans
                // son étau/casier d'origine — cf. point 5 en tête de fichier.
                rewardGourd.prop?.SetLoose();

                Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] Gourd cosmétique spawné à {rewardGourd.transform.position}.");
                return rewardGourd.gameObject;
            }
            catch (Exception ex)
            {
                // Ne doit jamais faire échouer la réception réelle de
                // l'item (déjà appliquée par ItemApplier avant cet appel) :
                // purement cosmétique, une exception ici ne doit avoir
                // aucune conséquence sur la vraie persistance.
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Échec du spawn cosmétique, ignoré : {ex}");
                return null;
            }
        }

        // Variante utilisée par CosmeticMonumentFillTracker pour restaurer,
        // au chargement, un gourd cosmétique déjà déposé lors d'une session
        // précédente : clone + épingle directement dans propHome (plutôt que
        // de le lâcher au hub). Retourne le GameObject créé, ou null en cas
        // d'échec (jamais d'exception propagée, même logique défensive que
        // SpawnCosmeticPickup).
        internal static GameObject SpawnCosmeticPickupPinnedTo(PropHome propHome)
        {
            if (!NetworkServer.active || propHome == null)
                return null;

            try
            {
                var rewardGourd = CreateNeutralizedClone(propHome.transform.position, propHome.transform.rotation);
                if (rewardGourd == null || rewardGourd.prop == null)
                    return null;

                // Stashed (pas Loose) : représente un gourd déjà déposé dans
                // son home, pas un gourd qui vient d'apparaître et attend
                // d'être ramassé — cohérent avec le fait qu'on l'épingle
                // directement ci-dessous, sans jamais passer par un état
                // "posé au sol". Ignoré par GourdStatePatch (garde sur
                // newGourdState == Loose uniquement), donc aucun risque de
                // faux check ici non plus.
                rewardGourd.ServerSetGourdState(GourdFlag.GourdState.Stashed);
                rewardGourd.prop.ServerSetPinned(propHome);

                return rewardGourd.gameObject;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Échec de la restauration d'un gourd cosmétique dans {propHome.saveableHomeName}, ignoré : {ex}");
                return null;
            }
        }

        // Cœur commun aux deux points d'entrée ci-dessus : clone un
        // RewardGourd existant à la position/rotation données, neutralisé et
        // prêt à l'emploi (réseauté, activé, visuellement correct). Ne fixe
        // ni l'état du puzzle (gourdState) ni la physique/pin — au choix de
        // l'appelant.
        private static RewardGourd CreateNeutralizedClone(Vector3 position, Quaternion rotation)
        {
            var template = FindTemplate();
            if (template == null)
            {
                Plugin.Log.LogInfo($"[{nameof(ReceivedItemSpawner)}] Aucun RewardGourd chargé à cloner, spawn cosmétique ignoré.");
                return null;
            }

            // MaterialPropertyBlock par-instance : capturé sur le template
            // AVANT clonage (cf. point 6 en tête de fichier), pour être
            // réappliqué explicitement sur le clone plus bas.
            var templateRenderers = template.gameObject.GetComponentsInChildren<Renderer>(true);
            var templatePropertyBlocks = new MaterialPropertyBlock[templateRenderers.Length];
            for (var i = 0; i < templateRenderers.Length; i++)
            {
                if (templateRenderers[i] == null)
                    continue;

                var block = new MaterialPropertyBlock();
                templateRenderers[i].GetPropertyBlock(block);
                templatePropertyBlocks[i] = block;
            }

            // Contournement du piège Mirror (point 1 en tête de fichier) :
            // désactiver le template avant Instantiate fait hériter le clone
            // de l'état inactif, ce qui diffère Awake()/OnEnable() jusqu'à
            // SetActive(true) plus bas — le temps de corriger sceneId et de
            // neutraliser saveablePropName/startHome pendant que le clone
            // est encore inactif.
            var templateWasActive = template.gameObject.activeSelf;
            GameObject clone;
            try
            {
                template.gameObject.SetActive(false);
                clone = UnityEngine.Object.Instantiate(template.gameObject, position, rotation);
            }
            finally
            {
                if (templateWasActive)
                    template.gameObject.SetActive(true);
            }

            clone.name = $"{template.gameObject.name} {CosmeticNameSuffix}";

            var cloneIdentity = clone.GetComponent<NetworkIdentity>();
            if (cloneIdentity != null)
            {
                cloneIdentity.sceneId = 0;

                // hasSpawned est exposé comme une propriété générée par
                // Il2CppInterop (pas un vrai FieldInfo réfléchissable malgré
                // sa déclaration en champ privé côté jeu) — réflexion sur le
                // setter de la propriété, pas sur un nom de champ deviné.
                GetPrivatePropertySetter<NetworkIdentity>("hasSpawned")?.Invoke(cloneIdentity, new object[] { false });
            }

            var rewardGourd = clone.GetComponent<RewardGourd>();
            if (rewardGourd == null)
            {
                Plugin.Log.LogWarning($"[{nameof(ReceivedItemSpawner)}] Clone sans RewardGourd, destruction et abandon.");
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            // Neutralisation AVANT activation (points 3 et 4 en tête de
            // fichier) — pas après ServerSetGourdState, ça a réellement
            // déclenché un faux check en test.
            NeutralizeProgression(rewardGourd.prop);
            var propsToMakeSavable = rewardGourd.propsToMakeSavable;
            if (propsToMakeSavable != null)
            {
                foreach (var block in propsToMakeSavable)
                {
                    if (block == null || block.props == null)
                        continue;

                    foreach (var prop in block.props)
                        NeutralizeProgression(prop);
                }
            }

            // Réapplication des MaterialPropertyBlock capturés plus haut,
            // par index (la hiérarchie de renderers du clone est une copie
            // exacte de celle du template) — avant activation, pour éviter
            // ne serait-ce qu'une frame avec l'apparence nue par défaut.
            var cloneRenderers = clone.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < cloneRenderers.Length && i < templatePropertyBlocks.Length; i++)
            {
                if (cloneRenderers[i] != null && templatePropertyBlocks[i] != null)
                    cloneRenderers[i].SetPropertyBlock(templatePropertyBlocks[i]);
            }

            clone.SetActive(true);

            // SpawnedFromInstantiate (point 2 en tête de fichier) : mis à
            // true PAR Awake() lui-même (donc seulement une fois SetActive
            // appelé ci-dessus), remis à false après coup par réflexion sur
            // le setter de la propriété.
            if (cloneIdentity != null)
                GetPrivatePropertySetter<NetworkIdentity>(nameof(NetworkIdentity.SpawnedFromInstantiate))?.Invoke(cloneIdentity, new object[] { false });

            NetworkServer.Spawn(clone);

            return rewardGourd;
        }

        // Partagé avec CosmeticMonumentFillTracker (détection dans l'event
        // PropHome.onAnyChangeServer) et les outils de debug : un clone
        // cosmétique est reconnu sans ambiguïté par saveablePropName ==
        // notSavable (jamais vrai pour un prop normal du jeu) + le suffixe
        // de nom, en double vérification.
        internal static bool IsCosmeticClone(Prop prop)
        {
            return prop != null
                && prop.saveablePropName == SaveablePropName.notSavable
                && prop.gameObject.name.Contains(CosmeticNameSuffix, StringComparison.Ordinal);
        }

        private static MethodInfo GetPrivatePropertySetter<T>(string propertyName)
        {
            return typeof(T)
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetSetMethod(nonPublic: true);
        }

        private static void NeutralizeProgression(Prop prop)
        {
            if (prop == null)
                return;

            prop.saveablePropName = SaveablePropName.notSavable;
            prop.propSaveType = PropSaveType.Never;

            // Point 4 en tête de fichier : sans ça, Prop.Start() (le
            // mécanisme de restauration au chargement déjà documenté dans
            // big-walk-archipelago-notes.md, section "RÉSOLU 2026-09-03")
            // téléporte le clone à l'emplacement du casier d'origine du
            // template, potentiellement à l'autre bout de la carte.
            prop.startHome = null;

            // `propGroups` N'EST PLUS vidé ici (contrairement à une version
            // antérieure) : décision du joueur (2026-09-11) de permettre
            // explicitement le pin dans un vrai monument, cf. section
            // "DÉCISION" en tête de fichier — CosmeticMonumentFillTracker
            // gère la persistance de ce pin séparément.
        }

        // Choisit l'InventorySpawn le plus proche du joueur local plutôt que
        // le premier trouvé — plusieurs InventorySpawn sont chargés
        // simultanément (un par zone ?), pas un point unique dédié au hub.
        private static InventorySpawn FindSpawnPoint()
        {
            var all = UnityEngine.Object.FindObjectsByType<InventorySpawn>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;

            var origin = FindLocalPlayerPosition();
            if (origin == null || all.Length == 1)
                return all[0];

            InventorySpawn closest = null;
            var closestSqrDistance = float.MaxValue;
            foreach (var candidate in all)
            {
                if (candidate == null)
                    continue;

                var sqrDistance = (candidate.transform.position - origin.Value).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = candidate;
                }
            }

            return closest ?? all[0];
        }

        private static Vector3? FindLocalPlayerPosition()
        {
            var all = PlayerCharacter.allPlayerCharacters;
            if (all == null)
                return null;

            foreach (var pc in all)
            {
                if (pc != null && pc.isLocalPlayer)
                    return pc.transform.position;
            }

            return null;
        }

        private static RewardGourd FindTemplate()
        {
            var all = UnityEngine.Object.FindObjectsByType<RewardGourd>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0)
                return null;

            // Exclut nos propres clones précédents (marqués par
            // CosmeticNameSuffix) : sinon un clone déjà spawné sert de
            // template au clone suivant, qui clone un clone qui clone un
            // clone... — toujours cloner depuis un vrai RewardGourd du jeu.
            //
            // Préfère un gourd déjà "Loose" (rendus/visuels dans l'état
            // "ramassable normal") ; à défaut, n'importe quel gourd chargé
            // fait l'affaire, son état sera de toute façon forcé explicitement
            // par l'appelant juste après le clonage.
            foreach (var candidate in all)
            {
                if (candidate != null && candidate.prop != null && candidate.gourdState == GourdFlag.GourdState.Loose
                    && !candidate.gameObject.name.Contains(CosmeticNameSuffix, StringComparison.Ordinal))
                    return candidate;
            }

            foreach (var candidate in all)
            {
                if (candidate != null && candidate.prop != null
                    && !candidate.gameObject.name.Contains(CosmeticNameSuffix, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }
    }
}
