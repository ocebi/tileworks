"""Writes the Tileworlds app icon (1024 px, square, full-bleed, no text):

    python3 -I make_icon.py <out>/svg && python3 -I ../render.py <out>/svg <out>/png <out>/sheet.png '#333' 4 256

iOS takes the square 1024 PNG and masks the corners itself. Android's adaptive icon uses the same art cropped to the
centre 66% circle, so everything that matters stays inside the middle 680 px."""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from scenekit import CUBES  # noqa: E402
from svgkit import blur, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
S = 1024

# Blast's cube glyphs (24-unit box), so the icon carries the same colour-to-shape key as the games.
GLYPHS = {
    "red": "M12 5.8a6.2 6.2 0 1 0 0 12.4a6.2 6.2 0 1 0 0-12.4z",
    "blue": "M12 4.6l7.4 7.4-7.4 7.4L4.6 12z",
    "green": "M12 5.2l7.4 13.2H4.6z",
    "yellow": "M12 4.2l2.3 4.7 5.2.8-3.8 3.6.9 5.2L12 16l-4.6 2.5.9-5.2-3.8-3.6 5.2-.8z",
    "purple": "M7.2 7.2h9.6v9.6H7.2z",
    "teal": "M12 4.6s5.4 5.8 5.4 9.4a5.4 5.4 0 0 1-10.8 0C6.6 10.4 12 4.6 12 4.6z",
}


def write(name, body):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, f"0 0 {S} {S}", S))


def burst(inner, outer, rays=24, ray_alpha=0.12, cy=0.5):
    """A radial sky with soft rays from the centre."""
    g, gd = rgrad([(0, inner, 1), (1, outer, 1)], cx=0.5, cy=cy, r=0.75)
    fade, faded = rgrad([(0, "#fff", 1), (0.6, "#fff", 0.35), (1, "#fff", 0)], cx=0.5, cy=cy, r=0.7)
    c = (S / 2, S * cy)
    wedges = ""
    for i in range(rays):
        a1 = 2 * math.pi * i / rays
        a2 = a1 + math.pi / rays
        wedges += p(f"M{c[0]} {c[1]}L{c[0] + 1500 * math.cos(a1):.0f} {c[1] + 1500 * math.sin(a1):.0f}"
                    f"L{c[0] + 1500 * math.cos(a2):.0f} {c[1] + 1500 * math.sin(a2):.0f}z")
    return (f'<defs>{gd}{faded}<mask id="rays{inner[1:]}"><rect width="{S}" height="{S}" fill="url(#{fade})"/></mask></defs>'
            + f'<rect width="{S}" height="{S}" fill="url(#{g})"/>'
            + f'<g mask="url(#rays{inner[1:]})" fill="#fff" fill-opacity="{ray_alpha}">{wedges}</g>')


def tile(x, y, s, colour, tilt=0, glyph=True):
    """A glossy jelly tile like the blast cubes, with its glyph pressed in."""
    light, base, dark, deep = CUBES[colour]
    r = s * 0.24
    body, bodyd = vgrad([(0, light, 1), (0.45, base, 1), (1, dark, 1)])
    face, faced = rgrad([(0, light, 1), (0.6, base, 1), (1, base, 0)], cx=0.5, cy=0.38, r=0.62)
    gloss, glossd = vgrad([(0, "#fff", 0.85), (1, "#fff", 0)])
    sh, shd = blur(s * 0.04)
    soft, softd = blur(s * 0.025)
    c, cd = clip(rrect(x, y, s, s * 0.95, r))
    out = (f'<defs>{bodyd}{faced}{glossd}{shd}{softd}{cd}</defs><g transform="rotate({tilt} {x + s / 2} {y + s / 2})">'
           + rrect(x + s * 0.03, y + s * 0.1, s, s, r, f'fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
           + rrect(x, y + s * 0.06, s, s * 0.95, r, f'fill="{deep}"')
           + rrect(x, y, s, s * 0.95, r, f'fill="url(#{body})"')
           + f'<g clip-path="url(#{c})">' + rrect(x + s * 0.1, y + s * 0.09, s * 0.8, s * 0.7, r * 0.8, f'fill="url(#{face})" filter="url(#{soft})"') + "</g>")
    if glyph:
        k = s / 24 * 0.62
        t = f'transform="translate({x + s * 0.19} {y + s * 0.15}) scale({k})"'
        out += (f'<g {t}>' + p(GLYPHS[colour], f'fill="{light}" fill-opacity="0.75" transform="translate(0 0.75)"')
                + p(GLYPHS[colour], f'fill="{deep}" fill-opacity="0.42"') + "</g>")
    out += (f'<ellipse cx="{x + s * 0.4}" cy="{y + s * 0.2}" rx="{s * 0.24}" ry="{s * 0.08}" transform="rotate(-6 {x + s * 0.4} {y + s * 0.2})" fill="url(#{gloss})"/>'
            + f'<circle cx="{x + s * 0.2}" cy="{y + s * 0.18}" r="{s * 0.035}" fill="#fff" fill-opacity="0.9"/>'
            + rrect(x, y, s, s * 0.95, r, f'fill="none" stroke="{deep}" stroke-opacity="0.55" stroke-width="{s * 0.02}"')
            + "</g>")
    return out


def sparkle(x, y, r, alpha=1.0):
    g, gd = blur(r * 0.25)
    d = f"M{x} {y - r}C{x + r * 0.12} {y - r * 0.12} {x + r * 0.12} {y - r * 0.12} {x + r} {y}C{x + r * 0.12} {y + r * 0.12} {x + r * 0.12} {y + r * 0.12} {x} {y + r}C{x - r * 0.12} {y + r * 0.12} {x - r * 0.12} {y + r * 0.12} {x - r} {y}C{x - r * 0.12} {y - r * 0.12} {x - r * 0.12} {y - r * 0.12} {x} {y - r}z"
    return f"<defs>{gd}</defs>" + p(d, f'fill="#fff" fill-opacity="{0.8 * alpha}" filter="url(#{g})"') + p(d, f'fill="#fff" fill-opacity="{alpha}"')


# The mosaic mark alone, four tiles with the games' glyphs.
gap, s = 30, 360
x0, y0 = S / 2 - s - gap / 2, S / 2 - s - gap / 2 + 10
write("icon_b_mosaic",
      burst("#8A7CFF", "#2B1C8F")
      + f'<g transform="rotate(-7 {S / 2} {S / 2})">'
      + tile(x0, y0, s, "blue") + tile(x0 + s + gap, y0, s, "yellow")
      + tile(x0, y0 + s + gap, s, "red") + tile(x0 + s + gap, y0 + s + gap, s, "green") + "</g>"
      + sparkle(800, 200, 56) + sparkle(210, 820, 34, 0.9))
print("icon svgs")
