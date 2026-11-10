"""Writes the game hub art: a card per game (a glossy button in the game's colour showing its pieces), the same pieces
alone as the game's hero for the hub and its Home, the gold glow behind the selected card, the mosaic mark beside the
wordmark and the shade under the cards. The cards and heroes embed the games' piece SVGs, so render those first into one
folder and run this again whenever they change:

    (cd ../tilematch && python3 -I make_tiles.py <pieces>/tilematch)
    (cd ../blast && python3 -I make_art.py <pieces>/blast)
    (cd ../match3 && python3 -I make_art.py <pieces>/match3)
    python3 -I make_hub.py <out>/svg <pieces> && python3 -I ../render.py <out>/svg <out>/png <out>/sheet.png '#3A6FB0' 6 400

then copy png/hub_*.png to Assets/Games/Hub/Art/ and each hero to its game's UI art folder: hero_tiles.png to
TileMatch/Art/UI, hero_blocks.png to BlockPuzzle/Art/UI, hero_cubes.png to Blast/Art/Design/UI and hero_gems.png to
Match3/Art/Design/UI."""
import math
import os
import sys

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
from scenekit import CUBES, toy_cube  # noqa: E402
from svgkit import blur, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT, PIECES = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
ANIMALS = os.path.join(os.path.dirname(os.path.dirname(TOOLS)), "Assets", "ThirdParty", "Kenney", "AnimalPackRemastered")

# The card body; the canvas leaves room below for the lip and the shadow. The glow shares the body's centre.
W, H = 320, 344
BX, BY, BS, BR = 10, 10, 300, 66
CX, CY = BX + BS / 2, BY + BS / 2
GLOW_PAD = 40

# Name: (centre light, body, lip, rim).
LOOKS = {
    "tilematch": ("#C9F77E", "#2FA648", "#1B7032", "#125024"),
    "blockpuzzle": ("#FFEE8A", "#FF961F", "#C25C08", "#8E3F04"),
    "blast": ("#9BE6FF", "#1F6FE0", "#134AA8", "#0C3278"),
    "match3": ("#FFA6E0", "#B928A8", "#7F1578", "#580C55"),
}


def write(name, size, body):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, f"0 0 {size[0]} {size[1]}", size))


def img(path, x, y, w, h, tilt=0):
    return (f'<image href="file://{path}" x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}"'
            f' transform="rotate({tilt} {x + w / 2:.1f} {y + h / 2:.1f})"/>')


def ground(cx, cy, rx, ry):
    b, bd = blur(6)
    return f'<defs>{bd}</defs><ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="#000" fill-opacity="0.3" filter="url(#{b})"/>'


def piece(rel, cx, cy, s, tilt=0, shadow=False):
    """A square piece SVG from the games' generators, placed by its centre."""
    out = ""
    if shadow:
        b, bd = blur(s * 0.05)
        out += f'<defs>{bd}</defs><ellipse cx="{cx + s * 0.03}" cy="{cy + s * 0.44}" rx="{s * 0.36}" ry="{s * 0.09}" fill="#000" fill-opacity="0.28" filter="url(#{b})"/>'
    return out + img(os.path.join(PIECES, rel), cx - s / 2, cy - s / 2, s, s, tilt)


def sparkle(x, y, r, alpha=0.9):
    g, gd = blur(r * 0.25)
    k = r * 0.12
    d = (f"M{x} {y - r}C{x + k} {y - k} {x + k} {y - k} {x + r} {y}C{x + k} {y + k} {x + k} {y + k} {x} {y + r}"
         f"C{x - k} {y + k} {x - k} {y + k} {x - r} {y}C{x - k} {y - k} {x - k} {y - k} {x} {y - r}z")
    return f"<defs>{gd}</defs>" + p(d, f'fill="#fff" fill-opacity="{0.8 * alpha}" filter="url(#{g})"') + p(d, f'fill="#fff" fill-opacity="{alpha}"')


def block(x, y, s, colour, tilt=0):
    """A glossy jelly block in the cube palette."""
    light, base, dark, deep = CUBES[colour]
    r = s * 0.24
    body, bodyd = vgrad([(0, light, 1), (0.45, base, 1), (1, dark, 1)])
    face, faced = rgrad([(0, light, 1), (0.6, base, 1), (1, base, 0)], cx=0.5, cy=0.38, r=0.62)
    gloss, glossd = vgrad([(0, "#fff", 0.85), (1, "#fff", 0)])
    soft, softd = blur(s * 0.025)
    c, cd = clip(rrect(x, y, s, s * 0.95, r))
    return (f'<defs>{bodyd}{faced}{glossd}{softd}{cd}</defs><g transform="rotate({tilt} {x + s / 2} {y + s / 2})">'
            + rrect(x, y + s * 0.07, s, s * 0.95, r, f'fill="{deep}"')
            + rrect(x, y, s, s * 0.95, r, f'fill="url(#{body})"')
            + f'<g clip-path="url(#{c})">' + rrect(x + s * 0.1, y + s * 0.09, s * 0.8, s * 0.7, r * 0.8, f'fill="url(#{face})" filter="url(#{soft})"') + "</g>"
            + f'<ellipse cx="{x + s * 0.4}" cy="{y + s * 0.2}" rx="{s * 0.24}" ry="{s * 0.08}" transform="rotate(-6 {x + s * 0.4} {y + s * 0.2})" fill="url(#{gloss})"/>'
            + f'<circle cx="{x + s * 0.2}" cy="{y + s * 0.18}" r="{s * 0.035}" fill="#fff" fill-opacity="0.9"/>'
            + rrect(x, y, s, s * 0.95, r, f'fill="none" stroke="{deep}" stroke-opacity="0.55" stroke-width="{s * 0.02}"')
            + "</g>")


def tile(cx, cy, w, animal, tilt=0):
    """A Tile Match tile (288 x 320 art) with its animal on the top face."""
    h = w * 320 / 288
    x, y = cx - w / 2, cy - w / 2
    a = w * 0.7
    ah = a * 132 / 154
    b, bd = blur(w * 0.05)
    return (f'<defs>{bd}</defs><g transform="rotate({tilt} {cx} {cy})">'
            + rrect(x + w * 0.04, y + w * 0.14, w, w, w * 0.2, f'fill="#0A2A10" fill-opacity="0.35" filter="url(#{b})"')
            + img(os.path.join(PIECES, "tilematch", "tile_face.svg"), x, y, w, h)
            + img(os.path.join(ANIMALS, animal + ".png"), cx - a / 2, cy - ah / 2 - w * 0.01, a, ah)
            + "</g>")


def tilematch():
    return (tile(78, 138, 112, "parrot", -18) + tile(242, 138, 112, "frog", 18) + tile(160, 182, 132, "panda")
            + sparkle(266, 64, 11))


def blockpuzzle():
    s, x0, y0 = 72, CX - 112, CY - 102
    cells = [(0, 0, "purple"), (0, 1, "purple"), (1, 1, "blue"), (2, 1, "blue"),
             (0, 2, "purple"), (1, 2, "purple"), (2, 2, "blue")]
    out = ground(CX, 262, 110, 16) + f'<g transform="rotate(-7 {CX} {CY})">'
    out += "".join(block(x0 + col * s, y0 + row * s, s, colour) for col, row, colour in cells)
    return out + "</g>" + block(190, 32, 76, "red", 14) + sparkle(70, 58, 10)


def blast():
    s = 96
    cubes = ((0, 0, "cube_4_yellow"), (1, 0, "cube_1_red"), (0, 1, "cube_1_red"), (1, 1, "cube_1_red"))
    out = ground(CX - 14, 276, 105, 16) + f'<g transform="rotate(-8 {CX} {CY})">'
    out += "".join(piece(f"blast/{name}.svg", CX - s / 2 + col * s - 20, CY - s / 2 + row * s + 26, s + 8) for col, row, name in cubes)
    return out + "</g>" + piece("blast/rocket_row.svg", 218, 74, 150, -38) + sparkle(64, 62, 10)


def match3():
    return (ground(CX, 262, 105, 16)
            + piece("match3/gem_1_red.svg", 92, 122, 130, -12, True) + piece("match3/gem_4_yellow.svg", 228, 120, 132, 12, True)
            + piece("match3/gem_2_blue.svg", 160, 176, 186, 0, True) + sparkle(256, 58, 11))


def card(name, content):
    """A lipped, glossy button in the game's colour with faint rays; the pieces are clipped to the body."""
    light, base, lip, rim = LOOKS[name]
    ry = BY + BS * 0.46
    g, gd = rgrad([(0, light, 0.55), (0.6, base, 0), (1, lip, 0.55)], cx=0.5, cy=0.46, r=0.8)
    fade, faded = rgrad([(0, "#fff", 1), (0.7, "#fff", 0.4), (1, "#fff", 0)], cx=0.5, cy=0.46, r=0.7)
    gloss, glossd = vgrad([(0, "#FFFFFF", 0.2), (1, "#FFFFFF", 0)])
    floor, floord = vgrad([(0, "#000", 0), (1, "#000", 0.22)])
    sh, shd = blur(9)
    edge, edged = blur(1.6)
    c, cd = clip(rrect(BX, BY, BS, BS, BR))
    rays = ""
    for i in range(20):
        a1 = math.pi * i / 10 + 0.1
        a2 = a1 + math.pi / 20
        rays += p(f"M{CX} {ry}L{CX + 400 * math.cos(a1):.0f} {ry + 400 * math.sin(a1):.0f}L{CX + 400 * math.cos(a2):.0f} {ry + 400 * math.sin(a2):.0f}z")
    out = (f'<defs>{gd}{faded}{glossd}{floord}{shd}{edged}{cd}<mask id="rays"><rect x="{BX}" y="{BY}" width="{BS}" height="{BS}" fill="url(#{fade})"/></mask></defs>'
           + rrect(BX + 6, BY + 22, BS - 12, BS, BR, f'fill="#1A0503" fill-opacity="0.38" filter="url(#{sh})"')
           + rrect(BX, BY + 12, BS, BS, BR, f'fill="{lip}" stroke="{rim}" stroke-width="5"')
           + rrect(BX, BY, BS, BS, BR, f'fill="{base}"') + rrect(BX, BY, BS, BS, BR, f'fill="url(#{g})"')
           + f'<g clip-path="url(#{c})">'
           + f'<g mask="url(#rays)" fill="#fff" fill-opacity="0.07">{rays}</g>'
           + rrect(BX, BY + BS * 0.6, BS, BS * 0.4, 0, f'fill="url(#{floor})"')
           + content
           + rrect(BX + 22, BY + 7, BS - 44, BS * 0.26, BR * 0.72, f'fill="url(#{gloss})"')
           + "</g>"
           + rrect(BX + 4, BY + 4, BS - 8, BS - 8, BR - 4, f'fill="none" stroke="#fff" stroke-opacity="0.3" stroke-width="2.5" filter="url(#{edge})"')
           + rrect(BX, BY, BS, BS, BR, f'fill="none" stroke="{rim}" stroke-width="5"'))
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


for name, hero, content in (("tilematch", "tiles", tilematch), ("blockpuzzle", "blocks", blockpuzzle), ("blast", "cubes", blast),
                            ("match3", "gems", match3)):
    card(name, content())
    with open(os.path.join(OUT, f"hero_{hero}.svg"), "w") as out:
        out.write(svg(content(), f"0 0 {W} {W}", (384, 384)))
glow()
mark()
shade()
print("hub svgs")
