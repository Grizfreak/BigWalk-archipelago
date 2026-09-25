<#
.SYNOPSIS
    Starts a second instance of the game on this PC, as the loopback guest,
    and has it join the first.

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
      launches a game; they are set here for the guest only and adds no
      file to the install.
    - --bwap-guest on the command line, which is how the mod knows its role
      (Debug/LoopbackGuest.cs). Never the .cfg: both instances read the same
      one.
    - The working directory set to the game folder, and no DOORSTOP_*
      variables inherited, so Doorstop resolves its relative paths and loads
      BepInEx.

    If the host is already hosting (something on UDP 7777), the guest also
    gets --bwap-join and joins on its own once its title menu is up;
    otherwise Ctrl+L in the guest's window joins. Either way the mod goes
    through the game's own NetworkMinder.SetTransportAndConnect.

    The guest runs windowed. Unity keeps the window mode and resolution in
    PlayerPrefs, which both instances share (HKCU\Software\House House\Big
    Walk), so the host could come back windowed next time. The Screenmanager
    values are saved before the launch and put back once the guest exits, by
    a hidden copy of this script that waits for it; any Screenmanager value
    the guest created is removed.

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

.PARAMETER NoJoin
    Never joins on its own, even if the host is hosting: Ctrl+L does it.

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
    [switch]$NoJoin,
    [switch]$Force,
    [int]$WatchGuest = 0,
    [string]$Snapshot = ''
)

$ErrorActionPreference = 'Stop'

$appId = '1478500'
$kcpPort = 7777
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
    $names = @()
    foreach ($entry in $entries) {
        $value = $entry.Value
        if ($entry.Kind -eq 'Binary') { $value = [byte[]]$value }
        Set-ItemProperty -Path $prefsKey -Name $entry.Name -Value $value -Type $entry.Kind
        $names += $entry.Name
    }

    # A windowed guest also writes values the host never had (the window
    # size, measured 2026-09-25); left behind, they would be the host's.
    foreach ($name in (Get-Item $prefsKey).Property | Where-Object { $_ -like 'Screenmanager *' -and $names -notcontains $_ }) {
        Remove-ItemProperty -Path $prefsKey -Name $name
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

$hosting = $false
if ($running.Count -ge 1) {
    $hostProcess = $running | Sort-Object StartTime | Select-Object -First 1
    $hostLog = Join-Path $GameDir 'BepInEx\LogOutput.log'
    $ready = (Test-Path $hostLog) -and (Get-Item $hostLog).LastWriteTime -ge $hostProcess.StartTime -and
        (Select-String -Path $hostLog -Pattern 'Harmony initialized' -Quiet)
    if (-not $ready -and -not $Force) {
        throw 'The host has not finished loading the mod yet (no "Harmony initialized" in LogOutput.log since it started). Wait for its main menu.'
    }

    $hosting = @(Get-NetUDPEndpoint -LocalPort $kcpPort -ErrorAction SilentlyContinue |
        Where-Object { $_.OwningProcess -eq $hostProcess.Id }).Count -gt 0
}

$join = $hosting -and -not $NoJoin
if ($hosting) {
    Write-Host "The host is hosting (UDP $kcpPort is open)." -ForegroundColor Green
} else {
    Write-Host "The host is not hosting yet (nothing on UDP $kcpPort): host a world, then press Ctrl+L in the guest's window." -ForegroundColor Yellow
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
# Through Start-Process (the shell), with the environment changed in this
# process for the length of the call: a process started directly inherits
# this console and prints Unity's boot output into it.
$unityLog = Join-Path $unityLogDir 'Player-guest.log'
$arguments = "--bwap-guest" + $(if ($join) { " --bwap-join" } else { "" }) +
    " -logFile `"$unityLog`" -screen-fullscreen 0 -screen-width $Width -screen-height $Height"

$previous = @{}
foreach ($name in 'SteamAppId', 'SteamGameId') {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $appId, 'Process')
}
foreach ($variable in @(Get-ChildItem env: | Where-Object { $_.Name -like 'DOORSTOP_*' })) {
    $previous[$variable.Name] = $variable.Value
    [Environment]::SetEnvironmentVariable($variable.Name, $null, 'Process')
}

$started = Get-Date
try {
    $guest = Start-Process -FilePath $exe -WorkingDirectory $GameDir -ArgumentList $arguments -PassThru
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
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

# --- When it joins on its own, say how far it got. ---
if ($bepLog -and $join) {
    Write-Host '  Waiting for the guest to join (it may be sitting on the microphone check: click through it)...'
    $deadline = (Get-Date).AddSeconds(180)
    $shown = 0
    while ((Get-Date) -lt $deadline -and -not $guest.HasExited) {
        $lines = @(Select-String -Path $bepLog.FullName -Pattern '\[LoopbackGuestMonitor\]|\[LoopbackGuest\] OnClientConnect')
        foreach ($line in $lines | Select-Object -Skip $shown) { Write-Host "    $($line.Line)" }
        $shown = $lines.Count
        if ($lines | Where-Object { $_.Line -match 'Connection: in the world' }) { break }
        Start-Sleep -Seconds 2
    }
}
