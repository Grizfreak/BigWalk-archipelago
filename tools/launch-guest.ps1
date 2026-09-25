<#
.SYNOPSIS
    Starts a second instance of the game on this PC, as the loopback guest.

.DESCRIPTION
    Testing co-op used to take two machines. The host (the ordinary
    instance, launched through Steam) listens on UDP 7777 as well as EOS
    whenever Steam is up: NetworkMinder.StartHost puts a MultiplexTransport
    over [KcpTransport, EosTransport], measured with Ctrl+N and
    Get-NetUDPEndpoint on 2026-09-25. So a second instance of the same
    install can join it as a real Mirror client over 127.0.0.1, and every
    replication bug a real guest meets is there to be met.

    This script starts that second instance. It is launched straight from
    the exe, because Steam refuses to launch a game twice, and three things
    make that work:

    - SteamAppId / SteamGameId in the guest's environment. Without them,
      SteamManager.Awake calls SteamAPI_RestartAppIfNecessary, which quits
      the process and asks Steam to start the game instead (decompiled
      2026-09-25) - most likely what "a direct launch does not load BepInEx"
      was on 2026-09-15. Steam sets these two variables itself when it
      launches a game; setting them here, for this process only, adds no
      file to the install.
    - --bwap-guest on the command line, which is how the mod knows its role
      (Debug/LoopbackGuest.cs). Never the .cfg: both instances read the same
      one.
    - The working directory set to the game folder, and no DOORSTOP_*
      variables inherited, so Doorstop resolves its relative paths and loads
      BepInEx.

    The guest runs windowed. Unity keeps the window mode and resolution in
    PlayerPrefs, which both instances share (HKCU\Software\House House\Big
    Walk), so the host could come back windowed next time. The Screenmanager
    values are saved before the launch and put back once the guest exits, by
    a hidden copy of this script that waits for it.

    Logs stay apart: BepInEx gives the second instance LogOutput.1.log when
    LogOutput.log is already open (it tries LogOutput.{0}.log), and Unity's
    log goes to Player-guest.log beside the usual Player.log.

.PARAMETER GameDir
    The installation both instances run from. It must be the Steam copy: the
    two sides of a Mirror session have to run exactly the same build.

.PARAMETER Width
    Width of the guest's window.

.PARAMETER Height
    Height of the guest's window.

.PARAMETER Force
    Starts the guest even if no host is running. The host has to be the
    first instance: Il2CppInterop regenerates BepInEx\interop\ on the first
    launch after a game update, and two instances doing it at once would
    write the same files.

.PARAMETER WatchGuest
    Internal: the process id the hidden watcher waits for before restoring
    the screen settings.

.PARAMETER Snapshot
    Internal: where the screen settings were saved.
#>
[CmdletBinding()]
param(
    [string]$GameDir = 'F:\SteamLibrary\steamapps\common\Big Walk',
    [int]$Width = 1280,
    [int]$Height = 720,
    [switch]$Force,
    [int]$WatchGuest = 0,
    [string]$Snapshot = ''
)

$ErrorActionPreference = 'Stop'

$appId = '1478500'
$prefsKey = 'HKCU:\Software\House House\Big Walk'
$unityLogDir = Join-Path (Split-Path $env:LOCALAPPDATA) 'LocalLow\House House\Big Walk'

# --- The hidden watcher: wait for the guest, then put the screen back. ---
if ($WatchGuest -ne 0) {
    $guest = Get-Process -Id $WatchGuest -ErrorAction SilentlyContinue
    if ($guest) { $guest.WaitForExit() }

    # Assigned before the loop, not wrapped in @(): Windows PowerShell's
    # ConvertFrom-Json writes a JSON array to the pipeline as ONE object, and
    # @() around it makes an array holding that array.
    $entries = Get-Content $Snapshot -Raw | ConvertFrom-Json
    foreach ($entry in $entries) {
        $value = $entry.Value
        if ($entry.Kind -eq 'Binary') { $value = [byte[]]$value }
        Set-ItemProperty -Path $prefsKey -Name $entry.Name -Value $value -Type $entry.Kind
    }
    Remove-Item $Snapshot -ErrorAction SilentlyContinue
    exit 0
}

$exe = Join-Path $GameDir 'Big Walk.exe'
if (-not (Test-Path $exe)) { throw "No game at $exe." }

# --- The host must already be up, and past the interop generation. ---
$running = @(Get-Process -Name 'Big Walk' -ErrorAction SilentlyContinue)
if ($running.Count -eq 0 -and -not $Force) {
    throw 'No host is running. Launch the game through Steam first and wait for its main menu (-Force to start the guest alone).'
}
if ($running.Count -ge 2) {
    Write-Host "Warning: $($running.Count) instances are already running; this makes one more." -ForegroundColor Yellow
}
if ($running.Count -ge 1) {
    $hostLog = Join-Path $GameDir 'BepInEx\LogOutput.log'
    $hostStart = ($running | Sort-Object StartTime | Select-Object -First 1).StartTime
    $ready = (Test-Path $hostLog) -and (Get-Item $hostLog).LastWriteTime -ge $hostStart -and
        (Select-String -Path $hostLog -Pattern 'Harmony initialized' -Quiet)
    if (-not $ready -and -not $Force) {
        throw 'The host has not finished loading the mod yet (no "Harmony initialized" in LogOutput.log since it started). Wait for its main menu.'
    }
}

# --- Save the screen settings the guest is about to overwrite. ---
$snapshotFile = Join-Path $env:TEMP 'bwap-guest-screen-prefs.json'
$saved = 0
if (Test-Path $prefsKey) {
    $key = Get-Item $prefsKey
    $entries = @(foreach ($name in $key.Property | Where-Object { $_ -like 'Screenmanager *' }) {
        [pscustomobject]@{ Name = $name; Kind = $key.GetValueKind($name).ToString(); Value = $key.GetValue($name) }
    })
    ConvertTo-Json -InputObject $entries -Depth 3 | Set-Content -Path $snapshotFile -Encoding utf8
    $saved = $entries.Count
}

# --- Start the guest. ---
$unityLog = Join-Path $unityLogDir 'Player-guest.log'
$psi = New-Object System.Diagnostics.ProcessStartInfo $exe
$psi.WorkingDirectory = $GameDir
$psi.UseShellExecute = $false
$psi.Arguments = "--bwap-guest -logFile `"$unityLog`" -screen-fullscreen 0 -screen-width $Width -screen-height $Height"
$psi.EnvironmentVariables['SteamAppId'] = $appId
$psi.EnvironmentVariables['SteamGameId'] = $appId
foreach ($name in @($psi.EnvironmentVariables.Keys)) {
    if ($name -like 'DOORSTOP_*') { $psi.EnvironmentVariables.Remove($name) }
}

$started = Get-Date
$guest = [System.Diagnostics.Process]::Start($psi)
Write-Host ("Guest started: process {0}, {1}x{2} windowed." -f $guest.Id, $Width, $Height) -ForegroundColor Green

if ($saved -gt 0) {
    Start-Process powershell -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
        '-WatchGuest', $guest.Id, '-Snapshot', "`"$snapshotFile`"")
    Write-Host "  $saved screen setting(s) saved; they are put back when the guest exits."
}

# --- Say where its logs are, which also proves BepInEx loaded. ---
Write-Host '  Waiting for the guest''s BepInEx log...'
$bepLog = $null
$deadline = $started.AddSeconds(90)
while ((Get-Date) -lt $deadline -and -not $guest.HasExited) {
    # Found by what it says rather than by its name: LogOutput.1.log when the
    # host holds LogOutput.log, LogOutput.log itself with -Force.
    $bepLog = Get-ChildItem (Join-Path $GameDir 'BepInEx') -Filter 'LogOutput*.log' -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $started } |
        Where-Object { Select-String -Path $_.FullName -Pattern 'Role: loopback guest' -Quiet } |
        Select-Object -First 1
    if ($bepLog) { break }
    Start-Sleep -Seconds 2
}

if ($bepLog) {
    Write-Host "  BepInEx log: $($bepLog.FullName)" -ForegroundColor Green
    Select-String -Path $bepLog.FullName -Pattern 'loaded \(build|\[LoopbackGuest\]' | ForEach-Object { "    $($_.Line)" }
} elseif ($guest.HasExited) {
    Write-Host "  The guest exited (code $($guest.ExitCode)) before BepInEx wrote a log." -ForegroundColor Red
} else {
    Write-Host '  No guest log naming its role after 90 s. Is Debug.Enabled on in the .cfg?' -ForegroundColor Yellow
}
Write-Host "  Unity log: $unityLog"
