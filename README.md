# BigWalk-archipelago

Intégration Archipelago (multiworld randomizer) pour *Big Walk* (House House).
Deux sous-projets distincts :

- [`mod/`](mod/README.md) — mod BepInEx (IL2CPP, Harmony) qui s'installe dans
  le jeu : détection des checks, matérialisation des items reçus, écran
  d'hébergement étendu avec les identifiants Archipelago. Voir
  [`mod/architecture-mod.md`](mod/architecture-mod.md) pour la conception.
- [`apworld/`](apworld/README.md) — monde Python Archipelago (`bigwalk`) :
  locations, items, logique, options, et le contrat que le mod devra respecter
  ([`apworld/protocol.md`](apworld/protocol.md)).

- [`tools/`](tools/) — outillage de test. `python tools/testroom.py`
  reconstruit l'apworld, génère une seed depuis `tools/players/` et héberge
  la room, avec les identifiants à saisir en jeu affichés à l'écran.
  `solo-smoke.yaml` est taillé pour valider la chaîne en quelques minutes en
  solo, `solo-full.yaml` est une vraie partie.

Notes de rétro-ingénierie et décisions de conception (goal, modèle des
monuments, softlocks, etc.) :
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md)
(comment le jeu fonctionne et comment le mod l'accroche) et
[`apworld/design-decisions.md`](apworld/design-decisions.md) (à quoi doit
ressembler le monde Archipelago).

## État d'avancement

- [x] Mod : scaffolding, détection de checks (gourds/big keys), matérialisation
      d'item reçu, écran d'hébergement avec champs Archipelago
- [x] apworld : monde complet, génération validée sur Archipelago 0.6.7 et
      0.6.8, `.apworld` packagé
- [x] Mod : client réseau Archipelago (`mod/src/Core/Net/`) — connexion,
      réception d'items, report des checks, goal. Validé contre une vraie
      room Archipelago ; contrat dans [`apworld/protocol.md`](apworld/protocol.md)
- [ ] Tester une session complète **dans le jeu** — le client n'y a encore
      jamais tourné
