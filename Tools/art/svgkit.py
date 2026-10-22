"""Small SVG helpers for the glossy, soft-shaded blast art."""
import itertools

_ids = itertools.count()


def uid(prefix="g"):
    return f"{prefix}{next(_ids)}"


def svg(body, view, size):
    w, h = size if isinstance(size, tuple) else (size, size)
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="{view}">'
            f'{body}</svg>\n')


def vgrad(stops, x1=0, y1=0, x2=0, y2=1):
    """Vertical (by default) linear gradient. stops: [(offset, colour, opacity)]. Returns (id, defs)."""
    i = uid("lg")
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    return i, f'<linearGradient id="{i}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">{s}</linearGradient>'


def rgrad(stops, cx=0.5, cy=0.5, r=0.5, fx=None, fy=None):
    i = uid("rg")
    f = f' fx="{fx}" fy="{fy}"' if fx is not None else ""
    s = "".join(f'<stop offset="{o}" stop-color="{c}" stop-opacity="{a}"/>' for o, c, a in stops)
    return i, f'<radialGradient id="{i}" cx="{cx}" cy="{cy}" r="{r}"{f}>{s}</radialGradient>'


def blur(sd):
    i = uid("bl")
    return i, f'<filter id="{i}" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="{sd}"/></filter>'


def clip(shape):
    i = uid("cp")
    return i, f'<clipPath id="{i}">{shape}</clipPath>'


def rrect(x, y, w, h, r, extra=""):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" {extra}/>'


def p(d, extra=""):
    return f'<path d="{d}" {extra}/>'


def circ(cx, cy, r):
    return f"M{cx - r} {cy}a{r} {r} 0 1 0 {2 * r} 0a{r} {r} 0 1 0 {-2 * r} 0z"


def glossy(d, top, bottom, rim, rim_w, gloss=0.6, mid=None, shadow=None, gloss_box=None):
    """A filled path with a vertical gradient, a darker rim and a clipped gloss highlight over its upper part.
    gloss_box: (x, y, w, h) of the highlight ellipse in path units; defaults to the upper half of the bbox guess."""
    stops = [(0, top, 1), (1, bottom, 1)] if mid is None else [(0, top, 1), (0.5, mid, 1), (1, bottom, 1)]
    g, gd = vgrad(stops)
    c, cd = clip(p(d))
    defs = gd + cd
    out = ""
    if shadow:
        dx, dy, sd, colour, alpha = shadow
        b, bd = blur(sd)
        defs += bd
        out += p(d, f'transform="translate({dx} {dy})" fill="{colour}" fill-opacity="{alpha}" filter="url(#{b})"')
    out += p(d, f'fill="url(#{g})"')
    if gloss and gloss_box:
        x, y, w, h = gloss_box
        hg, hgd = vgrad([(0, "#fff", gloss), (1, "#fff", 0)])
        defs += hgd
        out += f'<g clip-path="url(#{c})"><ellipse cx="{x + w / 2}" cy="{y + h / 2}" rx="{w / 2}" ry="{h / 2}" fill="url(#{hg})"/></g>'
    if rim_w:
        out += p(d, f'fill="none" stroke="{rim}" stroke-width="{rim_w}" stroke-linejoin="round" stroke-linecap="round"')
    return f"<defs>{defs}</defs>{out}"
