# Développement

Notes de travail sur l'intégration Archipelago de *Big Walk*. La
documentation joueur est ailleurs : [`README.md`](README.md) pour ce que fait
la randomisation, [`SETUP.md`](SETUP.md) pour l'installation.

## Les deux moitiés

- [`mod/`](mod/README.md) — mod BepInEx (IL2CPP, Harmony) qui s'installe dans
  le jeu : détection des checks, matérialisation des items reçus, écran
  d'hébergement étendu avec les identifiants Archipelago. Conception :
  [`mod/architecture-mod.md`](mod/architecture-mod.md).
- [`apworld/`](apworld/README.md) — monde Python Archipelago (`bigwalk`) :
  locations, items, logique, options.
- [`apworld/protocol.md`](apworld/protocol.md) — **le contrat entre les
  deux** : ids, `slot_data`, ce que le client C# envoie et applique. À lire
  avant de toucher à l'une ou l'autre moitié.
- [`tools/`](tools/) — outillage. `tools/deploy-mod.ps1` construit et déploie
  sur toutes les installations puis imprime une empreinte et dit si elles
  concordent ; `tools/package-mod.ps1` produit le zip joueur ;
  `python tools/testroom.py` reconstruit l'apworld, génère une seed depuis
  `tools/players/` et héberge la room en affichant les identifiants à saisir
  en jeu. `solo-smoke.yaml` valide la chaîne en quelques minutes,
  `solo-full.yaml` est une vraie partie, `coop-test.yaml` a son propre slot
  name pour ne pas polluer les compteurs.

Rétro-ingénierie et décisions :
[`mod/reverse-engineering-notes.md`](mod/reverse-engineering-notes.md)
(comment le jeu fonctionne et comment le mod l'accroche) et
[`apworld/design-decisions.md`](apworld/design-decisions.md) (à quoi doit
ressembler le monde Archipelago).

**Pour reprendre après une pause** :
[`NEXT-SESSION.md`](NEXT-SESSION.md) — où en est le projet, ce qui reste à
tester, les questions ouvertes et les pistes écartées.
[`COOP-TESTS.md`](COOP-TESTS.md) — le plan de test à deux machines, ordonné
par risque.

## Construire

```
dotnet build mod/BigWalkArchipelago.sln     # le mod
python apworld/build.py                     # dist/bigwalk.apworld
pwsh tools/package-mod.ps1                  # dist/BigWalkArchipelago-*.zip
```

Les artefacts de `dist/` et `apworld/dist/` ne sont frais que de la dernière
exécution de ces scripts. Les deux moitiés se versionnent ensemble
(`Plugin.PluginVersion`, le `.csproj`, `archipelago.json`, `WORLD_VERSION`) :
un mod plus ancien que son apworld ignore des champs de `slot_data`, ce qui
rend une seed injouable au lieu de planter.

Tests du monde, depuis un checkout source d'Archipelago où
`worlds/bigwalk` est une jonction vers `apworld/bigwalk` :

```
set AP_TEST_WORLDS=bigwalk
python -m pytest worlds/bigwalk/test -q     # tests du monde
python -m pytest test/general -q            # conformité Archipelago
```

## État d'avancement

- [x] Mod : scaffolding, détection de checks, matérialisation d'item reçu,
      écran d'hébergement avec les champs Archipelago
- [x] apworld : monde complet, génération validée sur Archipelago 0.6.7 et
      0.6.8, `.apworld` packagé
- [x] Client réseau Archipelago (`mod/src/Core/Net/`) — connexion, items,
      report des checks, goal ; validé contre une vraie room
- [x] Tous les chemins éprouvés en jeu : check sortant depuis une vraie
      énigme, item entrant, dépôts, goal, reconstruction des gourdes, survie
      à une coupure réseau, récupération sur save neuve
- [x] Session à deux joueurs (2026-09-20) et les trois goals confirmés de
      bout en bout (2026-09-21) : `gauntlet`, `ending`, `deposits`
- [x] Stations radio en items (2026-09-21), éprouvé en jeu de bout en bout
      (cf. `apworld/protocol.md` §11)
- [x] Big keys (2026-09-21) : la porte et la clé sont deux items distincts,
      25 checks de découpe + 7 dépôts. Construit et exercé **en solo
      seulement**
- [x] Filler = vrais objets de l'île, leurs exemplaires vanilla retirés de la
      carte (2026-09-22)
- [ ] Les big keys à deux machines — [`COOP-TESTS.md`](COOP-TESTS.md)
- [ ] Une seed jouée du premier check au goal
