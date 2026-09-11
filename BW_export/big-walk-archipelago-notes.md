# Big Walk — Notes de rétro-ingénierie pour implémentation Archipelago

*Dernière mise à jour : session du 10 septembre 2026*

## To-do actuelle (mise à jour le 2026-09-09)

### À implémenter côté mod

- **Client réseau Archipelago** — le vrai chantier manquant : rien ne se connecte à un serveur AP aujourd'hui. Remplacer `LocalLogReporter` par une vraie implémentation `ICheckReporter` (protocole AP, WebSocket/JSON), et appeler `ItemApplier` pour de vrai à la réception d'un item (au lieu du simulateur `DebugItemSimulator`).
- **`ItemApplier.ApplyGenericGourdFiller()`** — matérialise un item "gourd" générique (filler, pas 1:1) sur n'importe quel gourd non-résolu localement ; réutilise `ApplyGourdItem` en interne une fois la cible choisie ; no-op silencieux si plus aucun gourd non-résolu. Décidé le 2026-09-09 (cf. section "DÉCISION TRANCHÉE — modèle final location/item" plus bas) ; big keys restent 1:1, inchangées.
- **RÉSOLU (2026-09-11) — Spawn cosmétique du gourd reçu** — objet physique ramassable au hub, apparaît maintenant à chaque item appliqué via `ItemApplier` (`Core/ReceivedItemSpawner.cs`, nouveau fichier). Clone un `RewardGourd` déjà chargé en scène (`InventorySpawn.GetNextSpawnPosition()` pour la position, le plus proche du joueur si plusieurs sont chargés). Session de test riche en pièges Mirror/moteur, tous résolus (voir les commentaires en tête de `ReceivedItemSpawner.cs` pour le détail technique de chacun) :
  1. Cloner un objet placé en scène (pas un vrai prefab dynamique) fait hériter `NetworkIdentity.sceneId`/`hasSpawned` du template → warning moteur "already spawned" pendant `Awake()` (appelé de façon synchrone PAR `Instantiate()`, donc trop tard pour corriger après coup) — contourné en désactivant le template avant `Instantiate` (Awake différé jusqu'à `SetActive(true)`).
  2. `NetworkIdentity.SpawnedFromInstantiate` (mis à `true` par `Awake()`) à remettre à `false` aussi — ces deux-là (`hasSpawned`/`SpawnedFromInstantiate`) sont exposés comme des PROPRIÉTÉS générées par Il2CppInterop, pas de vrais champs réfléchissables malgré leur déclaration en champs privés côté jeu (`Traverse.Field` de Harmony échoue silencieusement dessus) — réflexion sur le setter de la propriété à la place.
  3. `saveablePropName` neutralisé (`notSavable`) **avant** activation/`ServerSetGourdState`, jamais après — testé dans l'autre sens, `GourdStatePatch` a alors traité l'appel comme une vraie résolution de puzzle et rapporté un faux check (`gourdTellerWindow` dans ce test).
  4. La vraie cause de "l'invisibilité" (tous les indicateurs Unity au vert — `enabled`/`isVisible`/`activeInHierarchy` — pourtant rien à l'écran) : le clone se **téléportait** ailleurs sur la carte quelques instants après le spawn, via `Prop.Start()` (le mécanisme de restauration au chargement, cf. section dédiée plus bas) qui lisait `startHome` (le casier d'origine du template, copié tel quel par `Instantiate`) — neutralisé (`startHome = null`) en même temps que `saveablePropName`. Diagnostiqué en comparant la position du clone juste après spawn vs. 3s plus tard (`DebugHotkeys`, touche P).
  5. `Prop.SetLoose()` à appeler explicitement — `RewardGourd.ServerSetGourdState(Loose)` ne pilote que le SyncVar visuel/logique du puzzle, pas l'état physique du `Prop` (sans ça, le clone reste "fixé"/sans gravité comme dans son étau d'origine).
  6. `MaterialPropertyBlock` par-instance du template (pas copié par `Instantiate`, contrairement aux références sérialisées) capturé puis réappliqué sur le clone par précaution.
  Confirmé en jeu : visible, ramassable/déposable normalement, physique correcte, aucun check ni collision `SaveManager`. Durée de vie = jusqu'au ramassage (pas de timer) ; visible de tous les joueurs (spawn réseau standard). Reste ouvert (mineur, pas creusé) : l'aspect exact (couleur/texture propre à chaque puzzle) n'est pas garanti correspondre au vrai puzzle représenté, `MaterialPropertyBlock` copié du template mais jamais vérifié visuellement si ça matche un puzzle précis ou reste un aspect générique.
  - **REVIREMENT DE DESIGN (2026-09-11, joueur, question posée après coup)** — première itération avait explicitement vidé `prop.propGroups` sur le clone pour l'empêcher d'être épinglé dans un vrai monument (crainte d'un faux remplissage, `PropHomeBlock.pinnedProp` étant une référence live indépendante de `SaveManager`). **Le joueur a ensuite précisé l'intention réelle : c'est VOULU** — puisque les puzzles ne donnent plus de gourd exploitable directement dans ce design (le vrai don passe par le réseau AP), les clones cosmétiques doivent devenir le **seul** moyen de remplir les monuments, et ce remplissage doit **persister**. `propGroups.Clear()` retiré ; `RewardGourd`/`Prop` du clone restent pleinement pin-ables dans n'importe quel vrai `PropHome`.
  - **RÉSOLU (2026-09-11) — persistance du remplissage, nouveau fichier `Core/CosmeticMonumentFillTracker.cs`** : investigation Ghidra/statique du mécanisme réel avant de choisir une approche (le joueur l'a explicitement demandé plutôt qu'une solution devinée). Confirmé : `GourdPinAudioBehaviour.GetNumberOfFilledHomes()` (et donc tout l'effet "monument rempli") se base entièrement sur des références **live** (`PropHome.pinnedProp != null`, comptées à la volée) — aucun compteur stocké nulle part. La restauration au chargement est fondamentalement **par identité de prop** : chaque prop retrouve sa place en relisant SA PROPRE clé `SaveManager[saveablePropName]` dans `Prop.Start()`. Un clone cosmétique n'a pas d'identité stable réutilisable sans revenir au risque de collision de check déjà réglé (cf. plus haut). Piste "réutiliser `gourdTesting00-39`" (40 valeurs `SaveablePropName` jamais utilisées en jeu normal, déjà confirmées exclues du registre de check par `GourdRegistry`/`GourdStatePatch`/`SaveValuePatch`) envisagée puis écartée : capacité insuffisante face aux ~45 emplacements de monuments réels du jeu (comptage F6, si le remplissage doit pouvoir se faire n'importe où) — et extension avec les 6 `bigKeyTesting*` explicitement écartée aussi (question du joueur) : sémantiquement des clés, pas des gourds, risque non-audité qu'un autre système du jeu scanne les clés `SaveManager` par préfixe `bigKey*` pour une raison indépendante des `PropGroup`/pin.
    - **Solution retenue : indexer par `PropHome` (la PLACE), pas par `Prop` (l'occupant)** — clé dédiée `SaveManager["ap_home_" + saveableHomeName] = 1`, aucune collision possible avec les clés `SaveablePropName`/`SaveableHomeName` du jeu, aucune limite de capacité (une place = une clé, autant de places que nécessaire).
    - **Détection du pin** : `PropHome.onAnyChangeServer` — un event **statique global** déjà exposé par le jeu (pas un Harmony patch), déclenché pour chaque changement de pin dans tous les `PropHome` du jeu. Piège Il2CppInterop rencontré en l'abonnant : `+=` direct sur un method group échoue (`PropHomeChangeEvent` n'est pas un delegate .NET normal) ; `new PropHome.PropHomeChangeEvent(methodGroup)` échoue aussi (seuls constructeurs réels : `(Il2CppSystem.Object, IntPtr)`/`(IntPtr)`, pas utilisables depuis du C# managé). Résolu en inspectant les vraies méthodes du type compilé (`System.Reflection.MetadataLoadContext` sur `Assembly-CSharp.dll`, pas le dump statique Il2CppInspectorRedux qui ne montre pas forcément la même chose) : un opérateur de conversion implicite `op_Implicit(System.Action<PropHome,Prop,Prop>)` existe — cast du method group en `Action<PropHome,Prop,Prop>` d'abord, conversion implicite ensuite. Un clone cosmétique est reconnu sans ambiguïté par `saveablePropName == notSavable` (jamais vrai pour un prop normal du jeu) + le suffixe de nom du clone.
    - **Restauration au chargement** : au world-ready (même timing qu'`ArchDoorUnlocker`), parcourt `PropHome.allPropHomes`, et pour chaque home avec un flag `ap_home_*` posé et `pinnedProp == null`, clone+épingle directement un gourd cosmétique dedans (nouvelle méthode `ReceivedItemSpawner.SpawnCosmeticPickupPinnedTo`, réutilise tout le cœur de clonage déjà validé, `GourdState.Stashed` au lieu de `Loose`).
    - **Hypothèse non vérifiée en jeu** : la restauration ne tourne qu'**une seule fois** par session (comme `ArchDoorUnlocker`), en supposant que tous les `PropHome` du jeu sont chargés simultanément (monde ouvert sans vrai streaming de zones). Si des monuments distants ne se chargent qu'à l'approche du joueur, cette restauration unique manquerait ceux hors de la zone de démarrage — à confirmer en jeu (prochaine session de test), pas encore fait.
    - **RÉSOLU, confirmé en jeu bout-en-bout (2026-09-11)** — deux bugs trouvés et corrigés en testant :
      1. **`DebugCosmeticPinForce` épinglait dans le mauvais type de `PropHome`** — `PropHome` est aussi utilisé pour des emplacements portés par le joueur (ex. une "ceinture" d'inventaire), pas seulement les monuments ; sans filtre, le gourd le plus proche vide était presque toujours cet emplacement porté (distance ~0, attaché au joueur), pas un vrai monument (log : "épinglé dans notSavable", et visuellement une "ceinture" apparaissait sur le joueur au lieu d'un dépôt). Fix : `ReceivedItemSpawner.IsMonumentHome` (préfixe `saveableHomeName` == "monoument"), utilisé pour filtrer `DebugCosmeticPinForce` ET `CosmeticMonumentFillTracker` (détection ET restauration).
      2. **`PropHome.onAnyChangeServer` (event statique global) ne se déclenche jamais en pratique** — abonnement confirmé réussi, mais zéro déclenchement malgré un pin par ailleurs confirmé réussi (`Prop.ServerSetPinned`) — probablement un champ jamais câblé par le jeu. Le vrai signal est `PropHome.onChangeServer`, la version **par-instance** du même type d'event : il faut s'abonner individuellement sur chacun des ~536 `PropHome` chargés (pas de raccourci global qui fonctionne). `PropHome.onPinServer` (`Action<Prop>` plus simple) fonctionne aussi mais ne couvre pas le dépin, laissé de côté.
      - Test complet validé : gourd cosmétique spawné (P) → épinglé de force dans `monoumentIntroSlot0` (N, `DebugCosmeticPinForce`) → `[CosmeticMonumentFillTracker] ap_home_monoumentIntroSlot0 = 1` confirmé dans les logs → jeu entièrement fermé et relancé → `[CosmeticMonumentFillTracker] 1 gourd(s) cosmétique(s) restauré(s) dans leurs monuments.` → **gourd retrouvé physiquement épinglé dans le monument**, confirmé visuellement par le joueur. Chaîne complète (spawn → pin → persistance → rechargement → restauration) validée de bout en bout.
      - Reste non vérifié : l'hypothèse "un seul passage de restauration au démarrage suffit" (tous les `PropHome` chargés simultanément, pas de vrai streaming de zones) — un seul monument testé, pas encore confirmé avec des monuments dans des zones distinctes/éloignées.
    - **Outil de debug ajouté** : le dépôt manuel dans un vrai monument nécessite l'interaction peck/maintien normale du jeu (même friction que les cloches "N-hold" déjà rencontrées) — `Debug/DebugCosmeticPinForce.cs`, touche **N**, épingle directement (`Prop.ServerSetPinned`, sans passer par l'interaction) le gourd cosmétique le plus proche dans le `PropHome` vide le plus proche (30m). Déclenche `PropHome.onAnyChangeServer` exactement comme un vrai pin, donc `CosmeticMonumentFillTracker` persiste normalement sans traitement spécial. `ReceivedItemSpawner.IsCosmeticClone` rendu `internal` (partagé entre `CosmeticMonumentFillTracker` et cet outil).
- **Détection des gourd deposit boxes (monuments)** — au minimum le mode "compteur global progressif" (le plus simple, `GourdPinAudioBehaviour.GetNumberOfFilledHomes()` déjà repéré), pensé pour devenir un réglage reçu du monde/slot AP plutôt qu'un mode figé en dur (cf. section dédiée plus bas).
- **Radio stations comme locations** — étendre `SaveValuePatch` pour tenter aussi `Enum.TryParse<SavableSystem>` (en plus de `SaveablePropName` déjà géré), reporter un check quand ça matche. Pas de matérialisation d'item nécessaire (juste de la détection).
- **`Config.cs`** — ajouter les entrées de config serveur AP (adresse, slot name, password) une fois le client réseau existant ; possibilité d'exposer ça dans le menu d'hébergement in-game plutôt qu'un fichier `.cfg` (idée notée, pas encore investiguée — cf. section dédiée).
- **RÉSOLU (2026-09-11) — la "boule noire" au hub/spawn, distincte de l'entrée du Gauntlet** (celle-là avait déjà été corrigée plus haut). Investiguée en jeu sur une save où le jeu vanilla a déjà été fini une fois : l'objet n'est pas une sphère intacte qui disparaît, mais se scinde visuellement en morceaux (`Sphere`/`ModelOrb Colliders` + pièces `_WallInterior`/`_WallRing`/`_WallAngle`/`_WallShort`/`_WallLong`/`_WallEnd`). **Aucun de ces objets ne porte de `TrackedPeckState`/`PeckSwitch`** (juste `Transform`+`Collider`) — ce n'est donc **pas** un mécanisme Peck comme tout le reste du jeu, mais probablement une décision prise une fois au chargement de la scène (quelle variante spawn) en lisant un flag existant.
  - **CORRECTION (2026-09-10)** — hypothèse initiale fausse : `<steamId>keyWalkingProven` (vu à `=1` dans le dump `SaveManager` de la save complétée) **n'est pas** un flag "jeu vanilla fini". Décompilation de `PlayerTeacher` (`il2cpp.cs`) : c'est le système de **tutoriel de marche** du jeu (`LearnState { Uninitialized, Observing, Teaching, Proven }`, `EnterTeachingZone()`/`ExitTeachingZone()`, `ServerSaveWalkingProven()`) — cohérent avec le titre "Big Walk", ça matérialise juste "ce joueur a appris à marcher" très tôt en partie, sans rapport avec la fin du jeu. Explique aussi l'observation : sur une save très jouée, `keyWalkingProven` est évidemment déjà `=1` depuis longtemps (le tutoriel est fait dès le début), donc le voir à 1 sur une save "complétée" ne prouve rien de spécifique à la vraie fin.
  - **Donc pas encore résolu** : soit la sphère/mur repérée est en fait le portail du tutoriel de marche lui-même (pas un gate "vraie fin"), soit il existe un autre objet distinct plus loin sur le chemin vers la vraie fin qu'on n'a pas encore localisé. **Ne pas forcer `keyWalkingProven` en pensant débloquer la vraie fin** — sans effet utile pour ça (même si probablement sans danger par ailleurs, système indépendant du goal/`EndingGate`/`GauntletComplete`). Prochaine étape suggérée : comparer la même zone sur une save **fraîche** (jamais fait le tutoriel) contre la save complétée, pour voir si c'est vraiment `keyWalkingProven` qui fait la différence ou autre chose.
  - **Piste `skipAidsActive` également écartée (2026-09-10)** : F11 (`unlocks=true`) testé directement sur la sphère — sans effet, portes ouvertes mais sphère inchangée. `SaveData.skipAidsActive` ajouté au dump F8 (`Debug/DebugSaveDump.cs`) et testé sur la save où la sphère est déjà cassée : **`skipAidsActive=False`** — donc pas non plus le pattern "défi déjà battu, proposer de sauter" utilisé par le Gauntlet (`SkipAid Guantlet`/`SkipAidToggler`). Mots-clés F9 élargis (`skip`/`proven`/`reward`/`postgame`/`complet`) sans rien trouver de nouveau près de `Sphere`/`_Wall*`/`ModelOrb Colliders`/`playerblocker` — toujours seulement `Transform`+`Collider`, aucun script.
  - **Décompilation Ghidra de la chaîne "fin de partie" (2026-09-10)** — repéré statiquement : `AutomaticDisconnector.StartEndingTransition(PlayerCharacter)` (une `PlayerZone` qui démarre la transition), `EndingTransition.SetActive()`/`OnTransitionEnd()`, et `PeckEffectEndingTransition.OnPeck` (celui-là *est* branché au système Peck, via `PeckSystemReference`/`stateFilter`). Décompilation réelle des 4 fonctions (venv PyGhidra, projet déjà labellisé, cf. `reference-bigwalk-dev-environment`) : **aucune n'écrit dans `SaveManager`** (ni `SetIntValue` ni `SetStringValue`). Tout le chemin ne fait que : filtrer sur l'état Peck reçu (`stateFilter.specificStates`), arrêter le réseau Mirror (`NetworkManager.StopClient/StopServer/OnStopHost`), régler le mode d'entrée du menu principal (`entryMode`), et lancer le fondu. **Conclusion : il n'existe probablement aucun flag dédié "jeu terminé"** — `WorldMenuManager` a bien deux `EndingTransition` (`endingTransition`/`secondEndingTransition`, fin standard vs vraie fin) mais aucune des deux ne persiste quoi que ce soit à elle seule.
  - **Implication pour la sphère du hub** : si elle vérifie vraiment une notion de "jeu déjà fini", elle le fait probablement en lisant une **combinaison de flags déjà connus** (ex. `EndingGate` + `GauntletComplete` + les 7 plinthes de big key) plutôt qu'une clé dédiée qu'on aurait ratée. Bonne nouvelle pour le mod si confirmé : ces flags sont déjà ceux qu'`ItemApplier`/`SaveValuePatch` gèrent nativement, donc la sphère se débloquerait d'elle-même pour un joueur Archipelago ayant fait les vrais checks prérequis — pas de code spécial à prévoir a priori. **Toujours pas confirmé en jeu** (pas trouvé le script qui lit cette combinaison, ni testé si positionner tous ces flags ensemble sans jamais avoir vu l'écran de fin suffit à casser la sphère).
  - **État réel (2026-09-10, fin de session)** : mécanisme non identifié avec certitude, mais fortement recentré sur "combinaison de flags existants" plutôt que "flag dédié introuvable". À reprendre : comparer une save 100% fraîche au même endroit, ou tester en forçant `EndingGate`+`GauntletComplete`+les 7 big keys sur une save qui n'a jamais vu l'écran de fin pour voir si la sphère casse.
  - **NOUVEAU (2026-09-11, relecture du `LogOutput.log` du 2026-09-10, pas encore exploité)** — un dump F9 (`DumpNearbyByKeyword`, 30m) fait juste au hub, sur une save où seul `EndingGate=1` est posé (pas de big keys, pas de `GauntletComplete`), révèle deux objets jamais notés jusqu'ici : **`Spawn_SecondEnding_Sphere_Whole`** (`MeshFilter`+`MeshRenderer`+`SphereCollider`, à 3,5m du joueur — quasi certainement LA sphère intacte elle-même, le nom "Whole" impliquant fortement une contrepartie "cassée" quelque part) et, dans un second cluster plus loin (~21m, celui où vivent déjà `ModelOrb Colliders`/`_WallInterior`/`_WallRing`/`_WallAngle` connus) un objet **`SecondEndingDoorLogic`** portant un `PeckEffectAnimancer`. **Ça contredit la conclusion du 2026-09-10** ("aucun de ces objets ne porte de Peck") : c'est vrai des objets `Sphere`/`_Wall*` eux-mêmes, mais pas plus loin — `SecondEndingDoorLogic` est bien Peck-driven. Juste à côté de la sphère (4,8m), un objet **`OpenSystem`** porte directement `TrackedPeckState`+`PeckSystemBlock`+`PeckBusConnection`+2×`PeckEffectToggle`+`PeckEffectAudio` — profil qui ressemble beaucoup au vrai déclencheur (2 `PeckEffectToggle`, cohérent avec "bascule entre variante intacte et variante cassée" + son de résolution). Repéré aussi : `Spawn_SecondEnding_Door (1)` (5,1m, un `BoxCollider` — probablement une deuxième porte/segment du même ensemble) et `EndingGateIndicator` (4,9m, juste `LODGroup`, purement visuel). **Pas encore confirmé** : `OpenSystem.savableSystem` (la vraie clé `SaveManager`) n'a pas été lu — `DebugPeckCombinatorLookup` (F7) ne le couvre pas (`OpenSystem` n'a pas de `PeckCombinator`), et `DebugTrackedPeckStateLookup` (Insert) est câblé en dur sur `SavableSystem.SpawnHubGate` seulement. **Fix apporté ce jour** : `DebugComponentLookup` (F9) logue maintenant, pour chaque objet trouvé, son chemin hiérarchique complet (parent chain) et, s'il porte un `TrackedPeckState`, son `savableSystem`/`saveGuid`/valeur `SaveManager` actuelle (même format que `DebugPeckCombinatorLookup`) — build déployé (DLL copiée dans `BepInEx/plugins/`), mais **pas encore retesté en jeu**. Prochaine session : se tenir au hub, presser F9, lire la ligne `OpenSystem` pour obtenir la vraie clé, et le chemin hiérarchique pour confirmer si `Spawn_SecondEnding_Sphere_Whole`/`OpenSystem`/`Spawn_SecondEnding_Door (1)` partagent un même parent (un seul ensemble "porte de la deuxième fin") plutôt que deux mécanismes indépendants.
  - **Idée du joueur (2026-09-11), alternative si le flag reste difficile à confirmer** : plutôt que de chercher le vrai flag/mécanisme Peck, simplement désactiver l'objet bloquant (`Spawn_SecondEnding_Sphere_Whole` et/ou son collider) à chaque démarrage de session — même philosophie que `ArchDoorUnlocker` ("pas d'intérêt à rester fermé pour un monde Archipelago"), mais en plus simple : pas de dépendance à `SaveManager`/persistance, juste un `SetActive(false)`/désactivation de collider rejoué à chaque session (contrairement à `ArchDoorUnlocker`, ne peut pas se contenter d'un flag "déjà fait une fois" puisque rien ne serait persisté). Piste retenue comme filet de sécurité si `OpenSystem` s'avère être un déclencheur à large broadcast (comme `PeckDevHelper.Trigger(unlocks=true)` l'était pour les Arch doors, cf. plus haut) plutôt qu'un switch ciblé.
  - **PISTE EXPLORÉE PUIS INVALIDÉE (2026-09-11)** — hypothèse initiale : la plinthe `bigKeyPlinthGoodbye2` (juste derrière la sphère) serait surveillée par `OpenSystem` (`PropHomeBlock`-like) et son remplissage déclencherait la casse de la sphère. **Corrigée par le joueur (connaissance du jeu) : c'est l'inverse** — la plinthe `bigKeyPlinthGoodbye2` n'est **physiquement accessible que si la sphère est déjà cassée** (donc seulement après une première complétion du jeu en vanilla). Le remplissage de cette plinthe est une **conséquence** de la sphère cassée, pas sa **cause** — `bigKeyOverflow`/`Goodbye2` est un bonus/collectible postgame positionné derrière la porte, pas le déclencheur de cette porte. Ça invalide le raisonnement "`ItemApplier` fait déjà un pin live donc la sphère se débloquerait toute seule" : dans l'autre sens, épingler `bigKeyOverflow` via le mod ne casserait pas la sphère (aucune raison causale), même si `ItemApplier.TryApplyLiveEffect` peut techniquement toujours pinner ce prop à distance (bypass code, sans marcher jusqu'à la plinthe) — ça n'a juste aucun rapport avec la sphère elle-même. **Retour à la case départ sur la vraie condition de la sphère** : très probablement encore la notion originelle "jeu déjà fini une fois" (peut-être juste `GauntletComplete`, le check de la vraie fin, vu que `Goodbye2` suit thématiquement `GoodbyeChapel`/`GoodbyeVoid` sur le chemin de la vraie fin) — non confirmé, et la touche debug **O** ajoutée ce jour (`ApplyBigKeyOverflowKey`) ne teste plus une hypothèse pertinente pour la sphère (gardée dans le code, inoffensive, mais ne pas en attendre un effet sur la sphère).
  - **DÉCISION (2026-09-11)** — vu la difficulté à confirmer la vraie condition sans plus de sessions de reverse engineering, et vu que le besoin réel du mod est juste "la sphère ne doit jamais bloquer un joueur Archipelago dès la première session" (peu importe la vraie condition vanilla), **abandon de la piste "trouver le vrai flag/mécanisme Peck" au profit de l'idée déjà notée comme filet de sécurité** : désactiver directement l'objet bloquant à chaque démarrage de session, sans dépendre de `SaveManager`/d'un flag quelconque. Implémenté : `Core/SecondEndingSphereUnlocker.cs` (même timing que `ArchDoorUnlocker` — poll sur `WorldManager.isReadyForEffects`/`NetworkServer.active`), désactive (`SetActive(false)`) tout GameObject chargé dont le nom commence par `Spawn_SecondEnding` (couvre `Spawn_SecondEnding_Sphere_Whole` ET `Spawn_SecondEnding_Door (1)`/ses clones, sans devoir lister chaque suffixe exact) — préfixe choisi précisément (pas un mot-clé générique comme "wall"/"block") pour éviter tout effet de bord ailleurs sur la carte. Contrairement à `ArchDoorUnlocker`, **pas** de flag `SaveManager` "déjà fait une fois" : rien n'étant persisté par un `SetActive` local, le composant doit rejouer cette désactivation à **chaque** session (comportement voulu, cf. demande initiale du joueur).
  - **Bug trouvé au premier test (2026-09-11)** : premier déploiement confirmé en jeu — la sphère était bien désactivée, mais l'entrée de la "deuxième fin" était **aussi** ouverte dès le début de partie, pas voulu (préfixe `Spawn_SecondEnding` trop large, attrapait aussi `Spawn_SecondEnding_Door (1)`, la vraie porte d'entrée — la progression réelle vers la deuxième fin doit rester intacte, seule la sphère doit disparaître). **Fix** : préfixe resserré à `Spawn_SecondEnding_Sphere` (`Core/SecondEndingSphereUnlocker.cs`), qui ne touche plus que `Spawn_SecondEnding_Sphere_Whole`.
  - **RÉSOLU, confirmé en jeu (2026-09-11, après fix)** — sphère toujours désactivée, porte de la deuxième fin bien refermée (progression intacte). `SecondEndingSphereUnlocker` fait exactement le travail attendu. Fin de cette investigation — plus besoin de creuser `OpenSystem`/le vrai flag pour ce besoin précis (resterait un intérêt purement académique si jamais utile ailleurs, mais plus bloquant pour le mod).
- **Idée notée (2026-09-10, joueur)** — colorer chaque big key reçue pour matcher la couleur de sa tour associée (RedZone/YellowZone/GreenZone/BlueZone), utile pour un futur spawn cosmétique ; `bigKeyBoss` et `bigKeyOverflow` sont déjà cohérents visuellement (noir/violet), pas besoin d'y toucher. Pas encore investigué techniquement (matériau/shader du prop à cibler).
- **CORRECTION (2026-09-10, joueur, précision du flow) — la "sphère noire" est l'entrée du Gauntlet, pas une porte de premier-run** : l'hypothèse initiale ("obstacle conditionné à une première complétion du jeu") était fausse. Flow réel confirmé par le joueur : `bigKeyBoss` obtenue → la chapelle (`GoodbyeChapel`) s'ouvre, avec une cloche à l'intérieur (**une seule cloche**, pas deux — corrige l'entrée ci-dessous) ; casser la cloche à 2 joueurs (`NHoldLogic`/`EndingGate`, déjà résolu) ouvre une porte vers une immense zone vide ; au bout de cette zone, la fameuse sphère noire = **l'entrée du Gauntlet** (pas un obstacle de reprogression) ; à l'intérieur, 6-7 salles-puzzle (`GauntletChamber0..6` dans `SavableSystem`, 7 valeurs — correspond bien), chacune ouvrant l'escalier vers la suivante. Donc la "deuxième location loin, qui pourrait compter comme goal completion" mentionnée initialement par le joueur == la **complétion du Gauntlet**, pas une deuxième cloche littérale.

### À tester/valider en jeu

- **PROCHAINE ÉTAPE PRIORITAIRE (2026-09-10) — tester l'hypothèse "combinaison de flags" pour la sphère noire du hub.** Nouvelle touche debug **PageDown** (`Debug/DebugForceEndingFlags.cs`, `ForceEndingFlagsKey`) déjà codée, buildée et déployée : force d'un coup les 7 big keys (via `ItemApplier`, pas besoin de les trouver physiquement) + `SaveManager[EndingGate]=1` + `SaveManager[GauntletComplete]=1`. Marche à suivre :
  1. Démarrer une partie **strictement neuve** (jamais vu l'écran de fin — sinon le test ne prouve rien).
  2. Noter l'état de la sphère au hub avant.
  3. Appuyer **PageDown**, recharger la zone si besoin.
  4. Revérifier la sphère : cassée en morceaux → hypothèse confirmée (pas de code spécial à prévoir côté mod, la sphère se débloque toute seule via les checks déjà gérés) ; toujours intacte → hypothèse invalidée, piste à abandonner et en chercher une autre (cf. contexte complet dans la section "boule noire" plus bas).
  - Contexte complet (pourquoi cette hypothèse, ce qui a déjà été écarté) : voir l'entrée "EN COURS — la boule noire au hub/spawn" plus bas dans cette même section, et la sous-section Ghidra qui l'accompagne.
- **La cloche de la chapelle (une seule, pas deux — cf. correction ci-dessus)** — résolue (`NHoldLogic` → `EndingGate`, minimumMatches=2). Plus de test nécessaire pour la cloche elle-même.
- **Le Gauntlet (6-7 salles-puzzle, `GauntletChamber0..6`)** — entrée = la sphère noire au bout de la zone vide après la chapelle. `VictoryLogic`/`NHoldSucess`/`ChallengeCompletedSystem` (vus près de l'entrée/salle de victoire) sont tous `NotSavable` : pas les bons objets pour la persistance par chambre. Reste à faire : aller F7/Delete **à l'intérieur de chaque chambre individuelle** (pas juste à l'entrée) pour trouver les `TrackedPeckState` avec `savableSystem=GauntletChamber0` (puis 1, 2, ... 6) — ils existent forcément quelque part vu que ces valeurs sont dans l'enum `SavableSystem`. Vérifier aussi si `SaveData.skipAidsActive` change après une complétion complète du Gauntlet (piste "déjà battu, proposer de sauter" plutôt qu'un vrai check par chambre).
- **RÉSOLU (2026-09-10)** — deuxième cloche en haut du Gauntlet = vrai check "fin de partie", confirmé formellement en jeu. Après avoir résolu les 6-7 chambres, une **deuxième cloche** au sommet du Gauntlet (entourée de 2 boutons, même mécanisme `NHoldLogic 2`/`PeckCombinator`, `minimumMatches=2`) écrit bien **`SaveManager[GauntletComplete]=1`** (`SavableSystem.GauntletComplete`, valeur 28) une fois cassée — confirmé par dump complet `SaveManager` (F8) juste après. C'est ce check-là qui correspond à la "deuxième location, goal completion" mentionnée initialement, pas le Gauntlet lui-même ni ses chambres individuelles.
  - **Chaîne technique complète confirmée** : `rule.systems[]` du `PeckCombinator` était vide (piège rencontré en testant) — les 2 `TrackedPeckState` réels ne sont pas dans `PeckRule.systems[]` mais dans `PeckRule.block.systems[]` (`rule.block` = un `PeckSystemBlock`, ex. `ButtonNHoldTwo`/`NHoldLogic 2`), un champ qu'`DebugPeckCombinatorLookup` ne loguait pas initialement — ajouté depuis. Les 2 slots pointent tous les deux vers un `TrackedPeckState` nommé `ButtonSystem` (un par bouton physique, nom générique réutilisé partout — pas de `SaveIdentity`/`savableSystem` dessus, seul `NHoldSucess`→`onConditionMet` propage en aval jusqu'à l'écriture réelle).
  - **Méthode de résolution en solo** : simuler des pressions de bouton (`PeckSwitch.Peck()`, `Debug/DebugPeckFire.cs`, touche PageUp) n'a pas suffi — soit parce que les boutons "N-hold" nécessitent un maintien continu (pas un simple tap), soit parce que le seul `UpSwitch` à portée était le sien propre (rebasculé au lieu de simuler le second joueur). Solution qui a marché : forcer directement l'état (`TrackedPeckState.SetState(int)`, court-circuite tout le système d'appui/maintien) sur les `systems[]`/`block.systems[]` du `PeckCombinator` proche — nouvel outil `Debug/DebugPeckCombinatorForce.cs`, touche **End**. A cassé la cloche du sommet en solo, sans second joueur.
  - **Chapel bell → `EndingGate` recoupé aussi** : le dump `SaveManager` (F8) après cassage de la cloche du sommet montre aussi `EndingGate=1` (déjà acquis plus tôt dans la session), confirmant les deux cloches comme deux checks distincts et indépendants dans le même store clé/valeur générique — aucun besoin de mécanisme spécial pour les gérer côté mod, le `SaveValuePatch` générique déjà en place devrait les capter comme n'importe quel autre check.
  - Reste ouvert : le Gauntlet lui-même (6-7 chambres, `GauntletChamber0..6`) n'a toujours aucune persistance individuelle confirmée — cf. entrée dédiée ci-dessus, toujours à investiguer chambre par chambre.
  - **CORRECTION IMPORTANTE (2026-09-10, joueur)** — `EndingGate` n'est **pas** un flag stable "check accompli" comme les gourds/big keys : c'est littéralement l'état vivant ouvert/fermé de la porte, réutilisé par `SpeechlessFieldLogic` pour piloter son animation (`GoToOff`=0/fermé, `GoToOn`=1/ouvert, `GoToAnimating`=2/en cours). Observé en jeu : en activant la porte de la chapelle via End (`DebugPeckCombinatorForce`), `EndingGate` est repassé à **0** au moment où la porte s'ouvrait (probablement un `GoToOff`/reset transitoire dans la séquence d'animation, pas une régression du check). Ça ne casse **pas** la détection de check côté mod : `SaveValuePatch` déclenche le check report dès la **première** écriture non-nulle, et `CheckTracker` marque la location comme reportée en permanence après — donc même si `EndingGate` revient ensuite à 0, le check déjà déclenché reste acquis (pas de perte, pas de re-déclenchement). Juste à garder en tête : ne pas utiliser "`EndingGate` est actuellement non-nul" comme moyen de vérifier l'état de la porte après coup, contrairement aux clés `gourdX`/`bigKeyXxx` qui restent stables une fois écrites.
- **`DebugPeckCombinatorLookup` (F7) a provoqué un freeze (2026-09-10)** — "Application Hang" Windows (pas de crash/exception loggée) en pressant F7 près de `VictoryLogic`, dans la zone Gauntlet (câblage Peck plus dense que les salles de cloche déjà testées : `ChallengeCompletedSystem` porte 3 `PeckRelay`, `VictoryDoorsBlock`/`PrecedingDoorsBlock`/`LiveSignalLogic` à proximité). Outil durci en conséquence (try/catch par combinator/règle/référence + log du nom avant toute lecture de champ, pour au moins savoir quel objet était en cours si ça replante) — mais un vrai plantage natif IL2CPP ne serait pas rattrapable par du try/catch C#. **Retesté avec succès après durcissement** (aucun freeze), mais un deuxième "Application Hang" totalement indépendant (aucune touche debug pressée juste avant, ticking moteur normal jusqu'à l'arrêt net) a eu lieu peu après — probablement un problème d'environnement préexistant (Steam/EOS, cf. `LogEOSRTC ... Ticks have been delayed` déjà visible en usage normal, ou Dissonance voice chat), pas causé par le mod.
- **Le Gauntlet ne persiste PAS via `SaveManager`/`TrackedPeckState` (2026-09-10)** — `VictoryLogic`, `NHoldSucess` et tout ce qui en dépend ont tous `savableSystem=NotSavable` et aucun `SaveIdentity` (confirmé par F7/Delete directement dessus). Contrairement aux gourds/big keys/la cloche EndingGate, rien de la chaîne locale n'écrit dans le store clé/valeur générique. Piste la plus probable : `SaveData.skipAidsActive` (champ `bool` brut trouvé dans le dump statique) + le GameObject `SkipAid Guantlet` (`SkipAidToggler`, `PeckRelay`) repéré à proximité — pattern classique "défi déjà battu une fois → proposer de le sauter la prochaine fois". Si confirmé, la détection du Gauntlet comme location Archipelago demanderait une approche différente (surveiller `SaveData.skipAidsActive` ou hooker `SkipAidToggler` directement) plutôt que le `SaveValuePatch` générique utilisé partout ailleurs. Pas encore vérifié en jeu.
- `gourdTelescopeToBox` (puzzle coopératif sans étau, nécessite 2 joueurs) — jamais testé : présence d'un `RewardGourd` ou non, moment exact de l'écriture `SaveManager`, interaction avec `TryApplyLiveEffect`.
- Non-régression d'un check gourd résolu **normalement** (hors item reçu) après le fix `DebugGourdLookup.FindNearestUncollectedProp`.
- Comportement en vrai multi-joueurs avec un client **non-host** (le modèle d'autorité host est codé partout mais jamais éprouvé en co-op réel avec quelqu'un d'autre que l'hôte).

### Décisions de conception encore ouvertes (bloquent l'écriture du monde Python, pas le mod C#)

- **Goal** — bloqué sur une décision de design (fin standard vs N tours vs combinaison des deux, cf. section dédiée) avant même de chercher le déclencheur technique.
- **Modèle des gourd deposit boxes** — probable option YAML (progressif global / séquentiel par tour / par slot précis sans item critique), à choisir en concevant le monde Python.
- **Implémentation de la dernière zone de fin, juste avant GoodbyeVoid (2026-09-10)** — probablement `GoodbyeChapel` (cf. `Enviro.LobbyLighting.AreaType`, juste avant `GoodbyeVoid` dans l'énumération) ; encore à définir comment cette zone s'intègre au modèle Archipelago (goal final vs simple étape intermédiaire vers `bigKeyPlinthGoodbye2`/`bigKeyOverflow`, cf. les deux cloches ci-dessus qui pourraient y être liées). Pas encore investigué techniquement — à examiner à l'occasion de la session de test des cloches, une fois sur place.

### Explicitement écarté / mis de côté

- **Key Cutters** — décidé de ne pas explorer, étape intermédiaire sans intérêt comme check séparé.
- **`lock_map_room`** et portes **Poet&Priest/Poet&Pontiff** — mis de côté faute de comprendre ce que c'est ; à reprendre seulement si l'un de vous tombe dessus en jouant.

## Historique — investigation initiale des big keys (2026-09-07, résolu)

1. **Faire apparaître une gourd visible au spawn/hub à la réception d'un item.**
   `Core/ItemApplier.ApplyGourdItem` (implémenté) gère déjà toute la persistance/matérialisation réelle sans rien spawner — le design retenu n'a **pas** besoin d'un objet physique porté par le joueur pour fonctionner (cf. section "Design retenu" plus bas). Ce spawn serait donc **purement cosmétique/notification** ("tiens, tu viens de recevoir gourdX"), pas le mécanisme de délivrance lui-même — à garder en tête pour ne pas réintroduire par erreur l'ancienne idée de gourd générique portée à la main.
   Pistes déjà identifiées côté technique (non vérifiées) :
   - `InventorySpawn.GetNextSpawnPosition()` : point d'ancrage avec dispersion aléatoire dans un rayon (`spawnRadius`) — candidat naturel pour la position de spawn.
   - Cloner un `RewardGourd`/`Prop` existant en scène (`UnityEngine.Object.Instantiate` sur une instance déjà présente, pas besoin de trouver une référence de prefab) + `NetworkServer.Spawn` pour le réseauter. Attention : un clone garde le `saveablePropName` de l'original — s'assurer que ce gourd cosmétique n'écrit jamais dans `SaveManager` sous ce nom (ou le neutraliser/detacher du système de save), sinon collision avec la vraie entrée du gourd reçu.
   - **RÉSOLU (2026-09-11)** : durée de vie = jusqu'au ramassage (pas de timer) ; visible de tous les joueurs (spawn réseau standard, pas de scoping par connexion). Implémenté dans `Core/ReceivedItemSpawner.cs`, cf. entrée dédiée tout en haut de ce document ("À implémenter côté mod") pour le détail technique complet (plusieurs pièges Mirror/moteur rencontrés et résolus).

2. **Système de clés (big keys) : découpler la feature débloquée de l'usage de la clé.**
   Une clé se débloque avec un nombre de gourds associées (pins) et débloque une feature du jeu. Idée du user : traiter séparément (a) le **check** "la clé a été utilisée/placée" et (b) l'**item** "on possède la clé" — même philosophie de découplage que pour les gourds (cf. section "Design retenu" plus bas), pas encore appliquée aux big keys.
   Pas encore investigué en profondeur cette session — pistes de départ à partir de ce qu'on sait déjà :
   - `SaveablePropName` a une famille dédiée : `bigKeyIntro`, `bigKeyRedZone`, `bigKeyGreenZone`, `bigKeyBlueZone`, `bigKeyYellowZone`, `bigKeyBoss`, `bigKeyOverflow`.
   - `SaveableHomeName` a les "plinthes" correspondantes : `bigKeyPlinthIntro`, `bigKeyPlinthMapRoom`, `bigKeyPlinthTrain`, `bigKeyPlinthSkiLift`, `bigKeyPlinthTunnels`, `bigKeyPlinthEnding`, `bigKeyPlinthGoodbye2` — pas une correspondance 1:1 par nom comme `gourdX`/`valetX` (7 clés vs 7 plinthes mais noms différents), à clarifier laquelle va où.
   - Confirmé plus tôt : pas de classe `BigKey` dédiée, les big keys passent par le même pipeline générique `RewardGourd`/`Prop` que les gourds — donc `GourdStatePatch`/`SaveValuePatch`/`ItemApplier` les couvrent probablement déjà mécaniquement, mais le lien "N gourds pinnées → clé utilisable → feature débloquée" (le vrai comportement de jeu spécifique aux clés) n'a pas été décompilé/tracé.
   - À investiguer : quelle fonction lit/compte les gourds associés à une clé, quelle fonction représente "utiliser la clé" (le check à détecter), et quelle fonction représente "la feature débloquée" (l'effet à matérialiser côté item reçu, séparément du check).

   **Investigation Ghidra du 2026-09-07** — décompilation ciblée (`PropHomeBlock`, `PeckEffectSavableHome`, `GourdPinAudioBehaviour`) via le projet Ghidra local (`gh-pj/`, dézippé depuis `BigWalk-ghidra-project.zip`). Deux pièges de setup supplémentaires rencontrés (aucun venv PyGhidra trouvé sur la machine malgré la note du 2026-09-03 — reconstruit from scratch cette session) :
   - `NotOwnerException` à l'ouverture headless du projet : `Big Walk.rep/project.prp` stocke le user Windows créateur (`OWNER`) et Ghidra refuse d'ouvrir le projet pour un autre user local. Fix : éditer directement ce XML (`VALUE="a26auber"` → user courant) — fichier texte simple, pas de risque.
   - Invocation qui marche : `ghidra/support/pyghidraRun.bat -H "<dossier projet>" "Big Walk" -process GameAssembly.dll -noanalysis -scriptPath <dossier script> -postScript mon_script.py` (script marqué `# @runtime PyGhidra` en tête). Le launcher installe tout seul le wheel `pyghidra` embarqué (pas besoin d'internet) dans un venv sous `%APPDATA%\ghidra\ghidra_<version>\venv` au premier lancement.

   Résultats de la décompilation :
   - **`PeckEffectSavableHome.Peck()` est la même fonction générique documentée plus haut pour les gourds, réutilisée telle quelle pour les big keys** : le corps ne contient aucun branchement spécifique "big key" — elle lit juste `prop.saveablePropName`/`propHome.saveableHomeName` (références sérialisées à l'instance, configurées dans l'inspector Unity) et appelle `SaveManager.SetIntValue(prop.saveablePropName.ToString(), propHome.saveableHomeName)`. Donc que le prop soit un gourd ou une big key, le check générique (`SaveValuePatch`) le détecte déjà mécaniquement — confirmé au niveau du corps de fonction, plus seulement par hypothèse de pipeline partagé.
   - **`PropHomeBlock` confirmé comme le mécanisme générique "N homes remplis → effet"** : `Awake()` branche `OnPinChange` sur l'event `onChangeServer` de chaque `PropHome` du groupe (`homes[]`) ; `OnPinChange` recalcule si tous les `homes[].pinnedProp` sont non-null et, si l'état "complet" change, appelle `SetFull(bool)`, qui pousse `0`/`1` dans `TrackedPeckState.SetState` (`isFullDirectControlSystem`). Aucune référence à "BigKey" en dur dans ces corps de fonction — c'est probablement le mécanisme qui révèle/débloque une big key une fois N gourds d'une zone pinnées, mais la suite de la chaîne part dans le système de peck générique réutilisé partout dans le jeu. **Pas la peine de la tracer plus loin : le design retenu pour la réception d'item (écriture `SaveManager` pure, sans jamais toucher l'objet vivant) rend cette partie non pertinente pour le mod** — qu'une clé soit révélée ou non en jeu ne change rien à la façon dont son état de sauvegarde est écrit à distance.
   - **Conclusion : aucune classe/logique spécifique aux big keys n'existe dans le code.** Tout ce qui a été décompilé (`RewardGourd`, `Prop`, `PeckEffectSavableHome`, `PropHomeBlock`) est le même pipeline générique que les gourds. L'architecture actuelle (`GourdStatePatch`, `SaveValuePatch`, design "écriture `SaveManager` pure" de `ItemApplier`) n'a donc besoin d'**aucun changement mécanique** pour les big keys.
   - **Seul vrai manque identifié : la table de correspondance `bigKeyXxx → bigKeyPlinthYyy`** (`GourdRegistry.TryGetHomeName` ne gère aujourd'hui que le préfixe `gourd`→`valet`). Cette correspondance est un fait de *scene wiring* Unity (quel `PropHome` avec quel `saveableHomeName` est placé physiquement où, et quel vice/switch de big key y est raccordé) — **invisible dans `GameAssembly.dll`/Ghidra**, ça n'existe que dans les assets de scène sérialisés, pas dans le code. `bigKeyIntro → bigKeyPlinthIntro` était la seule paire évidente par le nom.
   - **RÉSOLU (2026-09-07)** — mapping complet confirmé directement par le joueur (connaissance du jeu, pas de la rétro-ingénierie statique) et codé en dur dans `GourdRegistry.BigKeyHomesByProp` :
     `bigKeyIntro→bigKeyPlinthIntro`, `bigKeyRedZone→bigKeyPlinthMapRoom`, `bigKeyGreenZone→bigKeyPlinthSkiLift`, `bigKeyBlueZone→bigKeyPlinthTrain`, `bigKeyYellowZone→bigKeyPlinthTunnels`, `bigKeyBoss→bigKeyPlinthEnding`, `bigKeyOverflow→bigKeyPlinthGoodbye2`.
     Avec cette table, `ItemApplier.ApplyGourdItem` fonctionne maintenant aussi pour les big keys (plus seulement les gourds) — aucun autre changement mécanique nécessaire (cf. investigation Ghidra ci-dessus : pipeline générique, déjà couvert par `GourdStatePatch`/`SaveValuePatch` côté détection de check).
     **Confirmé une deuxième fois, indépendamment, plus tard dans la session** : un document tiers ("Big Walk Archipelago details.pdf", cf. section dédiée en bas de ce fichier) nomme les vraies tours (Red Funnel/Green Cup/Blue Castle/Yellow Twist/Black Monolith/Green Dome) et leurs lieux de dépôt de clé, qui recoupent exactement cette table ET les comptages F6 des monuments — très haute confiance sur ce mapping désormais.

   **CORRECTION IMPORTANTE (test en jeu du 2026-09-07)** — l'hypothèse "les big keys passent par le même pipeline RewardGourd/Prop que les gourds" (confirmée "plus tôt" selon une note antérieure, jamais vérifiée en jeu) s'est révélée **fausse sur un point précis** : une big key n'a **aucun composant `RewardGourd`**. Découvert via `Debug/DebugGourdLookup.LogNearby` (nouvel outil F5, scanne `FindObjectsByType<RewardGourd>` avec `FindObjectsInactive.Include`) : en se tenant devant une big key visible en jeu, 0 des 46 `RewardGourd` de la zone (actifs et inactifs confondus) ne correspondait à une big key — uniquement des gourds normaux. Donc pas de `GourdState` (Locked/Loose/Stashed/Hidden) pour une big key : c'est un `Prop` nu, probablement piloté par `Prop.onUseAsKey` (`PeckSwitch`) et `PropGroup.BigKey` (cf. section Ghidra ci-dessus), pas par l'étau/couffin des gourds.
   - **Conséquence sur la détection de check** : aucune, `SaveValuePatch` (générique sur `SaveManager.SetIntValue`) capte toujours l'événement peu importe l'absence de `RewardGourd` — seul `GourdStatePatch` (qui patch spécifiquement `RewardGourd.ServerSetGourdState`) ne se déclenchera jamais pour une big key, mais ce n'est pas un problème puisque `SaveValuePatch` fait déjà le travail en filet de sécurité.
   - **Conséquence sur les outils de debug** : `DebugGourdLookup.FindNearestLocked` (basé sur `RewardGourd.gourdState == Locked`) ne peut broadcast que trouver des gourds, jamais une big key — c'est ce qui rendait F4 (`DebugItemSimulator`) systématiquement aveugle aux big keys en test. **Fix** : nouveau lookup `DebugGourdLookup.FindNearestUncollectedProp`, basé sur `Prop.allProps` (registre statique qui couvre gourds ET big keys) et sur le signal canonique "`SaveManager` n'a pas encore de valeur pour ce `saveablePropName`" — le même signal que `CheckTracker`/`ItemApplier` utilisent déjà, plutôt que sur `gourdState` qui n'existe pas pour tous les props. `DebugItemSimulator` utilise maintenant ce lookup (il n'avait de toute façon jamais eu besoin de `RewardGourd`, seul `saveablePropName` lui sert). `DebugGourdUnlocker`/F3 reste basé sur `RewardGourd` pour l'instant (il simule un vrai déblocage local via `ServerSetGourdState`, un concept qui ne s'applique qu'aux gourds) — non testé sur les big keys, probablement pas pertinent pour elles.

## Idée notée pour plus tard — config serveur AP dans le menu d'hébergement plutôt qu'un fichier BepInEx (2026-09-09)

Quand le vrai client réseau Archipelago sera implémenté, il faudra une config (adresse serveur, slot name, password) — piste évidente : `BepInEx` config (`.cfg`, pattern déjà utilisé partout dans le mod, cf. `Config.cs`), mais oblige l'hôte à éditer un fichier texte hors du jeu.

Idée du joueur : gérer plutôt ça dans l'écran in-game d'hébergement de partie, qui a déjà un flow nom/mot de passe (vu en jeu : `SetGameName`, "password is correct", cf. logs de session). Si "slot name"/"password" AP recoupent sémantiquement ce que le joueur saisit déjà pour héberger, possible de réutiliser ces champs plutôt que d'ajouter un écran dédié — resterait à caser l'adresse serveur AP quelque part. Nécessiterait d'injecter une vraie UI Unity (pas juste des hotkeys/logs comme le reste du mod aujourd'hui) ou d'étendre `WorldMenuManager` — un chantier à part entière, pas encore commencé, pas encore investigué en détail (quels champs existent précisément sur cet écran, où les intercepter).

À rouvrir une fois le client réseau lui-même fonctionnel.

## Contexte technique

- **Jeu** : Big Walk (House House / Panic), sorti le 4 août 2026
- **Moteur** : Unity, backend **IL2CPP** (pas Mono)
- **Réseau multijoueur** : **Mirror** (`NetworkBehaviour`, `[SyncVar]`, `[Server]`, `[Client]`)
- **Modèle d'autorité** : le **host est la source de vérité** pour la sauvegarde et l'état de jeu (confirmé par la FAQ officielle et par le code : toute écriture de sauvegarde passe par des méthodes `[Server]`)

## Outils utilisés

| Outil | Rôle |
|---|---|
| [Il2CppInspectorRedux](https://github.com/LukeFZ/Il2CppInspectorRedux) | Extraction de `GameAssembly.dll` + `global-metadata.dat` → C# prototypes (structure) + métadonnées Ghidra |
| Ghidra 11.3+ (mode **PyGhidra**, via `support/pyghidraRun.bat`) | Désassemblage/décompilation du binaire natif IL2CPP, avec import du header `il2cpp.h` (**profil C vide**, option `-D_GHIDRA_`) |

**Piège rencontré** : le script `il2cpp.py` plante sur la phase *"Processing constructed generic methods"* (nom de namespace obfusqué invalide pour Ghidra). Sans importance pour nos besoins : la phase précédente (*"Processing method definitions"*, les méthodes non-génériques — l'essentiel du gameplay) se termine avec succès **avant** le crash, et les labels sont déjà appliqués à ce stade.

**Mise à jour (2026-09-03)** — deux pièges supplémentaires rencontrés en rejouant ce workflow en PyGhidra **headless** (pas la console interactive) :
1. Malgré la phrase ci-dessus, le projet `BW_metadata_ghidra/Big Walk` fourni n'avait **en fait jamais eu ses labels appliqués/persistés** (144k fonctions génériques `FUN_xxxxxxxx` constaté au début de cette session) — la note originale décrivait l'intention/un run antérieur non sauvegardé, pas l'état réel du projet sur disque.
2. `analyzeHeadless.bat -postScript foo.py` échoue sur tout script marqué `# @runtime PyGhidra` (comme `il2cpp.py`) avec *"Ghidra was not started with PyGhidra. Python is not available"* — il faut passer par le package pip `pyghidra` (venv à `%APPDATA%\ghidra\ghidra_<version>\venv`, déjà installé sur cette machine) et sa fonction `pyghidra.run_script(...)`/exécution manuelle, pas `analyzeHeadless.bat` classique.
3. Dans ce mode d'exécution "synthétique" (projet ouvert via `pyghidra.core._setup_project`/`_setup_script`, pas un vrai script lancé depuis le gestionnaire de scripts Ghidra), `getSourceFile()` retourne `None` — `il2cpp.py` plante silencieusement dans `get_script_directory()` (`AttributeError` sur `.getParentFile()`) en essayant de localiser `il2cpp.json` à côté de lui-même. **Aucune trace de cette erreur n'apparaît dans les logs** (le wrapper `PyGhidraScript.run()` avale l'exception et l'imprime via un `PrintWriter` Java qui ne semble jamais flush avant la fin du process) — seul un `exec()` Python direct avec capture d'exception native révèle le vrai traceback. Fix minimal : patcher `get_script_directory()` pour retourner le chemin en dur plutôt que de dépendre de `getSourceFile()`, sans toucher à la logique de labellisation elle-même.

Une fois ces deux pièges contournés, l'import du header (92 Mo, 2,3M lignes) a pris **~99s**, et le run complet du script (jusqu'au crash attendu sur les méthodes génériques) **~136s** — largement plus rapide que redouté (on avait initialement estimé 30-60+ min et mis cette investigation de côté pour cette raison). Résultat : **96 933 fonctions nommées** (contre 98 avant, qui étaient juste les exports natifs du PE).

## Chaîne complète : de l'action du joueur à la sauvegarde disque

```
Joueur pin/dépose un gourd
        │
        ▼
RewardGourd.ServerSetGourdState(GourdState)   ← [Server], point d'entrée réseau autoritatif
        │  (met à jour la SyncVar + invoque le hook localement côté serveur aussi)
        ▼
OnChangeGourdState(old, new)                  ← hook Mirror (SyncVar), tourne sur serveur ET clients
        │  → invoque GourdMap.refreshFlag(saveablePropName, newState)  [PUREMENT VISUEL : met à jour le petit drapeau sur la carte]
        │
        ▼ (chemin séparé, dans ServerSetGourdState)
Prop.SetSaveType(Always | Never)              ← Always si Loose/Stashed, Never sinon
        │
        ▼
Prop.SavePropHome(propHome, isPinned)
        │  key   = SaveablePropName.ToString()   (ex: "gourdCabinFever")
        │  value = propHome.saveableHomeName si isPinned, sinon 0
        ▼
SaveManager.SetIntValue(key, value)           ← ÉCRITURE PERSISTANTE (statique, wrapper de SaveData)
        │
        ▼
SaveData.entries : List<SaveEntry>            ← paires clé(string)/valeur(int), scan LINÉAIRE (pas de dictionnaire)
```

**Lecture** (au chargement) : `SaveManager.GetIntValue(key)` → `SaveData.GetIntValue()` fait le même scan linéaire en sens inverse.

**Correction (2026-09-07, décompilation typée de `RewardGourd.ServerSetGourdState` lui-même — jamais vérifié avant, le schéma ci-dessus datait d'une hypothèse de la toute première session)** :
- `ServerSetGourdState` n'agit **pas** sur `this.prop`, mais itère `propsToMakeSavable` (un `PropBlock[]`, chaque `PropBlock` contenant ses propres `Prop[]`) et appelle `Prop.SetSaveType(Always)` sur chacun si `newGourdState` est `Loose` ou `Stashed`.
- `Prop.SetSaveType(Always)` n'appelle `Prop.SavePropHome` (donc `SaveManager.SetIntValue`) **que si le prop est déjà épinglé à un `PropHome` valide** (`propHomeShellReference` résolu). Juste sorti de l'étau, un prop n'est pinné nulle part — donc `ServerSetGourdState(Loose)` seul, **sans action complémentaire, n'écrit rien dans la sauvegarde**.
- Ça confirme et explique le "3ᵉ chemin d'écriture" trouvé le 2026-09-03 : c'est `PeckEffectSavableHome.Peck()` (le "vice launch switch") qui fait le vrai travail en appelant `Prop.ServerSetPinned(panierDeRepli)` juste après la résolution — `ServerSetGourdState` seul seul ne suffit jamais à persister, dans le jeu normal comme dans un appel forcé.
- **Conséquence pour le mod** : `Plugin.DebugGourdUnlocker` (déblocage de debug, F3) appelait initialement juste `ServerSetGourdState(Loose)` — ça déclenchait bien le check (via `GourdStatePatch`) mais ne survivait pas à un rechargement (confirmé en jeu : le gourd réapparaissait dans son étau). Fix : appeler aussi `Prop.ServerSetPinned(prop.startHome)` juste après, pour simuler l'épinglage que fait `PeckEffectSavableHome` en vrai.

**RÉSOLU (2026-09-03, décompilation Ghidra typée)** — le point exact où l'état est relu au chargement pour repositionner un `Prop`/`RewardGourd` : **`Prop.Start()`**. Unity appelle `Start()` une fois par instance quand son GameObject devient actif dans la scène chargée — c'est un mécanisme **par-prop**, pas un balayage global fait par un manager. Logique (côté serveur/host uniquement, `netIdentity.isServer` vérifié en premier) :

```
Prop.Start()
  │  (uniquement si isServer == true)
  ▼
key = saveablePropName.ToString()  (ou savablePropGuid si canSaveHomeWithGuid)
  ▼
savedValue = SaveManager.GetIntValue(key, 0, false)
  │
  ├─ savedValue == 0 → home = startHome (position par défaut du prefab)
  │
  └─ savedValue != 0 → home = <PropHome trouvé en itérant le registre statique
  │                              des PropHome, celui dont .saveableHomeName == savedValue>
  ▼
Prop.ServerSetPinned(home)        ← épingle réellement le prop à ce home
  │  (dé-épingle l'ancien home s'il y en a un, met à jour NetworkpropHomeShellReference,
  │   déclenche home.onPin / home.onPinServer / home.onChangeServer)
  ▼
Prop.LocallySetPinned(home, ...)  ← appelé en aval, réécrit SaveManager.SetIntValue(key, home.saveableHomeName)
                                      (donc idempotent : re-pin un home déjà sauvegardé ré-écrit la même valeur)
```

`Prop.SavePropHome(propHome, isPinned)` (déjà documenté ci-dessus) est l'écriture "pure" : `SetIntValue(key, isPinned ? home.saveableHomeName : 0)`, appelée notamment depuis `Prop.SetSaveType` quand le type de sauvegarde change.

**Conséquence directe pour la réception d'item Archipelago** (remplace la recommandation n°2 plus bas, désormais validée plutôt qu'hypothétique) :
- **Cas zone déjà chargée** : appeler `SaveManager.SetIntValue(key, homeValue)` (persistance) **et** retrouver l'instance `Prop` vivante (via le registre `PropHome`/`GourdMap.GetFlag`) pour appeler `Prop.ServerSetPinned(propHome)` directement — ça réplique exactement ce que fait `Prop.Start()`, donc pas besoin de connaître d'API cachée.
- **Cas zone PAS chargée** : il suffit d'écrire `SaveManager.SetIntValue(key, homeValue)` **et rien d'autre** — la prochaine fois que cette zone se charge, `Prop.Start()` du prop concerné lira cette valeur tout seul et s'auto-épinglera. **Pas besoin de garder de référence en mémoire, pas besoin de logique de "rattrapage au chargement de zone" côté mod** : le jeu le fait déjà nativement. Ça répond complètement à la préoccupation "on ne peut pas garder de référence après un quit".

**Question de conception — où matérialiser un item reçu ?** (**TRANCHÉ le 2026-09-07**, après investigation approfondie ; discussion initiale du 2026-09-03 ci-dessous conservée pour trace)

Deux pistes envisagées puis écartées avant de trouver la bonne :
- **Gourd générique spawnée au hub, portée à n'importe quel valet libre** : écartée. `SaveableHomeName` a exactement 141 entrées, **toutes** dédiées à un puzzle/monoument/big-key précis (confirmé par dump réflexion complet de l'enum) — aucun slot `valetXxx` générique/libre n'existe. Une gourd générique n'a donc physiquement nulle part où se pinner sans entrer en collision avec le slot d'une vraie énigme (`PropHome.pinnedProp` est un champ singulier — un seul prop par home à la fois). De plus, la progression du jeu semble comptée par home rempli (`GourdPinAudioBehaviour.GetNumberOfFilledHomes()`), donc une gourd générique non routée vers son vrai `valetXxx` ne ferait pas avancer la progression vanilla.
- **Item nommé 1:1, pin live immédiat via `Prop.ServerSetPinned`** : écartée aussi, pour une raison différente — ça déplacerait physiquement l'objet vivant hors de son étau (repositionnement fait par `LocallySetPinned`) alors que `gourdState` resterait `Locked` (on n'y touche pas), créant un état visuel incohérent, et surtout ça réutilise le **même objet de scène** que celui qui doit rester disponible pour que le joueur déclenche le vrai check plus tard.

**Design retenu : item nommé 1:1, écriture `SaveManager` pure, sans jamais toucher l'objet de scène vivant.**

```
Réception d'un item Archipelago "gourdX"
        │
        ▼
SaveManager.SetIntValue("gourdX", valetX)   ← écriture directe, AUCUN appel à ServerSetPinned,
                                               AUCUNE interaction avec le RewardGourd/Prop vivant
```

Pourquoi ça résout les deux problèmes en même temps :
- **L'énigme reste intacte et solvable** : l'objet de scène (étau + gourd) n'est jamais touché par la réception d'item, donc le check local (`GourdStatePatch` sur `Loose`) reste toujours déclenchable normalement par le joueur, peu importe si l'item a déjà été reçu ou non.
- **La progression reste correcte, sans hack** : au **prochain chargement** de la scène concernée, `Prop.Start()` (mécanisme de restauration déjà validé, cf. section "RÉSOLU" plus haut) lit cette valeur et fait le vrai pin (position, `PropHome.pinnedProp`, déclenchement des events `onPin`/`onPinServer`) exactement comme pour une résolution normale d'une session précédente — aucun besoin de bidouiller `ProgressTracker`/`EndingTransition`, le système de progression natif du jeu voit un état parfaitement cohérent.
- **Idempotent en cas de résolution physique réelle, avant ou après réception de l'item** : le vrai dépôt (`PeckEffectSavableHome.Peck()` → `Prop.ServerSetPinned`) écrit la **même clé avec la même valeur** (même gourd, même cible 1:1) — donc peu importe l'ordre des deux événements, le résultat final converge vers le même état persistant correct, avec en plus l'état vivant (`PropHome.pinnedProp`) mis à jour immédiatement par l'action réelle du joueur.

**Compromis assumé** : pas de mise à jour visuelle instantanée si le joueur est déjà dans la scène de l'énigme au moment où l'item arrive — il faut quitter/recharger la scène pour que l'étau se vide sous ses yeux. Acceptable et courant dans les clients Archipelago ("s'applique au prochain chargement").

**Pistes explorées et écartées en cours de route** (gardées en mémoire au cas où un futur besoin les rendrait pertinentes) :
- `PropInventory`/`InventoryZone`/`SaveData.inventory` (`List<string>` de GUIDs, séparée de `entries`) : vrai système générique de "prop porté sans home fixe", avec zones de déclenchement physiques (`InventoryZone.OnPropEnterZone`). Utile pour des objets sans slot dédié, mais ne déclenche a priori pas la progression vanilla (pas de lien trouvé avec `GetNumberOfFilledHomes`) — pas retenu ici car les gourds Archipelago sont nommés 1:1 et ont bien un `valetXxx` cible.
- `InventorySpawn.GetNextSpawnPosition()` : point d'ancrage avec dispersion aléatoire dans un rayon (`spawnRadius`) — probablement le point de réapparition physique des props "en inventaire" au chargement de scène. Pertinent si un jour on doit vraiment faire apparaître un objet physique nouveau (pas notre cas ici, on réutilise l'objet d'énigme existant).
- `ProgressTracker.currentValue` (public get/set, pas de `NetworkBehaviour` donc pas de garde Mirror) : modifiable directement, mais s'est avéré être un composant générique réutilisé par des mini-énigmes (`PressInOrder`), pas un compteur global de fin de jeu — donc pas le bon levier pour "tricher" la condition de victoire globale. La vraie condition de fin reste à identifier précisément (candidats : les 9 slots `monoumentFinalSlot0-8`, `EndingTransition`/`EndingFadeBlind`) — non nécessaire avec le design retenu, qui laisse la progression native faire son travail normalement.

<details>
<summary>Discussion initiale du 2026-09-03 (dépassée, conservée pour trace)</summary>

la communauté Archipelago semble s'accorder sur l'idée de faire apparaître l'item reçu **au hub des joueurs** plutôt qu'à son `PropHome` d'origine (celui lié au puzzle). Pistes à vérifier avant de trancher/implémenter :
- Le comportement déjà observé "gourd porté en main → renvoyé à la base des joueurs à la déconnexion" (cf. section suivante) suggère qu'un système de hub/zone de dépôt existe déjà nativement dans le jeu — probablement plus approprié à réutiliser qu'à réinventer un point de spawn maison.
- `SaveManager.SetInventory()` / `SaveData.inventory : List<string>` est une liste **séparée** du dictionnaire clé/valeur `SaveData.entries` — piste à vérifier : c'est peut-être le vrai mécanisme de "objet porté/en possession du joueur" (plutôt que `PropHome`), ce qui changerait l'approche pour matérialiser un item reçu.
- Persistance tant que non-stashé : `Prop.Start()` relit `SaveManager` et repin le prop à **chaque** chargement (pas seulement au moment de l'octroi), donc tant que l'entrée de sauvegarde reste non-nulle — peu importe le home exact (hub, couffin, ou position finale) — l'item ne se perd jamais entre deux sessions, même si le joueur ne l'a pas "rangé" définitivement. Le détail précis de "où il atterrit exactement s'il n'est pas stashé" (logique de fallback à la déconnexion/rechargement) reste non identifié précisément en code — probablement une logique séparée de `Prop.Start()`, à creuser via Ghidra le jour où ce point de conception devra être tranché.

</details>

**Mise à jour (session de test en jeu du 2026-09-03)** — comportement du "panier" de secours pour un gourd loose, confirmé par observation + explication utilisateur (pas de la rétro-ingénierie statique) :
- Résoudre une énigme (peck qui libère le gourd de l'étau) déclenche `PeckEffectSavableHome.Peck()` sur le GameObject "vice launch switch" associé, qui appelle `Prop.SavePropHome` **directement**, sans passer par `RewardGourd.ServerSetGourdState`. Log Unity observé : `GourdViceLaunchSwitch (PeckEffectSavableHome) has saved prop gourdTellerWindow with new home valetTellerWindow`.
- Ce home (`valetTellerWindow` dans l'exemple) est un point de repli proche de l'énigme — pas la position finale/trophée — créé spécifiquement pour la reconnexion : si personne ne le récupère, le gourd loose doit pouvoir être retrouvé plutôt que perdu au rechargement.
- Trois cas à la déconnexion (comportement observé, pas encore vérifié en code) :
  1. Gourd porté en main par un joueur → renvoyé à la base des joueurs (hub).
  2. Gourd abandonné dans le monde (posé, pas porté) → renvoyé au panier proche de l'énigme d'origine.
  3. Gourd déjà stashé à son home final → reste en place (comportement normal).
- Conséquence pour le mod : `PeckEffectSavableHome` est donc un **troisième chemin d'écriture** vers `SaveManager.SetIntValue`, distinct de `RewardGourd.ServerSetGourdState` (`Loose`/`Stashed`), ce qui confirme le besoin du filet de sécurité générique déjà recommandé plus bas (`SaveValuePatch`, implémenté côté mod). Il répond aussi en partie au "non résolu" ci-dessus : ce système de panier de repli est probablement lié au mécanisme de restauration au chargement, bien que la fonction exacte de relecture au démarrage reste non identifiée.

## Classes et structures clés

### `SaveablePropName` (enum) — la table de locations/items

64 identifiants uniques hors valeurs de test (`gourdTesting00-39`) :
- **57 gourds** (`gourdCabinFever`, `gourdHighButton`, `gourdFielding`, ... `gourdPoetAndPontiff`) — un par puzzle/défi nommé du jeu
- **7 big keys** (`bigKeyIntro`, `bigKeyRedZone`, `bigKeyGreenZone`, `bigKeyBlueZone`, `bigKeyYellowZone`, `bigKeyBoss`, `bigKeyOverflow`) — déblocages majeurs par zone
- `notSavable = 0` (valeur nulle)

C'est la table candidate la plus directe pour définir les **locations** Archipelago (voir le fichier `types.cs`/`il2cpp.cs` fourni pour la liste complète avec les valeurs entières).

### `GourdFlag.GourdState` (enum)
```csharp
Locked = 0
Loose  = 1
Stashed = 2
Hidden  = 4
```

### `RewardGourd : NetworkBehaviour`
- `gourdState` : SyncVar Mirror, hook `OnChangeGourdState`
- `ServerSetGourdState(GourdState)` : `[Server]`, seul point d'écriture autorisé de l'état
- `prop` : référence vers le `Prop` physique associé
- `propsToMakeSavable` : props annexes dont le `SaveType` suit celui du gourd principal

### `GourdMap : MonoBehaviour`
- Registre central de tous les `GourdFlag` actifs dans la zone chargée (`List<GourdFlag> flags`)
- `static Action<SaveablePropName, GourdFlag.GourdState> refreshFlag` — event statique, bon point d'accroche pour observer les changements d'état en live sans avoir à hooker chaque `RewardGourd` individuellement
- `GetFlag(SaveablePropName)` — lookup direct par identifiant

### `Prop : NetworkBehaviour`
- `SetSaveType(PropSaveType)` : déclenche `SavePropHome` si le type change et qu'un `PropHome` valide existe
- `SavePropHome(PropHome, bool isPinned)` : écrit dans `SaveManager` (voir chaîne ci-dessus)
- `saveablePropName`, `savablePropGuid`, `canSaveHomeWithGuid` : deux systèmes d'identification possibles (par enum nommé, ou par GUID pour les props non-nommés/dynamiques)

### `SaveManager` (statique)
```csharp
static SaveData currentData
static void SetIntValue(string key, int value)
static int? GetIntValue(string key)
static void SetStringValue(string key, string value)
static string GetStringValue(string key)
static void SetInventory()
static bool GetIsInInventory(string savablePropGuid)
```

### `SaveData`
```csharp
List<SaveEntry> entries          // paires clé/valeur int, scan linéaire
List<SaveEntryString> stringEntries
List<string> inventory           // items d'inventaire portés/transportables
```

### `PropHome : MonoBehaviour`
- Système de "position de rangement" pour les objets (différent du système de récompense — sert à mémoriser où un objet a été laissé)
- `saveableHomeName` (enum `SaveableHomeName`, 141 valeurs) : identifiant de slot, incluant les défis `valetXxx` correspondant en fait aux **mêmes puzzles** que les `gourdXxx` de `SaveablePropName` (les deux enums utilisent des noms parallèles pour deux aspects différents — le gourd-récompense vs le slot où le loger)
- `onPinServer`, `onChangeServer` : events déclenchés côté serveur lors du placement/retrait d'un prop

## Recommandations pour le hook Archipelago

1. **Détection des checks** : hook Harmony **postfix** sur `SaveManager.SetIntValue(string key, int value)`, filtré sur les clés préfixées `gourd`/`bigKey` et `value != 0`. Un seul point d'interception générique au lieu de hooker 57+ instances individuelles de `RewardGourd`.

2. **Envoi d'un item à distance** (débloquer un gourd envoyé par un autre joueur) — **tranché le 2026-09-07** (voir "Design retenu" ci-dessus), remplace l'hypothèse initiale de `ServerSetGourdState(Stashed)` **et** la version précédente de cette recommandation (qui suggérait à tort un pin live en zone chargée) :
   - **Toujours** : `SaveManager.SetIntValue(key, homeValue)` seul, quelle que soit la zone chargée ou non — **jamais** de `Prop.ServerSetPinned` direct sur l'instance vivante. Un pin live déplacerait physiquement l'objet hors de son étau sans que `gourdState` suive (incohérence visuelle) et rendrait le check local associé impossible à redéclencher.
   - `Prop.Start()` du prop concerné lira la valeur et s'auto-épinglera tout seul à son prochain chargement de la scène — aucun code de mod supplémentaire nécessaire. Compromis assumé : pas d'effet instantané si le joueur est déjà dans la zone au moment de la réception.

3. **Respect du modèle d'autorité host** : toute écriture doit se faire **côté host** (les méthodes `[Server]` le garantissent déjà — un hook côté client sur un client non-host serait ignoré ou risquerait une désync). Le mod Archipelago devra tourner côté host, ou transmettre ses ordres au host via un canal séparé si l'architecture du mod le sépare du process de jeu.

3bis. **Faire disparaître le gourd physique après un check** (validé en jeu le 2026-09-03, solo) : `RewardGourd.ServerSetGourdState(GourdState.Hidden)` seul ne fait que rafraîchir l'icône sur la carte (`GourdMap.refreshFlag`) — le modèle 3D reste visible et ramassable. `GameObject.SetActive(false)` seul serait purement local et désyncerait les autres clients en co-op (Mirror ne réplique pas ça). La combinaison qui marche : `Mirror.NetworkServer.UnSpawn(gameObject)` (retire l'objet réseau de la vue de tous les clients sans le détruire définitivement, contrairement à `NetworkServer.Destroy`) **puis** `gameObject.SetActive(false)` localement côté host.

   **Persistance confirmée** (même session de test, relance complète du jeu après coup) : après un redémarrage complet, le gourd ne réapparaît **pas** au panier/couffin proche de l'énigme (couffin visible mais vide) — l'état "déjà collecté" (`SaveManager` a bien écrit `gourdTellerWindow`/`gourdHighButton` avec une valeur non nulle) survit donc à un rechargement complet, sans code supplémentaire côté mod. Ça suggère que la logique de restauration au chargement (toujours pas identifiée précisément, cf. "non résolu" plus haut) respecte déjà l'état persistant et ne réinstancie pas un `RewardGourd` déjà marqué comme récupéré — bonne nouvelle pour la fidélité d'un futur rechargement de session Archipelago. Pas encore testé : reconnexion en cours de session co-op (seulement redémarrage complet testé), et le cas d'un gourd qui aurait été cette fois réellement stashé.

4. **RÉSOLU (2026-09-07)** — anciennement "prochaine piste de recherche" : la restauration d'état au chargement redéclenchait bien un check déjà validé. Deux découvertes en implémentant la réception d'item (`Core/ItemApplier.cs`) :
   - `RewardGourd.ServerSetGourdState(Loose)` est **réellement rappelé** (pas seulement `Prop.Start()`) au chargement d'une zone pour un gourd déjà résolu — confirmé en jeu par un flag de diagnostic temporaire. Une vraie restauration légitime de l'état du gourd, pas une re-résolution par le joueur.
   - `CheckTracker` (dédoublonnage partagé entre `GourdStatePatch` et `SaveValuePatch`) était un simple `HashSet` **en mémoire**, vide à chaque redémarrage du process — donc incapable d'empêcher ce rappel de re-déclencher un check-report à **chaque** lancement du jeu, pour **n'importe quel** gourd déjà résolu (bug général du système de détection, pas spécifique à la réception d'item — juste jamais remarqué avant faute d'avoir testé un redémarrage avec un check déjà acquis en historique de log).
   - **Fix** : `CheckTracker.TryMarkReported` persiste maintenant dans `SaveManager` (clé `ap_reported_<locationId>`, valeur `1`) au lieu d'un `HashSet`. Testé en jeu : le check ne se redéclenche plus après un second redémarrage. Effet de bord positif : ça explique aussi pourquoi le gourd ne réapparaît jamais visuellement dans son couffin après restauration — `GourdStatePatch` exécute sa partie "cacher l'objet" (`UnSpawn`+`SetActive(false)`) à chaque fois que `Loose` est rappelé, y compris pendant la restauration ; seul le *report* du check est dédoublonné, pas le masquage. Cohérent avec l'observation du 2026-09-03 ("couffin visible mais vide").
   - Piste écartée en cours de route : comparer l'ancienne valeur à la nouvelle dans `SaveValuePatch` (`SaveManager.GetIntValue(key, 0, false)` avant l'écriture) — logiquement correcte mais insuffisante seule, puisque le vrai déclencheur du check fantôme passait aussi par `GourdStatePatch` (pas seulement `SaveValuePatch`). La leçon : dédoublonner au niveau central (`CheckTracker`) plutôt que dans chaque patch individuellement.

## Réception d'un item Archipelago (implémenté le 2026-09-07)

Voir "Design retenu" plus haut pour le raisonnement. Implémentation : `Core/ItemApplier.cs` (`ApplyGourdItem(SaveablePropName)`), simulable en jeu via le hotkey de debug F4 (`Debug/DebugItemSimulator.cs`), sans connexion Archipelago réelle.

Cycle de test complet validé en jeu :
1. F4 sur un gourd verrouillé → écriture `SaveManager` directe, aucun check-report immédiat (`CheckTracker` pré-marqué avant l'écriture), objet de scène intact.
2. Redémarrage complet → `Prop.Start()` repin le prop pour de vrai, `SaveManager` confirmé en sauvegarde (`.sav` inspecté directement).
3. Tentative de résolution physique du même gourd après coup → étau déjà ouvert (le prop a été relogé, plus rien à résoudre), aucun check en double.
4. Second redémarrage → toujours aucun check fantôme (fix `CheckTracker` persistant confirmé).

Point encore ouvert (mineur, cosmétique) : le modèle 3D du gourd n'est visible ni dans l'étau ni sur le couffin après restauration — expliqué ci-dessus (masqué par `GourdStatePatch`), cohérent avec le comportement d'un vrai check complété. Pas encore vérifié : un vrai check résolu normalement en jeu (hors item reçu) continue de fonctionner sans régression après ce fix.

## Réception d'un item Archipelago — validation en jeu pour les big keys (2026-09-07)

Cycle complet testé en jeu pour `bigKeyIntro` (après le fix `DebugGourdLookup.FindNearestUncollectedProp`, cf. section big keys plus haut) :
1. F4 devant `bigKeyIntro` (encore non-collectée) → `[ItemApplier] Item appliqué : bigKeyIntro -> bigKeyPlinthIntro.` — écriture `SaveManager` immédiate, aucune interaction avec l'objet vivant.
2. Le pont associé (feature déverrouillée par cette clé) **ne s'ouvre pas immédiatement** — comportement attendu, identique au compromis déjà documenté pour les gourds ("pas de mise à jour visuelle instantanée tant que la zone n'est pas rechargée").
3. Redémarrage complet du jeu → au rechargement, **le pont est bien ouvert**. Confirme que `Prop.Start()` refait le vrai pin (`ServerSetPinned`) pour une big key exactement comme pour un gourd, et que ce pin déclenche le vrai effet de jeu natif (`onPinServer`/`onChangeServer` sur le `PropHome` de la plinthe) sans aucun code supplémentaire côté mod.

Conclusion : le design "écriture `SaveManager` pure" fonctionne à l'identique pour les gourds et les big keys, aucune divergence de comportement — la seule vraie différence entre les deux était l'absence de composant `RewardGourd` sur les big keys (déjà documentée ci-dessus), qui n'affecte que les outils de debug/detection, pas le mécanisme de réception d'item lui-même.

Une clé de couleur a été testée juste après (en combinaison avec l'effet instantané ci-dessous) et le joueur a confirmé que ça fonctionnait ("ça a fonctionné") — mais sans capture de log précisant laquelle ni la paire exacte obtenue, donc **pas une confirmation formelle** au même niveau que `bigKeyIntro`. Non-régression d'un vrai check gourd résolu normalement (hors item reçu) après le fix `FindNearestUncollectedProp` : toujours pas testée.

## Confirmation formelle des 4 derniers mappings big key (2026-09-10)

Partie fraîche (`New Game`), les 4 mappings restants testés au F4 un par un, log confirmé (`BepInEx/LogOutput.log`) pour chacun :

- `bigKeyRedZone -> bigKeyPlinthMapRoom` — ouvre la map room. Effet **immédiat** (pas d'attente de rechargement — cf. `ItemApplier.TryApplyLiveEffect`, pin live pour tout prop sans `RewardGourd`).
- `bigKeyYellowZone -> bigKeyPlinthTunnels` — ouvre le tunnel. Immédiat.
- `bigKeyBoss -> bigKeyPlinthEnding` (la clé "noire", tour **Black Monolith** dans le doc tiers) — ouvre la zone de fin. Immédiat.
- `bigKeyOverflow -> bigKeyPlinthGoodbye2` — ouvre la zone de **vraie fin**. Immédiat.

Les 7 mappings big key sont donc maintenant tous confirmés formellement en jeu (`bigKeyIntro`, `bigKeyGreenZone`, `bigKeyBlueZone` précédemment + ces 4). Table complète dans `GourdRegistry.BigKeyHomesByProp`, inchangée.

**Non-régression confirmée** : save & quit après les 4 tests, relance du jeu → les 7 clés/portes déjà ouvertes le sont toujours (aucune régression, `Prop.Start()` restaure correctement l'état persistant à froid).

Deux observations notées par le joueur pendant ce test, reportées dans la section "À implémenter côté mod" tout en haut de ce document : idée de coloration cosmétique des clés par tour, et une sphère noire bloquant physiquement l'accès à la vraie fin (conditionnée à une première complétion du jeu, mécanisme non identifié en code).

**RÉSOLU (2026-09-09)** — `bigKeyGreenZone→bigKeyPlinthSkiLift` (téléphérique) et `bigKeyBlueZone→bigKeyPlinthTrain` (train) confirmés individuellement en jeu, chacun via une touche de debug dédiée (F7/F8, cf. `Config.cs`/`DebugHotkeys.cs`, plus besoin de passer par `FindNearestUncollectedProp` donc testable sans être physiquement devant l'objet). Effet instantané (`TryApplyLiveEffect`) validé pour les deux : le joueur confirme que le téléphérique et le train se sont activés immédiatement après F7/F8, sans reload. Détail observé en cours de test : la clé disparaît visuellement du socle du monument au moment du pin en direct — attendu, pas un bug (l'ordre d'exécution dans `ItemApplier.ApplyGourdItem` reporte déjà le check *avant* que `TryApplyLiveEffect` ne déplace l'objet vers sa plinthe de dépôt ; la disparition du socle est juste la conséquence visuelle du `ServerSetPinned` vers la plinthe, pas une perte d'état). Avec `bigKeyIntro` (2026-09-07), les 3 mappings couleur/plinthe testés à ce jour sont donc formellement confirmés ; les 4 restants (`bigKeyRedZone`, `bigKeyYellowZone`, `bigKeyBoss`, `bigKeyOverflow`) n'ont qu'une confirmation indirecte (cross-validation du document tiers, cf. section plus bas) — à tester individuellement si besoin d'une confirmation en jeu complète.

**Effet instantané pour les props sans `RewardGourd` (2026-09-07)** — suite à un test en jeu où le pont associé à `bigKeyIntro` ne s'ouvrait pas tant que la zone n'était pas rechargée (comportement "compromis assumé" ci-dessus), question posée : pour un gourd, ne jamais pinner en direct se justifie par deux raisons (désync `GourdState`/icône carte, préserver le puzzle local rejouable) — aucune des deux ne s'applique à une big key (pas de `GourdState` du tout, pas de puzzle répétable, cf. découverte ci-dessus). `ItemApplier.TryApplyLiveEffect` appelle donc `Prop.ServerSetPinned` en direct, mais **uniquement si le prop ciblé n'a pas de composant `RewardGourd`** ET que la zone est déjà chargée (sinon, comportement inchangé : `Prop.Start()` prendra le relais au prochain chargement). Validé en jeu : le pont s'ouvre immédiatement, sans reload, pour une big key.

## Point ouvert — la logique location/item mérite d'être repensée (2026-09-07)

En testant l'effet instantané ci-dessus, question soulevée par le joueur : est-ce que le fait d'appliquer un item annule la possibilité d'utiliser la même entrée comme location Archipelago ? Réponse creusée pendant la session, et le problème s'est avéré plus profond qu'anticipé — noté ici tel quel, **pas résolu**, juste documenté pour y revenir avec plus de recul.

**Le vrai bug trouvé (corrigé)** : `ItemApplier` marquait la location comme "déjà reportée" dans `CheckTracker` **sans jamais appeler `Plugin.Reporter.ReportCheck`**. Donc si l'item d'une location arrivait par Archipelago avant que le joueur ait lui-même résolu cette location en jeu, le check n'était **jamais reporté nulle part** — ni immédiatement, ni plus tard (`CheckTracker` bloque tout report futur pour cette clé). Concrètement : l'item randomisé qui *devrait* partir de cette location vers qui l'attend dans le multiworld ne partait jamais. Fix appliqué : `ItemApplier` appelle maintenant `Plugin.Reporter.ReportCheck` lui-même si c'est lui qui marque la location en premier (même pattern que `GourdStatePatch`/`SaveValuePatch` : premier arrivé — résolution réelle ou item reçu — reporte le check une fois).

**La question de fond, non résolue** : `GourdRegistry` utilise aujourd'hui **le même identifiant** (`propName.ToString()`, ex. `"bigKeyRedZone"`) à la fois comme id de **location** (ce que le joueur accomplit dans son propre monde) et comme id d'**item** (ce que `ItemApplier` matérialise quand il est reçu). En vrai Archipelago multiworld, ce sont deux espaces indépendants : la location "bigKeyRedZone" de ce joueur pourrait contenir n'importe quel item du multiworld (pas forcément "bigKeyRedZone"), et l'item "bigKeyRedZone" reçu par ce joueur peut venir de n'importe quelle location, chez n'importe qui. Les fusionner comme c'est fait actuellement n'est correct que si le monde Archipelago pour Big Walk est conçu comme un "shuffle sur place" (chaque slot nommé est à la fois sa propre location et son propre item, seul le lien entre les deux est mélangé) plutôt qu'un vrai multiworld à espaces items/locations découplés.

**Pourquoi ce n'est pas juste un choix arbitraire à trancher facilement** : contrainte physique du jeu, déjà bien documentée plus haut — un seul `PropHome`/slot de sauvegarde par `SaveablePropName`, réutilisé à la fois pour détecter la résolution locale ET pour matérialiser un item reçu (aucun mécanisme générique d'inventaire trouvé qui déclenche la vraie logique de déblocage du jeu, cf. section "Design retenu"/pistes écartées). Résultat : une fois un item matérialisé, l'objet physique est consommé (étau vide / clé déjà posée) — le joueur ne peut de toute façon plus jamais "vraiment" résoudre cette location lui-même après coup. Donc découpler complètement location et item (au sens d'un vrai multiworld) demanderait de retrouver un mécanisme de matérialisation qui ne consomme pas le même slot que la détection de check — et cette piste a déjà été explorée et abandonnée (voir "Pistes explorées et écartées" plus haut).

**À rouvrir plus tard** : est-ce que le modèle "chaque slot nommé = sa propre location ET son propre item, seul le lien est randomisé" est acceptable pour ce monde Archipelago, ou faut-il absolument un vrai découplage ? Ça dépend surtout de comment le monde Python (pas encore écrit) définira les locations/items — pas uniquement une question côté mod.

## Régions, tours et modèle locations/items — investigation du 2026-09-07

Suite de la discussion "point ouvert" ci-dessus, déclenchée par la même question. Résumé de tout ce qui a été découvert/discuté ensuite, classé par sujet — rien de tout ça n'est implémenté côté mod pour l'instant (uniquement les checks puzzle et clé existants), c'est de la préparation pour la conception du monde Python.

### Nouveaux outils de debug

Deux hotkeys ajoutées dans `Debug/DebugGourdLookup.cs` (actives seulement si `Debug.Enabled`) :
- **F5** (`DumpNearbyKey`) : logue les `Prop` savables les plus proches du joueur (nom, `gourdState` si un `RewardGourd` existe, `déjà sauvegardé`, actif/inactif, distance). Basé sur `Prop.allProps`, pas `RewardGourd`, pour ne rien manquer (cf. découverte big keys plus haut).
- **F6** (`DumpMonumentHomesKey`) : logue tous les `PropHome` réellement enregistrés (`PropHome.allPropHomes`) dont le nom contient "monoument", avec leur état de remplissage (`pinnedProp`).

### Bug trouvé : incohérence de nommage gourd/valet (non corrigé)

En comparant un par un les 58 `gourdXxx` (`SaveablePropName`) et les 58 `valetXxx` (`SaveableHomeName`), 3 paires ont une orthographe **incohérente entre les deux enums** — une faute de frappe du jeu lui-même, pas du mod :

| `SaveablePropName` | `valet` attendu par substitution naïve | `SaveableHomeName` réel |
|---|---|---|
| `gourdCenturonSong` | `valetCenturonSong` | `valetCenturionSong` |
| `gourdSingerAndSelecter` | `valetSingerAndSelecter` | `valetSingerAndSelector` |
| `gourdDancerAndSelecter` | `valetDancerAndSelecter` | `valetDancerAndSelector` |

`GourdRegistry.TryGetHomeName` fait `"valet" + name.Substring("gourd".Length)` puis `Enum.TryParse` — échouait silencieusement pour ces 3 gourds précis (`TryParse` renvoie `false` puisque le nom généré n'existe pas), donc `ItemApplier.ApplyGourdItem` retournait `false`/item ignoré pour eux uniquement.

**CORRIGÉ (2026-09-09)** : ajout de `GourdRegistry.GourdHomeNameExceptions` (même principe que `BigKeyHomesByProp`, vérifié avant la substitution naïve `"gourd"→"valet"`) pour les 3 paires. Pas encore retesté en jeu — à valider à l'occasion (F4 sur un de ces 3 gourds devrait maintenant appliquer l'item au lieu de logguer "Aucun home connu").

### Liste complète des 58 valets (`SaveableHomeName`)

```
valetCabinFever, valetHighButton, valetFielding, valetCannonBall, valetInvisibleInk,
valetTrapRoom, valetMediumSimPress, valetEasySimPress, valetRingRoom, valetBunker,
valetHighPegBoard, valetFirstPegBoard, valetObby, valetCarousel, valetCoordinates,
valetTelescopeToBox, valetObservationRoom, valetWindowLabyrinth, valetMagiciansTrick,
valetButtonBoothChallenge, valetBasketball, valetConcert, valetIndoorSemaphore,
valetOpticalTelegraph, valetPoetAndPreist, valetTileSoup, valetMemoryBombs,
valetPanopticon, valetMaypole, valetBlindfoldCircus, valetMessengerRun, valetHotPotato,
valetTileThief, valetCharadesRooms, valetMicrophoneArray, valetPointersParadise,
valetCoordinatesHolding, valetEggHunt, valetTellerWindow, valetSignalFlags,
valetCabinFeverLong, valetBreadcrumbLoop, valetScoutBombs, valetScoutTiles,
valetScoutCounting, valetCenturionSong, valetMusicalHoliday, valetKickUpPits,
valetSingerAndSelector, valetDancerAndSelector, valetSpeedObby, valetBlindfoldCatwalk,
valetBlindfoldFishtrap, valetPerspectiveCounting, valetCenturionSeance, valetFlareRun,
valetCannonballCommute, valetPoetAndPontiff
```

### Étau vs valet vs monument — clarification du cycle de vie d'un gourd

Point de confusion levé cette session (une hypothèse formulée en cours de discussion — "le pin à un valet nécessite deux joueurs" — était fausse, corrigée en recroisant avec les notes du 2026-09-03) :

1. **Étau** : le mécanisme qui bloque le gourd tant que le puzzle n'est pas résolu. Pas de `SaveableHomeName` dédié — c'est `Prop.startHome`, la position par défaut du prefab (`SaveManager` = 0/absent tant qu'on est dedans).
2. **Valet** (`valetXxx`) : confirmé par le log du 2026-09-03 (`PeckEffectSavableHome.Peck()` → "has saved prop gourdTellerWindow with new home valetTellerWindow") — un point de repli écrit **automatiquement, au moment même où le puzzle est résolu** (même action que la transition `Loose`, pas une étape séparée). Sert de filet de sécurité si personne ne récupère le gourd avant un reload. **Ce n'est pas l'étape à deux joueurs.**
3. **Monument** (`monoumentXSlotY`) : hypothèse — non vérifiée directement en jeu comme pour `valetTellerWindow`, mais cohérente avec les noms d'enum et le commentaire déjà présent dans `GourdStatePatch.cs` ("stasher un gourd nécessite deux joueurs, l'un ouvre le slot, l'autre y dépose le gourd") — le vrai emplacement final/trophée visé par `GourdState.Stashed`, un pedestal partagé à plusieurs emplacements génériques (pas liés à un gourd précis), d'où le besoin de coordination à deux joueurs.

Conséquence pour la conception des régions/tours : si `valetXxx` s'écrit automatiquement à chaque résolution de puzzle, un `PropHomeBlock` qui regroupe des `valetXxx` spécifiques se remplirait **simplement en résolvant les bons puzzles**, sans dépendre de l'étape de stash aux monuments (qui serait une progression séparée, genre salle des trophées, pas un pré-requis pour débloquer une tour).

### Monuments réels vs enum déclaré (F6, 2026-09-07)

Vérification en jeu via F6 : le nombre réel de `PropHome` "monoument" enregistrés est **inférieur** au nombre déclaré dans l'enum `SaveableHomeName` pour toutes les catégories — confirmé par le joueur qui se souvenait de "4 pour le tuto, 5 pour les tours" :

| Monument | Slots déclarés (enum) | Slots réels (`PropHome.allPropHomes`) |
|---|---|---|
| `monoumentIntro` | 0–7 (8) | **0–3 (4)** |
| `monoument0` | 0–8 (9) | **0–4 (5)** |
| `monoument1` | 0–8 (9) | **0–4 (5)** |
| `monoument2` | 0–8 (9) | **0–4 (5)** |
| `monoument3` | 0–8 (9) | **0–4 (5)** |
| `monoumentFinal` | 0–8 (9) | **0–5 (6)** |
| `monoumentOverflow` | 0–17 (18) | **0–14 (15)** |

**Leçon générale** (dépasse le cas des monuments) : les enums `SaveableHomeName`/`SaveablePropName` contiennent des valeurs de réserve jamais construites en scène, au-delà des entrées explicitement "Testing" déjà exclues par `GourdRegistry`. Ne jamais déduire une quantité réelle depuis le nombre brut de valeurs d'un enum — seul ce qui est effectivement enregistré en scène (`PropHome.allPropHomes`, `Prop.allProps`, ou `RewardGourd` trouvés via `FindObjectsByType`) fait foi.

**Hypothèse structurelle** (non vérifiée, mais suggestive) : 7 catégories de monuments (`0, 1, 2, 3, Final, Intro, Overflow`) pour 7 catégories de big keys (`RedZone, GreenZone, BlueZone, YellowZone, Boss, Intro, Overflow`) — `monoumentIntro`↔`bigKeyIntro` et `monoumentOverflow`↔`bigKeyOverflow` matchent déjà par le nom ; `monoumentFinal` correspondrait à `bigKeyBoss` ; `monoument0/1/2/3` aux 4 zones colorées, dans un ordre encore à déterminer.

### Retour communauté (message tiers via Discord, non vérifié en jeu)

Quelqu'un ayant apparemment déjà travaillé sur un monde Archipelago pour Big Walk (framework "Manual") a partagé son approche :
- N'a pas fait de l'item qui bloque la sortie du tutoriel (probablement `bigKeyIntro`) un item AP randomisé — combiné à une randomisation des portes, la logique deviendrait inexprimable dans Manual ("just doesn't work in a manual"). Point à garder en tête si on randomise un jour les portes.
- Pour débloquer les tours : assignation de **régions** aux gourds — certains gourds spécifiques doivent être amenés à la tour bleue, d'autres à la rouge, etc., plutôt qu'un compteur générique "24 gourds au choix parmi tous". Confirme/précise l'hypothèse `PropHomeBlock` (groupe fixe de homes par tour, pas un total générique) trouvée via Ghidra plus haut.
- Suggestion cosmétique : recolorer les gourds AP-shufflés façon "purple gourds" (variante déjà existante dans le jeu) pour les distinguer visuellement des gourds vanilla.

### Point ouvert — granularité des locations (puzzles / pins / clés)

Question posée en cours de session : les locations Archipelago pourraient-elles être (a) les puzzles, (b) les pins de gourds aux valets, et (c) les emplacements de clés, comme trois catégories séparées ?
- (a) et (c) sont déjà ce qu'on détecte aujourd'hui (`Loose` pour les gourds via `GourdStatePatch`, peck-vers-plinthe pour les clés faute d'alternative puisqu'elles n'ont pas de `RewardGourd`/`GourdState`).
- (b) est en fait **redondant avec (a)** une fois la clarification étau/valet/monument ci-dessus prise en compte : le valet s'écrit automatiquement au moment de la résolution, donc (a) et (b) se complètent toujours au même instant — pas une vraie distinction temporelle, contrairement à ce qu'on pensait avant de recroiser avec les notes du 2026-09-03.
- Le vrai second palier distinct serait plutôt le **stash à un monument** (nécessite deux joueurs, séparé dans le temps de la résolution) — mais ce n'est pas spécifique à un gourd précis (slots génériques partagés), donc le modéliser comme location demanderait une approche différente ("un gourd a été stashé au monument N slot M", pas "gourdX a été stashé").
- Le monde n'est pas prévu pour être jouable en solo (confirmé par le joueur) — donc la contrainte à deux joueurs pour le stash n'est pas un problème de conception à éviter, juste à garder en tête pour la logique d'accessibilité du monde Python (une location de stash ne serait jamais atteignable en solo, mais ce n'est pas un souci si le monde suppose du multi).

Rien de tout ça n'est implémenté côté mod pour l'instant — à rouvrir quand le monde Python sera conçu.

**Suite Discord "Big Walk" (23/08/2026, tiers, sujet "How to deal with gourd deposit boxes")** — reprend directement ce point ouvert, trois options de design proposées pour les "gourd deposit boxes" (= le stash au monument ci-dessus) :

1. **Progressives, un seul type de location** : tous les monuments contribuent à **une seule** location progressive globale (ex. "N gourds déposées au total, toutes tours confondues"), **pas** de récompense de complétion spécifique par tour.
2. **Non-progressives, déblocage séquentiel** : une seule tour "active" à la fois — la tour suivante ne se débloque qu'une fois **tous** les gourds déposés dans la tour courante.
3. **Non-progressives, mais sans item critique dedans** : chaque deposit box est une location comme une autre (indépendante par tour/slot), mais le monde exclut les items de progression critique de pouvoir s'y trouver — évite qu'un item bloquant tout le multiworld dépende d'un accomplissement à deux joueurs, tardif et spécifique.

**DÉCISION (2026-09-09)** : pas figé sur une seule option — le joueur fait remarquer que ce choix devrait plutôt être une **option YAML du monde AP** (même famille que `lock_puzzles`/`lock_pickups` déjà vus dans le doc tiers), pas quelque chose qu'on fige une fois pour toutes côté mod. Conséquence côté implémentation future : le mod devra recevoir ce réglage depuis le monde/slot AP (à la connexion) et adapter son comportement de détection en conséquence, plutôt que coder en dur un seul modèle. Priorité pratique suggérée par la faisabilité technique déjà connue : l'**option 1** (compteur global progressif) est la plus simple à détecter dès maintenant — `GourdPinAudioBehaviour.GetNumberOfFilledHomes()` (déjà repéré en Ghidra, cf. section "Classes et structures clés" plus haut) donne directement ce compte, sans investigation supplémentaire. Les options 2 et 3 restent valides comme réglages alternatifs à supporter plus tard, l'option 3 nécessitant en particulier de vérifier si le jeu expose un moyen de distinguer QUEL slot précis a été rempli (pas confirmé).

**Sur l'apparence de l'item gourd reçu** (reprend la toute première tâche notée en tout début de session, jamais implémentée) : le même tiers recommande que le gourd matérialisé soit à la fois **visible/spawné devant le joueur** ET **un objet d'inventaire** portable — pas juste une écriture invisible comme le fait `ItemApplier` aujourd'hui.

**DÉCISION (2026-09-09)** : spawn dans la **zone de spawn/hub** (pas lâché devant le joueur où qu'il soit dans le monde) — tranché par le joueur. Rappel du contexte technique déjà noté pour cette tâche (jamais réalisée) : `InventorySpawn.GetNextSpawnPosition()` (point d'ancrage avec dispersion aléatoire dans un rayon, situé au hub) et le clonage d'un `RewardGourd`/`Prop` existant sont les pistes techniques déjà identifiées, avec le risque de collision `saveablePropName` déjà documenté à neutraliser. Cohérent avec la décision "gourds = fillers génériques" du même jour : un item filler générique reçu pourrait logiquement apparaître comme un objet physique ramassable au hub, plutôt que de rester une pure abstraction `SaveManager`.

## Document externe tiers — "Big Walk Archipelago details.pdf" + fil Discord (2026-09-07)

Le joueur a partagé un document de conception d'APworld (options YAML, liste items/locations, guide de tuiles custom) et des extraits d'un fil Discord, très probablement du même auteur tiers ("trinity") déjà mentionné plus haut ("Retour communauté"). Ni le document ni le fil ne sont affiliés à ce mod — mais ils recoupent fortement (et parfois corrigent) ce qu'on a déduit aujourd'hui par rétro-ingénierie pure, en plus d'apporter des infos neuves. Tout ce qui suit vient de tiers, pas vérifié par nous en jeu, sauf mention contraire.

### Cross-validation forte : noms réels des tours et mapping des clés

Le document nomme explicitement les 6 tours + le tutoriel, avec leurs propres emplacements de dépôt de clé ("Key Deposit"). En recoupant l'ordre des sections du document (`Completion Rewards`) avec nos `monoumentX`/`bigKeyPlinthYyy` et le mapping couleur→plinthe donné par le joueur plus tôt cette session, tout concorde parfaitement :

| Tour (nom réel) | Big key (`SaveablePropName`) | Plinthe (`SaveableHomeName`) | Lieu de dépôt (nom du doc) | Deposit boxes (doc) | Slots réels (F6) |
|---|---|---|---|---|---|
| Tutorial | `bigKeyIntro` | `bigKeyPlinthIntro` | Drawbridge Key Deposit | 4 | **4** ✓ |
| Red Funnel Tower | `bigKeyRedZone` | `bigKeyPlinthMapRoom` | Map Room Key Deposit | 5 | **5** ✓ |
| Green Cup Tower | `bigKeyGreenZone` | `bigKeyPlinthSkiLift` | Chairlift Station Key Deposit | 5 | **5** ✓ |
| Blue Castle Tower | `bigKeyBlueZone` | `bigKeyPlinthTrain` | Train Station Key Deposit | 5 | **5** ✓ |
| Yellow Twist Tower | `bigKeyYellowZone` | `bigKeyPlinthTunnels` | Underground Tunnel Key Deposit | 5 | **5** ✓ |
| Black Monolith Tower | `bigKeyBoss` | `bigKeyPlinthEnding` | Dam Key Deposit | 6 | **6** ✓ |
| Green Dome Tower | `bigKeyOverflow` | `bigKeyPlinthGoodbye2` | Tutorial Key Deposit (keyhole, fin "big_game") | 15 (réductible à 6 en jeu, `limit_green_dome_deposit_boxes`) | **15** ✓ |

Chaque colonne "Deposit boxes (doc)" correspond **exactement** aux comptages réels trouvés via F6 cette session (`monoumentIntro`=4, `monoument0-3`=5 chacun, `monoumentFinal`=6, `monoumentOverflow`=15) — donc `monoument0/1/2/3` = les 4 tours "5 slots" (Red Funnel/Green Cup/Blue Castle/Yellow Twist, ordre exact entre elles pas encore déterminé), `monoumentFinal` = Black Monolith Tower, `monoumentOverflow` = Green Dome Tower. Confirme aussi indépendamment (donc avec un niveau de confiance bien plus élevé) le mapping couleur→plinthe donné par le joueur : Red→MapRoom, Green→SkiLift, Blue→Train, Yellow→Tunnels, Boss→Ending, Overflow→Goodbye2, déjà codé dans `GourdRegistry.BigKeyHomesByProp`.

Sens du mécanisme (déduit du doc) : après avoir rempli les deposit boxes ("gourd deposit boxes") d'une tour, on obtient la clé de cette tour ("Tower: Key Cutter 1-5" — encore un mécanisme "Key Cutter" pas investigué, 5 par tour, 25 au total sur les 5 premières tours), qu'on va ensuite déposer ailleurs (le "Key Deposit" suivant dans la séquence) pour débloquer la suite. Donc **chaque tour a son propre lieu de dépôt de clé, séparé du lieu où on obtient la clé** — cohérent avec notre design "clé déposée à une plinthe = check", mais révèle que la clé d'une tour est physiquement obtenue À cette tour puis déposée AILLEURS (pas à la même tour).

### Nouveau : 45 puzzles au lancement, 58 aujourd'hui

Le fil Discord date d'environ une semaine après la sortie du jeu ("only having released a few days ago", posts datés 07-08/08/2026 ; jeu sorti le 4 août 2026 selon nos notes). Le doc et le fil parlent de **45 puzzles au total**, avec un objectif "any 30 of 45". En comparant la liste de 45 puzzles du document avec les 58 `gourdXxx` de notre enum actuel, **13 gourds présents aujourd'hui sont absents de la liste du document** (`gourdBunker`, `gourdHighPegBoard`, `gourdFirstPegBoard`, `gourdMagiciansTrick`, `gourdButtonBoothChallenge`, `gourdTileSoup`, `gourdPanopticon`, `gourdMaypole`, `gourdBlindfoldCircus`, `gourdMessengerRun`, `gourdHotPotato`, `gourdScoutTiles`, `gourdScoutCounting`). Hypothèse la plus probable : **le jeu a reçu une mise à jour de contenu ajoutant 13 puzzles depuis la sortie**, entre la rédaction de ce document (~août 2026) et aujourd'hui (2026-09-07). À garder en tête si jamais ce document ou une future version du monde Python s'appuie sur l'ancien total de 45 — le vrai total actuel est 58.

### Terminologie confirmée indépendamment

Une remarque du même auteur, en inspectant les fichiers du jeu de son côté : *"the name for the puzzle reward object... is internally called 'Gourd'"* — confirme, par une source complètement indépendante de notre décompilation Ghidra, que `SaveablePropName.gourdXxx` est bien la structure interne exacte, pas une coïncidence de nommage de notre part.

### Le problème qu'ils ont rencontré, qu'on a évité par design

Extraits très pertinents du fil (paraphrasés) — l'auteur a passé au moins un mois (posts du 07/08 au 18/08/2026) coincé sur exactement le problème qu'on a résolu cette session, mais en l'abordant différemment :
- Approche initiale envisagée : **empêcher le "gourd clamp" (l'étau) de se déclamper tant qu'un item AP n'est pas reçu**, ou pouvoir le déclamper à volonté par code. Jamais aboutie : *"I haven't figured out how to accomplish stopping the gourd clamp from unclamping on puzzle solution, or being able to arbitrarily unclamp the gourd whenever I need to"*.
- Complication additionnelle notée : **certains puzzles n'ont pas d'étau du tout** (le gourd est déjà "ouvert" sans clamp), ex. le puzzle "Telescope to Box" du tutoriel — donc une approche par blocage de l'étau ne fonctionnerait de toute façon pas uniformément sur tous les puzzles.
- Pivot suggéré par un autre participant (Kimtroverted) : **verrouiller les *deposit zones* (les monuments/valets) plutôt que l'étau lui-même**. Adopté ("that might just be better yeah"), mais avec un coût reconnu : ça transforme "30 gourds au choix parmi 45" en "ces 30 emplacements de dépôt précis parmi 45" — perte de flexibilité du pool.
- Liste de problèmes non résolus qu'ils ont identifiés avec cette dernière approche : sur-marche arrière (aller collecter un gourd déjà résolu puis le ramener), ça encourage à se séparer du groupe (pour aller chercher les gourds) alors que les puzzles nécessitent souvent d'être ensemble, complexité de suivi de l'état (résolu ? récupéré ? déposé ?), plus de travail de programmation (tous les puzzles n'ont pas de clamp à affecter), exploitable via une fonction "objets perdus" du jeu, et tout ça reste fastidieux à jouer.

**Comparaison avec notre design** : notre approche ("écriture SaveManager pure, jamais toucher l'objet vivant", cf. "Design retenu" plus haut) évite structurellement presque tous ces problèmes — pas besoin de bloquer ou déverrouiller l'étau (on ne le touche jamais), fonctionne uniformément même pour les puzzles sans clamp (on écrit juste la valeur finale attendue dans `SaveManager`, peu importe le mécanisme physique de résolution), pas de sur-marche-arrière ni de séparation forcée du groupe (`Prop.Start()`/le pin live font le travail automatiquement). Bonne confirmation indirecte que le design retenu cette session (par une voie complètement différente : test empirique + Ghidra, pas de mise en commun avec ce fil Discord avant la rédaction) est solide.

### Modèle checks/items proposé par ce tiers (à comparer avec le nôtre)

- **Checks** : puzzle complété, clé obtenue d'une tour, station radio trouvée.
- **Items** : *déblocage* de la récompense d'un puzzle (pas la récompense elle-même — un item générique "tu peux maintenant résoudre/récupérer ce type de puzzle"), déblocage d'une clé (à l'exception de la dernière tour, gardée comme objectif plutôt que progression), activation d'une station radio.

Différence notable avec notre implémentation actuelle : dans ce modèle, l'item reçu n'est pas littéralement "le gourd X" mais un **déblocage indirect** (cohérent avec les options `lock_puzzles`/`lock_pickups` du YAML : `individual` = un item de déblocage par type d'objet, `all` = un seul item générique qui débloque tout, `disabled` = tout dispo dès le début). Notre mod fait actuellement l'inverse : l'item reçu EST directement `gourdX`/`bigKeyX` (matérialisé par écriture `SaveManager`), sans notion de "déblocage préalable" séparé. Aucune des deux approches n'est tranchée comme la bonne pour notre monde — à recroiser avec la discussion "Point ouvert — la logique location/item" plus haut quand le monde Python sera conçu.

### Nouveau retour communauté (Discord, 2026-09-09) — précise le modèle checks/items, mentionne les "bells"

Nouvelle réponse de Jack5 (auteur tiers déjà cité, doc Google partagé : "Big Walk Archipelago details", même contenu que le PDF déjà référencé plus haut) suite à une question directe du joueur ("comment gérer goal/items/locations, et les gourds — locations pour vessel ou à retirer complètement ?") :

- **Goal** : "both or either a certain number of each collectible and specific endings or areas to reach" — un nombre de collectibles (gourds/clés) ET/OU atteindre des fins/zones spécifiques. Recoupe directement notre "Point ouvert — Goal" plus haut (fin standard vs N tours vs autre) : cette réponse suggère que ce n'est pas exclusif, un monde AP peut combiner les deux approches selon les options YAML.
- **Items** : capacités du personnage (beaucoup liées au fait de porter certains types d'objets), objets d'inventaire (spawn), **arch doors**, **tours**, clés, pièges, et des **fillers** ("misnamed gourds" — des gourds "mal nommés", i.e. des items génériques/junk qui n'ont pas de lien 1:1 avec une vraie location gourd).
- **Locations** : premier ramassage d'objet, résolution de chaque puzzle, allumer les stations radio (confirme ce qu'on vient de trancher), déposer les gourds et les clés (le stash au monument — cf. distinction étau/valet/monument plus haut), et **détruire des cloches ("bells")**.

**Recoupement technique important** : "destroying bells" correspond très probablement au champ `bell` dans `PeckDevHelper.UnlockRules` (trouvé lors de l'investigation des Arch doors, cf. section dédiée — jamais suivi jusqu'ici, aux côtés de `chairlift`/`train`/`tunnel`/`map`/`gourd`). Piste concrète à creuser le jour où on s'attaque à cette catégorie : probablement la même technique que les Arch doors (`TrackedPeckState` de catégorie dédiée dans `SavableSystem`, à identifier).

**Précision joueur (2026-09-10)** : deux cloches précisément dans la zone de fin — une derrière `bigKeyBoss` (Black Monolith), une plus loin qui pourrait compter comme goal completion. Les deux se déclenchent par **deux boutons pressés simultanément** (donc injouable solo, nécessite 2 joueurs) — cohérent avec le champ `bell` de `PeckDevHelper.UnlockRules`, mais le vrai mécanisme "simultané" (probablement un `PeckContext`/`PeckSystemReference` partagé entre deux `PeckSwitch`, à vérifier) n'est pas encore localisé.

Recherche statique (`il2cpp.cs`) le 2026-09-10 : aucune classe `Bell`/`Chime`/`Gong` dédiée trouvée (`CowbellAudio` existe mais porte un `Prop CowbellProp` — semble être l'audio d'un gourd "cowbell" porté à la main, pas le mécanisme de cloche de fin). En revanche, deux pistes concrètes confirmées :
- `Enviro.LobbyLighting.AreaType` (enum d'éclairage de zone) a bien une entrée `BellRoom = 3`, aux côtés de `GoodbyeChapel = 4`/`Dream = 5`/`GoodbyeVoid = 6` — confirme qu'il existe une vraie pièce nommée "Bell Room" en zone de fin. **Correction joueur (2026-09-10)** : `BellRoom` est probablement une troisième pièce distincte, pas l'une des deux cloches — `GoodbyeChapel`/`GoodbyeVoid` collent mieux à la description ("une derrière le boss key, une loin qui pourrait compter comme goal completion"), les deux étant sur le chemin de la vraie fin (`bigKeyPlinthGoodbye2`, déjà confirmé). `Dream` reste non identifié (aucune autre référence dans le dump) — pas manifestement lié aux cloches. Les deux hits `SetToGoodbyeVoidMode()` trouvés (`CameraQualityManager`, `PostProcessingQualityManger`) ne sont que des bascules cinématiques caméra/post-processing, pas des déclencheurs de check — ne confirment ni n'infirment quoi que ce soit ici. Reste non résolu statiquement, à trancher en jeu (cf. plan F9/Delete plus bas, indépendant de la pièce exacte).
- `SavableSystem` (enum des `TrackedPeckState` génériques, même famille que `SpawnHubGate`/`EndingGate`) contient `BlackTowerInteriorDoor = 26`, `EndingGate = 27`, `GauntletComplete = 28`, et `GauntletChamber0..6 = 50..56` — clairement la zone/le challenge situés derrière `bigKeyBoss` (Black Tower). Aucune entrée `Bell` explicite dans cet enum, donc soit les cloches utilisent une autre catégorie non encore vue, soit elles passent par un autre système que `SavableSystem`.

**Prochaine étape (en jeu, pas trouvable statiquement)** : le mot-clé `bell`/`chime`/`gong`/`cowbell` a été ajouté à `Debug/DebugComponentLookup.cs` (`DefaultKeywords`, utilisé par F9) — se poster physiquement devant chaque cloche et appuyer F9 (composants + noms proches) et Delete (`DebugPeckSwitchTarget.DumpNearby`, dump les `PeckSwitch` avec leur vraie `trackedStateSystem`/clé `SaveManager`) pour identifier le vrai mécanisme sans deviner. Prévoir aussi F10 (`DebugPeckDevHelper.DumpNearby`) pour voir si l'un des `PeckDevHelper` déjà en scène porte `UnlockRules.bell=true`.

**Éclairage nouveau sur le "Point ouvert — logique location/item"** : cette réponse penche clairement vers un **vrai découplage multiworld**, pas un shuffle-sur-place — les gourds y sont explicitement des **fillers/items génériques** ("misnamed gourds"), pas des items 1:1 avec leur propre location comme notre implémentation actuelle (`ItemApplier` matérialise littéralement "gourdX" reçu). Ça répond en partie à la question du joueur ("gourds comme locations pour vessel, ou à retirer complètement ?") : dans ce modèle, les gourds *résolus* sont des locations (comme aujourd'hui), mais l'item qu'on reçoit en retour n'est pas censé être "ce gourd précis" — c'est un filler générique parmi d'autres, semblable à n'importe quel item de remplissage AP. Implication concrète si ce modèle est retenu : `GourdRegistry`/`ItemApplier` devront être repensés pour dissocier réception d'un "gourd filler" (peu importe lequel, matérialiser n'importe quel gourd non-résolu) de la détection de location (toujours 1:1 avec le gourd précis résolu). Pas encore décidé — à trancher avec le joueur avant de commencer le monde Python, puisque ça détermine directement l'architecture de `ItemApplier`.

Lien du document complet (partagé par le joueur) : https://docs.google.com/document/d/1T5QBFIr71wFKN-EDTNV8827-H6aXTLw-UT3azjbWGHs/edit?tab=t.0

### DÉCISION TRANCHÉE (2026-09-09) — modèle final location/item, gourds vs big keys

Répond définitivement au "Point ouvert" ci-dessus :

- **Gourds — items découplés (fillers génériques)** : un item "gourd" reçu par Archipelago n'est **plus** lié à un `SaveablePropName` précis — il se pose sur **n'importe quel** gourd non-résolu localement (peu importe lequel). Confirmé par le joueur : «évidemment qu'il faut des gourds génériques pour les placer dans les monuments». **Locations** : inchangé, toujours 1:1 (chaque gourd résolu = sa propre location précise, comme aujourd'hui).
- **Contrainte de world-design signalée par le joueur** (côté monde Python, pas le mod C#) : il faudra s'assurer qu'il existe **assez de gourds génériques dans le pool d'items** pour satisfaire le nombre de slots des monuments réellement actifs (cf. comptages F6 : `monoumentIntro`=4, `monoument0-3`=5 chacun, `monoumentFinal`=6, `monoumentOverflow`=15) — sinon un joueur pourrait se retrouver bloqué avec des monuments jamais remplissables faute d'assez d'items gourd dans son pool. **Nuance immédiate du joueur** : contrainte jugée peu risquée en pratique — le pool d'items AP n'a pas besoin d'être fait *que* de gourds génériques ; des traps et autres items sans effet peuvent combler le reste du pool sans casser l'équilibre. À garder en tête lors de la conception des options YAML du monde, mais pas un blocage majeur anticipé.
- **Big keys — restent 1:1** : chaque big key reçue reste précisément celle attendue (`bigKeyRedZone` reste `bigKeyRedZone`), **pas** de traitement filler. Raison du joueur : «les clés peuvent être des objets de progression donc il semble normal de les laisser en 1:1» — une clé aléatoire ne serait d'aucune utilité si ce n'est pas celle de la tour où le joueur doit progresser. Aucun changement par rapport à l'implémentation actuelle (`ItemApplier.ApplyGourdItem(bigKeyXxx)` déjà 1:1).
- **Cas limite — filler gourd reçu alors que tous les gourds locaux sont déjà résolus** : **no-op silencieux** (décision du joueur, pas de log même en cas de debug) — l'item est simplement absorbé sans effet, comme un filler AP classique déjà "au max".

**Implication concrète pour le code** (pas encore implémenté, à faire quand le client réseau existera ou pour préparer/tester en amont) : `ItemApplier.ApplyGourdItem(SaveablePropName)` reste inchangé et continue de servir tel quel pour les big keys (1:1) et en interne pour les gourds une fois la cible choisie. Il faut ajouter une nouvelle fonction, ex. `ItemApplier.ApplyGenericGourdFiller()`, qui :
1. Scanne les gourds (pas les big keys) non-résolus localement (même signal que `DebugGourdLookup.FindNearestUncollectedProp`, mais filtré aux seuls `gourdXxx` et sans notion de "plus proche" — n'importe lequel convient, la matérialisation n'a pas besoin de proximité).
2. S'il en trouve un, appelle `ApplyGourdItem` dessus (réutilise toute la plomberie existante : écriture `SaveManager`, effet en direct, garde anti-doublon de check).
3. S'il n'en trouve aucun (tous déjà résolus) : ne fait rien, silencieusement.

Le futur monde Python devra donc distinguer, côté items, un item générique type `"Gourd"` (compte = N, où N dépend des options) des items nommés 1:1 `"bigKeyRedZone"` etc. — à garder en tête pour la conception des `item_table`/`location_table` de l'apworld.

### Point à tester la prochaine session : puzzles coopératifs sans étau (ex. `gourdTelescopeToBox`)

Suite à la discussion ci-dessus, le joueur a précisé le fonctionnement réel de ces puzzles "sans clamp" : c'est en fait une mécanique **coopérative** — un joueur appuie sur un bouton ailleurs (qui déverrouille une caisse), l'autre récupère l'objet à l'intérieur. Filet de sécurité déjà observé : si l'objet est jeté loin de sa caisse, le jeu le replace dedans automatiquement.

`gourdTelescopeToBox`/`valetTelescopeToBox` (un de nos 58 gourds) est très probablement l'exemple exact cité par trinity dans le fil Discord — donc testable directement avec nos outils actuels, sans code supplémentaire. Nécessite deux joueurs (confirmé par le joueur). Questions à trancher au prochain test :
1. Ce puzzle a-t-il un composant `RewardGourd` ou non (F5 près de la caisse) ?
2. À quel moment précis `SaveManager` reçoit l'écriture pour cette clé — à l'ouverture de la caisse (bouton pressé) ou seulement à la récupération/rangement de l'objet ?
3. Le comportement "recharge dans sa caisse si perdu" cause-t-il des écritures `SaveManager` répétées, et est-ce que ça interagit avec `ItemApplier.TryApplyLiveEffect` (pin en direct pour les props sans `RewardGourd`) si un item AP arrive avant que la caisse soit ouverte ?

Hypothèse de travail (non vérifiée) : `SaveValuePatch` (filet de sécurité générique, indépendant de `RewardGourd`) devrait capter le check quel que soit le mécanisme physique exact — cohérent avec sa raison d'être documentée plus haut. À confirmer.

### Goal (condition de victoire côté AP) — dépend d'abord d'une décision de design (2026-09-09)

Pas encore de piste technique retenue, et volontairement pas encore investigué : avant de chercher *quelle fonction/état du jeu représente "terminé"*, il faut d'abord trancher **quel** goal le monde AP propose — ce n'est pas qu'une question technique. Options mentionnées : finir une fin standard du jeu (laquelle, il y en a peut-être plusieurs), ou exiger un nombre N de tours complétées (les 5-6 tours + tutoriel), ou autre variante. Le choix change complètement quoi hooker en jeu (candidats déjà repérés si besoin : `EndingGate`, `GauntletComplete`, les 9 `monoumentFinalSlot0-8`) — donc à rouvrir une fois cette décision de design prise, pas avant.

### Autres pistes non explorées, notées pour plus tard

- **Key Cutters** : 5 par tour (25 au total sur les 5 premières tours) — **décidé de ne pas explorer** (2026-09-09) : juste une étape intermédiaire mécanique vers "clé obtenue/complétée", pas un accomplissement distinct du joueur qui aurait du sens comme check séparé. Ne sera pas transformé en check.
- **Radio stations** (`SavableSystem.FmStation*`, 10 entrées dans l'enum) — décidé (2026-09-09) comme **checks/locations** (le joueur allume la radio en jeu → check reporté), pas comme items à matérialiser. Jugé facile : réutilise directement `SaveValuePatch` (déjà générique sur `SaveManager.SetIntValue`), il suffit d'élargir la reconnaissance de clé pour tenter aussi `Enum.TryParse<SavableSystem>` (en plus de `SaveablePropName` déjà géré) — aucune nouvelle plomberie de détection à écrire, juste étendre l'existante.
- **`lock_map_room`** et **`PoetAndPriestDoors`/`PoetAndPontiffDoors`** (ces deux derniers trouvés dans l'enum `SavableSystem`, hypothèse non vérifiée : liés aux puzzles `gourdPoetAndPreist`/`gourdPoetAndPontiff`) : ni le joueur ni l'investigation Ghidra n'ont permis d'identifier précisément ce que c'est (2026-09-09) — **laissé de côté pour l'instant**, à reprendre en jeu si l'un des deux tombe dessus par hasard ou reconnaît le mécanisme en jouant.
- Liste complète des items "vanilla" du jeu (capacités, déblocages de portage, objets d'inventaire) donnée dans le document — utile comme référence si le mod doit un jour s'étendre au-delà des gourds/big keys.

## Gourds "variant challenge" (violettes/postgame) — révélation automatique sur la carte (implémenté le 2026-09-09)

**Investigué et résolu.** Demande utilisateur : les gourds violettes (`RewardGourd.isVariantChallenge`, normalement débloquées après une première fin de partie) doivent être rendues visibles sur la carte dès le début, pour un monde Archipelago où elles sont accessibles depuis le départ.

**Mécanisme découvert par décompilation Ghidra** (`GourdMap.Initialize`/`Start`/`RevealHiddenGourds`, `RewardGourd.Awake` — cf. script `BW_export/decompile_variant_gourds.py`, sortie dans `BW_export/variant_gourds_decompiled.txt`) :
- `GourdMap.Initialize()` (appelée une fois par instance de `GourdMap`, donc à chaque chargement de zone) instancie l'icône de carte de chaque gourd (`flagPrefab` ou `flagPrefabVariantChallenge` selon `isVariantChallenge`) et force **systématiquement** `GourdFlag.SetState(Hidden)` pour tout gourd `isVariantChallenge == true` — un état purement local/session, **jamais lu ni écrit dans `SaveManager`**.
- `GourdMap.RevealHiddenGourds(PeckContext)` (méthode privée, déclenchée en vrai jeu via `GourdMap.revealSystem`, un `PeckSystemReference`, probablement lié à un event de fin de partie) fait l'inverse : pour tout `GourdFlag` actuellement `Hidden`, `SetState(Locked)` — révèle l'icône sur la carte.

**Solution retenue** : plutôt que de toucher à la méthode privée, on obtient le même résultat via l'event statique **public** `GourdMap.refreshFlag` (`Action<SaveablePropName, GourdFlag.GourdState>`, déjà utilisé par le jeu pour rafraîchir l'icône en live) :
```csharp
GourdMap.refreshFlag.Invoke(prop.saveablePropName, GourdFlag.GourdState.Locked);
```
appelé pour chaque `RewardGourd.isVariantChallenge == true` trouvé via `FindObjectsByType<RewardGourd>()`.

**Particularité par rapport aux Arch doors** : comme cet état n'est jamais persisté et est remis à `Hidden` à **chaque nouvelle instance de `GourdMap`** (donc à chaque chargement de zone, pas une seule fois par save), l'automatisation ne peut pas être un flag "une fois par save" — `Core/VariantGourdMapUnlocker.cs` (`MonoBehaviour` ajouté inconditionnellement, cf. `Plugin.Load()`) poll toutes les 2s (`WorldManager.isReadyForEffects` comme garde) et rappelle `Core/VariantGourdRevealer.RevealAll()` en continu. Pas besoin de garde `NetworkServer.active` : c'est un état d'affichage carte purement local/client, pas réseauté, pas persisté.

**BUG SÉRIEUX TROUVÉ ET CORRIGÉ (2026-09-09) — freeze total du jeu lié à l'invocation directe d'un delegate Il2Cpp statique** :

La toute première implémentation appelait directement `GourdMap.refreshFlag.Invoke(propName, GourdState.Locked)` (l'event statique `Action<SaveablePropName, GourdFlag.GourdState>` que le jeu utilise en interne pour rafraîchir l'icône en live). Deux freezes complets du jeu sont survenus dans la même session (aucune exception, aucun log, process "Responding" côté OS mais figé à l'écran, confirmé par le dialogue Windows "ne répond pas") — dans les deux cas, plusieurs dizaines de secondes *après* avoir révélé puis résolu un gourd violet, au premier alt-tab suivant.

**Isolation par test A/B en jeu** (l'utilisateur a confirmé "ce problème ne survenait pas avant") :
- Gourd normale (non-violette) résolue + alt-tab → aucun freeze.
- Désactiver `VariantGourdMapUnlocker` (le polling) entièrement → freeze quand même reproduit sur une gourd violette révélée via le hotkey `Home` (qui appelle la même logique `Invoke()`) puis résolue.
- Gourd violette découverte/résolue **sans jamais appeler `refreshFlag.Invoke()`** (jamais de `Home` pressé) + alt-tab → aucun freeze.

Conclusion : ni le polling en lui-même, ni les gourds violettes en tant que telles ne posent problème — c'est spécifiquement l'appel `Invoke()` sur le delegate Il2Cpp statique qui corrompt un état interne (probablement côté interop IL2CPP) ne se manifestant qu'au retour de focus (alt-tab), sans trace exploitable dans aucun log.

**Fix** : `VariantGourdRevealer.RevealAll()` ne touche plus jamais `GourdMap.refreshFlag`. Il trouve directement l'instance `GourdFlag` concernée (`FindObjectsByType<GourdFlag>()`, filtrée sur `gourdState == Hidden` et croisée avec les `RewardGourd.isVariantChallenge` trouvés) et appelle `GourdFlag.SetState(Locked)` **directement dessus** — un appel de méthode C# normal sur un composant, pas une invocation de delegate statique. C'est exactement ce que fait la méthode privée `GourdMap.RefreshFlag` en interne (elle aussi appelle `GourdFlag.SetState` directement, jamais via le delegate), donc tout aussi sûr côté jeu. Revalidé en jeu : plus aucun freeze sur plusieurs cycles révélation → résolution → alt-tab.

**Leçon générale pour la suite du mod** : ne jamais appeler `.Invoke()` sur un champ de delegate Il2Cpp statique du jeu (même public) depuis du code managé externe — préférer systématiquement l'appel direct de la méthode d'instance sous-jacente quand elle est accessible, même si ça demande de retrouver l'instance concernée soi-même plutôt que de passer par l'event.

**Validé en jeu (comportement final)** :
- Icônes des gourds violettes bien visibles sur la carte dès le début, sans action manuelle (hotkey `Home` conservé pour déclenchement manuel via `Debug/DebugVariantGourdReveal.cs`).
- Le prop physique lui-même est un `RewardGourd` tout à fait normal (`gourdState=Locked`, `actif=True`, pas de second verrou caché) — confirmé via F5 (`DebugGourdLookup.LogNearby`) à 3m d'un gourd violet.
- Check détecté et reporté normalement après résolution physique (`[Check] gourdCharadesRooms`/`[Check] gourdSpeedObby` observés en jeu, pipeline `GourdStatePatch`/`SaveValuePatch`/`CheckTracker` inchangé).
- Une fois un gourd violet résolu, son icône disparaît de la carte comme n'importe quel gourd classique — comportement normal, pas un bug.
- Aucun freeze reproduit après le fix, sur plusieurs cycles de test avec alt-tab.

## Arch doors / raccourcis du hub — ouverture automatique au premier lancement d'une save (implémenté le 2026-09-09)

**Investigué et résolu.** Nom interne : pas "Arch" du tout — le composant réel s'appelle `HubGate` (`HubGate_Plinth`, `HubGate_Gate`, `HubGate_DoorPieceL/R1-3`, `HubGate_DoorBlocker_Left/Right`, `GateMainSystem`). Décision utilisateur : ces raccourcis n'ont pas d'intérêt à rester fermés pour un monde Archipelago (contrairement aux gourds/big keys, qui sont le vrai contenu randomisé) — donc ouverts automatiquement dès la première session sur une save.

**Mécanisme découvert par décompilation Ghidra** (`TrackedPeckState.SetState`, `PeckDevHelper.Trigger`, `PeckRelay`, `PeckSwitch` — cf. script `BW_export/decompile_peck_chain.py`, sortie dans `BW_export/peck_chain_decompiled.txt`) :
- `TrackedPeckState.SetState(PeckContext)` écrit `SaveManager.SetIntValue(key, compressedState)`, où `key` = `saveIdentity.saveGuid` si `savableSystem == NotSavable`, sinon **`Enum.ToString(savableSystem)`** (le nom de l'enum `SavableSystem` lui-même, littéralement, pas un GUID par instance).
- `PeckDevHelper.Trigger(UnlockRules)` (cheat dev déjà intégré au jeu, probablement pour les tests internes de House House) : itère tous les `PeckDevHelper` chargés, et pour chacun dont les règles matchent (soit `unlockRules.Matches(rules)`, soit les champs simplifiés `fireWithUnlocks`/`fireWithLights`/`fireWithTrain`), appelle `PeckSwitch.Peck()` sur le `PeckSwitch` attaché au même GameObject — qui lit `trackedStateSystem` (référence assignée dans l'éditeur Unity, **pas nécessairement l'objet le plus proche visuellement**) et y appelle `SetState`.

**Piège rencontré** : `Trigger(new UnlockRules { unlocks = true })` ouvre bien les Arch doors, mais broadcaste à *toute* la catégorie "unlocks" — confirmé par inspection directe de plusieurs fichiers `.sav` (`grep -o '"key":"[^"]*","value":[0-9-]*'` sur le JSON) : persiste aussi `EndingGate=1` et les 7 `FmStation*`/4 `LookoutLight*` à `1`, en plus des 3 clés hub voulues. Le "ça se referme après reload" observé au premier test visuel était trompeur — en fait seule une partie de l'effet est purement visuel/transitoire (les switches sans catégorie "hub"), le reste (les 3 clés hub + accessoirement Ending/FmStations/LookoutLights) est bien persisté.

**Solution retenue** : écriture directe des clés `SaveManager`, sans jamais appeler `PeckDevHelper.Trigger` ni toucher un objet vivant — même philosophie que `ItemApplier` pour les gourds/big keys. Confirmé par test A/B sur plusieurs saves fraîches passées uniquement par le hook automatique (pas de F11 manuel mélangé) : `Trigger(unlocks:true)` écrit ensemble exactement `SpawnHubGate`, `HubTunnel`, `HubShortcutToSportsCreek` (jamais `EndingGate`/`FmStation*`/`LookoutLight*` dans ce sous-ensemble précis — ces 3 sont la vraie catégorie "raccourcis hub"). `Core/ArchDoorUnlocker.cs` écrit donc directement :
```csharp
SaveManager.SetIntValue("SpawnHubGate", 1);
SaveManager.SetIntValue("HubTunnel", 1);
SaveManager.SetIntValue("HubShortcutToSportsCreek", 1);
```
**Comportement confirmé en jeu** : contrairement à `Trigger()` (effet visuel immédiat via la vraie mécanique Peck), une écriture `SaveManager` brute seule n'ouvre rien en direct dans la session courante — il faut un rechargement complet pour que la restauration au chargement (mécanisme non identifié précisément, probablement l'équivalent de `Prop.Start()` mais pour `TrackedPeckState`) relise la valeur et ouvre réellement la porte. Validé : après redémarrage complet, les 3 raccourcis du hub sont ouverts, aucune autre clé (FmStations/LookoutLights) n'a été touchée.

**RÉSOLU pour l'effet en direct (2026-09-09)** : `Core/ArchDoorUnlocker.cs` cherche maintenant, pour chacune des 3 clés, le `TrackedPeckState` correspondant réellement chargé en scène (`FindObjectsByType<TrackedPeckState>()`, filtré sur `savableSystem == clé`) et appelle `SetState(1)` dessus directement (même écriture `SaveManager` en interne, confirmée par Ghidra, mais déclenche aussi les callbacks visuels/d'animation — sans le broadcast large de `Trigger()`). Si l'objet n'est pas chargé dans la zone actuelle, fallback sur l'écriture `SaveManager` brute seule (même philosophie que `ItemApplier.TryApplyLiveEffect` pour les gourds/big keys) : la restauration au chargement prendra le relais au prochain reload. Validé en jeu : les 3 raccourcis s'ouvrent immédiatement, dans la session même où la save est créée, sans reload.

**Timing du hook** : `WorldManager.OnWorldManagerStart` (event statique) et `WorldManager.instance.onLocalPlayerCharcterStart` se déclenchent tous les deux **trop tôt** (avant que le "peck manager" du jeu existe — confirmé en jeu par une rafale de logs `no peck manager instance. this is maybe too early` juste après le déclenchement, bien avant `"[id] World Started"`/`"world manager started"`). Sans incidence pour une écriture `SaveManager` brute (ça n'a pas besoin du peck manager), mais le composant final utilise quand même `WorldManager.isReadyForEffects` (propriété statique du jeu, conçue pour signaler "sûr de déclencher des effets") en polling dans `Update()`, par prudence/cohérence.

**Outils de debug ajoutés en cours de route** (`Debug/DebugComponentLookup.cs`, `Debug/DebugPeckDevHelper.cs`, `Debug/DebugTrackedPeckStateLookup.cs`, `Debug/DebugPeckSwitchTarget.cs`, touches F9-F12/Insert/Delete) : utiles pour toute investigation future d'un mécanisme de scène sans classe C# dédiée trouvable par nom — approche qui a fonctionné ici après plusieurs fausses pistes (recherche par mot-clé sur les composants proches, puis inspection des champs `PeckDevHelper.unlockRules`/`fireWith*`, puis `TrackedPeckState.savableSystem`/`saveIdentity`, puis `PeckSwitch.trackedStateSystem` — la référence Unity assignée dans l'éditeur, pas déductible par proximité).

## Fichiers de référence

- `il2cpp.cs` (export "C# prototypes") : structure complète de toutes les classes du jeu, utile pour grep rapide de nouveaux noms de classes/enums sans repasser par Ghidra.
- Projet Ghidra local (non fourni ici, sur la machine Windows) : contient les décompilations complètes, à consulter pour toute fonction non couverte par ce document.
