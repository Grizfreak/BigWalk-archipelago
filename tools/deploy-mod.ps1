<#
.SYNOPSIS
    Construit le mod et le déploie dans toutes les installations du jeu
    connues, puis vérifie qu'elles portent bien le même binaire.

.DESCRIPTION
    Remplace la boucle « package-mod.ps1 → envoyer le zip → l'autre joueur
    l'installe », qui a produit trois tests faussés en un week-end : à
    chaque fois, l'invité tournait sur un build antérieur et reproduisait
    un bug déjà corrigé. Le dossier de la seconde machine étant accessible
    en réseau local, les deux côtés se déploient désormais ensemble.

    L'empreinte SHA-256 affichée à la fin n'est pas décorative : c'est le
    seul moyen de savoir que les deux joueurs exécutent réellement le même
    code avant de mesurer quoi que ce soit. Deux lignes identiques, et un
    écart de comportement entre les deux machines est un vrai bug ; deux
    lignes différentes, et il n'y a rien à conclure.

    `package-mod.ps1` reste nécessaire pour quelqu'un dont le dossier n'est
    pas accessible : il embarque BepInEx, ce que ce script ne fait pas.

.PARAMETER Configuration
    Configuration de build (Debug par défaut : c'est celle qui est testée).

.PARAMETER Targets
    Dossiers d'installation du jeu. Par défaut : la machine hôte, la
    machine invitée via le partage réseau, et la copie Steam. Ceux qui
    n'existent pas sont signalés et ignorés, pas une erreur — toutes les
    machines ne sont pas allumées en même temps.

.PARAMETER SkipBuild
    Déploie la sortie de build existante sans reconstruire.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [string[]]$Targets = @(
        'F:\Games\Big Walk',
        'F:\shared\Big Walk',
        'F:\SteamLibrary\steamapps\common\Big Walk'
    ),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$sln = Join-Path $repo 'mod\BigWalkArchipelago.sln'
$buildOut = Join-Path $repo "mod\src\bin\$Configuration\net6.0"

# Les trois DLL à déployer. En oublier une fait échouer le chargement du mod
# entier, pas seulement la connexion : BepInEx résout les dépendances d'un
# plugin dans son propre dossier (cf. mod/README.md).
$dlls = @(
    'BigWalkArchipelago.dll',
    'Archipelago.MultiClient.Net.dll',
    'Newtonsoft.Json.dll'
)

if (-not $SkipBuild) {
    Write-Host '== Build ==' -ForegroundColor Cyan
    dotnet build $sln -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build échoué.' }
}

foreach ($dll in $dlls) {
    if (-not (Test-Path (Join-Path $buildOut $dll))) {
        throw "DLL manquante dans la sortie de build : $dll"
    }
}

Write-Host ''
Write-Host '== Déploiement ==' -ForegroundColor Cyan

$results = @()

foreach ($target in $Targets) {
    if (-not (Test-Path $target)) {
        Write-Host ("  {0,-45} absent, ignoré" -f $target) -ForegroundColor DarkGray
        continue
    }

    $pluginDir = Join-Path $target 'BepInEx\plugins\BigWalkArchipelago'
    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }

    $failed = $null
    foreach ($dll in $dlls) {
        try {
            Copy-Item (Join-Path $buildOut $dll) $pluginDir -Force
        }
        catch {
            # Cas courant et sans gravité : le jeu tourne et tient la DLL.
            # Dit explicitement plutôt que laissé à l'interprétation, parce
            # qu'un déploiement silencieusement partiel est exactement ce
            # qu'on cherche à éliminer.
            $failed = $_.Exception.Message
            break
        }
    }

    if ($failed) {
        Write-Host ("  {0,-45} ÉCHEC — le jeu tourne-t-il encore ?" -f $target) -ForegroundColor Red
        Write-Host "      $failed" -ForegroundColor DarkRed
        continue
    }

    $hash = (Get-FileHash (Join-Path $pluginDir 'BigWalkArchipelago.dll') -Algorithm SHA256).Hash
    $results += [pscustomobject]@{ Target = $target; Hash = $hash }
    Write-Host ("  {0,-45} OK" -f $target) -ForegroundColor Green
}

Write-Host ''

if ($results.Count -eq 0) {
    Write-Host 'Aucune installation mise à jour.' -ForegroundColor Red
    exit 1
}

# Forcé en tableau : avec une seule empreinte distincte, Select-Object
# rend une chaîne, et [0] indexerait son premier caractère.
$distinct = @($results.Hash | Select-Object -Unique)
Write-Host ("Empreinte du mod : {0}" -f $distinct[0].Substring(0, 16))

if ($distinct.Count -eq 1) {
    Write-Host ("{0} installation(s) à jour et identiques." -f $results.Count) -ForegroundColor Green
} else {
    # Ne devrait pas arriver puisque tout vient de la même sortie de build,
    # mais le dire vaut mieux que de le supposer.
    Write-Host 'ATTENTION : les installations ne portent pas le même binaire.' -ForegroundColor Red
    $results | ForEach-Object { "  {0}  {1}" -f $_.Hash.Substring(0, 16), $_.Target }
}
