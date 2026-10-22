"""Writes the Block Puzzle backdrops (a starry night over a town of block towers; Home sharp, Game blurred and dimmed) and the
block mascot."""
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from scenekit import CUBES, hill, toy_cube  # noqa: E402
from svgkit import blur, circ, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
W, H = 1080, 2340
HORIZON = 1330


def skyline(ground, seed):
    """Dark block towers with a few lit windows."""
    rng = random.Random(seed)
    out, x = "", -30
    while x < W + 30:
        cols, rows, s = rng.randint(1, 3), rng.randint(2, 6), rng.uniform(46, 64)
        for c in range(cols):
            height = rows - rng.randint(0, 2) if c else rows
            for r in range(height):
                bx, by = x + c * s, ground - (r + 1) * s
                out += rrect(bx + 2, by + 2, s - 4, s - 4, s * 0.18, 'fill="#2A1E66" stroke="#1A1248" stroke-width="3"')
                if rng.random() < 0.3:
                    out += rrect(bx + s * 0.32, by + s * 0.32, s * 0.36, s * 0.36, 4, 'fill="#FFD86B" fill-opacity="0.9"')
        x += cols * s + rng.uniform(10, 40)
    return out


def tetromino(x, ground, s, cells, colour, tilt=0):
    """A tetromino standing on the ground; cells are (column, row from the bottom)."""
    out = ""
    for c, r in cells:
        out += toy_cube(x + c * s, ground - (r + 1) * s, s, colour)
    return f'<g transform="rotate({tilt} {x} {ground})">{out}</g>'


def scene(towers=True):
    sky, skyd = vgrad([(0, "#0E0A2E", 1), (0.35, "#22195E", 1), (0.5, "#4A3290", 1), (0.57, "#A85A9E", 1)])
    glow, glowd = rgrad([(0, "#FFF6C8", 0.55), (1, "#FFF6C8", 0)], cx=0.5, cy=0.5, r=0.5)
    moon, moond = rgrad([(0, "#FFFBE0", 1), (0.6, "#FFEA8A", 1), (1, "#F5C842", 1)], cx=0.4, cy=0.35, r=0.7)
    trail, traild = vgrad([(0, "#FFFFFF", 0), (1, "#FFFFFF", 0.9)], x1=0, y1=0, x2=1, y2=0)
    tw, twd = blur(3)
    body = (f"<defs>{skyd}{glowd}{moond}{traild}{twd}</defs>" + f'<rect width="{W}" height="{H}" fill="url(#{sky})"/>')
    rng = random.Random(21)
    for _ in range(150):
        x, y = rng.uniform(0, W), rng.uniform(0, HORIZON - 80)
        body += p(circ(x, y, rng.uniform(1.2, 3.4)), f'fill="#FFFFFF" fill-opacity="{rng.uniform(0.35, 0.95):.2f}"')
    for x, y, s in ((140, 260, 1), (420, 180, 0.7), (980, 700, 0.8), (90, 900, 0.9), (700, 980, 0.6), (300, 1100, 0.7)):
        star = f"M{x} {y - 22 * s}Q{x + 3 * s} {y - 3 * s} {x + 22 * s} {y}Q{x + 3 * s} {y + 3 * s} {x} {y + 22 * s}Q{x - 3 * s} {y + 3 * s} {x - 22 * s} {y}Q{x - 3 * s} {y - 3 * s} {x} {y - 22 * s}z"
        body += p(star, f'fill="#FFFFFF" fill-opacity="0.7" filter="url(#{tw})"') + p(star, 'fill="#FFFFFF"')
    body += f'<g transform="rotate(-24 220 470)">' + rrect(60, 466, 300, 7, 3.5, f'fill="url(#{trail})"') + p(circ(360, 469.5, 7), 'fill="#FFFFFF"') + "</g>"
    # A sleepy moon with a soft halo.
    mx, my = 850, 330
    body += p(circ(mx, my, 260), f'fill="url(#{glow})"')
    body += p(circ(mx, my, 118), f'fill="url(#{moon})" stroke="#E0A82E" stroke-width="5"')
    for cx, cy, r in ((mx + 50, my - 52, 18), (mx - 62, my + 58, 12), (mx + 64, my + 40, 9)):
        body += p(circ(cx, cy, r), 'fill="#F0C850" fill-opacity="0.7"')
    for x in (mx - 40, mx + 34):
        body += p(f"M{x - 20} {my - 4}Q{x} {my + 12} {x + 20} {my - 4}", 'fill="none" stroke="#7A4A10" stroke-width="7" stroke-linecap="round"')
    body += p(f"M{mx - 22} {my + 34}Q{mx - 3} {my + 52} {mx + 18} {my + 34}", 'fill="none" stroke="#7A4A10" stroke-width="7" stroke-linecap="round"')
    body += f'<ellipse cx="{mx - 70}" cy="{my + 26}" rx="20" ry="12" fill="#FF9AA6" fill-opacity="0.6"/><ellipse cx="{mx + 66}" cy="{my + 26}" rx="20" ry="12" fill="#FF9AA6" fill-opacity="0.6"/>'
    body += '<ellipse cx="808" cy="250" rx="34" ry="14" transform="rotate(-30 808 250)" fill="#FFFFFF" fill-opacity="0.6"/>'
    # Town of dark block towers on the horizon, glowing hills, then bright tetromino towers in front (left out behind the
    # board, where they would look like playable pieces).
    body += skyline(HORIZON + 30, 4)
    body += hill([(-40, HORIZON + 40), (240, HORIZON - 10), (540, HORIZON + 30), (820, HORIZON - 20), (1120, HORIZON + 20)], "#5A3AA8", "#2A1B66", "#9C7AF0", H + 20)
    if towers:
        body += tetromino(70, 1520, 72, [(0, 0), (0, 1), (0, 2), (1, 0)], "blue", -3) + tetromino(880, 1500, 68, [(0, 0), (1, 0), (1, 1), (2, 1)], "red", 2)
        body += tetromino(430, 1440, 50, [(0, 0), (1, 0), (2, 0), (1, 1)], "yellow")
    body += hill([(-40, 1700), (260, 1620), (560, 1680), (860, 1600), (1120, 1650)], "#4A2E96", "#1E1450", "#8A6AE0", H + 20)
    if towers:
        body += tetromino(40, 2140, 110, [(0, 0), (1, 0), (0, 1), (1, 1)], "purple", -4) + tetromino(820, 2190, 104, [(0, 0), (0, 1), (0, 2), (0, 3)], "teal", 3)
        body += tetromino(170, 1830, 70, [(0, 0), (1, 0), (2, 0), (2, 1)], "green", 2) + tetromino(820, 1800, 66, [(0, 0), (1, 0), (1, 1), (1, 2)], "yellow", -3)
    ff, ffd = blur(4)
    body += f"<defs>{ffd}</defs>"
    for _ in range(26):
        x, y = rng.uniform(20, 1060), rng.uniform(1700, 2300)
        body += p(circ(x, y, 9), f'fill="#FFE27A" fill-opacity="0.6" filter="url(#{ff})"') + p(circ(x, y, 3.5), 'fill="#FFF6C8"')
    return body


home = scene()
with open(os.path.join(OUT, "bg_bp_home.svg"), "w") as f:
    f.write(svg(home, f"0 0 {W} {H}", (W, H)))
b, bd = blur(7)
dim, dimd = vgrad([(0, "#05031A", 0.2), (1, "#05031A", 0.35)])
with open(os.path.join(OUT, "bg_bp_game.svg"), "w") as f:
    f.write(svg(f"<defs>{bd}{dimd}</defs><g filter=\"url(#{b})\">{scene(towers=False)}</g>" + f'<rect width="{W}" height="{H}" fill="url(#{dim})"/>',
                f"0 0 {W} {H}", (W, H)))


def block_buddy():
    light, base, dark, deep = CUBES["purple"]
    body_g, body_gd = vgrad([(0, light, 1), (0.45, base, 1), (1, dark, 1)])
    face_g, face_gd = rgrad([(0, light, 1), (0.6, base, 1), (1, base, 0)], cx=0.5, cy=0.38, r=0.62)
    soft, softd = blur(4)
    eye_g, eye_gd = rgrad([(0, "#FFFFFF", 1), (0.8, "#FFFFFF", 1), (1, "#E3E8F2", 1)], cx=0.45, cy=0.4, r=0.6)
    c, cd = clip(rrect(56, 60, 400, 380, 92))
    out = (f"<defs>{body_gd}{face_gd}{softd}{eye_gd}{cd}</defs>"
           + rrect(56, 84, 400, 380, 92, f'fill="{deep}"')
           + rrect(56, 60, 400, 380, 92, f'fill="url(#{body_g})"')
           + f'<g clip-path="url(#{c})">' + rrect(96, 90, 320, 290, 70, f'fill="url(#{face_g})" filter="url(#{soft})"') + "</g>"
           + '<ellipse cx="200" cy="104" rx="104" ry="30" transform="rotate(-6 200 104)" fill="#fff" fill-opacity="0.6"/>'
           + p(circ(108, 98, 14), 'fill="#fff" fill-opacity="0.9"'))
    out += p("M134 190Q180 166 226 190", f'fill="none" stroke="{deep}" stroke-width="18" stroke-linecap="round"')
    out += f'<ellipse cx="180" cy="258" rx="54" ry="62" fill="url(#{eye_g})" stroke="{deep}" stroke-width="6"/>'
    out += p(circ(186, 252, 27), 'fill="#1A0E33"') + p(circ(176, 240, 10), 'fill="#fff"')
    out += p("M290 262Q332 228 374 262", f'fill="none" stroke="{deep}" stroke-width="16" stroke-linecap="round"')
    out += p("M296 186Q334 172 372 196", f'fill="none" stroke="{deep}" stroke-width="18" stroke-linecap="round"')
    out += '<ellipse cx="118" cy="330" rx="34" ry="20" fill="#FF9AC8" fill-opacity="0.75"/><ellipse cx="394" cy="330" rx="34" ry="20" fill="#FF9AC8" fill-opacity="0.75"/>'
    out += p("M196 330Q256 392 316 330", f'fill="none" stroke="{deep}" stroke-width="14" stroke-linecap="round"')
    for x in (130, 382):
        out += rrect(x - 56, 430, 112, 70, 34, f'fill="url(#{body_g})" stroke="{deep}" stroke-width="6"')
    out += rrect(56, 60, 400, 380, 92, f'fill="none" stroke="{deep}" stroke-opacity="0.6" stroke-width="5"')
    return out


with open(os.path.join(OUT, "char_block.svg"), "w") as f:
    f.write(svg(block_buddy(), "0 0 512 512", (384, 384)))
print("block puzzle scene svgs")
