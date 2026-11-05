# Adding cards, leaders and decks

All game content lives in `game/data/` as plain JSON. You need no code change and no recompile: add a file, then restart the game.
Each file has a `$schema` reference, so editors such as VS Code autocomplete and validate fields as you type.

```
game/data/
├── cards/
│   ├── realms/    one file per United Realms card
│   ├── abyss/     one file per Abyssal Legion card
│   ├── horde/     one file per Wild Horde card
│   └── neutral/   one file per Neutral card (usable by every faction)
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
| `musterGroup` | with `muster` | Cards sharing a group are pulled from the deck together. A group can hold one card id (its copies muster each other, e.g. `sapling`) or several ids. |
| `concord` | with `concord` | Number of cards each player draws (1 or more). |
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
| `scorch` | Destroys the strongest non-hero unit(s) on the whole board, both sides and all ties, like the `scorch` special (the unit itself included). |
| `concord` | You draw `concord` cards, then your opponent draws as many (each only what is left in their deck, up to the hand limit). |

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

A special card cannot have `strength`, `hero`, `abilities`, `musterGroup` or `concord`.

## Leaders

```json
{
  "$schema": "../schema/leader.schema.json",
  "id": "underlord",
  "name": "Underlord",
  "faction": "abyss",
  "ability": "play_weather_from_deck",
  "row": "melee",
  "abilityName": "Atrophy Aura"
}
```

A leader belongs to `realms`, `abyss` or `horde`. Its ability can be used once per match.
In game the leader's tooltip reads `<abilityName>: <rules text>` (e.g. "Atrophy Aura: Play a melee weather card from your deck."); the rules text is generated from `ability` and `row`, so it always matches what the ability does.

| Ability | Effect | Needs `row` |
|---|---|---|
| `clear_weather` | Removes all weather. | no |
| `play_weather_from_deck` | Plays a weather card affecting `row` from the deck (unusable without one). | yes |
| `horn_row` | Doubles the player's `row` (unusable if its horn slot is filled). | yes |
| `scorch_row` | Destroys the strongest enemy unit(s) in `row` if that row totals 10+. | yes |
| `return_from_discard` | Returns a chosen card from the discard pile to the hand. | no |
| `scorch` | Destroys the strongest non-hero unit(s) on the whole board, both sides. | no |
| `return_from_board` | Returns one of the player's non-hero units from the board to the hand; the player clicks the unit. | no |

## Decks

```json
{
  "$schema": "../schema/deck.schema.json",
  "id": "abyss_starter",
  "name": "Hellborn Host",
  "faction": "abyss",
  "leader": "underlord",
  "cards": { "terrorblade": 1, "hellbear_smasher": 3, "manta_style": 2 }
}
```

Rules, with defaults defined in `DeckRules`:

- the faction is `realms`, `abyss` or `horde`, and the leader belongs to the same faction;
- cards come from the deck's faction or from Neutral;
- each count is at least 1, with at most 3 copies of a card and at most 1 copy of a hero;
- the deck has at least 22 units and at most 10 specials.

The shipped decks are the computer opponent's decks. They also seed the player's own decks (`user://decks.json`) on first launch only, so editing them later does not change existing players' decks. New cards appear in the deck builder's book automatically. When renaming or retiring a card or leader id, add the old id to `LegacyIds` (and bump `DeckCollectionSerializer.CurrentVersion`) so saved decks follow.

## Art

Art is optional. A card without art shows a placeholder. To add art, drop an image named after the id. Card art goes in the folder of the card's faction, like its JSON file:

- card art: `game/assets/cards/<faction>/<id>.png` (or `.webp` / `.jpg`), e.g. `game/assets/cards/realms/sven.png`
- leader art: `game/assets/leaders/<id>.png` (or `.webp` / `.jpg`)

A non-hero card can have up to three images, so its copies look different: `<id>.png`, `<id>_2.png`, `<id>_3.png` (e.g. `dire_creep.png` and `dire_creep_2.png`). The copies in a deck cycle through the images: with 1 image all copies share it, with 2 the third copy reuses the first, with 3 each copy has its own. The deck builder and card book show the first image. Heroes have a single image.
- card back (picked per deck in the deck builder): `game/assets/card_backs/<back-id>.png` (or `.webp` / `.jpg`); see [its README](../game/assets/card_backs/README.md)

Use a 3:4 portrait image, ideally 768x1024 (every card face, leaders included, has that ratio, so it fills the card with no cropping). Keep the important part away from the top-left corner (strength medallion), the bottom-right corner (row icon) and the bottom ~15% (name banner). The frame color comes from the faction, and heroes get a gold border, so the art itself needs no frame. Open the Godot editor once (or run `--import`) so the image gets imported. Use a lower-case extension: the smoke test (`-- --smoke`, also run on release builds) fails on card art in the wrong faction folder, named after no card id, a second image for a hero, or a variant with a gap (`_3` without `_2`, or `_2` without the base image).

## Validation

The loader collects **every** error, each with its file path, before the game starts. Invalid data shows an error screen listing those errors instead of crashing.

The test suite also loads the shipped data (`ShippedDataTests`), so CI fails on any invalid card, leader or deck. Run it locally after editing:

```powershell
dotnet test --project tests/Gwota.Core.Tests
```
