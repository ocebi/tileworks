# Blast art generators

The blast pieces and UI kit are drawn as SVG by these scripts and rendered to PNG with headless Chrome.

```
python3 -I make_art.py <out>/svg && python3 -I ../render.py <out>/svg <out>/png <out>/sheet.png '#3A1A2C' 9 128
python3 -I make_ui.py <out>/ui-svg && python3 -I ../render.py <out>/ui-svg <out>/ui-png <out>/ui-sheet.png '#C63D31' 8 112
```

Copy `png/*.png` to `Assets/Games/Blast/Art/Design/` and `ui-png/*.png` to `Assets/Games/Blast/Art/Design/UI/`, then run
`Tileworks/Blast/Rebuild Scenes`. Names, sizes and 9-slice borders must stay as they are (the borders are listed in `BlastArtBuilder`).
`fx_hammer` and `fx_shuffle` (256 px) are the boosters' board effects, so they go to `Design/` with the other `fx_*` sprites.

The backdrops and mascot: `python3 -I make_scene.py <out>/scene && python3 -I ../render.py <out>/scene <out>/scene-png <out>/scene-sheet.png '#222' 3 300`.
Copy `bg_blast_home.png` and `bg_blast_game.png` to `Assets/Games/Blast/Art/Design/Backgrounds/` and `char_cube.png` to `Design/UI/`.
The backdrops' bottom edge colour is the camera clear colour in `BlastSceneBuilder`.
