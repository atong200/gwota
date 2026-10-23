# Card art

Drop one image per card here, named after the card `id` (the JSON file name):

```
game/assets/cards/<card-id>.png   (or .webp / .jpg)
```

Example: `game/data/cards/realms/sven.json` (`"id": "sven"`) -> `game/assets/cards/sven.png`.

- No code or data change is needed: the art is picked up automatically the next time the game runs
  (open the project in the Godot editor once so the image gets imported).
- Cards without art show their initials on a faction-coloured placeholder.
- Recommended size: portrait, 2:3 ratio (e.g. 400x600). The image covers the whole card face, so other ratios are
  cropped around the centre.

Leader art goes in [`../leaders`](../leaders/README.md).
