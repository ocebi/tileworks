# Match-3 art generators

The match-3 pieces, blockers, board and backdrops are drawn as SVG by these scripts and rendered to PNG with headless Chrome.

```
python3 -I make_art.py <out>/svg && python3 -I ../render.py <out>/svg <out>/png <out>/sheet.png '#303D74' 8 128
python3 -I make_scene.py <out>/scene && python3 -I ../render.py <out>/scene <out>/scene-png <out>/scene-sheet.png '#222' 3 300
```

Copy `png/*.png` to `Assets/Games/Match3/Art/Design/`, `bg_match3_home.png` and `bg_match3_game.png` to `Design/Backgrounds/`
and `char_star.png` to `Design/UI/`. The hub card comes from `../hub/make_hub.py` once those are in place.

- Every piece is 128 px with a transparent background.
- `board_frame` is a 9-slice with 32 px borders.
- `cell_light`, `cell_dark`, `floor_1` and `floor_2` fill the whole cell, so they tile edge to edge.
- `select_ring` and `hint_glow` are white so the code can tint them.
- The balloon, the duck, the `fx_*` effects and the UI kit are Blast's.
- The bottom edge colour of the backdrops (`EDGE` in `make_scene.py`, #F0C27A) is the camera clear colour.
