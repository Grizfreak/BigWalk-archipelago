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

## Fichiers de référence

- `il2cpp.cs` (export "C# prototypes") : structure complète de toutes les classes du jeu, utile pour grep rapide de nouveaux noms de classes/enums sans repasser par Ghidra.
- Projet Ghidra local (non fourni ici, sur la machine Windows) : contient les décompilations complètes, à consulter pour toute fonction non couverte par ce document.
