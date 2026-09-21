# tools/ghidra/

Two scripts for asking the game binary a question directly, instead of
inferring an answer from method names and testing it in game.

They exist because of 2026-09-20. A full day went into working out how a
player comes to be holding something — `PickUp`, `Drop`, `SetLoose`,
`SetHeld` all sound like they might publish it — at one two-machine round
trip per guess. Twenty minutes of decompilation settled it: both
`PlayerHands.PickUp` and `Prop.SetHeld` are purely local, and the server
half of the game's own Command, `PlayerNetworking.UserCode_CmdPickUp`,
writes `NetworkplayerHeldInformation`, the only field another machine ever
sees. The guess under test at that moment would have changed nothing.

## Running them

This Ghidra has no Python (`Ghidra was not started with PyGhidra`), hence
Java scripts. The project in `mod/gh-pj/` is already analysed, so
`-noanalysis` keeps it to seconds rather than re-analysing 64 MB.

```powershell
$g = "F:\Archipelago\ghidra\support\analyzeHeadless.bat"
$s = "F:\Archipelago\dev\BigWalk-archipelago\tools\ghidra"

# What does this function do? (addresses come from BW_export/il2cpp.cs)
& $g "F:\Archipelago\dev\BigWalk-archipelago\mod\gh-pj" "Big Walk" `
     -process "GameAssembly.dll" -noanalysis -scriptPath $s `
     -postScript DecompileAt.java 0x00000001803C3A30

# Who touches this? — a name fragment, and what calls each match
& $g "F:\Archipelago\dev\BigWalk-archipelago\mod\gh-pj" "Big Walk" `
     -process "GameAssembly.dll" -noanalysis -scriptPath $s `
     -postScript FindCallers.java "HeldInformation"
```

Output goes to stdout among Ghidra's own logging; redirect to a file and
grep it. Every line the scripts print is prefixed `INFO  <script>>`.

`FindCallers` is usually the one to reach for first: it turns "which
function writes this networked field" into a list, and the names IL2CPP
keeps are descriptive enough to recognise the answer
(`UserCode_CmdPickUp__PlayerHeldInformation` was unmistakable). Note that
the caller lists themselves are often empty — IL2CPP dispatches
indirectly, so Ghidra resolves few static call edges. The names are the
signal, not the edges.

## The caveat that matters

`mod/gh-pj/` and `BW_export/il2cpp.cs` are both from the **Steam build of
2026-09-07**. The game actually being played is **1.48, the binaries of
2026-08-10**. Addresses and code can differ, and this has already bitten
once: `passwordRequired` is in the dump and does not exist in 1.48.

So treat what you read here as indicative, and confirm anything you depend
on against the 1.48 interop assembly — a member's mere existence is cheap
to check:

```powershell
$b = [System.IO.File]::ReadAllBytes("F:\shared\Big Walk\BepInEx\interop\Assembly-CSharp.dll")
[System.Text.Encoding]::UTF8.GetString($b) -match "NetworkplayerHeldInformation"
```
