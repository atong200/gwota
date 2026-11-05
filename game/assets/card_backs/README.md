# Card backs

Each image here is a card back that players can pick per deck (deck builder: click the back next to the leader).

```
game/assets/card_backs/<back-id>.png   (or .webp / .jpg)
```

Example: `ember.png` -> back id `ember`, shown as "Ember" in the picker (`red_mist.png` would show "Red Mist"). The
bundled backs use single-word names: `aegis`, `gilded`, `crimson`.

- No code or data change is needed: every image in this folder is listed automatically (folders, dot-files and other
  files are ignored).
- Back ids are the file names without extension, lower-case with `_` between words. Renaming or deleting a file makes
  the decks that used it fall back to the default back,
  unless the old id is mapped to the new one in `CardBacks` (`src/Gwota.Core/Data/CardBacks.cs`).
- `default.png` is optional: it replaces the built-in placeholder as the default back (new decks, computer opponents,
  unknown backs from a LAN opponent). Without it, the default back is drawn by the game in the deck's faction colours
  (a "G" inside tilted lines) under the pewter frame `game/assets/ui/card_back_frame.png`, so it differs per faction.
- In a match, each side shows the back of its own deck (draw pile, opponent hand, cards flying face down).
- Recommended size: portrait, 3:4 ratio, ideally 768x1024, like the card faces. Other ratios are cropped around the
  centre. The game adds no frame, so the image can use the full card. The bundled backs draw their own bevelled
  pewter border, about 30 px wide at 768x1024; new art should include a similar border to match them.
- New images are imported by Godot once: open the project in the editor, or run the Godot executable with
  `--headless --path game --import`. Commit the generated `<name>.png.import` file with the image.
