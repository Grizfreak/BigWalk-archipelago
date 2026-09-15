# apworld/ (pas encore commencé)

Futur monde Python Archipelago (`worlds/bigwalk/`) pour *Big Walk*. Rien n'est
codé ici pour l'instant — voir [`../BW_export/big-walk-archipelago-notes.md`](../BW_export/big-walk-archipelago-notes.md)
pour l'état complet des décisions de conception et des blocages.

## Bloquants avant de commencer

- **Client réseau Archipelago côté mod** (`../mod/`) — rien ne se connecte
  encore à un serveur AP. Le monde Python n'a rien à piloter sans lui.
- Le reste (goal, modèle des monuments, big keys) est déjà tranché pour une
  alpha — voir la section "Décisions de conception encore ouvertes" des notes.

## Ce qui est déjà prêt côté mod pour ce monde

- `Core/ItemApplier.ApplyGourdItem()`/`ApplyBigKeyItem()` — les deux chemins
  de réception d'item, prêts à être appelés par le futur client réseau.
- `Core/CosmeticMonumentFillTracker.GetFilledMonumentCount()` — comptage
  agrégé des monuments (Option A, décidée pour l'alpha).
- L'écran d'hébergement du jeu expose déjà un slot name, un mot de passe
  Archipelago et un champ host:port (`mod/src/Patches/HostMenuConfirmPatch.cs`)
  — lisibles via `Core/ApSessionConfig.cs` une fois le client réseau écrit.

## Structure attendue (à créer)

```
apworld/
└── bigwalk/
    ├── __init__.py       # World, create_regions/create_items/set_rules
    ├── Items.py
    ├── Locations.py
    ├── Options.py        # dont le modèle de comptage des monuments (Option A)
    └── test/
```
