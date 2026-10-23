# gwota

A card game heavily inspired by GWENT from The Witcher 3, Dota 2 themed.
Two factions from Dota lore, the Realms of Men and The Seven Hells, fight offline against an AI.

## Rules in a nutshell

The rules are classic Witcher 3 Gwent.

- A match is best of three rounds. Each player has 2 gems and loses one per round lost; a tie costs both players a gem.
- Each player draws 10 cards. In the **draft phase**, before the first round, you select up to 2 of them (click to select, click again to deselect) and confirm to redraw them. Cards are rarely drawn after that, so card advantage is the core of the game.
- On your turn you play a card, use your leader once per match, or pass the round. Once you pass, you are out for the rest of the round.
- Units go on the melee, ranged or siege row, and the higher total strength wins the round.
- Abilities: spy, medic, muster, tight bond, morale boost, horn and row scorch. Special cards: horn, scorch, decoy, weather and clear weather.
- Faction perks:
  - **Realms of Men**: draw a card after winning a round.
  - **The Seven Hells**: one random unit stays on the board after each round.

## Prerequisites

- [Godot 4.7 .NET (mono)](https://godotengine.org/download)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Running

Open `game/project.godot` in the Godot editor and press Play, or run it from the command line:

```powershell
$godot = "C:\Program Files\Godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
& $godot --path game                 # play
& $godot --path game -- --demo       # watch an AI-vs-AI match on the real board
& $godot --headless --path game -- --smoke   # load data and play AI matches headless, exit code 0 = OK
& $godot --path game -- --name Bob --port 7780   # LAN name and port for this run (two copies on one computer)
```

In the game:

- **Main menu**: Play (Computer lets you pick one of your decks; Player versus player offers Local network, and Online, which is coming soon), Decks, Options, Quit. Escape goes back one page.
- **Decks**: the card "book" fills the left side (Realms / Hells / Neutral bookmark tabs on a tinted page, row filters, 10 cards per page, mouse wheel or arrows to turn pages); each card shows how many copies are available ("x3", with stacked copies behind it). Your decks are listed on the right. Click a deck (or New deck, which asks for its faction; it cannot be changed later) to edit it: the right pane then shows the deck (leader portrait, click to change it; name with rename and delete buttons; faction and READY / INCOMPLETE status; unit and special counts; cards grouped by row) and the book narrows to the deck's faction and Neutral, counting the copies you can still add. Click a book card to add a copy, click a deck entry to remove one. Done, Back or Escape closes the deck; once more returns to the main menu. Changes save immediately; at least one deck must remain. Incomplete decks are allowed but tagged and cannot be played; hover the tag to see what is missing.
  - Decks are saved to `user://decks.json` (`%APPDATA%\Godot\app_userdata\Gwota\decks.json` on Windows). On first launch it is seeded with the shipped decks. If the file is unreadable, it is copied to `decks.json.bak` and the defaults are restored.
  - The computer opponent always plays the shipped decks from `game/data/decks`.
  - Decks saved before the faction rename (Radiant / Dire) are migrated on load: factions, leaders and renamed cards keep their place, and retired cards are dropped.
- **Options**: tabbed settings (Sound: master volume; Gameplay: animation speed Normal / Fast / Off; Profile: username, `Player` by default). The LAN room name and port are saved there too. Settings are saved to `user://settings.json`, which on Windows is `%APPDATA%\Godot\app_userdata\Gwota\settings.json`.
- **In a match**:
  - Hover any card (hand, board, draft) to see its details next to the cursor. A card shows its strength in the top-left medallion (white for regular cards, gold for heroes, green or red when boosted or weakened) and its row as an icon: sword (melee), bow (ranged), catapult (siege). Heroes have a gold frame.
  - Cards move on the board: played cards fly from the hand, drawn cards come from the draw pile, and destroyed or end-of-round cards go to the discard pile. The opponent's cards are revealed briefly. Set the speed (or turn it off) in Options > Gameplay.
  - The medallion next to each player's name is their total strength (gold while leading); each row shows its own total. The red crystals are round lives: you lose one for each lost round (both players lose one on a draw), and losing both loses the match.
  - Click a card in your hand to select it; the places it can go light up. Click a highlighted row (or card, for a decoy) to play it. Weather, scorch and similar cards accept a click anywhere on the board. Right-click or Escape cancels the selection.
  - **Pass round** ends your participation in the current round.
  - Your draw and discard piles sit in the left column (the opponent's at the top, yours at the bottom); hover a pile for its exact count. Hover the opponent's hand to enlarge it and see how many cards it holds.
  - The computer opponent is a `BOT <name>`, with a random name per match.
  - Escape or the gear button (bottom left) opens the pause menu (Resume, Options, Forfeit, Exit to main menu, Quit) and pauses the opponent. Forfeit asks for confirmation and ends the match as a defeat. The match log on the right collapses with its arrow button (the board then widens), and its icon button copies the log to the clipboard.

## Local network play

Two computers on the same network can play each other: Play > Player versus player > Local network.

- Pick a complete deck (you play under your Options > Profile username). One player optionally names the room, then clicks **Host game**; the other sees the room under **Games on your network** and clicks **Join**. The list updates by itself; the refresh button clears it and searches again. If the game does not show up, type the host's address (shown in the host's panel, e.g. `192.168.1.36`, plus `:port` if the port is not 7777) under **Join by IP address**. Both players need the same game version and card data.
- The room name and the port are remembered in the settings. Only the room name, the host's username and the address are shown to other players.
- The host decides the order of commands and the guest checks every step against the host's state, so both games always agree. If they ever differ, the match stops with a message.
- Leaving the match, quitting or losing the connection gives the other player the win by forfeit. After a match, both players must click **Rematch** to play again.
- Games use TCP port 7777 by default (editable in the host panel); discovery uses UDP port 7778 broadcasts.

**Firewall**: the first time you host, Windows asks whether Gwota (the Godot executable) may use the network. Allow it on **Private networks**. If the prompt was denied, or you cannot change firewall settings (for example on a work computer), let the other player host and join them by address: joining only makes outgoing connections, which are normally allowed. To fix a denied prompt, open "Allow an app through Windows Firewall" and tick Private for the Godot or Gwota executable. The network itself must be set as Private in Windows (Settings > Network > your connection > Network profile).

**Testing on one computer**: start two copies with different names and ports, then host in one and join in the other (the game shows up in the list, or join `127.0.0.1:7780`):

```powershell
$godot = "C:\Program Files\Godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
Start-Process $godot '--path game -- --name Alice'
Start-Process $godot '--path game -- --name Bob --port 7780'
```

## Tests

The rules engine lives in a plain .NET library, so its tests run without Godot:

```powershell
dotnet test --project tests/Gwota.Core.Tests
dotnet test --project tests/Gwota.Net.Tests
```

The core tests cover the engine, abilities, leaders, perks, the AI, the data loader, the presentation helpers, and the shipped JSON content. The network tests cover the protocol, real TCP and in-memory connections, LAN discovery, the lobby and full matches between a host and a guest (including rematches, disconnects and desync detection).

## Project layout

```
src/Gwota.Core/         rules engine (pure C#, no Godot dependency)
  Cards/                card, leader and enum definitions
  Match/                match state, commands, events, engine
  Abilities/            unit abilities, special effects, leaders, faction perks, strength calculation
  Ai/                   heuristic AI
  Data/                 JSON loader, validation, deck rules
  Session/              match session (human vs AI turn flow)
  Presentation/         UI-agnostic text and view helpers
  Settings/             player settings model and JSON serializer
  DeckBuilding/         deck editor, card book paging and filters, player deck collection
src/Gwota.Net/          LAN play (pure C#): protocol, TCP transport, discovery, lobby, host/guest match
tests/Gwota.Core.Tests/ unit tests (xunit v3 + Shouldly)
tests/Gwota.Net.Tests/  network tests
game/                   Godot project (UI only; references Gwota.Core)
  data/                 cards, leaders, decks as JSON (+ schemas)
  assets/               optional art, resolved by id
  scripts/              Godot C# scripts
docs/                   documentation
```

The Godot layer only renders state and forwards player input. Every rule lives in `Gwota.Core`, behind tests.

## Credits

- Fonts: game elements (the match board, cards, the deck builder and their dialogs) use Cinzel in capitals; menus, settings, network screens and information text (descriptions, tooltips, hints) use Alegreya Sans (Medium weight, with lining figures). To try another body font, drop its `<Family>-Medium/Bold/MediumItalic.ttf` files in `game/assets/fonts` and change `Fonts.BodyFamily`.
- Card titles and numbers use [Cinzel](https://github.com/google/fonts/tree/main/ofl/cinzel) by Natanael Gama, under the SIL Open Font License (`game/assets/fonts/Cinzel-OFL.txt`).
- Body text uses [Alegreya Sans](https://github.com/huertatipografica/Alegreya-Sans) by Huerta Tipográfica, under the SIL Open Font License (`game/assets/fonts/AlegreyaSans-OFL.txt`).
- Inspired by GWENT (CD PROJEKT RED) and Dota 2 (Valve). This is a fan project.

## Adding content

Cards, leaders and decks are one JSON file each, and art is optional and resolved by file name. See [docs/adding-cards.md](docs/adding-cards.md).

## CI and releases

- **CI** (`.github/workflows/ci.yml`): every push and pull request builds the solution and runs the tests.
- **Release** (`.github/workflows/release.yml`): pushing a `v*` tag runs the tests, then exports Windows and Linux builds. Each build is smoke-tested and the builds are published as a GitHub release. Tags containing `-` (such as `v0.2.0-beta.1`) are marked as prereleases.

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The version shown in-game is stamped from the tag at build time.

To export locally, install the Godot 4.7 mono export templates, then run:

```powershell
& $godot --headless --path game --export-release "Windows Desktop" build/windows/Gwota.exe
& $godot --headless --path game --export-release "Linux" build/linux/Gwota.x86_64
```

## License

[MIT](LICENSE)
