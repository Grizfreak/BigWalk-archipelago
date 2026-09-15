# Architecture — BigWalkArchipelago (mod BepInEx IL2CPP)

## Principe directeur

Séparer strictement trois responsabilités qui n'ont aucune raison d'être couplées :

1. **Détection** — repérer qu'un check vient d'être validé en jeu (hooks Harmony)
2. **Reporting** — décider quoi faire de cette info (pour l'instant : logguer ; plus tard : parler au serveur Archipelago)
3. **Debug/dev tools** — outils de confort pour toi pendant le développement (vol, vitesse), complètement indépendants des deux premiers

Le but : le jour où tu branches le vrai client réseau Archipelago, tu n'as **aucune ligne à toucher** dans les hooks du jeu — tu écris juste une nouvelle implémentation de l'interface de reporting.

## Structure de dossiers

```
BigWalkArchipelago/
├── BigWalkArchipelago.sln
├── src/
│   ├── Plugin.cs                      # Point d'entrée BasePlugin, charge Harmony + tous les modules
│   │
│   ├── Core/
│   │   ├── GourdRegistry.cs           # Table statique SaveablePropName <-> id de location AP
│   │   ├── ICheckReporter.cs          # Interface : void ReportCheck(string locationId)
│   │   └── LocalLogReporter.cs        # Impl #1 (POC actuel) : logue juste dans la console BepInEx
│   │
│   ├── Patches/
│   │   ├── GourdStatePatch.cs         # Postfix sur RewardGourd.ServerSetGourdState
│   │   │                              #   → transition vers Stashed = check validé
│   │   └── SaveValuePatch.cs          # Filet de sécurité : postfix sur SaveManager.SetIntValue
│   │                                  #   → capte tout check même si un autre chemin de code y mène
│   │
│   ├── Debug/
│   │   ├── DebugFlightTool.cs         # Bascule vol/vitesse (basé sur PlayerCheater à investiguer,
│   │   │                              #   ou implémentation maison via PlayerMover/Rigidbody si le
│   │   │                              #   système interne s'avère trop verrouillé/insuffisant)
│   │   └── DebugHotkeys.cs            # Écoute des touches de raccourci, actif seulement si
│   │                                  #   Config.DebugModeEnabled == true
│   │
│   └── Config.cs                      # BepInEx.Configuration : DebugModeEnabled, futurs
│                                       #   AP.ServerAddress / AP.SlotName / AP.Password
│
├── lib/                               # Références locales, PAS commitées dans git
│   └── (copie de BepInEx/interop/Assembly-CSharp.dll depuis le dossier du jeu)
│
└── README.md                          # Instructions de build + déploiement
```

## Pourquoi cette séparation

- **`Patches/` ne connaît que `ICheckReporter`**, jamais `LocalLogReporter` directement. Un patch appelle `Plugin.Reporter.ReportCheck(id)` sans savoir ce qui se passe derrière.
- **`GourdRegistry`** centralise la traduction "identifiant interne du jeu" (`saveablePropName`, ex. `gourdCabinFever`) ↔ "identifiant de location Archipelago". Aujourd'hui c'est une table 1:1 triviale ; plus tard, si tu veux exclure certains gourds de test (`gourdTesting00-39`) ou gérer les variantes/big keys différemment, c'est le seul endroit à modifier.
- **`Debug/` ne dépend de rien d'autre** — tu dois pouvoir désactiver tout le module de debug (un simple bool dans `Config.cs`) sans toucher au reste, et inversement livrer une version sans les outils de triche activés.
- **Le filet de sécurité `SaveValuePatch`** existe parce qu'on a confirmé pendant la session de reverse engineering qu'on ne connaît pas tous les chemins de code qui peuvent mener à `ServerSetGourdState` (le déclenchement exact depuis la résolution d'énigme reste flou — UnityEvent non tracé statiquement). Avoir deux points de détection qui convergent vers le même `ICheckReporter` sécurise contre un chemin de déclenchement qu'on n'aurait pas anticipé.

## Étapes de mise en œuvre suggérées (dans cet ordre)

1. Scaffolding du projet + `Plugin.cs` minimal qui charge Harmony (reprendre `GourdInterceptPOC.cs` comme base pour `GourdStatePatch.cs`, mais **remplacer l'action "cacher le gourd" par un simple log** — le POC précédent testait l'interception, pas encore le comportement final voulu)
2. `ICheckReporter` + `LocalLogReporter` + `GourdRegistry`
3. Brancher `GourdStatePatch` sur cette interface
4. `Debug/DebugFlightTool` — décompiler `PlayerCheater.CheckForCheat` et `CameraCheatMover` pour voir s'il y a un mode fantôme réutilisable tel quel avant d'en coder un maison
5. `SaveValuePatch` en filet de sécurité une fois le reste stable
