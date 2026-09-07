# Big Walk — Notes de rétro-ingénierie pour implémentation Archipelago

*Dernière mise à jour : session de reverse engineering du 3 septembre 2026*

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

**Question de conception ouverte — où matérialiser un item reçu ?** (discussion du 2026-09-03, pas encore tranchée) : la communauté Archipelago semble s'accorder sur l'idée de faire apparaître l'item reçu **au hub des joueurs** plutôt qu'à son `PropHome` d'origine (celui lié au puzzle). Pistes à vérifier avant de trancher/implémenter :
- Le comportement déjà observé "gourd porté en main → renvoyé à la base des joueurs à la déconnexion" (cf. section suivante) suggère qu'un système de hub/zone de dépôt existe déjà nativement dans le jeu — probablement plus approprié à réutiliser qu'à réinventer un point de spawn maison.
- `SaveManager.SetInventory()` / `SaveData.inventory : List<string>` est une liste **séparée** du dictionnaire clé/valeur `SaveData.entries` — piste à vérifier : c'est peut-être le vrai mécanisme de "objet porté/en possession du joueur" (plutôt que `PropHome`), ce qui changerait l'approche pour matérialiser un item reçu.
- Persistance tant que non-stashé : `Prop.Start()` relit `SaveManager` et repin le prop à **chaque** chargement (pas seulement au moment de l'octroi), donc tant que l'entrée de sauvegarde reste non-nulle — peu importe le home exact (hub, couffin, ou position finale) — l'item ne se perd jamais entre deux sessions, même si le joueur ne l'a pas "rangé" définitivement. Le détail précis de "où il atterrit exactement s'il n'est pas stashé" (logique de fallback à la déconnexion/rechargement) reste non identifié précisément en code — probablement une logique séparée de `Prop.Start()`, à creuser via Ghidra le jour où ce point de conception devra être tranché.

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

2. **Envoi d'un item à distance** (débloquer un gourd envoyé par un autre joueur) — **validé** (voir la section "RÉSOLU" plus haut sur `Prop.Start()`), remplace l'hypothèse initiale de `ServerSetGourdState(Stashed)` :
   - Zone chargée : `SaveManager.SetIntValue(key, homeValue)` + `Prop.ServerSetPinned(propHome)` sur l'instance vivante (trouvable via `GourdMap.GetFlag`/le registre `PropHome`) pour un effet immédiat.
   - Zone non chargée : `SaveManager.SetIntValue(key, homeValue)` seul suffit — `Prop.Start()` de ce prop lira la valeur et s'auto-épinglera tout seul à son prochain chargement, aucun code de mod supplémentaire nécessaire.

3. **Respect du modèle d'autorité host** : toute écriture doit se faire **côté host** (les méthodes `[Server]` le garantissent déjà — un hook côté client sur un client non-host serait ignoré ou risquerait une désync). Le mod Archipelago devra tourner côté host, ou transmettre ses ordres au host via un canal séparé si l'architecture du mod le sépare du process de jeu.

3bis. **Faire disparaître le gourd physique après un check** (validé en jeu le 2026-09-03, solo) : `RewardGourd.ServerSetGourdState(GourdState.Hidden)` seul ne fait que rafraîchir l'icône sur la carte (`GourdMap.refreshFlag`) — le modèle 3D reste visible et ramassable. `GameObject.SetActive(false)` seul serait purement local et désyncerait les autres clients en co-op (Mirror ne réplique pas ça). La combinaison qui marche : `Mirror.NetworkServer.UnSpawn(gameObject)` (retire l'objet réseau de la vue de tous les clients sans le détruire définitivement, contrairement à `NetworkServer.Destroy`) **puis** `gameObject.SetActive(false)` localement côté host.

   **Persistance confirmée** (même session de test, relance complète du jeu après coup) : après un redémarrage complet, le gourd ne réapparaît **pas** au panier/couffin proche de l'énigme (couffin visible mais vide) — l'état "déjà collecté" (`SaveManager` a bien écrit `gourdTellerWindow`/`gourdHighButton` avec une valeur non nulle) survit donc à un rechargement complet, sans code supplémentaire côté mod. Ça suggère que la logique de restauration au chargement (toujours pas identifiée précisément, cf. "non résolu" plus haut) respecte déjà l'état persistant et ne réinstancie pas un `RewardGourd` déjà marqué comme récupéré — bonne nouvelle pour la fidélité d'un futur rechargement de session Archipelago. Pas encore testé : reconnexion en cours de session co-op (seulement redémarrage complet testé), et le cas d'un gourd qui aurait été cette fois réellement stashé.

4. **Prochaine piste de recherche** (si nécessaire) : la restauration d'état au chargement d'une session existante — pour s'assurer qu'un check déjà validé côté Archipelago avant une reconnexion ne redemande pas sa collecte, ou pour repeupler l'état visuel correctement à la connexion.

## Fichiers de référence

- `il2cpp.cs` (export "C# prototypes") : structure complète de toutes les classes du jeu, utile pour grep rapide de nouveaux noms de classes/enums sans repasser par Ghidra.
- Projet Ghidra local (non fourni ici, sur la machine Windows) : contient les décompilations complètes, à consulter pour toute fonction non couverte par ce document.
