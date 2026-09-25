<#
.SYNOPSIS
    Builds the mod, deploys it to every known installation of the game, and
    checks that they all carry the same binary.

.DESCRIPTION
    Replaces the "package-mod.ps1 -> send the zip -> the other player
    installs it" loop, which produced three wasted tests in one weekend:
    every time, the guest was running an older build and reproducing a bug
    that had already been fixed. The second machine's folder is reachable
    over the local network, so both sides are now deployed together.

    The SHA-256 fingerprint printed at the end is not decoration: it is the
    only way to know that both players are really running the same code
    before measuring anything. Two identical lines, and a difference in
    behaviour between the two machines is a real bug; two different lines,
    and there is nothing to conclude.

    `package-mod.ps1` is still needed for anyone whose folder is not
    reachable: it carries BepInEx, which this script does not.

.PARAMETER Configuration
    Build configuration (Debug by default: it is the one that gets tested).

.PARAMETER Targets
    The game's installation folders. By default: the host machine, the guest
    machine over the network share, and the Steam copy. Any that do not
    exist are reported and skipped rather than treated as an error - the
    machines are not all switched on at the same time.

.PARAMETER SharedDrop
    A folder that also receives BigWalkArchipelago.dll on its own, for the
    other machine to pick up (F:\shared by default). Skipped if absent.

.PARAMETER SkipBuild
    Deploys the existing build output without rebuilding.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [string[]]$Targets = @(
        'F:\Games\Big Walk',
        'F:\shared\Big Walk',
        'F:\SteamLibrary\steamapps\common\Big Walk'
    ),
    [string]$SharedDrop = 'F:\shared',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$sln = Join-Path $repo 'mod\BigWalkArchipelago.sln'
$buildOut = Join-Path $repo "mod\src\bin\$Configuration\net6.0"

# The three DLLs to deploy. Leaving one out fails the load of the whole mod,
# not just the connection: BepInEx resolves a plugin's dependencies inside
# that plugin's own folder (see mod/README.md).
$dlls = @(
    'BigWalkArchipelago.dll',
    'Archipelago.MultiClient.Net.dll',
    'Newtonsoft.Json.dll'
)

if (-not $SkipBuild) {
    Write-Host '== Build ==' -ForegroundColor Cyan
    dotnet build $sln -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

foreach ($dll in $dlls) {
    if (-not (Test-Path (Join-Path $buildOut $dll))) {
        throw "DLL missing from the build output: $dll"
    }
}

Write-Host ''
Write-Host '== Deploy ==' -ForegroundColor Cyan

$results = @()

foreach ($target in $Targets) {
    if (-not (Test-Path $target)) {
        Write-Host ("  {0,-45} not found, skipped" -f $target) -ForegroundColor DarkGray
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
            # Common and harmless: the game is running and holding the DLL.
            # Said out loud rather than left to interpretation, because a
            # silently partial deployment is exactly what this script exists
            # to eliminate.
            $failed = $_.Exception.Message
            break
        }
    }

    if ($failed) {
        Write-Host ("  {0,-45} FAILED - is the game still running?" -f $target) -ForegroundColor Red
        Write-Host "      $failed" -ForegroundColor DarkRed
        continue
    }

    $hash = (Get-FileHash (Join-Path $pluginDir 'BigWalkArchipelago.dll') -Algorithm SHA256).Hash
    $results += [pscustomobject]@{ Target = $target; Hash = $hash }
    Write-Host ("  {0,-45} OK" -f $target) -ForegroundColor Green
}

# The second machine takes its copy from the root of the share, not from the
# game folder under it (player, 2026-09-25): the DLL goes there on every
# deploy, so a guest update is one copy on their side.
if ($SharedDrop -and (Test-Path $SharedDrop)) {
    try {
        Copy-Item (Join-Path $buildOut 'BigWalkArchipelago.dll') $SharedDrop -Force
        Write-Host ("  {0,-45} OK (DLL for the other machine)" -f $SharedDrop) -ForegroundColor Green
    }
    catch {
        Write-Host ("  {0,-45} FAILED" -f $SharedDrop) -ForegroundColor Red
        Write-Host "      $($_.Exception.Message)" -ForegroundColor DarkRed
    }
}

Write-Host ''

if ($results.Count -eq 0) {
    Write-Host 'No installation was updated.' -ForegroundColor Red
    exit 1
}

# Forced into an array: with a single distinct fingerprint, Select-Object
# returns a string, and [0] would index its first character.
$distinct = @($results.Hash | Select-Object -Unique)
Write-Host ("Mod fingerprint: {0}" -f $distinct[0].Substring(0, 16))

if ($distinct.Count -eq 1) {
    Write-Host ("{0} installation(s) up to date and identical." -f $results.Count) -ForegroundColor Green
} else {
    # Should not happen, since everything comes from the same build output,
    # but saying so beats assuming it.
    Write-Host 'WARNING: the installations do not carry the same binary.' -ForegroundColor Red
    $results | ForEach-Object { "  {0}  {1}" -f $_.Hash.Substring(0, 16), $_.Target }
}
