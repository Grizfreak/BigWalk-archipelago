<#
.SYNOPSIS
    Construit le mod et produit un zip « tout compris » à donner à un autre
    joueur : BepInEx (IL2CPP) + le plugin, à extraire dans le dossier du jeu.

.DESCRIPTION
    Le zip contient exactement ce qui manque à une installation Steam vierge :

      doorstop_config.ini, winhttp.dll, dotnet\      <- le loader BepInEx
      BepInEx\core\, BepInEx\unity-libs\, patchers\  <- BepInEx lui-même
      BepInEx\plugins\BigWalkArchipelago\            <- le mod (3 DLL)
      LISEZMOI.txt                                   <- instructions

    Volontairement PAS inclus :
      - BepInEx\interop\ et BepInEx\cache\ : générés au premier lancement, et
        liés à la version exacte du jeu installée sur la machine cible ;
      - BepInEx\config\ : le .cfg du mod se régénère seul, et celui d'ici
        contient l'adresse du serveur Archipelago du développeur ;
      - les logs.

    BepInEx est repris de l'installation locale du jeu (même build que celui
    sur lequel le mod a été testé, 6.0.0-be.781) plutôt que téléchargé : c'est
    la seule façon de garantir au pote la version exacte qui fonctionne.

.PARAMETER GameDir
    Installation du jeu servant de source pour BepInEx.

.PARAMETER Configuration
    Configuration de build (Debug par défaut : c'est celle qui a été testée).

.PARAMETER OutDir
    Où déposer le zip.
#>
[CmdletBinding()]
param(
    [string]$GameDir = 'F:\SteamLibrary\steamapps\common\Big Walk',
    [string]$Configuration = 'Debug',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$sln = Join-Path $repo 'mod\BigWalkArchipelago.sln'
$buildOut = Join-Path $repo "mod\src\bin\$Configuration\net6.0"

if (-not (Test-Path $GameDir)) {
    throw "Dossier du jeu introuvable : $GameDir"
}
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))) {
    throw "BepInEx IL2CPP absent de $GameDir — rien à empaqueter."
}

Write-Host '== Build ==' -ForegroundColor Cyan
dotnet build $sln -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build échoué.' }

# Les trois DLL à déployer. En oublier une fait échouer le chargement du mod
# entier, pas seulement la connexion (BepInEx résout les dépendances d'un
# plugin dans son propre dossier) — cf. mod/README.md.
$pluginDlls = @(
    'BigWalkArchipelago.dll',
    'Archipelago.MultiClient.Net.dll',
    'Newtonsoft.Json.dll'
)

$version = (Get-Item (Join-Path $buildOut 'BigWalkArchipelago.dll')).VersionInfo.FileVersion
$stamp = Get-Date -Format 'yyyyMMdd'
$stage = Join-Path $env:TEMP "bigwalk-ap-package-$stamp-$PID"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

Write-Host '== Assemblage ==' -ForegroundColor Cyan

# Loader (Doorstop) à la racine du jeu.
Copy-Item (Join-Path $GameDir 'doorstop_config.ini') $stage
Copy-Item (Join-Path $GameDir 'winhttp.dll') $stage
Copy-Item (Join-Path $GameDir 'dotnet') (Join-Path $stage 'dotnet') -Recurse

# BepInEx, sans interop/ ni cache/ ni config/ ni logs.
foreach ($sub in @('core', 'unity-libs', 'patchers')) {
    $src = Join-Path $GameDir "BepInEx\$sub"
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $stage "BepInEx\$sub") -Recurse
    }
}

$pluginDir = Join-Path $stage 'BepInEx\plugins\BigWalkArchipelago'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
foreach ($dll in $pluginDlls) {
    $src = Join-Path $buildOut $dll
    if (-not (Test-Path $src)) { throw "DLL manquante dans la sortie de build : $dll" }
    Copy-Item $src $pluginDir
}

$readme = @'
Big Walk — mod Archipelago
==========================

Installation
------------
1. Fermer le jeu.
2. Ouvrir le dossier d'installation du jeu. Sur Steam :
   clic droit sur Big Walk > Gérer > Parcourir les fichiers locaux.
   (Il doit contenir « Big Walk.exe ».)
3. Extraire TOUT le contenu de ce zip dedans, en fusionnant les dossiers.
   On doit obtenir, à côté de « Big Walk.exe » :
     winhttp.dll, doorstop_config.ini, dotnet\, BepInEx\
4. Lancer le jeu PAR STEAM (pas en double-cliquant l'exe : sur certaines
   machines un lancement direct ne charge pas le mod).
5. Le premier lancement est long (une à deux minutes d'écran noir) : BepInEx
   génère son cache. Les suivants sont normaux.

Vérifier que ça a marché
------------------------
Le fichier BepInEx\LogOutput.log doit contenir une ligne
« Big Walk Archipelago v... loaded. ».

Jouer à deux
------------
- Les deux joueurs installent ce zip, la même version du mod et la même
  version du jeu.
- L'invité n'a rien à configurer : seul l'hôte saisit les identifiants
  Archipelago sur l'écran d'hébergement, et c'est sa sauvegarde qui retient
  les checks, les items et les dépôts. Garder le même hôte pour toute la
  partie.

Désinstaller
------------
Supprimer winhttp.dll, doorstop_config.ini, dotnet\ et BepInEx\ du dossier
du jeu. Le jeu redevient parfaitement vanilla (rien n'est modifié dans ses
propres fichiers).
'@
Set-Content -Path (Join-Path $stage 'LISEZMOI.txt') -Value $readme -Encoding utf8

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
$zip = Join-Path (Resolve-Path $OutDir) "BigWalkArchipelago-$version-$stamp.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host '== Compression ==' -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ''
Write-Host "Paquet prêt : $zip ($size Mo)" -ForegroundColor Green
