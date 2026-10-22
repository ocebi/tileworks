"""Writes the blast board pieces as glossy, soft-shaded SVGs (128 px, transparent, one piece per file)."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from svgkit import blur, circ, clip, glossy, p, rgrad, rrect, svg, uid, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
GLYPH_MODE = os.environ.get("GLYPH", "deboss")

# index, name, light, base, dark, deep, glyph (24-unit box)
COLOURS = [
    (1, "red", "#FF8577", "#F2463A", "#C8281F", "#8A1712", circ(12, 12, 6.2)),
    (2, "blue", "#7DBBFF", "#2F86F5", "#1C5BCB", "#103C8E", "M12 4.6l7.4 7.4-7.4 7.4L4.6 12z"),
    (3, "green", "#8BEA70", "#34BE52", "#1D8E37", "#115F23", "M12 5.2l7.4 13.2H4.6z"),
    (4, "yellow", "#FFEB7A", "#FFC92E", "#E59A0E", "#A66606", "M12 4.2l2.3 4.7 5.2.8-3.8 3.6.9 5.2L12 16l-4.6 2.5.9-5.2-3.8-3.6 5.2-.8z"),
    (5, "purple", "#CFA4FF", "#9D5EF7", "#6E35CF", "#47208F", "M7.2 7.2h9.6v9.6H7.2z"),
    (6, "teal", "#73E8F2", "#16AFC4", "#0D7F92", "#075665", "M12 4.6s5.4 5.8 5.4 9.4a5.4 5.4 0 0 1-10.8 0C6.6 10.4 12 4.6 12 4.6z"),
]


def write(name, text):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(text)


def glyph_layer(glyph, light, dark, deep):
    t = 'transform="translate(15.2 14.4) scale(1.4)"'
    if GLYPH_MODE == "raised":
        b, bd = blur(0.5)
        g, gd = vgrad([(0, "#fff", 1), (1, light, 1)])
        return (f'<defs>{bd}{gd}</defs><g {t}>'
                + p(glyph, f'fill="{deep}" fill-opacity="0.5" transform="translate(0 0.9)" filter="url(#{b})"')
                + p(glyph, f'fill="url(#{g})" stroke="{dark}" stroke-opacity="0.35" stroke-width="0.5" stroke-linejoin="round"') + "</g>")
    # Debossed: a light edge below, the glyph pressed in a darker tone of the cube.
    return (f'<g {t}>'
            + p(glyph, f'fill="{light}" fill-opacity="0.75" transform="translate(0 0.75)" stroke-linejoin="round"')
            + p(glyph, f'fill="{deep}" fill-opacity="0.42" stroke-linejoin="round"') + "</g>")


# Cubes: a soft jelly block with a visible lower lip, a lit top face and a gloss highlight.
for index, name, light, base, dark, deep, glyph in COLOURS:
    body_g, body_gd = vgrad([(0, light, 1), (0.45, base, 1), (1, dark, 1)])
    face_g, face_gd = rgrad([(0, light, 1), (0.6, base, 1), (1, base, 0)], cx=0.5, cy=0.38, r=0.62)
    gloss_g, gloss_gd = vgrad([(0, "#fff", 0.85), (1, "#fff", 0.0)])
    soft, soft_d = blur(1.6)
    edge, edge_d = blur(0.6)
    sh, sh_d = blur(1.2)
    c, cd = clip(rrect(3, 2.5, 58, 55, 14))
    body = (
        f"<defs>{body_gd}{face_gd}{gloss_gd}{soft_d}{edge_d}{sh_d}{cd}</defs>"
        + rrect(4, 7, 56, 55, 14, f'fill="#000" fill-opacity="0.28" filter="url(#{sh})"')
        + rrect(3, 5.5, 58, 56, 14, f'fill="{deep}"')
        + rrect(3, 2.5, 58, 55, 14, f'fill="url(#{body_g})"')
        + f'<g clip-path="url(#{c})">'
        + rrect(9, 8, 46, 41, 11, f'fill="url(#{face_g})" filter="url(#{soft})"')
        + rrect(3, 2.5, 58, 55, 14, f'fill="none" stroke="#fff" stroke-opacity="0.55" stroke-width="2.4" filter="url(#{edge})" transform="translate(0 1.2)"')
        + "</g>"
        + glyph_layer(glyph, light, dark, deep)
        + f'<ellipse cx="25" cy="12.5" rx="15" ry="5.2" transform="rotate(-6 25 12.5)" fill="url(#{gloss_g})"/>'
        + '<circle cx="12.5" cy="11.5" r="2.3" fill="#fff" fill-opacity="0.9"/>'
        + rrect(3, 2.5, 58, 55, 14, f'fill="none" stroke="{deep}" stroke-opacity="0.55" stroke-width="1.2"')
    )
    write(f"cube_{index}_{name}", svg(body, "0 0 64 64", 128))


def rocket(vertical):
    t = 'transform="rotate(-90 12 12)"' if vertical else ""
    flame_g, flame_gd = vgrad([(0, "#FFF3A0", 1), (0.5, "#FFC22E", 1), (1, "#FF6A1A", 1)], x1=1, y1=0, x2=0, y2=0)
    glow, glow_d = blur(0.9)
    window_g, window_gd = rgrad([(0, "#BFE3FF", 1), (0.6, "#3C8EF0", 1), (1, "#1A4FB0", 1)], cx=0.4, cy=0.35, r=0.7)
    flame = "M4.2 9.6C2.6 10.2 0.6 11.1 0.2 12c.4.9 2.4 1.8 4 2.4z"
    return (
        f"<defs>{flame_gd}{glow_d}{window_gd}</defs><g {t}>"
        + p(flame, f'fill="#FF8A2A" fill-opacity="0.8" filter="url(#{glow})"')
        + p(flame, f'fill="url(#{flame_g})"')
        + glossy("M5.6 8.6L3.4 5.2Q3.3 4.8 3.8 4.8H7.2L9.4 8.6zM5.6 15.4L3.4 18.8Q3.3 19.2 3.8 19.2H7.2L9.4 15.4z",
                 "#FF7A6B", "#C9271F", "#7A1410", 0.5)
        + glossy("M6.4 8.2H16.6L21.8 12L16.6 15.8H6.4A3.8 3.8 0 0 1 6.4 8.2z", "#FFFFFF", "#9AA6BE", "#4A5468", 0.55,
                 mid="#E4E9F3", gloss_box=(3, 7.4, 17, 4.6), gloss=0.9)
        + glossy("M16.6 8.2L21.8 12L16.6 15.8z", "#FF7A6B", "#C21F18", "#6E120E", 0.5)
        + p("M9.5 8.3v7.4", 'stroke="#C21F18" stroke-width="1.1"')
        + p(circ(13, 12, 1.9), f'fill="url(#{window_g})" stroke="#4A5468" stroke-width="0.5"')
        + p(circ(12.4, 11.4, 0.55), 'fill="#fff" fill-opacity="0.95"')
        + "</g>"
    )


write("rocket_row", svg(rocket(False), "-0.4 -0.4 24.8 24.8", 128))
write("rocket_column", svg(rocket(True), "-0.4 -0.4 24.8 24.8", 128))

# Bomb: a dark lacquered sphere with a brass cap, a fuse and a glowing spark.
sphere_g, sphere_gd = rgrad([(0, "#7A80A6", 1), (0.45, "#33364E", 1), (1, "#121320", 1)], cx=0.38, cy=0.32, r=0.75)
spark_glow, spark_glow_d = blur(1.1)
spec, spec_d = blur(0.35)
sh, sh_d = blur(0.9)
write("bomb", svg(
    f"<defs>{sphere_gd}{spark_glow_d}{spec_d}{sh_d}</defs>"
    + p(circ(11, 14.8, 7.6), f'fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
    + p("M14.6 7.2q1.6-3.6 4.6-3", 'fill="none" stroke="#6B4524" stroke-width="2.1" stroke-linecap="round"')
    + p("M14.6 7.2q1.6-3.6 4.6-3", 'fill="none" stroke="#C99A62" stroke-width="0.9" stroke-linecap="round"')
    + glossy("M12.2 6.4l3.4 1.9-1.3 2.3-3.4-1.9z", "#FFE08A", "#C98A1A", "#6B4510", 0.45)
    + p(circ(11, 14, 7.3), f'fill="url(#{sphere_g})" stroke="#0B0C14" stroke-width="0.6"')
    + f'<ellipse cx="8.3" cy="10.8" rx="2.6" ry="1.6" transform="rotate(-35 8.3 10.8)" fill="#fff" fill-opacity="0.75" filter="url(#{spec})"/>'
    + p(circ(7.6, 10.4, 0.6), 'fill="#fff"')
    + p(circ(19.4, 4.1, 2.6), f'fill="#FFB02E" fill-opacity="0.9" filter="url(#{spark_glow})"')
    + p("M19.4 1.4l.75 1.85 1.95.3-1.45 1.3.4 1.95-1.65-1-1.65 1 .4-1.95-1.45-1.3 1.95-.3z", 'fill="#FFF6B0" stroke="#FF9A1A" stroke-width="0.4" stroke-linejoin="round"'),
    "-0.2 -0.2 24.4 24.4", 128))

# Disco ball: a mirror ball of curved facets in the target colour, lit from the upper left, with a sparkle.
def mix(a, b, t):
    a = [int(a[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(b[i:i + 2], 16) for i in (1, 3, 5)]
    return "#" + "".join(f"{round(x + (y - x) * t):02X}" for x, y in zip(a, b))


def facets(light, base, deep, cx=12, cy=12, r=9, lats=8, lons=10):
    lx, ly, lz = -0.45, 0.55, 0.7
    n = (lx * lx + ly * ly + lz * lz) ** 0.5
    lx, ly, lz = lx / n, ly / n, lz / n
    out = ""
    for i in range(lats):
        f1, f2 = math.pi * (-0.5 + i / lats), math.pi * (-0.5 + (i + 1) / lats)
        for j in range(lons):
            l1, l2 = math.pi * (-0.5 + j / lons), math.pi * (-0.5 + (j + 1) / lons)
            pts = [(cx + r * math.cos(f) * math.sin(l), cy - r * math.sin(f)) for f, l in ((f1, l1), (f1, l2), (f2, l2), (f2, l1))]
            fm, lm = (f1 + f2) / 2, (l1 + l2) / 2
            nx, ny, nz = math.cos(fm) * math.sin(lm), math.sin(fm), math.cos(fm) * math.cos(lm)
            lit = max(0.0, nx * lx + ny * ly + nz * lz)
            jitter = ((i * 7 + j * 13) % 5) / 10 - 0.2
            t = min(1.0, max(0.0, lit + jitter))
            colour = mix(deep, base, min(1, t * 1.6)) if t < 0.62 else mix(base, light, (t - 0.62) / 0.38)
            if (i * 5 + j * 3) % 11 == 0 and lit > 0.35:
                colour = "#FFFFFF"
            d = "M" + "L".join(f"{x:.2f} {y:.2f}" for x, y in pts) + "z"
            out += p(d, f'fill="{colour}" stroke="{deep}" stroke-width="0.32" stroke-opacity="0.7" stroke-linejoin="round"')
    return out


for index, name, light, base, dark, deep, _ in COLOURS:
    sh, sh_d = blur(0.9)
    glow, glow_d = blur(0.6)
    halo, halo_d = blur(1.2)
    gloss_g, gloss_gd = rgrad([(0, "#fff", 0.0), (0.75, "#fff", 0.0), (1, "#fff", 0.35)], cx=0.5, cy=0.5, r=0.5)
    write(f"disco_{index}_{name}", svg(
        f"<defs>{sh_d}{glow_d}{halo_d}{gloss_gd}</defs>"
        + p(circ(12, 12.9, 9), f'fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
        + p(circ(12, 12, 9), f'fill="{deep}"')
        + facets(light, base, deep)
        + p(circ(12, 12, 9), f'fill="url(#{gloss_g})"')
        + p(circ(12, 12, 9), f'fill="none" stroke="{deep}" stroke-width="0.7"')
        + f'<ellipse cx="8.4" cy="7.6" rx="3" ry="1.8" transform="rotate(-35 8.4 7.6)" fill="#fff" fill-opacity="0.75" filter="url(#{glow})"/>'
        + p(circ(17, 6.2, 1.6), f'fill="#fff" fill-opacity="0.8" filter="url(#{halo})"')
        + p("M17 3.4l.55 2.25 2.25.55-2.25.55-.55 2.25-.55-2.25-2.25-.55 2.25-.55z", 'fill="#fff"'),
        "0.6 0.6 22.8 22.8", 128))

# Boxes are fixed in place: a block set into its cell (no drop shadow, a dark lip along the bottom), bolted at the corners.
# Two layers: iron straps that run edge to edge into the frame; the last layer is the bare oak block, cracked.
for layers in (1, 2):
    top, bottom, deep = ("#E2A564", "#B06C32", "#5E3214") if layers == 1 else ("#C4854A", "#8C5226", "#4A2510")
    wood_g, wood_gd = vgrad([(0, top, 1), (1, bottom, 1)])
    lip_g, lip_gd = vgrad([(0, "#000", 0), (1, "#000", 0.55)])
    sheen_g, sheen_gd = vgrad([(0, "#fff", 0.3), (1, "#fff", 0)])
    iron_g, iron_gd = vgrad([(0, "#9AA3B4", 1), (0.45, "#5E6676", 1), (1, "#353B48", 1)])
    bolt_g, bolt_gd = rgrad([(0, "#F2F4F8", 1), (0.55, "#9CA5B6", 1), (1, "#424958", 1)], cx=0.38, cy=0.32, r=0.7)
    body = f"<defs>{wood_gd}{lip_gd}{sheen_gd}{iron_gd}{bolt_gd}</defs>"
    body += rrect(1.2, 1.2, 21.6, 21.6, 2.4, f'fill="url(#{wood_g})" stroke="{deep}" stroke-width="0.9"')
    body += "".join(p(f"M2.4 {y}h19.2", f'stroke="{deep}" stroke-opacity="0.45" stroke-width="0.5"') for y in (6.6, 12, 17.4))
    body += "".join(p(f"M{x} 3q.9 3 0 6M{x + 6} 13q-.8 2.6 0 5.2", f'fill="none" stroke="{deep}" stroke-opacity="0.25" stroke-width="0.45"')
                    for x in (6, 11))
    body += rrect(1.2, 1.2, 21.6, 11, 2.4, f'fill="url(#{sheen_g})"')
    body += rrect(1.7, 2.4, 1.1, 18.4, 0.55, 'fill="#fff" fill-opacity="0.22"')
    body += rrect(21.2, 2.4, 1.1, 18.4, 0.55, 'fill="#000" fill-opacity="0.18"')
    body += rrect(1.6, 1.6, 20.8, 1.3, 0.65, 'fill="#fff" fill-opacity="0.5"')
    body += rrect(1.2, 17.8, 21.6, 5, 2.4, f'fill="url(#{lip_g})"')
    if layers == 2:
        for y in (4.6, 14.8):
            body += (rrect(0.9, y, 22.2, 4.4, 0.8, f'fill="url(#{iron_g})" stroke="#262A33" stroke-width="0.55"')
                     + rrect(1.3, y + 0.35, 21.4, 0.9, 0.45, 'fill="#fff" fill-opacity="0.45"'))
            body += "".join(p(circ(x, y + 2.2, 1.05), f'fill="url(#{bolt_g})" stroke="#262A33" stroke-width="0.35"') for x in (3.3, 20.7))
    else:
        body += p("M14.2 1.6l-1.6 4.4 2 2.2-1.4 4.6 1.8 2.8", f'fill="none" stroke="{deep}" stroke-width="0.85" stroke-linecap="round" stroke-linejoin="round"')
        body += p("M14.9 1.8l-1.5 4.2 2 2.2-1.4 4.5 1.7 2.7", 'fill="none" stroke="#fff" stroke-opacity="0.25" stroke-width="0.4" stroke-linecap="round"')
        body += "".join(p(circ(x, y, 1.05), f'fill="url(#{bolt_g})" stroke="#262A33" stroke-width="0.35"')
                        for x, y in ((3.4, 3.4), (20.6, 3.4), (3.4, 20.6), (20.6, 20.6)))
    write(f"box_{layers}", svg(body, "0.6 0.6 22.8 22.8", 128))

balloon = "M12 2.4c4.3 0 7 3.3 7 7.2 0 4.5-3.7 8.4-7 8.4s-7-3.9-7-8.4c0-3.9 2.7-7.2 7-7.2z"
balloon_g, balloon_gd = rgrad([(0, "#FFC2DC", 1), (0.45, "#FF5A9E", 1), (1, "#B3195C", 1)], cx=0.38, cy=0.3, r=0.75)
spec, spec_d = blur(0.4)
write("balloon", svg(
    f"<defs>{balloon_gd}{spec_d}</defs>"
    + p("M12 18.4q-1.2 2 .9 4.4", 'fill="none" stroke="#8A6A72" stroke-width="0.8" stroke-linecap="round"')
    + p("M10.9 17.5h2.2l.6 1.4h-3.4z", 'fill="#D02A72" stroke="#7A0E3C" stroke-width="0.4" stroke-linejoin="round"')
    + p(balloon, f'fill="url(#{balloon_g})" stroke="#7A0E3C" stroke-width="0.6"')
    + f'<ellipse cx="9.2" cy="6.8" rx="2.2" ry="1.4" transform="rotate(-40 9.2 6.8)" fill="#fff" fill-opacity="0.8" filter="url(#{spec})"/>'
    + p(circ(15.6, 13.6, 0.5), 'fill="#fff" fill-opacity="0.5"'),
    "-1 -1 26 26", 128))

duck = ("M3.5 14.5c0-2.6 2.4-3.6 4.6-3.4.2-3.4 2.4-5.6 5.4-5.6 3.2 0 5 2.4 5 5 0 1.4-.5 2.4-1.2 3.2 1.8.3 3.2 1.6 3.2 3.4"
        " 0 2.6-3.2 3.9-8.3 3.9-5.3 0-8.7-2-8.7-6.5z")
duck_g, duck_gd = rgrad([(0, "#FFF6B8", 1), (0.5, "#FFD43B", 1), (1, "#E39A00", 1)], cx=0.45, cy=0.3, r=0.75)
sh, sh_d = blur(0.7)
write("duck", svg(
    f"<defs>{duck_gd}{sh_d}</defs>"
    + p(duck, f'transform="translate(0 0.9)" fill="#000" fill-opacity="0.28" filter="url(#{sh})"')
    + p(duck, f'fill="url(#{duck_g})" stroke="#9A6400" stroke-width="0.6" stroke-linejoin="round"')
    + glossy("M18.1 9.1l3.9 1-3.6 1.8z", "#FFB066", "#F06A10", "#8A3A06", 0.45)
    + p("M8.2 15.6q3 2.1 6.6-.3", 'fill="none" stroke="#D99A00" stroke-width="1" stroke-linecap="round"')
    + p(circ(15, 8.8, 1.05), 'fill="#2A1A10"') + p(circ(15.35, 8.45, 0.35), 'fill="#fff"')
    + '<ellipse cx="11.6" cy="7.6" rx="1.8" ry="1" transform="rotate(-30 11.6 7.6)" fill="#fff" fill-opacity="0.7"/>',
    "-1 -1 26 26", 128))

# Board: a recessed cell and a bevelled frame for 9-slicing (32 px borders at 128 px).
cell_g, cell_gd = vgrad([(0, "#3E1B2E", 1), (1, "#55283F", 1)])
inner, inner_d = blur(1.4)
c, cd = clip(rrect(2, 2, 60, 60, 11))
write("cell", svg(
    f"<defs>{cell_gd}{inner_d}{cd}</defs>"
    + rrect(2, 2, 60, 60, 11, f'fill="url(#{cell_g})"')
    + f'<g clip-path="url(#{c})">' + rrect(-2, -4, 68, 12, 6, f'fill="#000" fill-opacity="0.35" filter="url(#{inner})"') + "</g>"
    + rrect(2.5, 2.5, 59, 59, 10.5, 'fill="none" stroke="#7E4664" stroke-opacity="0.45" stroke-width="1"'),
    "0 0 64 64", 128))

rim_g, rim_gd = vgrad([(0, "#F7A6A0", 1), (0.12, "#B8506A", 1), (1, "#6E2440", 1)])
fill_g, fill_gd = vgrad([(0, "#2A1020", 1), (1, "#371629", 1)])
inner, inner_d = blur(3)
c, cd = clip(rrect(9, 9, 110, 110, 20))
write("board_frame", svg(
    f"<defs>{rim_gd}{fill_gd}{inner_d}{cd}</defs>"
    + rrect(2, 2, 124, 124, 27, 'fill="#4A1426"')
    + rrect(3, 3, 122, 121, 26, f'fill="url(#{rim_g})"')
    + rrect(9, 9, 110, 110, 20, f'fill="url(#{fill_g})"')
    + f'<g clip-path="url(#{c})">' + rrect(0, -6, 128, 20, 8, f'fill="#000" fill-opacity="0.55" filter="url(#{inner})"') + "</g>"
    + rrect(9, 9, 110, 110, 20, 'fill="none" stroke="#1A0812" stroke-width="2"'),
    "0 0 128 128", 128))

# Effects stay white so they can be tinted per colour.
write("fx_flash", svg(
    '<defs><radialGradient id="g"><stop offset="0" stop-color="#fff" stop-opacity="1"/>'
    '<stop offset="0.3" stop-color="#fff" stop-opacity="0.8"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient></defs>'
    '<circle cx="64" cy="64" r="62" fill="url(#g)"/>', "0 0 128 128", 128))
glow, glow_d = blur(5)
write("fx_ring", svg(
    f"<defs>{glow_d}</defs>"
    f'<circle cx="64" cy="64" r="50" fill="none" stroke="#fff" stroke-opacity="0.7" stroke-width="14" filter="url(#{glow})"/>'
    '<circle cx="64" cy="64" r="50" fill="none" stroke="#fff" stroke-width="6"/>', "0 0 128 128", 128))
glow, glow_d = blur(3)
star = "M32 3C33.6 22 42 30.4 61 32 42 33.6 33.6 42 32 61 30.4 42 22 33.6 3 32 22 30.4 30.4 22 32 3z"
write("fx_spark", svg(
    f"<defs>{glow_d}</defs>" + p(star, f'fill="#fff" fill-opacity="0.8" filter="url(#{glow})"') + p(star, 'fill="#fff"'),
    "0 0 64 64", 128))
bit_g, bit_gd = vgrad([(0, "#FFFFFF", 1), (1, "#BDBDBD", 1)])
write("fx_bit", svg(
    f"<defs>{bit_gd}</defs>"
    + rrect(8, 11, 48, 47, 11, 'fill="#8E8E8E"')
    + rrect(8, 7, 48, 47, 11, f'fill="url(#{bit_g})"')
    + '<ellipse cx="26" cy="16" rx="12" ry="4" fill="#fff"/>'
    + rrect(8, 7, 48, 47, 11, 'fill="none" stroke="#7A7A7A" stroke-width="2"'),
    "0 0 64 64", 128))
print(len(os.listdir(OUT)), "svgs")
