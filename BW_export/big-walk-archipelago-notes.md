# Big Walk — Notes de rétro-ingénierie pour implémentation Archipelago

*Dernière mise à jour : session de reverse engineering du 3 septembre 2026*

## Prochaines étapes (notées le 2026-09-07)

1. **Faire apparaître une gourd visible au spawn/hub à la réception d'un item.**
   `Core/ItemApplier.ApplyGourdItem` (implémenté) gère déjà toute la persistance/matérialisation réelle sans rien spawner — le design retenu n'a **pas** besoin d'un objet physique porté par le joueur pour fonctionner (cf. section "Design retenu" plus bas). Ce spawn serait donc **purement cosmétique/notification** ("tiens, tu viens de recevoir gourdX"), pas le mécanisme de délivrance lui-même — à garder en tête pour ne pas réintroduire par erreur l'ancienne idée de gourd générique portée à la main.
   Pistes déjà identifiées côté technique (non vérifiées) :
   - `InventorySpawn.GetNextSpawnPosition()` : point d'ancrage avec dispersion aléatoire dans un rayon (`spawnRadius`) — candidat naturel pour la position de spawn.
   - Cloner un `RewardGourd`/`Prop` existant en scène (`UnityEngine.Object.Instantiate` sur une instance déjà présente, pas besoin de trouver une référence de prefab) + `NetworkServer.Spawn` pour le réseauter. Attention : un clone garde le `saveablePropName` de l'original — s'assurer que ce gourd cosmétique n'écrit jamais dans `SaveManager` sous ce nom (ou le neutraliser/detacher du système de save), sinon collision avec la vraie entrée du gourd reçu.
   - Reste à définir : durée de vie (disparaît après combien de temps / une fois vu ?), comportement multi-joueurs (spawn visible pour tous ou seulement le destinataire ?).

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

Une clé de couleur a été testée juste après (en combinaison avec l'effet instantané ci-dessous) et le joueur a confirmé que ça fonctionnait ("ça a fonctionné") — mais sans capture de log précisant laquelle ni la paire exacte obtenue, donc **pas une confirmation formelle** au même niveau que `bigKeyIntro`. À reconfirmer avec un log explicite à l'occasion. Non-régression d'un vrai check gourd résolu normalement (hors item reçu) après le fix `FindNearestUncollectedProp` : toujours pas testée.

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

`GourdRegistry.TryGetHomeName` fait `"valet" + name.Substring("gourd".Length)` puis `Enum.TryParse` — échoue silencieusement pour ces 3 gourds précis (`TryParse` renvoie `false` puisque le nom généré n'existe pas), donc `ItemApplier.ApplyGourdItem` retourne `false`/item ignoré pour eux uniquement. **Pas encore corrigé.** Fix évident pour la prochaine session : ajouter ces 3 paires comme exceptions explicites dans `GourdRegistry` (même principe que `BigKeyHomesByProp`).

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

### Point à tester la prochaine session : puzzles coopératifs sans étau (ex. `gourdTelescopeToBox`)

Suite à la discussion ci-dessus, le joueur a précisé le fonctionnement réel de ces puzzles "sans clamp" : c'est en fait une mécanique **coopérative** — un joueur appuie sur un bouton ailleurs (qui déverrouille une caisse), l'autre récupère l'objet à l'intérieur. Filet de sécurité déjà observé : si l'objet est jeté loin de sa caisse, le jeu le replace dedans automatiquement.

`gourdTelescopeToBox`/`valetTelescopeToBox` (un de nos 58 gourds) est très probablement l'exemple exact cité par trinity dans le fil Discord — donc testable directement avec nos outils actuels, sans code supplémentaire. Nécessite deux joueurs (confirmé par le joueur). Questions à trancher au prochain test :
1. Ce puzzle a-t-il un composant `RewardGourd` ou non (F5 près de la caisse) ?
2. À quel moment précis `SaveManager` reçoit l'écriture pour cette clé — à l'ouverture de la caisse (bouton pressé) ou seulement à la récupération/rangement de l'objet ?
3. Le comportement "recharge dans sa caisse si perdu" cause-t-il des écritures `SaveManager` répétées, et est-ce que ça interagit avec `ItemApplier.TryApplyLiveEffect` (pin en direct pour les props sans `RewardGourd`) si un item AP arrive avant que la caisse soit ouverte ?

Hypothèse de travail (non vérifiée) : `SaveValuePatch` (filet de sécurité générique, indépendant de `RewardGourd`) devrait capter le check quel que soit le mécanisme physique exact — cohérent avec sa raison d'être documentée plus haut. À confirmer.

### Autres pistes non explorées, notées pour plus tard

- **Key Cutters** : 5 par tour (25 au total sur les 5 premières tours), mécanisme pas du tout investigué côté mod — semble être une étape intermédiaire entre "gourds déposés" et "clé obtenue".
- **Radio stations** (7, "Radio Station 1-7" + "All Radio Stations") : autre catégorie de check potentielle, jamais regardée.
- **Arch doors** (3 : Tutorial/Left/Right) et **lock_map_room** : mécanismes de portes/accès qui pourraient interagir avec le level design des checks, pas investigués.
- Liste complète des items "vanilla" du jeu (capacités, déblocages de portage, objets d'inventaire) donnée dans le document — utile comme référence si le mod doit un jour s'étendre au-delà des gourds/big keys.

## Fichiers de référence

- `il2cpp.cs` (export "C# prototypes") : structure complète de toutes les classes du jeu, utile pour grep rapide de nouveaux noms de classes/enums sans repasser par Ghidra.
- Projet Ghidra local (non fourni ici, sur la machine Windows) : contient les décompilations complètes, à consulter pour toute fonction non couverte par ce document.
