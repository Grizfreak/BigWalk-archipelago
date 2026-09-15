# BigWalk-archipelago

Intégration Archipelago (multiworld randomizer) pour *Big Walk* (House House).
Deux sous-projets distincts :

- [`mod/`](mod/README.md) — mod BepInEx (IL2CPP, Harmony) qui s'installe dans
  le jeu : détection des checks, matérialisation des items reçus, écran
  d'hébergement étendu avec les identifiants Archipelago. Voir
  [`mod/architecture-mod.md`](mod/architecture-mod.md) pour la conception.
- [`apworld/`](apworld/README.md) — futur monde Python Archipelago
  (`worlds/bigwalk/`), pas encore commencé.

Notes de rétro-ingénierie et décisions de conception (goal, modèle des
monuments, softlocks, etc.), pertinentes pour les deux sous-projets :
[`BW_export/big-walk-archipelago-notes.md`](BW_export/big-walk-archipelago-notes.md).

## État d'avancement

- [x] Mod : scaffolding, détection de checks (gourds/big keys), matérialisation
      d'item reçu, écran d'hébergement avec champs Archipelago
- [ ] Mod : vrai client réseau Archipelago (bloquant principal, cf. notes)
- [ ] apworld : rien commencé, en attente du client réseau
