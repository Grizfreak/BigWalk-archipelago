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

Copier **trois** DLL de `src/bin/Debug/net6.0/` dans
`<dossier du jeu>/BepInEx/plugins/BigWalkArchipelago/` :

- `BigWalkArchipelago.dll` — le mod
- `Archipelago.MultiClient.Net.dll` — le client Archipelago officiel
- `Newtonsoft.Json.dll` — embarqué par le paquet précédent

Les deux dernières viennent du NuGet `Archipelago.MultiClient.Net` et sont
copiées automatiquement dans la sortie de build. BepInEx résout les
dépendances d'un plugin dans son propre dossier : oublier l'une des deux fait
échouer le chargement du mod entier, pas seulement la connexion.

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
- [x] 6. Client réseau Archipelago (`src/Core/Net/`) — connexion, réception
      d'items, report des checks, goal. Contrat et validation :
      [`../apworld/protocol.md`](../apworld/protocol.md)
- [x] 7. Chargement et connexion depuis le jeu confirmés (2026-09-15)
- [x] 8. Tous les chemins éprouvés en jeu (2026-09-15) : check sortant, items,
      big keys, dépôts, goal, gourdes reconstruites, coupure réseau, save neuve
- [ ] 9. Partie complète, et première session à deux joueurs

## Configuration Archipelago

Section `[Archipelago]` du `.cfg` : `Enabled` (coupe la connexion et revient
au simple log local des checks) et `HostPort` (mémorise la dernière adresse
saisie). Le slot name et le mot de passe sont les deux champs de l'écran
d'hébergement, relus depuis la sauvegarde.

Confort : `SpawnGourdAtPlayer` et `PutGourdInHands` (une gourde reçue en
cours de partie atterrit dans tes mains ou devant toi ; le réassort de
début de session va toujours au hub), `GourdColor`, `GourdRestoreInterval`,
`ShowConnectionStatus`.

`ResyncGourdsKey` (**Ctrl+R** par défaut, hôte connecté uniquement) balaie
toutes tes gourdes qui ne sont pas dans un monument et remet au hub le
nombre exact dû par le serveur. À utiliser quand l'une d'elles est devenue
inatteignable — coincée dans une salle scellée, par exemple. Les dépôts en
monument ne sont jamais touchés, donc rien de ce que compte Archipelago ne
peut être perdu.
