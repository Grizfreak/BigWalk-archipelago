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

**Non résolu** : le point exact où l'état est *relu* pour repositionner visuellement les `RewardGourd` au chargement d'une partie. `RewardGourd.OnSpawn(isInventory)` ne fait que forcer l'état à `Loose` si le gourd est un item d'inventaire flottant — ce n'est pas le point de restauration principal. Piste probable : le système `PropHome`/`Prop` doit itérer les homes savables au démarrage et déclencher `onPinServer`/`onChangeServer`, mais la fonction précise n'a pas été identifiée. **À creuser si besoin d'une restauration fidèle de l'état lors de la connexion à une session Archipelago.**

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

2. **Envoi d'un item à distance** (débloquer un gourd envoyé par un autre joueur) : nécessite probablement un double appel —
   - `SaveManager.SetIntValue(key, homeSlotId)` pour la persistance disque
   - **et** `RewardGourd.ServerSetGourdState(GourdState.Stashed)` sur l'instance vivante correspondante pour que ça se reflète visuellement/en jeu immédiatement (sans ça, le changement ne sera visible qu'au prochain redémarrage/rechargement de zone)
   - Alternative plus légère à explorer : invoquer directement l'event statique `GourdMap.refreshFlag` pour forcer un rafraîchissement visuel sans repasser par tout le pipeline réseau.

3. **Respect du modèle d'autorité host** : toute écriture doit se faire **côté host** (les méthodes `[Server]` le garantissent déjà — un hook côté client sur un client non-host serait ignoré ou risquerait une désync). Le mod Archipelago devra tourner côté host, ou transmettre ses ordres au host via un canal séparé si l'architecture du mod le sépare du process de jeu.

3bis. **Faire disparaître le gourd physique après un check** (validé en jeu le 2026-09-03, solo) : `RewardGourd.ServerSetGourdState(GourdState.Hidden)` seul ne fait que rafraîchir l'icône sur la carte (`GourdMap.refreshFlag`) — le modèle 3D reste visible et ramassable. `GameObject.SetActive(false)` seul serait purement local et désyncerait les autres clients en co-op (Mirror ne réplique pas ça). La combinaison qui marche : `Mirror.NetworkServer.UnSpawn(gameObject)` (retire l'objet réseau de la vue de tous les clients sans le détruire définitivement, contrairement à `NetworkServer.Destroy`) **puis** `gameObject.SetActive(false)` localement côté host.

   **Persistance confirmée** (même session de test, relance complète du jeu après coup) : après un redémarrage complet, le gourd ne réapparaît **pas** au panier/couffin proche de l'énigme (couffin visible mais vide) — l'état "déjà collecté" (`SaveManager` a bien écrit `gourdTellerWindow`/`gourdHighButton` avec une valeur non nulle) survit donc à un rechargement complet, sans code supplémentaire côté mod. Ça suggère que la logique de restauration au chargement (toujours pas identifiée précisément, cf. "non résolu" plus haut) respecte déjà l'état persistant et ne réinstancie pas un `RewardGourd` déjà marqué comme récupéré — bonne nouvelle pour la fidélité d'un futur rechargement de session Archipelago. Pas encore testé : reconnexion en cours de session co-op (seulement redémarrage complet testé), et le cas d'un gourd qui aurait été cette fois réellement stashé.

4. **Prochaine piste de recherche** (si nécessaire) : la restauration d'état au chargement d'une session existante — pour s'assurer qu'un check déjà validé côté Archipelago avant une reconnexion ne redemande pas sa collecte, ou pour repeupler l'état visuel correctement à la connexion.

## Fichiers de référence

- `il2cpp.cs` (export "C# prototypes") : structure complète de toutes les classes du jeu, utile pour grep rapide de nouveaux noms de classes/enums sans repasser par Ghidra.
- Projet Ghidra local (non fourni ici, sur la machine Windows) : contient les décompilations complètes, à consulter pour toute fonction non couverte par ce document.
