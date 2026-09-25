<#
.SYNOPSIS
    Builds the mod and produces the all-in-one zip to hand to another player:
    BepInEx (IL2CPP) plus the plugin, to extract into the game folder.

.DESCRIPTION
    The zip holds exactly what a clean Steam installation is missing:

      doorstop_config.ini, winhttp.dll, dotnet\      <- the BepInEx loader
      BepInEx\core\, BepInEx\patchers\              <- BepInEx itself
      BepInEx\plugins\BigWalkArchipelago\            <- the mod (3 DLLs)
      BepInEx\licenses\                              <- every bundled license
      README.txt, THIRD-PARTY-NOTICES.txt            <- instructions, credits

    Deliberately NOT included:
      - BepInEx\unity-libs\: Unity's own engine assemblies, which belong to
        Unity Technologies and are not ours to redistribute. BepInEx
        downloads them itself on the first launch (UnityBaseLibrariesSource
        in BepInEx.cfg) - the dev install's copy even holds the archive it
        fetched. Shipped until 2026-09-25, when preparing the first public
        release turned up that they were in the zip at all;
      - BepInEx\interop\ and BepInEx\cache\: generated on the first launch,
        and tied to the exact game version installed on the target machine;
      - BepInEx\config\: the mod's .cfg regenerates itself, and the one here
        holds the developer's own Archipelago server address;
      - the logs.

    BepInEx is taken from the local installation of the game - the same build
    the mod was tested against, 6.0.0-be.781 - rather than downloaded. That
    is the only way to guarantee the other player the exact version that
    works.

.PARAMETER GameDir
    The game installation to take BepInEx from.

.PARAMETER Configuration
    Build configuration (Debug by default: it is the one that gets tested).

.PARAMETER OutDir
    Where to drop the zip.
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
    throw "Game folder not found: $GameDir"
}
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))) {
    throw "No BepInEx IL2CPP in $GameDir - nothing to package."
}

Write-Host '== Build ==' -ForegroundColor Cyan
dotnet build $sln -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# The three DLLs to deploy. Leaving one out fails the load of the whole
# mod, not just the connection (BepInEx resolves a plugin's dependencies
# inside that plugin's own folder) - see mod/README.md.
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

Write-Host '== Staging ==' -ForegroundColor Cyan

# The loader (Doorstop), at the root of the game folder.
Copy-Item (Join-Path $GameDir 'doorstop_config.ini') $stage
Copy-Item (Join-Path $GameDir 'winhttp.dll') $stage
Copy-Item (Join-Path $GameDir 'dotnet') (Join-Path $stage 'dotnet') -Recurse

# BepInEx, without interop/, cache/, config/ or the logs.
foreach ($sub in @('core', 'patchers')) {
    $src = Join-Path $GameDir "BepInEx\$sub"
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $stage "BepInEx\$sub") -Recurse
    }
}

# Every license the package owes, and the index that says which is whose
# (mod\packaging\THIRD-PARTY-NOTICES.txt). BepInEx, Doorstop and
# Il2CppInterop are LGPL, and every other part asks for its license text to
# travel with the binaries.
$packaging = Join-Path $repo 'mod\packaging'
Copy-Item (Join-Path $packaging 'THIRD-PARTY-NOTICES.txt') $stage
Copy-Item (Join-Path $packaging 'licenses') (Join-Path $stage 'BepInEx\licenses') -Recurse
# The plugin's own license, from the repository root.
Copy-Item (Join-Path $repo 'LICENSE') (Join-Path $stage 'BepInEx\licenses\BigWalkArchipelago.MIT.txt')

$pluginDir = Join-Path $stage 'BepInEx\plugins\BigWalkArchipelago'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
foreach ($dll in $pluginDlls) {
    $src = Join-Path $buildOut $dll
    if (-not (Test-Path $src)) { throw "DLL missing from the build output: $dll" }
    Copy-Item $src $pluginDir
}

$readme = @'
Big Walk - Archipelago mod
==========================

Installation
------------
1. Close the game.
2. Open the game's install folder. On Steam: right-click Big Walk >
   Manage > Browse local files. It is the folder that contains
   "Big Walk.exe".
3. Extract EVERYTHING in this zip into it, merging folders. Next to
   "Big Walk.exe" you should end up with:
     winhttp.dll, doorstop_config.ini, dotnet\ and BepInEx\
4. Launch the game THROUGH STEAM (not by double-clicking the executable:
   on some machines a direct launch does not load the mod).
5. The first launch is slow - a minute or two of black screen - while
   BepInEx downloads the Unity libraries it needs and builds its cache,
   so be online for it. The launches after it are normal.

Checking that it worked
-----------------------
BepInEx\LogOutput.log should contain a line reading
"Big Walk Archipelago v... loaded.".

Playing together
----------------
- Every player installs this zip, the same mod version and the same game
  version.
- Guests configure nothing: only the host types the Archipelago details on
  the hosting screen, and it is their save that holds the checks, the items
  and the deposits. Keep the same host for the whole game.

Uninstalling
------------
Delete winhttp.dll, doorstop_config.ini, dotnet\, BepInEx\, README.txt and
THIRD-PARTY-NOTICES.txt from the game folder. The game goes back to being
perfectly vanilla - nothing in its own files is ever modified.

Credits
-------
The Big Walk Archipelago plugin is released under the MIT license
(BepInEx\licenses\BigWalkArchipelago.MIT.txt). This package also bundles BepInEx, Doorstop, Il2CppInterop, the .NET runtime and
their libraries, each under its own license: see THIRD-PARTY-NOTICES.txt and
BepInEx\licenses\.
'@
Set-Content -Path (Join-Path $stage 'README.txt') -Value $readme -Encoding utf8

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
$zip = Join-Path (Resolve-Path $OutDir) "BigWalkArchipelago-$version-$stamp.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host '== Compressing ==' -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ''
Write-Host "Package ready: $zip ($size MB)" -ForegroundColor Green
