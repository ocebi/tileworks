"""Writes the Tile Match backdrops (a forest lake in the morning; Home sharp, Game blurred and dimmed) and the bear mascot."""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from scenekit import bush, cloud, hill, tree  # noqa: E402
from svgkit import blur, circ, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
W, H = 1080, 2340
SHORE = 1270
WATER_END = 1760


def pine(x, ground, s, top, bottom, rim):
    g, gd = vgrad([(0, top, 1), (1, bottom, 1)])
    tiers = ""
    for i, (w, y0, y1) in enumerate(((0.5, 0.0, 0.42), (0.62, 0.26, 0.7), (0.74, 0.52, 0.95))):
        tiers += p(f"M{x} {ground - s * (1 - y0)}L{x + s * w / 2} {ground - s * (1 - y1)}L{x - s * w / 2} {ground - s * (1 - y1)}z")
    trunk = rrect(x - s * 0.05, ground - s * 0.08, s * 0.1, s * 0.12, s * 0.02, 'fill="#5A3A22"')
    return f"<defs>{gd}</defs>" + trunk + f'<g fill="url(#{g})" stroke="{rim}" stroke-width="{max(2, s * 0.02)}" stroke-linejoin="round">{tiers}</g>'


def forest(ground, count, size, top, bottom, rim, seed):
    rng = random.Random(seed)
    out = ""
    x = -40
    while x < W + 40:
        s = size * rng.uniform(0.75, 1.2)
        out += pine(x, ground + rng.uniform(-8, 8), s, top, bottom, rim)
        x += s * rng.uniform(0.32, 0.5)
    return out


def scene():
    sky, skyd = vgrad([(0, "#4FAEF0", 1), (0.25, "#8FD2FF", 1), (0.45, "#D6F0FF", 1), (0.53, "#FFEBC8", 1)])
    sun, sund = rgrad([(0, "#FFF8E0", 1), (0.25, "#FFF1C4", 0.85), (1, "#FFF1C4", 0)], cx=0.5, cy=0.5, r=0.5)
    fade, faded = rgrad([(0, "#fff", 1), (0.5, "#fff", 0.35), (1, "#fff", 0)], cx=0.5, cy=620 / H, r=0.5)
    mount, mountd = vgrad([(0, "#B9C9F2", 1), (1, "#93A9E0", 1)])
    water, waterd = vgrad([(0, "#A6E4F7", 1), (0.35, "#5EC0E8", 1), (1, "#2B84BE", 1)])
    rays = ""
    for i in range(18):
        a1 = 2 * math.pi * i / 18
        a2 = a1 + math.pi / 18
        rays += f'<path d="M540 620L{540 + 2400 * math.cos(a1):.0f} {620 + 2400 * math.sin(a1):.0f}L{540 + 2400 * math.cos(a2):.0f} {620 + 2400 * math.sin(a2):.0f}z"/>'
    body = (f"<defs>{skyd}{sund}{faded}{mountd}{waterd}"
            f'<mask id="rays"><rect width="{W}" height="{H}" fill="url(#{fade})"/></mask></defs>'
            + f'<rect width="{W}" height="{H}" fill="url(#{sky})"/>'
            + f'<g mask="url(#rays)" fill="#FFFFFF" fill-opacity="0.12">{rays}</g>'
            + f'<circle cx="540" cy="620" r="420" fill="url(#{sun})"/>'
            + cloud(160, 300, 1.05) + cloud(910, 430, 1.15) + cloud(720, 170, 0.6, 0.9) + cloud(230, 880, 0.7, 0.85))
    body += p("M-20 1190L140 1010L260 1110L420 960L560 1100L700 1000L860 1120L980 1030L1100 1150L1100 1260L-20 1260z",
              f'fill="url(#{mount})" stroke="#A7B9EA" stroke-width="4" stroke-linejoin="round"')
    body += p("M420 960L384 1004L404 998L420 1018L440 996L458 1002z M700 1000L668 1038L688 1034L702 1050L718 1032L734 1038z", 'fill="#FFFFFF" fill-opacity="0.9"')
    # Far and near pine forest on the far shore, the grass strip, then the lake with their reflections.
    far = forest(SHORE - 30, 0, 110, "#7FB9A8", "#5E9A8C", "#4E8A7C", 3)
    near = forest(SHORE, 0, 160, "#4E9A72", "#2E6E52", "#245A44", 5)
    body += far + near
    body += rrect(-20, SHORE - 14, W + 40, 30, 14, 'fill="#8CD16A"')
    rc, rcd = clip(f'<rect y="{SHORE + 12}" width="{W}" height="{WATER_END - SHORE}"/>')
    body += f"<defs>{rcd}</defs>" + f'<rect y="{SHORE + 12}" width="{W}" height="{WATER_END - SHORE}" fill="url(#{water})"/>'
    body += (f'<g clip-path="url(#{rc})" opacity="0.28"><g transform="translate(0 {2 * SHORE + 24}) scale(1 -1)">{near}</g></g>')
    rng = random.Random(11)
    shimmer = ""
    for _ in range(40):
        x, y = rng.uniform(0, W), rng.uniform(SHORE + 60, WATER_END - 20)
        w = rng.uniform(30, 120) * (0.5 + (y - SHORE) / (WATER_END - SHORE))
        shimmer += rrect(x, y, w, 5, 2.5, f'fill="#FFFFFF" fill-opacity="{rng.uniform(0.25, 0.55):.2f}"')
    body += shimmer
    pad, padd = rgrad([(0, "#8EE07A", 1), (1, "#3E9E35", 1)], cx=0.4, cy=0.35, r=0.7)
    for x, y, s in ((170, 1560, 1.0), (300, 1640, 0.7), (890, 1600, 0.9)):
        body += (f"<defs>{padd}</defs>" + p(f"M{x} {y}m{-60 * s} 0a{60 * s} {22 * s} 0 1 0 {120 * s} 0a{60 * s} {22 * s} 0 0 0 -50 -18L{x} {y}z",
                                             f'fill="url(#{pad})" stroke="#2E7A2A" stroke-width="3"'))
        if s > 0.8:
            body += "".join(p(f"M{x} {y - 14 * s}l{dx * s} {-26 * s}l{8 * s} {22 * s}z", 'fill="#FF9AC0" stroke="#D45A8A" stroke-width="2"')
                            for dx in (-14, -4, 6))
    # Near bank: grass, reeds, trees and a wooden dock running out into the lake.
    body += hill([(-40, 1790), (260, 1720), (560, 1770), (860, 1710), (1120, 1760)], "#7ED957", "#3E9E35", "#C6F09A", H + 20)
    for x, h in ((60, 150), (95, 190), (130, 140), (970, 170), (1010, 140), (1040, 190)):
        body += p(f"M{x} 1790Q{x - 10} {1790 - h * 0.6} {x + 4} {1790 - h}", 'fill="none" stroke="#3E8A3A" stroke-width="7" stroke-linecap="round"')
        body += rrect(x - 6, 1790 - h - 10, 12, 44, 6, 'fill="#8A5A30" stroke="#5A3418" stroke-width="2"')
    wood, woodd = vgrad([(0, "#C98B4F", 1), (1, "#A8672E", 1)])
    dock = "M470 1640L610 1640L720 2360L360 2360z"
    planks = "".join(p(f"M{470 - (y - 1640) * 0.153:.1f} {y}L{610 + (y - 1640) * 0.153:.1f} {y}", 'stroke="#7A4520" stroke-width="4"')
                     for y in range(1680, 2360, 46))
    body += (f"<defs>{woodd}</defs>" + rrect(458, 1640, 14, 70, 4, 'fill="#6E3F1C"') + rrect(608, 1640, 14, 70, 4, 'fill="#6E3F1C"')
             + p(dock, f'fill="url(#{wood})" stroke="#6E3F1C" stroke-width="6" stroke-linejoin="round"') + planks
             + p("M470 1640L610 1640", 'stroke="#E9B47A" stroke-width="5"'))
    body += tree(80, 1880, 170) + tree(1000, 1850, 160) + bush(250, 1890, 100) + bush(860, 1930, 90)
    body += pine(180, 2140, 220, "#5CB07E", "#2E6E52", "#245A44") + pine(930, 2180, 240, "#5CB07E", "#2E6E52", "#245A44")
    rng = random.Random(9)
    for _ in range(40):
        x, y = rng.uniform(20, 1060), rng.uniform(1830, 2320)
        if 330 < x < 750:
            continue
        colour = rng.choice(["#FFFFFF", "#FFD43B", "#FF7A8F", "#C9A4FF"])
        r = rng.uniform(6, 11)
        body += p(circ(x, y, r), f'fill="{colour}" stroke="#2E7A2A" stroke-width="2"') + p(circ(x, y, r * 0.35), 'fill="#FFB02E"')
    return body


home = scene()
with open(os.path.join(OUT, "bg_tm_home.svg"), "w") as f:
    f.write(svg(home, f"0 0 {W} {H}", (W, H)))
b, bd = blur(7)
dim, dimd = vgrad([(0, "#0B1A3A", 0.22), (1, "#0B2A10", 0.35)])
with open(os.path.join(OUT, "bg_tm_game.svg"), "w") as f:
    f.write(svg(f"<defs>{bd}{dimd}</defs><g filter=\"url(#{b})\">{home}</g>" + f'<rect width="{W}" height="{H}" fill="url(#{dim})"/>',
                f"0 0 {W} {H}", (W, H)))


def bear():
    fur, furd = rgrad([(0, "#D9A066", 1), (0.55, "#A86A36", 1), (1, "#7A4A22", 1)], cx=0.4, cy=0.32, r=0.75)
    ear, eard = rgrad([(0, "#C98B52", 1), (1, "#7A4A22", 1)], cx=0.4, cy=0.35, r=0.7)
    muzzle, muzzled = vgrad([(0, "#FBE8C8", 1), (1, "#E6C08E", 1)])
    nose, nosed = rgrad([(0, "#6A4A3A", 1), (1, "#2A160C", 1)], cx=0.4, cy=0.3, r=0.7)
    eye, eyed = rgrad([(0, "#FFFFFF", 1), (0.8, "#FFFFFF", 1), (1, "#E3E8F2", 1)], cx=0.45, cy=0.4, r=0.6)
    rim = "#4E2C12"
    out = f"<defs>{furd}{eard}{muzzled}{nosed}{eyed}</defs>"
    for x in (130, 382):
        out += p(circ(x, 130, 64), f'fill="url(#{ear})" stroke="{rim}" stroke-width="7"') + p(circ(x, 136, 34), 'fill="#F0C49A"')
    out += f'<ellipse cx="256" cy="282" rx="196" ry="176" fill="url(#{fur})" stroke="{rim}" stroke-width="7"/>'
    out += '<ellipse cx="190" cy="168" rx="70" ry="24" transform="rotate(-18 190 168)" fill="#FFFFFF" fill-opacity="0.35"/>'
    for x, tilt in ((178, -10), (334, 10)):
        out += p(f"M{x - 38} 200Q{x} {176 + tilt} {x + 38} 200", f'fill="none" stroke="{rim}" stroke-width="12" stroke-linecap="round"')
        out += f'<ellipse cx="{x}" cy="250" rx="40" ry="46" fill="url(#{eye})" stroke="{rim}" stroke-width="5"/>'
        out += p(circ(x - 4, 256, 22), 'fill="#2A160C"') + p(circ(x - 12, 246, 8), 'fill="#FFFFFF"') + p(circ(x + 4, 266, 3.5), 'fill="#FFFFFF"')
    out += '<ellipse cx="128" cy="320" rx="30" ry="18" fill="#FF8A8A" fill-opacity="0.6"/><ellipse cx="384" cy="320" rx="30" ry="18" fill="#FF8A8A" fill-opacity="0.6"/>'
    out += f'<ellipse cx="256" cy="340" rx="96" ry="72" fill="url(#{muzzle})" stroke="{rim}" stroke-width="5"/>'
    mouth = "M206 352Q256 362 306 352Q300 410 256 410Q212 410 206 352z"
    m, md = clip(p(mouth))
    out += (f"<defs>{md}</defs>" + p(mouth, 'fill="#5A1A0E"') + f'<g clip-path="url(#{m})">' + p(circ(256, 422, 34), 'fill="#FF7A8A"') + "</g>"
            + p(mouth, f'fill="none" stroke="{rim}" stroke-width="5" stroke-linejoin="round"'))
    out += f'<ellipse cx="256" cy="312" rx="36" ry="24" fill="url(#{nose})"/>' + '<ellipse cx="244" cy="304" rx="12" ry="6" fill="#FFFFFF" fill-opacity="0.7"/>'
    for x in (150, 362):
        out += f'<ellipse cx="{x}" cy="462" rx="64" ry="44" fill="url(#{fur})" stroke="{rim}" stroke-width="7"/>'
        out += "".join(p(circ(x + dx, 448, 9), 'fill="#5A3418"') for dx in (-24, 0, 24))
    return out


with open(os.path.join(OUT, "char_bear.svg"), "w") as f:
    f.write(svg(bear(), "0 0 512 512", (384, 384)))
print("tile match scene svgs")
