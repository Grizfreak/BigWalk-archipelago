# apworld/

Monde Python Archipelago pour *Big Walk*. Le code vit dans [`bigwalk/`](bigwalk/)
et se package en `dist/bigwalk.apworld`.

- [`protocol.md`](protocol.md) — **le contrat avec le mod** : ids de locations
  et d'items, contenu de `slot_data`, ce que le client C# doit envoyer et
  appliquer, et la liste du travail restant côté mod. À lire avant d'écrire le
  client réseau.
- [`design-decisions.md`](design-decisions.md) — l'historique des décisions de
  conception (goal, modèle des monuments, softlocks, retours communauté).
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

**Locations** — 58 énigmes, 7 dépôts de big key, 7 stations radio
(optionnelles), et les dépôts de gourdes aux monuments (aucun, tous les 5, ou
tous — option). Les dépôts sont comptés globalement, jamais par tour : c'est ce
qui rend le modèle insensible au softlock identifié le 2026-09-11 (Option A).

**Items** — un item générique `Gourd` en autant d'exemplaires qu'il y a de
slots de monument en jeu (30, 36 ou 45), les 7 big keys en 1:1, les 7
`Radio Music: …` qui rendent chaque station audible (option), et du
filler sans effet. La clé du tutoriel est donnée au départ par défaut.

**Logique** — une seule ressource conditionne quoi que ce soit : le nombre de
gourdes reçues. Aucune énigme n'est bloquée (le mod ne verrouille rien), et la
seule porte structurelle modélisée est la zone de fin, derrière la Black
Monolith Key. Une gourde déposée ne peut pas être ressortie d'un monument
(impossible dans le jeu de base), donc les coûts en gourdes des big keys sont
cumulatifs : la dernière clé coûte tout le pool.

**Goal** — `gauntlet` (défaut), `ending` ou `deposits`.

Les hypothèses que la logique fait sans qu'elles aient été vérifiées en jeu
sont listées à la fin de [`protocol.md`](protocol.md) — ce sont elles qui
décident si une seed générée est réellement finissable.
