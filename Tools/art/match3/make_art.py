"""Writes the match-3 board art as glossy SVGs (128 px, transparent, one centred piece per file): six cut gems, the
specials, the blockers, the checker cells, the board frame and the selection and hint overlays."""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from svgkit import blur, circ, clip, glossy, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
LIGHT = (-0.55, -0.83)

# The blast cube colours with the same colour-to-shape key as the cube glyphs, so a player reads both games alike.
GEMS = [
    (1, "red", "#FF8577", "#F2463A", "#C8281F", "#8A1712", "round"),
    (2, "blue", "#7DBBFF", "#2F86F5", "#1C5BCB", "#103C8E", "diamond"),
    (3, "green", "#8BEA70", "#34BE52", "#1D8E37", "#115F23", "triangle"),
    (4, "yellow", "#FFEB7A", "#FFC92E", "#E59A0E", "#A66606", "star"),
    (5, "purple", "#CFA4FF", "#9D5EF7", "#6E35CF", "#47208F", "square"),
    (6, "teal", "#73E8F2", "#16AFC4", "#0D7F92", "#075665", "drop"),
]


def write(name, text):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(text)


def mix(a, b, t):
    a = [int(a[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(b[i:i + 2], 16) for i in (1, 3, 5)]
    return "#" + "".join(f"{round(x + (y - x) * t):02X}" for x, y in zip(a, b))


def poly(pts):
    return "M" + "L".join(f"{x:.2f} {y:.2f}" for x, y in pts) + "z"


def scaled(pts, c, s):
    return [(c[0] + (x - c[0]) * s, c[1] + (y - c[1]) * s) for x, y in pts]


def ring(cx, cy, r, n, start=-90.0):
    return [(cx + r * math.cos(math.radians(start + 360 * i / n)), cy + r * math.sin(math.radians(start + 360 * i / n))) for i in range(n)]


def shape(kind):
    """Outer outline, centre and the scales of the inner facet rings (the last ring is the table)."""
    if kind == "round":
        return ring(32, 31, 27, 12, -75), (32, 31), (0.56,)
    if kind == "diamond":
        tips = [(32, 3.5), (60, 31), (32, 58.5), (4, 31)]
        return [q for i, t in enumerate(tips) for q in (t, ((t[0] + tips[(i + 1) % 4][0]) / 2, (t[1] + tips[(i + 1) % 4][1]) / 2))], (32, 31), (0.5,)
    if kind == "triangle":
        tips = [(32, 4.5), (60, 53), (4, 53)]
        mids = [((a[0] + b[0]) / 2, (a[1] + b[1]) / 2) for a, b in zip(tips, tips[1:] + tips[:1])]
        c = (32, 37)
        mids = [(c[0] + (x - c[0]) * 1.1, c[1] + (y - c[1]) * 1.1) for x, y in mids]
        return [q for t, m in zip(tips, mids) for q in (t, m)], c, (0.48,)
    if kind == "star":
        pts = [(32 + (29 if i % 2 == 0 else 14) * math.cos(math.radians(-90 + 36 * i)),
                33 + (29 if i % 2 == 0 else 14) * math.sin(math.radians(-90 + 36 * i))) for i in range(10)]
        return pts, (32, 33), (0.46,)
    if kind == "square":
        a, b, k = 6, 58, 9
        return [(a + k, a), (b - k, a), (b, a + k), (b, b - k), (b - k, b), (a + k, b), (a, b - k), (a, a + k)], (32, 32), (0.8, 0.58)
    # A pear: the teardrop curve x = sin t * sin(t / 2), y = cos t, tip up.
    pts = []
    for i in range(16):
        t = 2 * math.pi * i / 16
        pts.append((32 + 24 * math.sin(t) * math.sin(t / 2) ** 0.9, 33 - 27 * math.cos(t)))
    return pts, (32, 39), (0.5,)


def facet_colour(quad, centre, light, base, dark, gain):
    mx = sum(x for x, _ in quad) / 4 - centre[0]
    my = sum(y for _, y in quad) / 4 - centre[1]
    n = math.hypot(mx, my) or 1
    t = max(-1.0, min(1.0, (mx * LIGHT[0] + my * LIGHT[1]) / n * gain))
    return mix(base, light, t * 0.85) if t > 0 else mix(base, dark, -t)


def gem(kind, light, base, dark, deep):
    outer, centre, rings = shape(kind)
    levels = [outer] + [scaled(outer, centre, s) for s in rings]
    sh, shd = blur(1.5)
    soft, softd = blur(1.2)
    table_g, table_gd = rgrad([(0, mix(light, "#FFFFFF", 0.35), 1), (0.55, light, 1), (1, base, 1)], cx=0.36, cy=0.3, r=0.8)
    gloss_g, gloss_gd = vgrad([(0, "#FFFFFF", 0.75), (1, "#FFFFFF", 0)])
    c, cd = clip(p(poly(outer)))
    out = (f"<defs>{shd}{softd}{table_gd}{gloss_gd}{cd}</defs>"
           + p(poly(outer), f'transform="translate(0.5 3.6)" fill="#000" fill-opacity="0.32" filter="url(#{sh})"')
           + p(poly(outer), f'transform="translate(0 2.4)" fill="{deep}" stroke="{deep}" stroke-width="1.6" stroke-linejoin="round"'))
    n = len(outer)
    for depth, (a, b) in enumerate(zip(levels, levels[1:])):
        for i in range(n):
            j = (i + 1) % n
            quad = [a[i], a[j], b[j], b[i]]
            colour = facet_colour(quad, centre, light, base, dark, 1.0 if depth == 0 else 0.7)
            out += p(poly(quad), f'fill="{colour}" stroke="{colour}" stroke-width="0.3" stroke-linejoin="round"')
            out += p(f"M{b[i][0]:.2f} {b[i][1]:.2f}L{a[i][0]:.2f} {a[i][1]:.2f}", f'stroke="{light}" stroke-opacity="0.45" stroke-width="0.5"')
    table = levels[-1]
    tx = [x for x, _ in table]
    ty = [y for _, y in table]
    out += p(poly(table), f'fill="url(#{table_g})" stroke="#FFFFFF" stroke-opacity="0.55" stroke-width="0.7" stroke-linejoin="round"')
    gx, gy, gw, gh = min(tx), min(ty), max(tx) - min(tx), (max(ty) - min(ty)) * 0.5
    out += (f'<g clip-path="url(#{c})">'
            + f'<ellipse cx="{gx + gw * 0.42:.2f}" cy="{gy + gh * 0.55:.2f}" rx="{gw * 0.42:.2f}" ry="{gh * 0.5:.2f}" transform="rotate(-18 {gx + gw * 0.42:.2f} {gy + gh * 0.55:.2f})" fill="url(#{gloss_g})"/>'
            + p(poly(outer), f'fill="none" stroke="#FFFFFF" stroke-opacity="0.5" stroke-width="2.2" filter="url(#{soft})" transform="translate(0.6 1)"')
            + "</g>")
    out += p(poly(outer), f'fill="none" stroke="{deep}" stroke-width="1.5" stroke-linejoin="round"')
    sx, sy = gx + gw * 0.22, gy + gh * 0.45
    out += p(f"M{sx} {sy - 4.2}l1 3.2 3.2 1-3.2 1-1 3.2-1-3.2-3.2-1 3.2-1z", 'fill="#FFFFFF" fill-opacity="0.95"')
    return out


for index, name, light, base, dark, deep, kind in GEMS:
    write(f"gem_{index}_{name}", svg(gem(kind, light, base, dark, deep), "0 0 64 64", 128))


# Rockets: Blast's rocket in the lagoon colours (white body, magenta nose and fins).
def rocket(vertical):
    t = 'transform="rotate(-90 12 12)"' if vertical else ""
    flame_g, flame_gd = vgrad([(0, "#FFF3A0", 1), (0.5, "#FFC22E", 1), (1, "#FF6A1A", 1)], x1=1, y1=0, x2=0, y2=0)
    glow, glow_d = blur(0.9)
    window_g, window_gd = rgrad([(0, "#C8FBFF", 1), (0.6, "#22B8D0", 1), (1, "#0D6F82", 1)], cx=0.4, cy=0.35, r=0.7)
    flame = "M4.2 9.6C2.6 10.2 0.6 11.1 0.2 12c.4.9 2.4 1.8 4 2.4z"
    return (
        f"<defs>{flame_gd}{glow_d}{window_gd}</defs><g {t}>"
        + p(flame, f'fill="#FF8A2A" fill-opacity="0.8" filter="url(#{glow})"')
        + p(flame, f'fill="url(#{flame_g})"')
        + glossy("M5.6 8.6L3.4 5.2Q3.3 4.8 3.8 4.8H7.2L9.4 8.6zM5.6 15.4L3.4 18.8Q3.3 19.2 3.8 19.2H7.2L9.4 15.4z",
                 "#FF8FCB", "#D8247A", "#760B3C", 0.5)
        + glossy("M6.4 8.2H16.6L21.8 12L16.6 15.8H6.4A3.8 3.8 0 0 1 6.4 8.2z", "#FFFFFF", "#9AA6BE", "#4A5468", 0.55,
                 mid="#E4E9F3", gloss_box=(3, 7.4, 17, 4.6), gloss=0.9)
        + glossy("M16.6 8.2L21.8 12L16.6 15.8z", "#FF8FCB", "#D21F72", "#6E0A36", 0.5)
        + p("M9.5 8.3v7.4", 'stroke="#D21F72" stroke-width="1.1"')
        + p(circ(13, 12, 1.9), f'fill="url(#{window_g})" stroke="#4A5468" stroke-width="0.5"')
        + p(circ(12.4, 11.4, 0.55), 'fill="#fff" fill-opacity="0.95"')
        + "</g>"
    )


write("rocket_row", svg(rocket(False), "-0.4 -0.4 24.8 24.8", 128))
write("rocket_column", svg(rocket(True), "-0.4 -0.4 24.8 24.8", 128))

# Bomb: a lacquered plum sphere with a gold cap, a fuse and a glowing spark.
sphere_g, sphere_gd = rgrad([(0, "#B49BE0", 1), (0.45, "#4A3577", 1), (1, "#1A1030", 1)], cx=0.38, cy=0.32, r=0.75)
spark_glow, spark_glow_d = blur(1.1)
spec, spec_d = blur(0.35)
sh, sh_d = blur(0.9)
write("bomb", svg(
    f"<defs>{sphere_gd}{spark_glow_d}{spec_d}{sh_d}</defs>"
    + p(circ(11, 14.8, 7.6), f'fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
    + p("M14.6 7.2q1.6-3.6 4.6-3", 'fill="none" stroke="#6B4524" stroke-width="2.1" stroke-linecap="round"')
    + p("M14.6 7.2q1.6-3.6 4.6-3", 'fill="none" stroke="#C99A62" stroke-width="0.9" stroke-linecap="round"')
    + glossy("M12.2 6.4l3.4 1.9-1.3 2.3-3.4-1.9z", "#FFE08A", "#C98A1A", "#6B4510", 0.45)
    + p(circ(11, 14, 7.3), f'fill="url(#{sphere_g})" stroke="#120A22" stroke-width="0.6"')
    + p("M4.6 15.6q6.4 2.6 12.8 0", 'fill="none" stroke="#FFD23A" stroke-width="1.2" stroke-opacity="0.9"')
    + f'<ellipse cx="8.3" cy="10.8" rx="2.6" ry="1.6" transform="rotate(-35 8.3 10.8)" fill="#fff" fill-opacity="0.75" filter="url(#{spec})"/>'
    + p(circ(7.6, 10.4, 0.6), 'fill="#fff"')
    + p(circ(19.4, 4.1, 2.6), f'fill="#FFB02E" fill-opacity="0.9" filter="url(#{spark_glow})"')
    + p("M19.4 1.4l.75 1.85 1.95.3-1.45 1.3.4 1.95-1.65-1-1.65 1 .4-1.95-1.45-1.3 1.95-.3z", 'fill="#FFF6B0" stroke="#FF9A1A" stroke-width="0.4" stroke-linejoin="round"'),
    "-0.2 -0.2 24.4 24.4", 128))


# Disco: a colourless mirror ball whose facets catch every gem colour.
def rainbow_facets(cx=12, cy=12, r=9, lats=8, lons=10):
    lx, ly, lz = -0.45, 0.55, 0.7
    n = (lx * lx + ly * ly + lz * lz) ** 0.5
    lx, ly, lz = lx / n, ly / n, lz / n
    hues = [g[3] for g in GEMS]
    out = ""
    for i in range(lats):
        f1, f2 = math.pi * (-0.5 + i / lats), math.pi * (-0.5 + (i + 1) / lats)
        for j in range(lons):
            l1, l2 = math.pi * (-0.5 + j / lons), math.pi * (-0.5 + (j + 1) / lons)
            pts = [(cx + r * math.cos(f) * math.sin(l), cy - r * math.sin(f)) for f, l in ((f1, l1), (f1, l2), (f2, l2), (f2, l1))]
            fm, lm = (f1 + f2) / 2, (l1 + l2) / 2
            nx, ny, nz = math.cos(fm) * math.sin(lm), math.sin(fm), math.cos(fm) * math.cos(lm)
            lit = max(0.0, nx * lx + ny * ly + nz * lz)
            silver = mix("#3A3F5C", "#F4F7FF", min(1.0, lit * 1.25))
            tint = hues[(i * 3 + j * 2) % len(hues)]
            colour = mix(silver, tint, 0.38 if (i + j) % 2 else 0.18)
            if (i * 5 + j * 3) % 11 == 0 and lit > 0.35:
                colour = "#FFFFFF"
            out += p(poly(pts), f'fill="{colour}" stroke="#2A2E48" stroke-width="0.3" stroke-opacity="0.7" stroke-linejoin="round"')
    return out


sh, sh_d = blur(0.9)
glow, glow_d = blur(0.6)
halo, halo_d = blur(1.2)
rim_g, rim_gd = rgrad([(0, "#fff", 0.0), (0.75, "#fff", 0.0), (1, "#fff", 0.4)], cx=0.5, cy=0.5, r=0.5)
write("disco", svg(
    f"<defs>{sh_d}{glow_d}{halo_d}{rim_gd}</defs>"
    + p(circ(12, 12.9, 9), f'fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
    + p(circ(12, 12, 9), 'fill="#2A2E48"')
    + rainbow_facets()
    + p(circ(12, 12, 9), f'fill="url(#{rim_g})"')
    + p(circ(12, 12, 9), 'fill="none" stroke="#1E2238" stroke-width="0.7"')
    + f'<ellipse cx="8.4" cy="7.6" rx="3" ry="1.8" transform="rotate(-35 8.4 7.6)" fill="#fff" fill-opacity="0.8" filter="url(#{glow})"/>'
    + p(circ(17, 6.2, 1.6), f'fill="#fff" fill-opacity="0.8" filter="url(#{halo})"')
    + p("M17 3.4l.55 2.25 2.25.55-2.25.55-.55 2.25-.55-2.25-2.25-.55 2.25-.55z", 'fill="#fff"'),
    "0.6 0.6 22.8 22.8", 128))

# Flyer: a folded paper plane with a swirl behind it; it takes off and homes in on a goal.
nose, tail, fold, foot, keel = (21.6, 3.8), (2.2, 11.4), (9.8, 13.2), (13.6, 21.8), (9.4, 18.2)
up_g, up_gd = vgrad([(0, "#FFFFFF", 1), (1, "#D7F4FF", 1)])
low_g, low_gd = vgrad([(0, "#9FE6FF", 1), (1, "#3AA6E6", 1)])
sh, sh_d = blur(0.8)
outline = poly([nose, tail, fold, keel, foot])
write("flyer", svg(
    f"<defs>{up_gd}{low_gd}{sh_d}</defs>"
    + p("M1.2 18.2q3-1.2 5.6.6", 'fill="none" stroke="#22B8D0" stroke-width="1.3" stroke-linecap="round" stroke-opacity="0.85"')
    + p("M2.6 21.6q2.4-.8 4.4.6", 'fill="none" stroke="#22B8D0" stroke-width="1" stroke-linecap="round" stroke-opacity="0.6"')
    + p(outline, f'transform="translate(0.4 1.4)" fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
    + p(poly([fold, keel, foot]), 'fill="#2C7FBF" stroke="#164A7A" stroke-width="0.6" stroke-linejoin="round"')
    + p(poly([nose, fold, foot]), f'fill="url(#{low_g})" stroke="#164A7A" stroke-width="0.6" stroke-linejoin="round"')
    + p(poly([nose, tail, fold]), f'fill="url(#{up_g})" stroke="#164A7A" stroke-width="0.6" stroke-linejoin="round"')
    + p(f"M{nose[0] - 1.6} {nose[1] + 1.4}L{tail[0] + 3.4} {tail[1] + 0.2}", 'stroke="#FFFFFF" stroke-width="0.9" stroke-linecap="round"')
    + p(outline, 'fill="none" stroke="#164A7A" stroke-width="0.9" stroke-linejoin="round"')
    + p("M18.6 4.2l.5 1.5 1.5.5-1.5.5-.5 1.5-.5-1.5-1.5-.5 1.5-.5z", 'fill="#FFF6B0"'),
    "-0.4 1.2 23.2 23.2", 128))


# Boxes: carved stone blocks bolted to the board; the two-layer block is bound with iron straps.
def stone(layers):
    face_g, face_gd = vgrad([(0, "#E6EBF2", 1), (1, "#AEB8C8", 1)])
    sh, shd = blur(1.4)
    out = (f"<defs>{face_gd}{shd}</defs>"
           + rrect(4, 7, 56, 55, 9, f'fill="#000" fill-opacity="0.35" filter="url(#{sh})"')
           + rrect(3, 5, 58, 57, 10, 'fill="#4E5A70"')
           + rrect(3, 3, 58, 56, 10, 'fill="#7C889C"')
           + p("M3 13Q3 3 13 3H51Q61 3 61 13L51 15H13z", 'fill="#F4F7FB"')
           + p("M3 13L13 15V49L3 51z", 'fill="#D4DBE6"')
           + p("M61 13L51 15V49L61 51z", 'fill="#8E9AAE"')
           + p("M3 51L13 49H51L61 51Q61 59 51 59H13Q3 59 3 51z", 'fill="#68748A"')
           + rrect(13, 15, 38, 34, 4, f'fill="url(#{face_g})"')
           + rrect(15, 17, 20, 4, 2, 'fill="#fff" fill-opacity="0.55"')
           + rrect(3, 3, 58, 56, 10, 'fill="none" stroke="#3A4558" stroke-width="1.6"'))
    if layers == 2:
        strap_g, strap_gd = vgrad([(0, "#8E7A6A", 1), (1, "#4A3A30", 1)])
        out += f"<defs>{strap_gd}</defs>"
        for y in (18, 40):
            out += (rrect(1.5, y, 61, 7, 2, f'fill="url(#{strap_g})" stroke="#2A1E18" stroke-width="1"')
                    + rrect(3, y + 1, 58, 1.6, 0.8, 'fill="#fff" fill-opacity="0.3"'))
            for x in (7.5, 56.5):
                out += p(circ(x, y + 3.5, 2.2), 'fill="#FFD23A" stroke="#7A4E0A" stroke-width="0.8"') + p(circ(x - 0.6, y + 2.9, 0.7), 'fill="#fff"')
    else:
        out += p("M24 15l4 9-5 6 6 9-3 10", 'fill="none" stroke="#5E6A80" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"')
        out += p("M41 49l-3-8 5-5", 'fill="none" stroke="#5E6A80" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"')
    for x, y in ((8, 8), (56, 8), (8, 54), (56, 54)):
        if layers == 1 or y in (8, 54):
            out += p(circ(x, y, 2.4), 'fill="#C9D2DE" stroke="#3A4558" stroke-width="0.8"') + p(f"M{x - 1.4} {y}h2.8", 'stroke="#3A4558" stroke-width="0.8"')
    return out


write("box_1", svg(stone(1), "0 0 64 64", 128))
write("box_2", svg(stone(2), "0 0 64 64", 128))


# Floor: sand that tiles seamlessly (flat colour, grains wrapped across the edges). Two layers are deeper, wetter sand.
def sand(layers):
    base, dots = ("#F7DFA6", [("#E8C27A", 22), ("#FFF2D0", 16)]) if layers == 1 else ("#E5AE58", [("#C78A36", 30), ("#F7CF85", 20), ("#FFFFFF", 6)])
    rng = random.Random(11 + layers)
    out = f'<rect width="64" height="64" fill="{base}"/>'
    for colour, count in dots:
        for _ in range(count):
            x, y, r = rng.uniform(0, 64), rng.uniform(0, 64), rng.uniform(0.6, 1.4)
            for dx in (-64, 0, 64):
                for dy in (-64, 0, 64):
                    if -2 < x + dx < 66 and -2 < y + dy < 66:
                        out += p(circ(x + dx, y + dy, r), f'fill="{colour}"')
    if layers == 2:
        out += p("M10 50q2-4 4 0q2-4 4 0", 'fill="none" stroke="#FFFFFF" stroke-opacity="0.45" stroke-width="1.2" stroke-linecap="round"')
        out += p("M44 12q2-4 4 0q2-4 4 0", 'fill="none" stroke="#FFFFFF" stroke-opacity="0.45" stroke-width="1.2" stroke-linecap="round"')
    return out


write("floor_1", svg(sand(1), "0 0 64 64", 128))
write("floor_2", svg(sand(2), "0 0 64 64", 128))


# Chain: two steel chains crossed over the piece, with a ring where they meet.
def chain_links(a, b, count):
    out = ""
    ang = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    for i in range(count):
        t = (i + 0.5) / count
        x, y = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        if i % 2 == 0:
            out += (f'<ellipse cx="{x:.2f}" cy="{y:.2f}" rx="5.4" ry="3.4" transform="rotate({ang:.1f} {x:.2f} {y:.2f})" fill="none" stroke="#2E3546" stroke-width="3.6"/>'
                    + f'<ellipse cx="{x:.2f}" cy="{y:.2f}" rx="5.4" ry="3.4" transform="rotate({ang:.1f} {x:.2f} {y:.2f})" fill="none" stroke="#D9E1EC" stroke-width="1.9"/>'
                    + f'<ellipse cx="{x - 1:.2f}" cy="{y - 1.2:.2f}" rx="2.4" ry="0.8" transform="rotate({ang:.1f} {x:.2f} {y:.2f})" fill="#fff"/>')
        else:
            out += (rrect(x - 5.6, y - 1.9, 11.2, 3.8, 1.9, f'transform="rotate({ang:.1f} {x:.2f} {y:.2f})" fill="#8C97AA" stroke="#2E3546" stroke-width="1.2"')
                    + rrect(x - 4.4, y - 1.2, 8.8, 1, 0.5, f'transform="rotate({ang:.1f} {x:.2f} {y:.2f})" fill="#fff" fill-opacity="0.7"'))
    return out


sh, sh_d = blur(1)
write("chain", svg(
    f"<defs>{sh_d}</defs>"
    + f'<g transform="translate(0.6 1.6)" opacity="0.35" filter="url(#{sh})">' + p("M5 5L59 59M59 5L5 59", 'stroke="#000" stroke-width="6"') + "</g>"
    + chain_links((3, 3), (61, 61), 7) + chain_links((61, 3), (3, 61), 7)
    + p(circ(32, 32, 6), 'fill="none" stroke="#2E3546" stroke-width="4.4"') + p(circ(32, 32, 6), 'fill="none" stroke="#FFD23A" stroke-width="2.4"')
    + p("M28 28.6a5 5 0 0 1 4-2.4", 'fill="none" stroke="#FFF6C8" stroke-width="1.2" stroke-linecap="round"'),
    "0 0 64 64", 128))

# Board: flat checker cells (they tile edge to edge) and a gold-rimmed frame for 9-slicing (32 px borders at 128 px).
write("cell_light", svg('<rect width="64" height="64" fill="#3B4A86"/>', "0 0 64 64", 128))
write("cell_dark", svg('<rect width="64" height="64" fill="#303D74"/>', "0 0 64 64", 128))

# The frame is drawn in two passes, every band's rim and then every band's fill inset 9 px, so bands merge into one
# shape. Both are shaded in rings, not top to bottom, so neighbouring bands meet without a step in colour.
write("board_rim", svg(
    rrect(1, 1, 126, 126, 27, 'fill="#6E3E0C"')
    + rrect(2.2, 2.2, 123.6, 123.6, 26, 'fill="#D8992F"')
    + rrect(4.6, 4.6, 118.8, 118.8, 24, 'fill="none" stroke="#FFE7A0" stroke-width="1.6"')
    + rrect(6.6, 6.6, 114.8, 114.8, 22, 'fill="none" stroke="#B9761E" stroke-width="1.4"')
    + rrect(8, 8, 112, 112, 21, 'fill="#141A3C"'),
    "0 0 128 128", 128))
write("board_fill", svg(rrect(0, 0, 128, 128, 20, 'fill="#252F62"'), "0 0 128 128", 128))

# Selection ring and hint glow stay white so the code can tint them.
glow, glow_d = blur(3)
write("select_ring", svg(
    f"<defs>{glow_d}</defs>"
    + rrect(8, 8, 112, 112, 26, f'fill="none" stroke="#FFE27A" stroke-width="12" stroke-opacity="0.9" filter="url(#{glow})"')
    + rrect(8, 8, 112, 112, 26, 'fill="none" stroke="#FFFFFF" stroke-width="6"'),
    "0 0 128 128", 128))
glow, glow_d = blur(9)
write("hint_glow", svg(
    f"<defs>{glow_d}</defs>" + rrect(18, 18, 92, 92, 26, f'fill="#FFFFFF" fill-opacity="0.85" filter="url(#{glow})"'),
    "0 0 128 128", 128))
print(len(os.listdir(OUT)), "svgs")
