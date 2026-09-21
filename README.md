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

**Pour reprendre après une pause** :
[`NEXT-SESSION.md`](NEXT-SESSION.md) — où en est le projet, ce qui reste à
tester (solo puis à deux), les questions encore ouvertes et les pistes
écartées.

## État d'avancement

- [x] Mod : scaffolding, détection de checks (gourds/big keys), matérialisation
      d'item reçu, écran d'hébergement avec champs Archipelago
- [x] apworld : monde complet, génération validée sur Archipelago 0.6.7 et
      0.6.8, `.apworld` packagé
- [x] Mod : client réseau Archipelago (`mod/src/Core/Net/`) — connexion,
      réception d'items, report des checks, goal. Validé contre une vraie
      room Archipelago ; contrat dans [`apworld/protocol.md`](apworld/protocol.md)
- [x] Le mod charge et **se connecte depuis le jeu** (confirmé le 2026-09-15)
- [x] Tous les chemins éprouvés en jeu : check sortant depuis une vraie
      énigme, item entrant, big key, dépôts, goal, reconstruction des
      gourdes, survie à une coupure réseau, et récupération sur save neuve
- [x] Session à deux joueurs (2026-09-20) et les trois goals confirmés de
      bout en bout (2026-09-21) : `gauntlet`, `ending`, `deposits`
- [x] Stations radio en items (2026-09-21) — éprouvé en jeu de bout en bout :
      suppression locale, octroi par l'item, survie au ré-hébergement et au
      redémarrage. Cf. `apworld/protocol.md` §11
