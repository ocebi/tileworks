"""Writes the match-3 backdrops (a gem lagoon; Home sharp, Game blurred and dimmed) and the star gem mascot."""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from scenekit import cloud, smooth  # noqa: E402
from svgkit import blur, circ, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
W, H = 1080, 2340
EDGE = "#F0C27A"  # bottom edge colour, used as the camera clear colour
GEM = {
    "red": ("#FF8577", "#F2463A", "#C8281F", "#8A1712"), "blue": ("#7DBBFF", "#2F86F5", "#1C5BCB", "#103C8E"),
    "green": ("#8BEA70", "#34BE52", "#1D8E37", "#115F23"), "yellow": ("#FFEB7A", "#FFC92E", "#E59A0E", "#A66606"),
    "purple": ("#CFA4FF", "#9D5EF7", "#6E35CF", "#47208F"), "teal": ("#73E8F2", "#16AFC4", "#0D7F92", "#075665"),
}


def crystal(x, ground, w, h, colour, tilt=0):
    """A pointed crystal standing on the sand: a lit left face, a shaded right face and a bright tip."""
    light, base, dark, deep = GEM[colour]
    top = ground - h
    left = f"M{x - w / 2} {ground}L{x - w / 2} {top + h * 0.28}L{x} {top}L{x} {ground}z"
    right = f"M{x} {ground}L{x} {top}L{x + w / 2} {top + h * 0.28}L{x + w / 2} {ground}z"
    lg, lgd = vgrad([(0, light, 1), (1, base, 1)])
    rg, rgd = vgrad([(0, base, 1), (1, dark, 1)])
    return (f'<defs>{lgd}{rgd}</defs><g transform="rotate({tilt} {x} {ground})">'
            + p(left, f'fill="url(#{lg})"') + p(right, f'fill="url(#{rg})"')
            + p(f"M{x - w / 2} {top + h * 0.28}L{x} {top}L{x + w / 2} {top + h * 0.28}L{x} {top + h * 0.4}z", f'fill="{light}" fill-opacity="0.85"')
            + rrect(x - w / 2 + w * 0.1, top + h * 0.36, w * 0.12, h * 0.5, w * 0.06, 'fill="#fff" fill-opacity="0.55"')
            + p(f"M{x - w / 2} {ground}L{x - w / 2} {top + h * 0.28}L{x} {top}L{x + w / 2} {top + h * 0.28}L{x + w / 2} {ground}",
                f'fill="none" stroke="{deep}" stroke-width="{w * 0.06}" stroke-linejoin="round"')
            + "</g>")


def cluster(x, ground, s, colours):
    sh, shd = blur(8)
    out = f"<defs>{shd}</defs>" + f'<ellipse cx="{x}" cy="{ground + 4}" rx="{s * 1.2}" ry="{s * 0.22}" fill="#8A5A1E" fill-opacity="0.35" filter="url(#{sh})"/>'
    for (dx, hw, hh, tilt), colour in zip(((-0.62, 0.42, 0.9, -16), (0.6, 0.4, 0.8, 14), (0, 0.55, 1.45, 0)), colours):
        out += crystal(x + dx * s, ground, hw * s, hh * s, colour, tilt)
    return out


def palm(x, ground, s, lean):
    trunk = f"M{x - s * 0.07} {ground}Q{x + lean * 0.4} {ground - s * 0.9} {x + lean} {ground - s * 1.75}L{x + lean + s * 0.08} {ground - s * 1.72}Q{x + lean * 0.4 + s * 0.12} {ground - s * 0.9} {x + s * 0.09} {ground}z"
    tg, tgd = vgrad([(0, "#C98D4E", 1), (1, "#8A5A2C", 1)], x1=0, y1=0, x2=1, y2=0)
    lg, lgd = rgrad([(0, "#9BE86A", 1), (0.6, "#3FAE3C", 1), (1, "#22782A", 1)], cx=0.3, cy=0.3, r=0.9)
    sh, shd = blur(7)
    tx, ty = x + lean + s * 0.04, ground - s * 1.74
    out = (f"<defs>{tgd}{lgd}{shd}</defs>"
           + f'<ellipse cx="{x}" cy="{ground}" rx="{s * 0.5}" ry="{s * 0.08}" fill="#8A5A1E" fill-opacity="0.35" filter="url(#{sh})"/>'
           + p(trunk, f'fill="url(#{tg})" stroke="#5E3A18" stroke-width="{s * 0.02}"'))
    for i in range(7):
        y = ground - s * (0.2 + i * 0.22)
        t = (ground - y) / (s * 1.75)
        cx = x + lean * t * t * 0.9 + lean * t * 0.1
        out += p(f"M{cx - s * 0.08} {y}q{s * 0.08} {s * 0.05} {s * 0.16} 0", f'fill="none" stroke="#6E4520" stroke-width="{s * 0.018}" stroke-linecap="round"')
    for ang, length, droop in ((-160, 0.95, 0.35), (-125, 0.8, 0.25), (-90, 0.6, 0.1), (-55, 0.8, 0.25), (-20, 0.95, 0.35), (150, 0.7, 0.4), (30, 0.7, 0.4)):
        a = math.radians(ang)
        ex, ey = tx + math.cos(a) * s * length, ty + math.sin(a) * s * length * 0.6 + s * droop
        mx, my = tx + math.cos(a) * s * length * 0.5, ty + math.sin(a) * s * length * 0.5 - s * 0.12
        nx, ny = -(ey - ty), ex - tx
        n = math.hypot(nx, ny) or 1
        nx, ny = nx / n * s * 0.11, ny / n * s * 0.11
        leaf = f"M{tx} {ty}Q{mx + nx:.1f} {my + ny:.1f} {ex:.1f} {ey:.1f}Q{mx - nx:.1f} {my - ny:.1f} {tx} {ty}z"
        out += p(leaf, f'fill="url(#{lg})" stroke="#1E6A26" stroke-width="{s * 0.02}" stroke-linejoin="round"')
        out += p(f"M{tx} {ty}Q{mx:.1f} {my:.1f} {ex:.1f} {ey:.1f}", f'fill="none" stroke="#C8F59A" stroke-opacity="0.6" stroke-width="{s * 0.012}"')
    out += p(circ(tx - s * 0.05, ty + s * 0.06, s * 0.06), 'fill="#7A4A1E"') + p(circ(tx + s * 0.06, ty + s * 0.08, s * 0.055), 'fill="#8A5A2A"')
    return out


def starfish(x, y, r, colour, turn):
    pts = []
    for i in range(10):
        rr = r if i % 2 == 0 else r * 0.45
        a = math.radians(turn - 90 + 36 * i)
        pts.append((x + rr * math.cos(a), y + rr * math.sin(a) * 0.75))
    d = "M" + "L".join(f"{a:.1f} {b:.1f}" for a, b in pts) + "z"
    return p(d, f'fill="{colour}" stroke="#B0442A" stroke-width="3" stroke-linejoin="round"') + p(circ(x, y, r * 0.12), 'fill="#FFE0C8"')


def shell(x, y, r):
    d = f"M{x - r} {y}Q{x} {y - r * 1.6} {x + r} {y}Q{x} {y + r * 0.5} {x - r} {y}z"
    ridges = "".join(f"M{x} {y + r * 0.2}L{x + r * math.cos(math.radians(a)):.1f} {y - r * 0.9 * math.sin(math.radians(a)):.1f}" for a in (30, 60, 90, 120, 150))
    return p(d, 'fill="#FFE6EE" stroke="#D88AA0" stroke-width="3" stroke-linejoin="round"') + p(ridges, 'fill="none" stroke="#E7A3B6" stroke-width="2"')


def scene():
    sky, skyd = vgrad([(0, "#2FAFEF", 1), (0.3, "#7CD6FF", 1), (0.46, "#C8F0FF", 1), (0.505, "#FFF2D6", 1)])
    sun, sund = rgrad([(0, "#FFF8E0", 1), (0.25, "#FFF0BF", 0.85), (1, "#FFF0BF", 0)], cx=0.5, cy=0.5, r=0.5)
    fade, faded = rgrad([(0, "#fff", 1), (0.5, "#fff", 0.4), (1, "#fff", 0)], cx=0.5, cy=640 / H, r=0.55)
    sea, sead = vgrad([(0, "#8BEAF0", 1), (0.25, "#3CCFE0", 1), (1, "#1597BE", 1)])
    sand, sandd = vgrad([(0, "#FFEDC2", 1), (0.5, "#FADB9C", 1), (1, EDGE, 1)])
    wet, wetd = vgrad([(0, "#E9C48A", 1), (1, "#E9C48A", 0)])
    isle, isled = vgrad([(0, "#7CCB8C", 1), (1, "#4FA592", 1)])
    rays = ""
    for i in range(20):
        a1 = 2 * math.pi * i / 20
        a2 = a1 + math.pi / 20
        rays += f'<path d="M540 640L{540 + 2400 * math.cos(a1):.0f} {640 + 2400 * math.sin(a1):.0f}L{540 + 2400 * math.cos(a2):.0f} {640 + 2400 * math.sin(a2):.0f}z"/>'
    body = (f"<defs>{skyd}{sund}{faded}{sead}{sandd}{wetd}{isled}"
            f'<mask id="rays"><rect width="{W}" height="{H}" fill="url(#{fade})"/></mask></defs>'
            + f'<rect width="{W}" height="{H}" fill="url(#{sky})"/>'
            + f'<g mask="url(#rays)" fill="#FFFFFF" fill-opacity="0.13">{rays}</g>'
            + f'<circle cx="540" cy="640" r="430" fill="url(#{sun})"/>'
            + cloud(160, 320, 1.05) + cloud(930, 450, 1.2) + cloud(760, 180, 0.7, 0.9) + cloud(280, 930, 0.75, 0.85) + cloud(900, 1010, 0.55, 0.8))
    # The sea with two far islands, glints and soft wave lines.
    body += f'<rect y="1170" width="{W}" height="700" fill="url(#{sea})"/>'
    body += p("M-20 1182Q60 1120 170 1130Q260 1105 330 1182z", f'fill="url(#{isle})"') + p("M720 1182Q800 1100 900 1112Q1010 1095 1100 1182z", f'fill="url(#{isle})"')
    body += palm(220, 1150, 70, 14) + palm(880, 1125, 80, -16) + palm(960, 1140, 60, 12)
    rng = random.Random(5)
    glints = ""
    for _ in range(60):
        x, y = rng.uniform(0, W), rng.uniform(1200, 1640)
        w = rng.uniform(14, 46) * (0.5 + (y - 1200) / 880)
        glints += rrect(x, y, w, 4 + (y - 1200) / 160, 3, f'fill="#FFFFFF" fill-opacity="{rng.uniform(0.35, 0.8):.2f}"')
    body += glints
    for y in (1290, 1420, 1560):
        body += p(f"M-20 {y}" + "".join(f"q45 -14 90 0t90 0" for _ in range(7)), f'fill="none" stroke="#FFFFFF" stroke-opacity="0.3" stroke-width="5"')
    # The beach: wet sand at the waterline, a foam edge and dry sand to the bottom.
    shore = [(-40, 1700), (220, 1650), (520, 1690), (820, 1630), (1120, 1670)]
    edge = f"M{shore[0][0]} {shore[0][1]}" + smooth(shore)
    body += p(edge + f"L1120 {H + 20}L-40 {H + 20}z", f'fill="url(#{sand})"')
    body += p(edge + "L1120 1760L-40 1760z", f'fill="url(#{wet})" fill-opacity="0.8"')
    foam, foamd = blur(4)
    body += f"<defs>{foamd}</defs>" + p(edge, f'fill="none" stroke="#FFFFFF" stroke-width="18" stroke-linecap="round" filter="url(#{foam})"')
    body += p(edge, 'fill="none" stroke="#FFFFFF" stroke-width="7" stroke-linecap="round"')
    body += palm(70, 1980, 260, 60) + palm(1010, 1930, 240, -70)
    body += cluster(170, 2240, 110, ["purple", "teal", "blue"]) + cluster(900, 2270, 100, ["red", "green", "yellow"])
    body += cluster(560, 1780, 46, ["teal", "red", "purple"])
    body += starfish(330, 2110, 34, "#FF8A5C", 12) + starfish(760, 1880, 26, "#FF6F8A", -20)
    body += shell(640, 2160, 26) + shell(420, 1830, 18) + shell(860, 2080, 20)
    dots = ""
    for _ in range(140):
        x, y = rng.uniform(0, W), rng.uniform(1720, H)
        dots += p(circ(x, y, rng.uniform(2, 4.5)), f'fill="{rng.choice(["#E6B66A", "#FFF6DD"])}" fill-opacity="0.8"')
    body += dots
    return body


home = scene()
with open(os.path.join(OUT, "bg_match3_home.svg"), "w") as f:
    f.write(svg(home, f"0 0 {W} {H}", (W, H)))
b, bd = blur(7)
dim, dimd = vgrad([(0, "#0B1A3A", 0.25), (1, "#3A2208", 0.3)])
with open(os.path.join(OUT, "bg_match3_game.svg"), "w") as f:
    f.write(svg(f"<defs>{bd}{dimd}</defs><g filter=\"url(#{b})\">{home}</g>" + f'<rect width="{W}" height="{H}" fill="url(#{dim})"/>',
                f"0 0 {W} {H}", (W, H)))


def mascot():
    light, base, dark, deep = GEM["yellow"]
    cx, cy = 256, 282
    pts = []
    for i in range(10):
        r = 222 if i % 2 == 0 else 120
        a = math.radians(-90 + 36 * i)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    star = "M" + "L".join(f"{x:.1f} {y:.1f}" for x, y in pts) + "z"
    body_g, body_gd = vgrad([(0, light, 1), (0.5, base, 1), (1, dark, 1)])
    sh, shd = blur(9)
    soft, softd = blur(5)
    eye_g, eye_gd = rgrad([(0, "#FFFFFF", 1), (0.8, "#FFFFFF", 1), (1, "#E3E8F2", 1)], cx=0.45, cy=0.4, r=0.6)
    c, cd = clip(p(star, 'stroke-width="44" stroke-linejoin="round"'))
    out = (f"<defs>{body_gd}{shd}{softd}{eye_gd}{cd}</defs>"
           + f'<ellipse cx="{cx}" cy="488" rx="170" ry="20" fill="#000" fill-opacity="0.28" filter="url(#{sh})"/>'
           + p(star, f'transform="translate(0 18)" fill="{deep}" stroke="{deep}" stroke-width="44" stroke-linejoin="round"')
           + p(star, f'fill="url(#{body_g})" stroke="url(#{body_g})" stroke-width="44" stroke-linejoin="round"'))
    # Soft facets: a lit and a shaded half on each arm, like a cut gem.
    for i in range(0, 10, 2):
        tip, left, right = pts[i], pts[i - 1], pts[(i + 1) % 10]
        a = math.atan2(tip[1] - cy, tip[0] - cx)
        lit = -(math.cos(a) * -0.55 + math.sin(a) * -0.83)
        out += p(f"M{cx} {cy}L{left[0]:.1f} {left[1]:.1f}L{tip[0]:.1f} {tip[1]:.1f}z", f'fill="{light if lit < 0 else deep}" fill-opacity="{0.35 if lit < 0 else 0.12}"')
        out += p(f"M{cx} {cy}L{tip[0]:.1f} {tip[1]:.1f}L{right[0]:.1f} {right[1]:.1f}z", f'fill="{deep}" fill-opacity="0.1"')
        out += p(f"M{cx} {cy}L{tip[0]:.1f} {tip[1]:.1f}", f'stroke="{light}" stroke-opacity="0.7" stroke-width="5" stroke-linecap="round"')
    out += p(circ(cx, cy + 6, 120), f'fill="{base}" fill-opacity="0.55" filter="url(#{soft})"')
    out += '<ellipse cx="240" cy="128" rx="34" ry="13" transform="rotate(-62 240 128)" fill="#fff" fill-opacity="0.75"/>'
    out += p(circ(128, 214, 10), 'fill="#fff" fill-opacity="0.9"')
    # Face: brows, big eyes, rosy cheeks and an open grin.
    for x, tilt in ((206, -10), (306, 10)):
        out += p(f"M{x - 34} 214Q{x} {190 + tilt} {x + 34} 214", f'fill="none" stroke="{deep}" stroke-width="13" stroke-linecap="round"')
        out += f'<ellipse cx="{x}" cy="268" rx="40" ry="47" fill="url(#{eye_g})" stroke="{deep}" stroke-width="5"/>'
        out += p(circ(x - 4, 262, 21), 'fill="#2A0E0B"') + p(circ(x - 12, 252, 8), 'fill="#fff"') + p(circ(x + 3, 272, 3.5), 'fill="#fff"')
    out += '<ellipse cx="152" cy="324" rx="26" ry="15" fill="#FF8A6A" fill-opacity="0.7"/><ellipse cx="360" cy="324" rx="26" ry="15" fill="#FF8A6A" fill-opacity="0.7"/>'
    mouth = "M210 320Q256 326 302 320Q298 380 256 380Q214 380 210 320z"
    m, md = clip(p(mouth))
    out += (f"<defs>{md}</defs>" + p(mouth, 'fill="#5A1A0A"')
            + f'<g clip-path="url(#{m})">' + p(circ(256, 392, 34), 'fill="#FF6F7E"') + rrect(218, 314, 76, 16, 6, 'fill="#fff"') + "</g>"
            + p(mouth, f'fill="none" stroke="{deep}" stroke-width="6" stroke-linejoin="round"'))
    out += p(star, f'fill="none" stroke="{deep}" stroke-width="44" stroke-linejoin="round" stroke-opacity="0" ')
    out += p("M404 92l9 24 24 9-24 9-9 24-9-24-24-9 24-9z", 'fill="#fff"')
    return out


with open(os.path.join(OUT, "char_star.svg"), "w") as f:
    f.write(svg(mascot(), "0 0 512 512", (384, 384)))
print("scene svgs")
