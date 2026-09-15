# BigWalkArchipelago

Mod BepInEx (IL2CPP, Harmony) pour *Big Walk* (House House). Objectif final :
brancher un client Archipelago (multiworld randomizer) sur la boucle de jeu.
Voir [architecture-mod.md](architecture-mod.md) pour la conception détaillée.

## Build

Prérequis : .NET SDK (testé avec 10.0.301), DLL locales dans `lib/` — voir
[lib/README.md](lib/README.md) pour la provenance exacte de chaque fichier.

```
dotnet build BigWalkArchipelago.sln
```

Cible `net6.0` pour matcher le runtime BepInEx IL2CPP embarqué dans le jeu
(`.NET 6.0.7`, confirmé dans `BepInEx/LogOutput.log`).

## Déploiement

Copier `src/bin/Debug/net6.0/BigWalkArchipelago.dll` dans :
```
<dossier du jeu>/BepInEx/plugins/BigWalkArchipelago/
```

Sur cette machine, le jeu est installé dans :
```
F:\SteamLibrary\steamapps\common\Big Walk\
```
(lancer via Steam, `steam://run/1478500` — un lancement direct de l'exe ne charge pas BepInEx sur cette machine).

## État d'avancement

- [x] 1. Scaffolding + `Plugin.cs` minimal chargeant Harmony
- [x] 2. `ICheckReporter` + `LocalLogReporter` + `GourdRegistry`
- [x] 3. Brancher `GourdStatePatch` sur `ICheckReporter`
- [x] 4. `Debug/DebugFlightTool` (investigation `PlayerCheater`/`CameraCheatMover`)
- [x] 5. `SaveValuePatch` en filet de sécurité
