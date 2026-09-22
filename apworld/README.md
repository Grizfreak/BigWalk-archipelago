# apworld/

Monde Python Archipelago pour *Big Walk*. Le code vit dans [`bigwalk/`](bigwalk/)
et se package en `dist/bigwalk.apworld`.

- [`protocol.md`](protocol.md) — **le contrat avec le mod** : ids de locations
  et d'items, contenu de `slot_data`, ce que le client C# doit envoyer et
  appliquer. À relire avant de toucher à l'une ou l'autre moitié.
- [`design-decisions.md`](design-decisions.md) — l'historique des décisions de
  conception (goal, modèle des monuments, big keys, softlocks, retours
  communauté).
- [`bigwalk/docs/`](bigwalk/docs/) — la documentation joueur telle que le
  webhost Archipelago l'affiche : la page du jeu et le guide d'installation.
  Les équivalents à la racine du dépôt ([`../README.md`](../README.md),
  [`../SETUP.md`](../SETUP.md)) disent la même chose ; les quatre fichiers
  bougent ensemble.
- [`../mod/reverse-engineering-notes.md`](../mod/reverse-engineering-notes.md) —
  le fonctionnement interne du jeu dont tout ce qui suit dépend.

## État

- [x] Monde Python complet (locations, items, logique, options, docs, tests)
- [x] Génération réelle validée : 3 slots, Archipelago 0.6.8 (source) et 0.6.7
      (installation locale), `.apworld` packagé inclus
- [x] Client réseau Archipelago côté mod — connecté et joué en solo comme à
      deux (cf. `protocol.md`)
- [x] Stations radio en items (`radio_station_items`, 2026-09-21) : les deux
      moitiés écrites, la génération validée, et la chaîne complète éprouvée
      en jeu (cf. `protocol.md` §11)
- [x] Big keys (2026-09-21) : la porte et la clé sont deux items distincts,
      25 checks de découpe et 7 dépôts. Exercé en jeu, mais en solo seulement
      (cf. [`../COOP-TESTS.md`](../COOP-TESTS.md))
- [x] Treize locations d'énigme retirées (2026-09-21) : elles existaient dans
      les métadonnées du jeu mais rien dans le build publié ne les produit,
      donc la génération pouvait poser de la progression sur un check
      impossible à envoyer. Il reste 45 énigmes (`data.ABSENT_FROM_THE_BUILD`)

## Build

```
python build.py
```

Produit `dist/bigwalk.apworld`. À copier dans `custom_worlds/` d'une
installation Archipelago (0.6.7 minimum).

## Développement et tests

Pour travailler avec les tests d'Archipelago, lier le dossier du monde dans un
checkout source d'Archipelago plutôt que de copier :

```
mklink /J "<checkout>\worlds\bigwalk" "<ici>\apworld\bigwalk"
```

Puis, depuis le checkout :

```
set AP_TEST_WORLDS=bigwalk
python -m pytest worlds/bigwalk/test -q     # tests du monde
python -m pytest test/general -q            # conformité Archipelago
```

(Les tests `test/webhost` demandent Flask, absent de cette machine.)

## Ce que fait le monde

**Locations** — 45 énigmes, les 25 segments de découpe des big keys, 7 dépôts
de big key, 7 stations radio (optionnelles), et les dépôts de gourdes aux
monuments (aucun, tous les 5, ou tous — option). 93 locations sur les options
par défaut. Les dépôts de gourdes sont comptés globalement, jamais par tour :
c'est ce qui rend le modèle insensible au softlock identifié le 2026-09-11
(Option A).

**Items** — un item générique `Gourd` en autant d'exemplaires qu'il y a de
slots de monument en jeu (30 ou 45), les 7 *features* que les big keys
ouvraient (`Drawbridge`, `Map Room`, `Chairlift`, `Train`, `Tunnels`, `Dam`,
`Green Dome`), les 7 big keys elles-mêmes, les 7 `Radio Music: …` qui rendent
chaque station audible (option), et du filler : les objets à main de l'île,
matérialisés à la réception et retirés de la carte pour qu'on ne puisse pas
les ramasser gratuitement. Le Drawbridge est donné au départ par défaut.

**Logique** — la clé et la porte sont deux items distincts. Une big key
n'ouvre rien : elle porte six checks (cinq découpes et un dépôt), et ces six
locations ne dépendent que d'elle. Ce sont les *features* qui ouvrent l'île,
et trois d'entre elles gardent vraiment quelque chose : `Chairlift` et
`Tunnels` enferment des locations (mesuré en jeu le 2026-09-21), et `Dam` —
la feature de la tour Black Monolith — ouvre la zone de fin. Aucune énigme
n'est verrouillée par le
mod. Le nombre de gourdes reçues ne conditionne plus que les dépôts de
gourdes eux-mêmes.

**Goal** — `gauntlet` (défaut), `ending` ou `deposits`.

Les hypothèses que la logique fait sans qu'elles aient été vérifiées en jeu
sont listées à la fin de [`protocol.md`](protocol.md) — ce sont elles qui
décident si une seed générée est réellement finissable.
