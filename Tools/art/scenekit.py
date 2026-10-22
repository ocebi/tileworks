"""Shared parts for the illustrated backdrops: smooth hills, clouds, trees, bushes and glossy toy cubes."""
from svgkit import blur, circ, p, rgrad, rrect, vgrad

CUBES = {
    "red": ("#FF8577", "#F2463A", "#C8281F", "#8A1712"), "blue": ("#7DBBFF", "#2F86F5", "#1C5BCB", "#103C8E"),
    "green": ("#8BEA70", "#34BE52", "#1D8E37", "#115F23"), "yellow": ("#FFEB7A", "#FFC92E", "#E59A0E", "#A66606"),
    "purple": ("#CFA4FF", "#9D5EF7", "#6E35CF", "#47208F"), "teal": ("#73E8F2", "#16AFC4", "#0D7F92", "#075665"),
}


def smooth(points):
    """Catmull-Rom through the points as cubic Béziers (path data without the leading M)."""
    d = ""
    for i in range(len(points) - 1):
        p0 = points[i - 1] if i > 0 else points[i]
        p1, p2 = points[i], points[i + 1]
        p3 = points[i + 2] if i + 2 < len(points) else p2
        c1 = (p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6)
        d += f"C{c1[0]:.1f} {c1[1]:.1f} {c2[0]:.1f} {c2[1]:.1f} {p2[0]:.1f} {p2[1]:.1f}"
    return d


def hill(points, top, bottom, rim, y_bottom):
    g, gd = vgrad([(0, top, 1), (1, bottom, 1)])
    edge = f"M{points[0][0]} {points[0][1]}" + smooth(points)
    shape = edge + f"L{points[-1][0]} {y_bottom}L{points[0][0]} {y_bottom}z"
    soft, softd = blur(3)
    return (f"<defs>{gd}{softd}</defs>" + p(shape, f'fill="url(#{g})"')
            + p(edge, f'fill="none" stroke="{rim}" stroke-width="10" stroke-opacity="0.8" stroke-linecap="round" filter="url(#{soft})" transform="translate(0 6)"'))


def cloud(cx, cy, s, alpha=1.0):
    puffs = [(-60, 10, 42), (-20, -18, 55), (30, -10, 48), (70, 12, 36), (0, 20, 50), (-90, 26, 28), (100, 28, 26)]
    g, gd = vgrad([(0, "#FFFFFF", 1), (0.6, "#FFFFFF", 1), (1, "#CFE3F5", 1)])
    sh, shd = blur(6 * s)
    shapes = "".join(p(circ(cx + x * s, cy + y * s, r * s)) for x, y, r in puffs)
    base = rrect(cx - 105 * s, cy + 8 * s, 210 * s, 46 * s, 23 * s)
    return (f"<defs>{gd}{shd}</defs><g opacity=\"{alpha}\">"
            + f'<g fill="#7FB6E0" fill-opacity="0.35" filter="url(#{sh})" transform="translate(0 {10 * s})">{shapes}{base}</g>'
            + f'<g fill="url(#{g})">{shapes}{base}</g>'
            + p(circ(cx - 26 * s, cy - 34 * s, 16 * s), 'fill="#fff"') + "</g>")


def toy_cube(x, y, s, colour, tilt=0):
    light, base, dark, deep = CUBES[colour]
    g, gd = vgrad([(0, light, 1), (0.45, base, 1), (1, dark, 1)])
    r = s * 0.22
    return (f'<defs>{gd}</defs><g transform="rotate({tilt} {x + s / 2} {y + s / 2})">'
            + rrect(x, y + s * 0.06, s, s, r, f'fill="{deep}"')
            + rrect(x, y, s, s * 0.95, r, f'fill="url(#{g})" stroke="{deep}" stroke-opacity="0.5" stroke-width="{s * 0.025}"')
            + f'<ellipse cx="{x + s * 0.42}" cy="{y + s * 0.2}" rx="{s * 0.26}" ry="{s * 0.08}" fill="#fff" fill-opacity="0.7"/>'
            + "</g>")


def tower(x, ground, s, colours):
    out, y = "", ground
    sh, shd = blur(8)
    out += f"<defs>{shd}</defs>" + f'<ellipse cx="{x + s / 2}" cy="{ground}" rx="{s * 0.75}" ry="{s * 0.16}" fill="#1E5A1C" fill-opacity="0.35" filter="url(#{sh})"/>'
    for i, colour in enumerate(colours):
        y -= s * 0.98
        out += toy_cube(x + (i % 2) * s * 0.06 - s * 0.03, y, s, colour, tilt=(-4 if i % 2 else 3))
    return out


def tree(x, ground, s):
    trunk, trunkd = vgrad([(0, "#A8703C", 1), (1, "#6E4220", 1)])
    leaf, leafd = rgrad([(0, "#9BE86A", 1), (0.55, "#4DB33C", 1), (1, "#2A7A27", 1)], cx=0.38, cy=0.32, r=0.75)
    sh, shd = blur(6)
    return (f"<defs>{trunkd}{leafd}{shd}</defs>"
            + f'<ellipse cx="{x}" cy="{ground}" rx="{s * 0.55}" ry="{s * 0.12}" fill="#1E5A1C" fill-opacity="0.35" filter="url(#{sh})"/>'
            + rrect(x - s * 0.08, ground - s * 0.75, s * 0.16, s * 0.75, s * 0.06, f'fill="url(#{trunk})"')
            + p(circ(x, ground - s * 0.95, s * 0.5), f'fill="url(#{leaf})" stroke="#24692A" stroke-width="{s * 0.03}"')
            + f'<ellipse cx="{x - s * 0.15}" cy="{ground - s * 1.2}" rx="{s * 0.18}" ry="{s * 0.1}" fill="#fff" fill-opacity="0.35"/>')


def bush(x, ground, s):
    leaf, leafd = rgrad([(0, "#8FE063", 1), (1, "#2E8A2C", 1)], cx=0.4, cy=0.3, r=0.8)
    shapes = p(circ(x - s * 0.35, ground - s * 0.25, s * 0.32)) + p(circ(x, ground - s * 0.38, s * 0.4)) + p(circ(x + s * 0.38, ground - s * 0.22, s * 0.3))
    return f"<defs>{leafd}</defs>" + f'<g fill="url(#{leaf})" stroke="#24692A" stroke-width="{s * 0.03}">{shapes}</g>'


def path_road(centre, widths):
    """A winding sandy path as a polygon around a centre line, narrowing into the distance."""
    left = [(x - w / 2, y) for (x, y), w in zip(centre, widths)]
    right = [(x + w / 2, y) for (x, y), w in zip(centre, widths)][::-1]
    d = f"M{left[0][0]} {left[0][1]}" + smooth(left) + f"L{right[0][0]} {right[0][1]}" + smooth(right) + "z"
    g, gd = vgrad([(0, "#F3D9A4", 1), (1, "#E9B96E", 1)])
    return f"<defs>{gd}</defs>" + p(d, f'fill="url(#{g})" stroke="#C99550" stroke-width="5" stroke-linejoin="round"')


