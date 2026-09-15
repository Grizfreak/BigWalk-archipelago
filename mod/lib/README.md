# lib/

Ce dossier contient des copies locales de DLL nécessaires à la compilation,
**non commitées dans git** (voir `.gitignore`). Elles proviennent toutes de
l'installation réelle du jeu avec BepInEx installé — jamais des DLL "dummy"
générées par Il2CppInspectorRedux (celles-ci ne sont pas invocables au runtime,
seulement utiles pour la lecture statique du code dans `BW_export/il2cpp.cs`).

Sur cette machine, le jeu est installé ici :
```
F:\SteamLibrary\steamapps\common\Big Walk\
```
avec BepInEx déjà initialisé (build `6.0.0-be.781`, runtime `.NET 6.0.7`, IL2CPP —
voir `BepInEx\LogOutput.log` dans ce dossier pour confirmation). **Important** :
lancer via Steam (`steam://run/1478500`), pas en exécutant l'exe directement —
confirmé en session le 2026-09-15, un lancement direct de l'exe ne charge pas
BepInEx (aucune trace dans `LogOutput.log`) sur cette machine. Il existe aussi
une copie du jeu dans `F:\Games\Big Walk\` (jamais lancée par Steam, à ignorer
pour le déploiement).

## DLL actuellement copiées

Depuis `<jeu>\BepInEx\core\` (étape 1) :
- `BepInEx.Core.dll`
- `BepInEx.Unity.IL2CPP.dll`
- `0Harmony.dll`

Depuis `<jeu>\BepInEx\interop\` (générées par Il2CppInterop au premier
lancement — PAS les dummy DLL) :
- `Assembly-CSharp.dll` (étape 2) — types du jeu (`SaveablePropName`, `RewardGourd`,
  `Prop`, `SaveManager`, ...)
- `Mirror.dll` (étape 3) — `NetworkBehaviour`, base de `RewardGourd`/`Prop`
- `UnityEngine.CoreModule.dll` (étape 3) — `MonoBehaviour`, base de `NetworkBehaviour`
- `Il2Cppmscorlib.dll` (étape 3) — `Il2CppSystem.Object`, base de `UnityEngine.Object`
- `Unity.TextMeshPro.dll` (étape 4, 2026-09-15) — `TMP_InputField` (`HostMenuConfirm.gameNameField`/`passwordField`), pour `Debug/DebugMenuLookup.cs`
- `UnityEngine.UI.dll` (étape 4, 2026-09-15) — `Selectable`, classe de base de `TMP_InputField`
- `UnityEngine.IMGUIModule.dll` (étape 5, 2026-09-15) — `OnGUI`/`GUI.Label`, pour le témoin d'état Archipelago à l'écran (`Core/Net/ApStatusOverlay.cs`)

Depuis `<jeu>\BepInEx\core\` (étape 3) :
- `Il2CppInterop.Runtime.dll` — `Il2CppObjectBase`, base de `Il2CppSystem.Object`

Cette chaîne de dépendances (`Object` → `Il2CppSystem.Object` → `Il2CppObjectBase`,
`MonoBehaviour` → `NetworkBehaviour` → `Prop`/`RewardGourd`) est typique de l'interop
IL2CPP : référencer un type du jeu oblige à référencer toute sa hiérarchie de classes
de base jusqu'à `Il2CppObjectBase`, chacune dans sa propre DLL.

**Important** : `<Nullable>disable</Nullable>` dans le csproj n'est pas cosmétique —
ces DLL interop embarquent leur propre `System.Runtime.CompilerServices.NullableAttribute`
incomplet (sans tous les constructeurs), ce qui fait échouer la compilation (CS0656)
si Nullable Reference Types est actif dès qu'on référence l'une d'elles.

## À ajouter aux étapes suivantes

- Autres DLL `UnityEngine.*Module.dll` (physique, input, caméra) au besoin,
  selon ce que touche `Debug/DebugFlightTool.cs`.

## Si tu changes de machine ou que le jeu est mis à jour

Recopie les DLL depuis le dossier `BepInEx/` du jeu **après** avoir lancé le jeu
au moins une fois avec BepInEx installé (c'est ce premier lancement qui génère
`BepInEx/interop/` via Il2CppInterop). Une version de jeu différente peut changer
la signature de certaines méthodes — recompiler après mise à jour du jeu.
