"""Writes the game hub art: a card per game (a window onto its Home backdrop with its mascot standing in it), the gold glow
behind the selected card, the mosaic mark beside the wordmark and the shade under the cards. The cards embed the games'
backdrops and mascots, so run this again whenever those change:

    python3 -I make_hub.py <out>/svg && python3 -I ../render.py <out>/svg <out>/png <out>/sheet.png '#3A6FB0' 6 400

then copy png/*.png to Assets/Games/Hub/Art/."""
import os
import sys

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
from scenekit import toy_cube  # noqa: E402
from svgkit import blur, clip, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
GAMES = os.path.join(os.path.dirname(os.path.dirname(TOOLS)), "Assets", "Games")
BACKDROP_W, BACKDROP_H = 1080, 2340

# Name: (backdrop, mascot, top of the square cropped from the backdrop, mascot tilt).
CARDS = {
    "tilematch": ("TileMatch/Art/Backgrounds/bg_tm_home.png", "TileMatch/Art/UI/char_bear.png", 820, -4),
    "blockpuzzle": ("BlockPuzzle/Art/Backgrounds/bg_bp_home.png", "BlockPuzzle/Art/UI/char_block.png", 180, 4),
    "blast": ("Blast/Art/Design/Backgrounds/bg_blast_home.png", "Blast/Art/Design/UI/char_cube.png", 520, -4),
    "match3": ("Match3/Art/Design/Backgrounds/bg_match3_home.png", "Match3/Art/Design/UI/char_star.png", 860, 4),
}

# The card body; the canvas leaves room below for the lip and the shadow. The glow shares the body's centre.
W, H = 320, 344
BX, BY, BS, BR = 10, 10, 300, 66
WIN, WR = 16, 50
RIM = "#A89482"
GLOW_PAD = 40


def write(name, size, body):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, f"0 0 {size[0]} {size[1]}", size))


def card(name, backdrop, mascot, crop_top, tilt):
    frame_g, frame_gd = vgrad([(0, "#FFFFFF", 1), (0.6, "#FBF7F2", 1), (1, "#ECE2D6", 1)])
    gloss_g, gloss_gd = vgrad([(0, "#FFFFFF", 0.55), (1, "#FFFFFF", 0)])
    shade_g, shade_gd = vgrad([(0, "#000000", 0), (1, "#000000", 0.28)])
    sh, shd = blur(9)
    soft, softd = blur(5)
    edge, edged = blur(2)
    wx, wy, ws = BX + WIN, BY + WIN, BS - 2 * WIN
    window = rrect(wx, wy, ws, ws, WR)
    c, cd = clip(window)
    scale = ws / BACKDROP_W
    m = ws * 0.7
    out = (f"<defs>{frame_gd}{gloss_gd}{shade_gd}{shd}{softd}{edged}{cd}</defs>"
           + rrect(BX + 6, BY + 22, BS - 12, BS, BR, f'fill="#1A0503" fill-opacity="0.38" filter="url(#{sh})"')
           + rrect(BX, BY + 12, BS, BS, BR, f'fill="#CDBBA8" stroke="{RIM}" stroke-width="5"')
           + rrect(BX, BY, BS, BS, BR, f'fill="url(#{frame_g})"')
           + rrect(BX + 3, BY + 4, BS - 6, BS - 6, BR - 3, f'fill="none" stroke="#fff" stroke-opacity="0.9" stroke-width="5" filter="url(#{edge})"')
           + f'<g clip-path="url(#{c})">'
           + f'<image href="file://{GAMES}/{backdrop}" x="{wx}" y="{wy - crop_top * scale:.2f}" width="{ws}" height="{BACKDROP_H * scale:.2f}"/>'
           + rrect(wx, wy + ws * 0.55, ws, ws * 0.45, 0, f'fill="url(#{shade_g})"')
           + f'<ellipse cx="{wx + ws / 2}" cy="{wy + ws - 18}" rx="{m * 0.36}" ry="{m * 0.07}" fill="#0A0A20" fill-opacity="0.35" filter="url(#{soft})"/>'
           + f'<g transform="rotate({tilt} {wx + ws / 2} {wy + ws - 20})">'
           + f'<image href="file://{GAMES}/{mascot}" x="{wx + (ws - m) / 2:.2f}" y="{wy + ws - m - 6:.2f}" width="{m:.2f}" height="{m:.2f}"/></g>'
           + rrect(wx + 18, wy + 6, ws - 36, ws * 0.3, WR * 0.8, f'fill="url(#{gloss_g})"')
           + "</g>"
           + rrect(wx, wy, ws, ws, WR, 'fill="none" stroke="#5A3A20" stroke-opacity="0.45" stroke-width="3"')
           + rrect(BX, BY, BS, BS, BR, f'fill="none" stroke="{RIM}" stroke-width="5"'))
    write(f"hub_card_{name}", (W, H), out)


def glow():
    """A gold ring a little outside the card body with a warm halo, drawn behind the selected card."""
    w, h = W + 2 * GLOW_PAD, H + 2 * GLOW_PAD
    x, y, s, r = BX + GLOW_PAD - 12, BY + GLOW_PAD - 12, BS + 24, BR + 12
    gold, goldd = vgrad([(0, "#FFF3A8", 1), (0.45, "#FFD23A", 1), (1, "#F29A12", 1)])
    halo, halod = blur(11)
    edge, edged = blur(1.5)
    out = (f"<defs>{goldd}{halod}{edged}</defs>"
           + rrect(x, y, s, s + 12, r, f'fill="none" stroke="#FFC42E" stroke-opacity="0.9" stroke-width="30" filter="url(#{halo})"')
           + rrect(x, y, s, s + 12, r, f'fill="none" stroke="#A85E08" stroke-width="20"')
           + rrect(x, y, s, s + 12, r, f'fill="none" stroke="url(#{gold})" stroke-width="14"')
           + rrect(x - 3, y - 3, s + 6, s + 18, r + 3, 'fill="none" stroke="#FFFBE0" stroke-opacity="0.8" stroke-width="2.5" filter="url(#{0})"'.format(edge)))
    write("hub_card_glow", (w, h), out)


def mark():
    """Four toy tiles set like a mosaic, one per colour the games share."""
    s, gap = 78, 10
    out = ""
    for i, (colour, tilt) in enumerate((("blue", -5), ("purple", 4), ("red", 3), ("yellow", -4))):
        col, row = i % 2, i // 2
        out += toy_cube(14 + col * (s + gap), 12 + row * (s + gap), s, colour, tilt)
    write("hub_mark", (192, 192), out)


def shade():
    """A white fade from clear at the top to solid at the bottom; tinted dark, it seats the cards and Play on any backdrop."""
    g, gd = vgrad([(0, "#FFFFFF", 0), (0.5, "#FFFFFF", 0.45), (1, "#FFFFFF", 1)])
    write("hub_shade", (16, 512), f"<defs>{gd}</defs>" + f'<rect width="16" height="512" fill="url(#{g})"/>')


for name, (backdrop, mascot, crop_top, tilt) in CARDS.items():
    card(name, backdrop, mascot, crop_top, tilt)
glow()
mark()
shade()
print("hub svgs")
