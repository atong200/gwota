# Adding cards, leaders and decks

All game content lives in `game/data/` as plain JSON. You need no code change and no recompile: add a file, then restart the game.
Each file has a `$schema` reference, so editors such as VS Code autocomplete and validate fields as you type.

```
game/data/
├── cards/
│   ├── realms/    one file per Realms of Men card
│   ├── hells/     one file per Seven Hells card
│   └── neutral/   one file per Neutral card (usable by both factions)
├── leaders/       one file per leader
├── decks/         one file per deck
└── schema/        JSON schemas (card, leader, deck)
```

## Ids

- Ids are `snake_case`, using lowercase letters, digits and underscores.
- The id must match the file name, so `id: "crystal_maiden"` goes in `crystal_maiden.json`.
- Ids are unique per content type.
- Enum values are written in snake_case too, for example `tight_bond` and `clear_weather`.

## Cards

A card file must sit in the folder of its faction.

### Unit

```json
{
  "$schema": "../../schema/card.schema.json",
  "id": "sven",
  "name": "Sven",
  "faction": "realms",
  "kind": "unit",
  "rows": ["melee"],
  "strength": 10,
  "hero": true,
  "abilities": [],
  "description": "Rogue Knight. God's Strength."
}
```

| Field | Required | Notes |
|---|---|---|
| `rows` | yes | `melee`, `ranged`, `siege`. With more than one row (agile), the player chooses. |
| `strength` | yes | 0 or more. |
| `hero` | no | A hero is immune to weather, horns, morale, scorch, decoy and medic. |
| `abilities` | no | See below. |
| `musterGroup` | with `muster` | Cards sharing a group are pulled from the deck together. |
| `description` | no | Flavour or rules text shown on the card. |

Unit abilities:

| Ability | Effect |
|---|---|
| `spy` | Played on the opponent's side. Its player draws 2 cards. |
| `medic` | Revives a non-hero unit from your discard pile. |
| `muster` | Plays every card of the same `musterGroup` from your deck. |
| `tight_bond` | Strength is multiplied by the number of copies of this card in the row. |
| `morale_boost` | Gives +1 to every other unit in its row. |
| `horn` | Doubles every other unit in its row. Does not stack with a horn special. |
| `scorch_row` | Destroys the strongest enemy unit(s) in the matching row if that row totals 10 or more. |

### Special

```json
{
  "$schema": "../../schema/card.schema.json",
  "id": "freezing_field",
  "name": "Freezing Field",
  "faction": "neutral",
  "kind": "special",
  "effect": "weather",
  "rows": ["melee"],
  "description": "Sets all melee units to 1."
}
```

| Effect | Effect description | `rows` |
|---|---|---|
| `horn` | Doubles the non-hero units in a row you choose. | no |
| `scorch` | Destroys the strongest non-hero unit(s) on the whole board. | no |
| `decoy` | Swaps with one of your non-hero units, returning it to your hand. | no |
| `weather` | Sets non-hero units in the given rows (both sides) to 1. | **required** |
| `clear_weather` | Removes all weather. | no |

A special card cannot have `strength`, `hero`, `abilities` or `musterGroup`.

## Leaders

```json
{
  "$schema": "../schema/leader.schema.json",
  "id": "underlord",
  "name": "Underlord",
  "faction": "hells",
  "ability": "play_weather_from_deck",
  "row": "melee",
  "description": "Firestorm: plays a melee weather card from your deck."
}
```

A leader belongs to `realms` or `hells`. Its ability can be used once per match.

| Ability | Needs `row` |
|---|---|
| `clear_weather` | no |
| `play_weather_from_deck` | yes |
| `horn_row` | yes |
| `scorch_row` | yes |
| `return_from_discard` | no |

## Decks

```json
{
  "$schema": "../schema/deck.schema.json",
  "id": "hells_starter",
  "name": "Hellborn Legion",
  "faction": "hells",
  "leader": "underlord",
  "cards": { "terrorblade": 1, "hellbear_smasher": 3, "manta_style": 2 }
}
```

Rules, with defaults defined in `DeckRules`:

- the faction is `realms` or `hells`, and the leader belongs to the same faction;
- cards come from the deck's faction or from Neutral;
- each count is at least 1, with at most 3 copies of a card and at most 1 copy of a hero;
- the deck has at least 22 units and at most 10 specials.

The shipped decks are the computer opponent's decks. They also seed the player's own decks (`user://decks.json`) on first launch only, so editing them later does not change existing players' decks. New cards appear in the deck builder's book automatically. When renaming or retiring a card or leader id, add the old id to `LegacyIds` (and bump `DeckCollectionSerializer.CurrentVersion`) so saved decks follow.

## Art

Art is optional. A card without art shows a placeholder. To add art, drop an image named after the id:

- card art: `game/assets/cards/<id>.png` (or `.webp` / `.jpg`)
- leader art: `game/assets/leaders/<id>.png` (or `.webp` / `.jpg`)
- card back (draw piles, opponent hand, redraw animation): `game/assets/card_back.png` (or `.webp` / `.jpg`)

Use a 2:3 portrait image (for example 400x600); it is cropped to cover the card. Keep the important part away from the top-left corner (strength medallion and row icon) and the bottom edge (name banner). The frame color comes from the faction, and heroes get a gold border, so the art itself needs no frame. Open the Godot editor once (or run `--import`) so the image gets imported.

## Validation

The loader collects **every** error, each with its file path, before the game starts. Invalid data shows an error screen listing those errors instead of crashing.

The test suite also loads the shipped data (`ShippedDataTests`), so CI fails on any invalid card, leader or deck. Run it locally after editing:

```powershell
dotnet test --project tests/Gwota.Core.Tests
```
