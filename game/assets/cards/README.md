# Card art

Drop one image per card in the folder of its faction, named after the card `id` (the JSON file name). The folders
mirror `game/data/cards`:

```
game/assets/cards/<faction>/<card-id>.png   (or .webp / .jpg)
```

`<faction>` is one of `realms`, `abyss`, `horde` or `neutral`.

Example: `game/data/cards/realms/sven.json` (`"id": "sven"`) -> `game/assets/cards/realms/sven.png`.

A non-hero card can have up to three images so that its copies differ: `<card-id>.png`, `<card-id>_2.png`,
`<card-id>_3.png`. The copies in a deck cycle through them (1 image: shared; 2 images: the third copy reuses the first;
3 images: one each). The deck builder and card book show the first image. Heroes have a single image.

- No code or data change is needed: the art is picked up automatically the next time the game runs
  (open the project in the Godot editor once so the image gets imported).
- Cards without art show their initials on a faction-coloured placeholder.
- Recommended size: portrait, 3:4 ratio, ideally 768x1024. Every card face (hand, board, deck builder, hover preview,
  leaders) has that ratio, so the image fills it with no cropping. Other ratios are cropped around the centre.
- Keep the important part away from the top-left corner (strength medallion), the bottom-right corner (row icon) and
  the bottom ~15% (name banner).
- New images are imported by Godot once: open the project in the editor, or run the Godot executable with
  `--headless --path game --import`. Commit the generated `<name>.png.import` file with the image.
- Use a lower-case extension. The smoke test (`-- --smoke`, also run on release builds) fails on an image in the wrong
  faction folder, an image named after no card id, an upper-case extension, a second image for a hero, or a variant
  with a gap (`_3` without `_2`, `_2` without the base image), and lists each one.

Leader art goes in [`../leaders`](../leaders/README.md).
