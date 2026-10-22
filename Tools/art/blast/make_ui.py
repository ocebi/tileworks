"""Writes the blast UI kit as glossy, soft-shaded SVGs: 9-slice frames for the 1080-wide canvas, badges and icons.
Names, sizes and 9-slice borders match the first kit, so the PNGs drop in without code changes."""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from svgkit import blur, circ, clip, glossy, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
slices = {}

# Borders (left, bottom, right, top) the importer already uses.
BORDERS = {
    "ui_panel": [103, 128, 103, 103], "ui_button": [76, 92, 76, 76], "ui_card": [67, 78, 67, 67], "ui_pill": [72, 72, 72, 72],
    "ui_tile": [67, 81, 67, 67], "ui_chip": [48, 48, 48, 48], "ui_ribbon": [59, 59, 59, 59],
}


def write(name, size, body, border=None):
    w, h = size
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, f"0 0 {w} {h}", (w, h)))
    slices[name] = {"size": [w, h], "border_lbrt": border}


def frame(name, size, stops, rim, radius, rim_w, border, lip=0, lip_colour=None, shadow=0, gloss=0.0, gloss_h=0, edge_light=0.5,
          inner_shadow=0.0, bottom_light=None):
    """A bevelled 9-slice frame: soft drop shadow, a darker lip for depth, a gradient body, top edge light and a gloss band."""
    w, h = size
    bx = by = rim_w / 2 + 2
    bw = w - 2 * bx
    bh = h - by - rim_w / 2 - 2 - lip - shadow
    body = rrect(bx, by, bw, bh, radius)
    g, gd = vgrad(stops)
    c, cd = clip(body)
    defs = gd + cd
    out = ""
    if shadow:
        b, bd = blur(shadow * 0.35)
        defs += bd
        out += rrect(bx + 2, by + lip + shadow * 0.55, bw - 4, bh, radius, f'fill="#1A0503" fill-opacity="0.38" filter="url(#{b})"')
    if lip:
        out += rrect(bx, by + lip, bw, bh, radius, f'fill="{lip_colour}" stroke="{rim}" stroke-width="{rim_w}"')
    out += rrect(bx, by, bw, bh, radius, f'fill="url(#{g})"')
    inner = ""
    if edge_light:
        e, ed = blur(rim_w * 0.35)
        defs += ed
        inner += rrect(bx, by + rim_w * 0.55, bw, bh, radius,
                       f'fill="none" stroke="#fff" stroke-opacity="{edge_light}" stroke-width="{rim_w * 1.1}" filter="url(#{e})"')
    if inner_shadow:
        s, sd = blur(rim_w * 1.2)
        defs += sd
        inner += rrect(bx - 10, by - rim_w * 3.2, bw + 20, rim_w * 5, rim_w * 2, f'fill="#000" fill-opacity="{inner_shadow}" filter="url(#{s})"')
    if bottom_light:
        e, ed = blur(rim_w * 0.3)
        defs += ed
        inner += rrect(bx, by - rim_w * 0.5, bw, bh, radius, f'fill="none" stroke="{bottom_light}" stroke-width="{rim_w * 0.9}" filter="url(#{e})"')
    if gloss:
        gg, ggd = vgrad([(0, "#fff", gloss), (1, "#fff", 0.04)])
        defs += ggd
        inset = radius * 0.42
        inner += rrect(bx + inset, by + rim_w * 0.9, bw - 2 * inset, gloss_h, gloss_h / 2, f'fill="url(#{gg})"')
    out += f'<g clip-path="url(#{c})">{inner}</g>'
    out += rrect(bx, by, bw, bh, radius, f'fill="none" stroke="{rim}" stroke-width="{rim_w}"')
    write(name, size, f"<defs>{defs}</defs>{out}", border)


# Popup panel: a golden bevelled frame around a cream card, on a soft shadow.
def panel():
    w, h, rim_w, shadow = 256, 256, 6, 22
    bx = by = rim_w / 2 + 2
    bw, bh = w - 2 * bx, h - by - rim_w / 2 - 2 - shadow
    frame_g, frame_gd = vgrad([(0, "#FFE59A", 1), (0.5, "#FFC453", 1), (1, "#E8912E", 1)])
    # Flat through the middle rows, which the 9-slice stretches.
    cream_g, cream_gd = vgrad([(0, "#FFF4DE", 1), (0.3, "#FFFAF1", 1), (0.7, "#FFFAF1", 1), (1, "#FCEBD2", 1)])
    sh, shd = blur(8)
    ins, insd = blur(4)
    inner = rrect(bx + 15, by + 15, bw - 30, bh - 30, 72)
    c, cd = clip(inner)
    body = (f"<defs>{frame_gd}{cream_gd}{shd}{insd}{cd}</defs>"
            + rrect(bx + 4, by + shadow * 0.6, bw - 8, bh, 88, f'fill="#1A0503" fill-opacity="0.42" filter="url(#{sh})"')
            + rrect(bx, by, bw, bh, 88, f'fill="url(#{frame_g})"')
            + rrect(bx + 3, by + 3, bw - 6, bh - 6, 85, 'fill="none" stroke="#fff" stroke-opacity="0.55" stroke-width="3"')
            + inner.replace("/>", ' fill="url(#' + cream_g + ')"/>')
            + f'<g clip-path="url(#{c})">' + rrect(bx, by - 4, bw, 22, 11, f'fill="#B8661C" fill-opacity="0.35" filter="url(#{ins})"') + "</g>"
            + inner.replace("/>", ' fill="none" stroke="#C9772A" stroke-width="3"/>')
            + rrect(bx, by, bw, bh, 88, f'fill="none" stroke="#7A3414" stroke-width="{rim_w}"'))
    write("ui_panel", (w, h), body, BORDERS["ui_panel"])


panel()

BUTTONS = {
    "yellow": ([(0, "#FFEB7C", 1), (0.45, "#FFC830", 1), (1, "#F6A21A", 1)], "#A85E08", "#D98510"),
    "green": ([(0, "#97EE78", 1), (0.45, "#3CC457", 1), (1, "#1F9A3D", 1)], "#11652A", "#178035"),
    "white": ([(0, "#FFFFFF", 1), (0.45, "#FBF6F0", 1), (1, "#E6DACD", 1)], "#9C8676", "#C4B2A2"),
    "pink": ([(0, "#FFB0BE", 1), (0.45, "#FF7D93", 1), (1, "#EB5070", 1)], "#A0233F", "#C93A5A"),
}
# The shared kit for the other games adds blue, red and purple buttons.
if "--shared" in sys.argv:
    BUTTONS.update({
        "blue": ([(0, "#9ED2FF", 1), (0.45, "#3C8FF5", 1), (1, "#1E62D0", 1)], "#0F3A8A", "#1A50B0"),
        "red": ([(0, "#FFA294", 1), (0.45, "#F2463A", 1), (1, "#C8281F", 1)], "#7A0E08", "#A81E16"),
        "purple": ([(0, "#DDB8FF", 1), (0.45, "#9D5EF7", 1), (1, "#6E35CF", 1)], "#3E1A80", "#5A2AB0"),
    })
for colour, (stops, rim, lip) in BUTTONS.items():
    frame(f"ui_button_{colour}", (192, 192), stops, rim, 58, 6, BORDERS["ui_button"], lip=13, lip_colour=lip, shadow=10,
          gloss=0.7 if colour != "white" else 0.0, gloss_h=34, edge_light=0.6)

# HUD card and tile: near-white so the tints in the scene builder still read; a short lip gives them depth.
frame("ui_card", (160, 160), [(0, "#FFFFFF", 1), (0.6, "#FBF7F2", 1), (1, "#ECE2D6", 1)], "#A89482", 52, 5, BORDERS["ui_card"],
      lip=7, lip_colour="#CDBBA8", shadow=4, edge_light=0.8)
frame("ui_tile", (160, 160), [(0, "#FFFDF8", 1), (0.6, "#FFF6E8", 1), (1, "#F2E2CC", 1)], "#A88B6E", 52, 5, BORDERS["ui_tile"],
      lip=9, lip_colour="#D1B795", shadow=5, edge_light=0.8)
# Deep-blue sockets for counters and the pause button, matching the sky: shaded from the top, lit along the bottom edge.
frame("ui_pill", (160, 160), [(0, "#0E2A5C", 1), (1, "#1F4E96", 1)], "#0A1B40", 58, 5, BORDERS["ui_pill"], edge_light=0,
      inner_shadow=0.5, bottom_light="#79B2F2")
frame("ui_chip", (128, 128), [(0, "#FFFFFF", 1), (1, "#F1E9DF", 1)], "#D9C8B6", 36, 4, BORDERS["ui_chip"], edge_light=0.9)
frame("ui_ribbon", (192, 192), [(0, "#FFEB7C", 1), (0.5, "#FFC830", 1), (0.8, "#F6A21A", 1), (1, "#E08A12", 1)], "#A85E08", 42, 6,
      BORDERS["ui_ribbon"], gloss=0.7, gloss_h=30, edge_light=0.6)
frame("ui_ribbon_pink", (192, 192), [(0, "#FFB0BE", 1), (0.5, "#FF7D93", 1), (0.8, "#EB5070", 1), (1, "#D63E5E", 1)], "#A0233F", 42, 6,
      BORDERS["ui_ribbon"], gloss=0.7, gloss_h=30, edge_light=0.6)
# Hard and Super Hard level badges.
frame("ui_ribbon_red", (192, 192), [(0, "#FF9A86", 1), (0.5, "#F2503C", 1), (0.8, "#D23424", 1), (1, "#B2241A", 1)], "#6E120C", 42, 6,
      BORDERS["ui_ribbon"], gloss=0.7, gloss_h=30, edge_light=0.6)
frame("ui_ribbon_purple", (192, 192), [(0, "#D9B4FF", 1), (0.5, "#A564F5", 1), (0.8, "#8040D8", 1), (1, "#6A2EBD", 1)], "#3A137A", 42, 6,
      BORDERS["ui_ribbon"], gloss=0.7, gloss_h=30, edge_light=0.6)


def badge(name, light, base, deep, rim):
    g, gd = rgrad([(0, light, 1), (0.55, base, 1), (1, deep, 1)], cx=0.42, cy=0.32, r=0.72)
    hg, hgd = vgrad([(0, "#fff", 0.85), (1, "#fff", 0.05)])
    write(name, (96, 96), f"<defs>{gd}{hgd}</defs>"
          + f'<circle cx="48" cy="50" r="42" fill="{rim}"/>'
          + f'<circle cx="48" cy="47" r="41" fill="url(#{g})" stroke="{rim}" stroke-width="5"/>'
          + f'<ellipse cx="44" cy="26" rx="24" ry="11" fill="url(#{hgd and hg})"/>')


badge("ui_badge_red", "#FF8C80", "#EC3A2E", "#A8170E", "#7A0E08")
badge("ui_badge_green", "#9BEE7E", "#30B954", "#167A2E", "#0E5A20")
badge("ui_badge_purple", "#DDB8FF", "#9D5EF7", "#6A2EBD", "#3A137A")


def icon(name, body, view="0 0 24 24", size=96):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, view, (size, size)))
    slices[name] = {"size": [size, size], "border_lbrt": None}


def chunky(d, outer, stops, w=2.6, rim=1.5, shadow=True):
    """A stroked glyph drawn as a toy: soft shadow, a darker outline, a gradient core and a thin highlight."""
    g, gd = vgrad(stops)
    b, bd = blur(0.45)
    cap = 'stroke-linecap="round" stroke-linejoin="round" fill="none"'
    out = f"<defs>{gd}{bd}</defs>"
    if shadow:
        out += p(d, f'{cap} stroke="#000" stroke-opacity="0.3" stroke-width="{w + rim}" transform="translate(0 0.8)" filter="url(#{b})"')
    out += p(d, f'{cap} stroke="{outer}" stroke-width="{w + rim}"')
    out += p(d, f'{cap} stroke="url(#{g})" stroke-width="{w}"')
    out += p(d, f'{cap} stroke="#fff" stroke-opacity="0.45" stroke-width="{w * 0.3}" transform="translate(-0.25 -0.35)"')
    return out


def drop(sd=0.5, dy=0.8, a=0.3):
    b, bd = blur(sd)
    return bd, f'transform="translate(0 {dy})" fill="#000" fill-opacity="{a}" filter="url(#{b})"'


HEART = ("M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3"
         " 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z")
icon("icon_heart", glossy(HEART, "#FF9AAA", "#B81834", "#7A0A1E", 1.0, mid="#F43D58", shadow=(0, 0.7, 0.5, "#000", 0.3),
                          gloss_box=(3.5, 3.2, 11, 7), gloss=0.8)
     + p(circ(6.4, 7.2, 0.9), 'fill="#fff" fill-opacity="0.9"'))

coin_g, coin_gd = vgrad([(0, "#FFF0A0", 1), (0.5, "#FFC52E", 1), (1, "#D9880C", 1)])
ring_g, ring_gd = vgrad([(0, "#E39B14", 1), (1, "#FFE27A", 1)])
star_g, star_gd = vgrad([(0, "#FFF8C8", 1), (1, "#F5B81E", 1)])
sd, sh = drop()
STAR = "M12 7.6l1.35 2.75 3.05.45-2.2 2.15.5 3.05L12 14.55 9.3 16l.5-3.05-2.2-2.15 3.05-.45z"
icon("icon_coin", f"<defs>{coin_gd}{ring_gd}{star_gd}{sd}</defs>"
     + p(circ(12, 12, 10), sh)
     + p(circ(12, 12, 10), f'fill="url(#{coin_g})" stroke="#8A5206" stroke-width="1"')
     + p(circ(12, 12, 7), f'fill="url(#{ring_g})" stroke="#B87108" stroke-width="0.6"')
     + p(STAR, f'fill="url(#{star_g})" stroke="#A86A06" stroke-width="0.5" stroke-linejoin="round"')
     + '<ellipse cx="9" cy="6" rx="4.2" ry="2" transform="rotate(-25 9 6)" fill="#fff" fill-opacity="0.65"/>')

icon("icon_plus", chunky("M12 6v12M6 12h12", "#0E5A20", [(0, "#FFFFFF", 1), (1, "#E2F5DA", 1)], w=3.6, rim=1.8))
sd, sh = drop(0.6, 0.9, 0.4)
bars = rrect(5.5, 4.5, 4.8, 15, 1.8) + rrect(13.7, 4.5, 4.8, 15, 1.8)
pg, pgd = vgrad([(0, "#FFFFFF", 1), (1, "#F2D9D4", 1)])
icon("icon_pause", f"<defs>{sd}{pgd}</defs>" + f'<g {sh}>{bars}</g>' + f'<g fill="url(#{pg})">{bars}</g>')
icon("icon_close", chunky("M6.5 6.5l11 11M17.5 6.5l-11 11", "#7A0E08", [(0, "#FF8C80", 1), (1, "#D9281E", 1)], w=3.6, rim=1.8))

check_g, check_gd = rgrad([(0, "#9BEE7E", 1), (0.55, "#30B954", 1), (1, "#167A2E", 1)], cx=0.42, cy=0.32, r=0.72)
sd, sh = drop()
icon("icon_check", f"<defs>{check_gd}{sd}</defs>"
     + p(circ(12, 12, 10), sh)
     + p(circ(12, 12, 10), f'fill="url(#{check_g})" stroke="#0E5A20" stroke-width="1"')
     + '<ellipse cx="11" cy="6.4" rx="5.6" ry="2.4" fill="#fff" fill-opacity="0.5"/>'
     + p("M7.4 12.4l3.1 3.1 6.2-6.6", 'fill="none" stroke="#0E5A20" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round"')
     + p("M7.4 12.4l3.1 3.1 6.2-6.6", 'fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"'))

# Boosters for Blast and Gem Swap. The hammer is a toy mallet: a red barrel head with gold bands on a wooden handle.
# fx_hammer is the same mallet at piece size, for the strike on the board.
wood = [(0, "#F2BE7A", 1), (1, "#9A5A26", 1)]
gold = ("#FFF0A0", "#D9880C", "#8A5206", "#FFC52E")
mallet = ("M6.6 3.2h10.8a2.8 2.8 0 0 1 2.8 2.8v4.4a2.8 2.8 0 0 1-2.8 2.8H6.6a2.8 2.8 0 0 1-2.8-2.8V6a2.8 2.8 0 0 1 2.8-2.8z")
band = "M{x} 3.2h1.9v10h-1.9z"
HAMMER = (f'<g transform="translate(11.5 12.9) rotate(40) scale(1.04) translate(-12 -12)">'
          + chunky("M12 12.6V21", "#4E2A0E", wood, w=2.8, rim=1.4)
          + glossy("M10.3 19.6h3.4v2.2a1.2 1.2 0 0 1-1.2 1.2h-1a1.2 1.2 0 0 1-1.2-1.2z", "#FF8A70", "#B81E16", "#5A0C08", 0.6)
          + glossy(mallet, "#FF9A80", "#9E1A12", "#5A0C08", 0.9, mid="#E8362A", shadow=(0, 0.8, 0.5, "#000", 0.32),
                   gloss_box=(5, 3.6, 14, 4.2), gloss=0.75)
          + glossy(band.format(x=6.6), gold[0], gold[1], gold[2], 0.5, mid=gold[3])
          + glossy(band.format(x=15.5), gold[0], gold[1], gold[2], 0.5, mid=gold[3])
          + p(mallet, 'fill="none" stroke="#5A0C08" stroke-width="0.9"')
          + p(circ(8.6, 5.4, 0.7), 'fill="#fff" fill-opacity="0.9"') + "</g>")
icon("icon_hammer", HAMMER)
icon("fx_hammer", HAMMER, size=256)
icon("icon_sweep", chunky("M4 12h16M4 12l3.2-3.2M4 12l3.2 3.2M20 12l-3.2-3.2M20 12l-3.2 3.2", "#8A3A06",
                          [(0, "#FFD27A", 1), (1, "#F07A1A", 1)], w=2.6, rim=1.6))
# Two arrows that cross: the green one passes under the blue one. fx_shuffle is the same at piece size.
SHUFFLE = (chunky("M3.6 16.8h2.6c2.2 0 3.6-1.2 4.9-3.4l2.2-3.4c1.3-2.1 2.7-3.2 4.9-3.2h1.6M16.8 3.8l3.4 3-3.4 3", "#0E5A20",
                            [(0, "#9BEE7E", 1), (1, "#22A046", 1)], w=2.8, rim=1.6)
     + chunky("M3.6 7.2h2.6c2.2 0 3.6 1.2 4.9 3.4l2.2 3.4c1.3 2.1 2.7 3.2 4.9 3.2h1.6M16.8 14.2l3.4 3-3.4 3", "#103C8E",
              [(0, "#8CC6FF", 1), (1, "#2F7BF0", 1)], w=2.8, rim=1.6))
icon("icon_shuffle", SHUFFLE)
icon("fx_shuffle", SHUFFLE, size=256)
roof_g, roof_gd = vgrad([(0, "#FF8A70", 1), (1, "#C8281F", 1)])
wall_g, wall_gd = vgrad([(0, "#FFFFFF", 1), (1, "#EADCC8", 1)])
sd, sh = drop()
house = "M5.5 10.5V19.5a1 1 0 0 0 1 1h11a1 1 0 0 0 1-1V10.5L12 5.5z"
icon("icon_home", f"<defs>{roof_gd}{wall_gd}{sd}</defs>" + p(house, sh)
     + p(house, f'fill="url(#{wall_g})" stroke="#6B4A36" stroke-width="1"')
     + glossy("M10 20.5v-5a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v5z", "#C98B4F", "#7A4520", "#4E2A0E", 0.6)
     + chunky("M3.2 11.6L12 4.2l8.8 7.4", "#7A0E08", [(0, "#FF8A70", 1), (1, "#C8281F", 1)], w=2.6, rim=1.4, shadow=False))
white = [(0, "#FFFFFF", 1), (1, "#F2DCD6", 1)]
icon("icon_shop", chunky("M3 4h2.5l2.2 11h10.6l2-7.5H6.3", "#5A140F", white, w=2.2, rim=1.4)
     + p(circ(9.5, 19.5, 1.5), 'fill="#fff" stroke="#5A140F" stroke-width="0.8"') + p(circ(17, 19.5, 1.5), 'fill="#fff" stroke="#5A140F" stroke-width="0.8"'))
icon("icon_stats", chunky("M5 20V12M10 20V6M15 20v-9M20 20V4", "#5A140F", white, w=2.6, rim=1.4))
squares = "".join(rrect(x, y, 7, 7, 2) for x, y in ((3.5, 3.5), (13.5, 3.5), (3.5, 13.5), (13.5, 13.5)))
sg, sgd = vgrad(white)
sd, sh = drop()
icon("icon_games", f"<defs>{sgd}{sd}</defs>" + f"<g {sh}>{squares}</g>" + f'<g fill="url(#{sg})" stroke="#5A140F" stroke-width="1">{squares}</g>')
# Levels: a list whose first two rows are ticked.
rows = (3.6, 10.0, 16.4)
bars = "".join(rrect(9.2, y, 11.8, 4.4, 2.2) for y in rows)
dots = "".join(p(circ(5.4, y + 2.2, 2.6)) for y in rows)
sd, sh = drop()
lg, lgd = rgrad([(0, "#9BEE7E", 1), (0.55, "#30B954", 1), (1, "#167A2E", 1)], cx=0.42, cy=0.32, r=0.72)
icon("icon_levels", f"<defs>{sgd}{lgd}{sd}</defs>" + f"<g {sh}>{bars}{dots}</g>"
     + f'<g fill="url(#{sg})" stroke="#5A140F" stroke-width="1">{bars}{p(circ(5.4, rows[2] + 2.2, 2.6))}</g>'
     + f'<g fill="url(#{lg})" stroke="#0E5A20" stroke-width="1">{p(circ(5.4, rows[0] + 2.2, 2.6))}{p(circ(5.4, rows[1] + 2.2, 2.6))}</g>'
     + "".join(p(f"M4.1 {y + 2.3}l1 1 1.7-1.9", 'fill="none" stroke="#fff" stroke-width="1" stroke-linecap="round" stroke-linejoin="round"') for y in rows[:2]))
icon("icon_flag", chunky("M5 21V4", "#3E4656", [(0, "#F4F7FB", 1), (1, "#8C97AA", 1)], w=2, rim=1.2)
     + glossy("M6 4h12.5l-3 4 3 4H6z", "#8BEA70", "#1D8E37", "#115F23", 0.9, mid="#34BE52", shadow=(0, 0.6, 0.45, "#000", 0.3),
              gloss_box=(6, 3, 12, 4), gloss=0.7))
icon("icon_lock", chunky("M8 11V8a4 4 0 0 1 8 0v3", "#4A5468", [(0, "#F4F7FB", 1), (1, "#8C97AA", 1)], w=2.2, rim=1.4)
     + glossy("M6.2 10.5h11.6a1.2 1.2 0 0 1 1.2 1.2v7.6a1.7 1.7 0 0 1-1.7 1.7H6.7a1.7 1.7 0 0 1-1.7-1.7v-7.6a1.2 1.2 0 0 1 1.2-1.2z",
              "#FFE98A", "#D9880C", "#7A4A06", 1, mid="#FFC52E", shadow=(0, 0.7, 0.5, "#000", 0.3), gloss_box=(5, 9.6, 14, 4.6), gloss=0.7)
     + p("M12 14v2.4", 'stroke="#7A4A06" stroke-width="1.8" stroke-linecap="round"'))
deep = [(0, "#E0604A", 1), (1, "#8A2418", 1)]
speaker = "M4 9.5h3.5L12 5.5v13l-4.5-4H4z"
spg, spgd = vgrad(deep)
sd, sh = drop()
icon("icon_sound", f"<defs>{spgd}{sd}</defs>" + p(speaker, sh) + p(speaker, f'fill="url(#{spg})" stroke="#4A120B" stroke-width="1" stroke-linejoin="round"')
     + chunky("M15.5 9a4 4 0 0 1 0 6M18 6.5a7.5 7.5 0 0 1 0 11", "#4A120B", deep, w=1.9, rim=1.2, shadow=False))
icon("icon_vibration", glossy("M8.4 3.5h7.2a1.4 1.4 0 0 1 1.4 1.4v14.2a1.4 1.4 0 0 1-1.4 1.4H8.4A1.4 1.4 0 0 1 7 19.1V4.9a1.4 1.4 0 0 1 1.4-1.4z",
                              "#E0604A", "#8A2418", "#4A120B", 1, shadow=(0, 0.7, 0.5, "#000", 0.3), gloss_box=(7, 2.5, 10, 7), gloss=0.6)
     + rrect(9, 6, 6, 10, 0.8, 'fill="#FFE9E2" fill-opacity="0.9"')
     + chunky("M3.6 8.5v7M20.4 8.5v7", "#4A120B", deep, w=1.8, rim=1.2, shadow=False))
icon("icon_motion", chunky("M3 12c2.5-5 5.5-5 8 0s5.5 5 8 0M18.6 6.2l2 2-2 2", "#4A120B", deep, w=2.2, rim=1.4))
def gear_path(cx, cy, outer, inner, teeth):
    pts = []
    step = 2 * math.pi / teeth
    for i in range(teeth):
        a = i * step
        for da, r in ((-0.30, inner), (-0.17, outer), (0.17, outer), (0.30, inner)):
            pts.append(f"{cx + r * math.cos(a + da * step):.2f} {cy + r * math.sin(a + da * step):.2f}")
    return "M" + "L".join(pts) + "z"


icon("icon_settings", chunky(gear_path(12, 12, 9.2, 6.8, 8), "#4A120B", deep, w=2.2, rim=1.4)
     + chunky(circ(12, 12, 2.4), "#4A120B", deep, w=1.8, rim=1.2, shadow=False))
icon("icon_music", chunky("M9 17.2V7.4l8.4-2.3v9.9", "#4A120B", deep, w=2, rim=1.2)
     + glossy(circ(6.9, 17.4, 2.7), "#E0604A", "#8A2418", "#4A120B", 1, shadow=(0, 0.7, 0.5, "#000", 0.3))
     + glossy(circ(15.3, 15.2, 2.7), "#E0604A", "#8A2418", "#4A120B", 1, shadow=(0, 0.7, 0.5, "#000", 0.3)))
SKULL = ("M12 3C7.6 3 4.5 6.1 4.5 10.2c0 2.4 1.1 4.3 2.8 5.5V18a1.5 1.5 0 0 0 1.5 1.5h6.4a1.5 1.5 0 0 0 1.5-1.5v-2.3"
         "c1.7-1.2 2.8-3.1 2.8-5.5C19.5 6.1 16.4 3 12 3z")
icon("icon_skull", glossy(SKULL, "#FFFFFF", "#B9BDCB", "#3E4256", 1, mid="#ECEEF4", shadow=(0, 0.7, 0.5, "#000", 0.3),
                          gloss_box=(6.5, 3.8, 9, 4), gloss=0.7)
     + p(circ(8.9, 11.1, 2.2), 'fill="#2C2F40"') + p(circ(15.1, 11.1, 2.2), 'fill="#2C2F40"')
     + p(circ(8.3, 10.5, 0.6), 'fill="#fff" fill-opacity="0.8"') + p(circ(14.5, 10.5, 0.6), 'fill="#fff" fill-opacity="0.8"')
     + p("M12 13.5l-1.1 1.9h2.2z", 'fill="#2C2F40"')
     + p("M10.3 17.3v2M12 17.3v2M13.7 17.3v2", 'stroke="#3E4256" stroke-width="0.9" stroke-linecap="round"'))


def star_path(cx, cy, outer, inner):
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = outer if i % 2 == 0 else inner
        pts.append(f"{cx + r * math.cos(a):.1f} {cy + r * math.sin(a):.1f}")
    return "M" + "L".join(pts) + "z"


# The win popup's hero: a chubby gold star with rounded tips, a darker lip, an inner bevel and a gloss; the ray glow that
# turns behind it, a sparkle and a trophy for the score.
if "--shared" in sys.argv:
    S = star_path(160, 158, 124, 64)
    body_g, body_gd = vgrad([(0, "#FFF29A", 1), (0.45, "#FFCB2E", 1), (1, "#F39A12", 1)])
    bevel_g, bevel_gd = vgrad([(0, "#FFFBD6", 0.95), (1, "#FFD24A", 0)])
    shine_g, shine_gd = vgrad([(0, "#fff", 0.85), (1, "#fff", 0)])
    sb, sbd = blur(7)
    soft, softd = blur(3)
    c, cd = clip(p(S, 'stroke="#000" stroke-width="18" stroke-linejoin="round"'))
    round_ = 'stroke-linejoin="round"'
    write("ui_star", (320, 320), f"<defs>{body_gd}{bevel_gd}{shine_gd}{sbd}{softd}{cd}</defs>"
          + p(S, f'fill="#7A3E04" fill-opacity="0.35" stroke="#7A3E04" stroke-opacity="0.35" stroke-width="30" {round_} transform="translate(0 16)" filter="url(#{sb})"')
          + p(S, f'fill="#B8620A" stroke="#B8620A" stroke-width="30" {round_} transform="translate(0 10)"')
          + p(S, f'fill="#A85E08" stroke="#A85E08" stroke-width="30" {round_}')
          + p(S, f'fill="url(#{body_g})" stroke="url(#{body_g})" stroke-width="18" {round_}')
          + p(star_path(160, 166, 86, 44), f'fill="url(#{bevel_g})" stroke="url(#{bevel_g})" stroke-width="14" {round_} filter="url(#{soft})"')
          + f'<g clip-path="url(#{c})"><ellipse cx="128" cy="92" rx="92" ry="44" transform="rotate(-18 128 92)" fill="url(#{shine_g})"/></g>'
          + p(circ(96, 104, 9), 'fill="#fff" fill-opacity="0.9"') + p(circ(214, 122, 5), 'fill="#fff" fill-opacity="0.7"'))

    rays = ""
    for i in range(16):
        a, half = math.radians(i * 22.5), math.radians(6.5 if i % 2 == 0 else 4)
        rays += p(f"M256 256L{256 + 300 * math.cos(a - half):.1f} {256 + 300 * math.sin(a - half):.1f}"
                  f"L{256 + 300 * math.cos(a + half):.1f} {256 + 300 * math.sin(a + half):.1f}z")
    write("ui_rays", (512, 512),
          '<defs><radialGradient id="rayfade" cx="256" cy="256" r="256" gradientUnits="userSpaceOnUse">'
          '<stop offset="0" stop-color="#FFF4B8" stop-opacity="1"/><stop offset="0.45" stop-color="#FFD84E" stop-opacity="0.75"/>'
          '<stop offset="1" stop-color="#FFC21E" stop-opacity="0"/></radialGradient>'
          '<radialGradient id="raycore" cx="256" cy="256" r="190" gradientUnits="userSpaceOnUse">'
          '<stop offset="0" stop-color="#FFF8D0" stop-opacity="0.95"/><stop offset="1" stop-color="#FFE27A" stop-opacity="0"/></radialGradient></defs>'
          + f'<g fill="url(#rayfade)">{rays}</g>' + p(circ(256, 256, 190), 'fill="url(#raycore)"'))

    glint = "M32 4Q35 29 60 32Q35 35 32 60Q29 35 4 32Q29 29 32 4z"
    gb, gbd = blur(3)
    write("ui_sparkle", (64, 64), f"<defs>{gbd}</defs>"
          + p(glint, f'fill="#FF9A10" stroke="#FF9A10" stroke-width="6" stroke-linejoin="round" filter="url(#{gb})"')
          + p(glint, 'fill="#FFFFFF" stroke="#FFB21E" stroke-width="2.5" stroke-linejoin="round"') + p(circ(32, 32, 5), 'fill="#FFF6C8"'))

    icon("icon_trophy", chunky("M7.4 6H4.6v1.6a3.4 3.4 0 0 0 3.4 3.4M16.6 6h2.8v1.6a3.4 3.4 0 0 1-3.4 3.4", gold[2],
                               [(0, gold[0], 1), (1, gold[1], 1)], w=1.8, rim=1.3)
         + glossy("M10.6 13.4h2.8v3.4h-2.8z", gold[0], gold[1], gold[2], 0.8)
         + glossy("M8 16.6h8a1 1 0 0 1 1 1v2.4H7v-2.4a1 1 0 0 1 1-1z", gold[0], gold[1], gold[2], 0.9, mid=gold[3],
                  shadow=(0, 0.7, 0.5, "#000", 0.3))
         + glossy("M6.6 3.6h10.8v4.6a5.4 5.4 0 0 1-10.8 0z", gold[0], gold[1], gold[2], 1, mid=gold[3], shadow=(0, 0.7, 0.5, "#000", 0.3),
                  gloss_box=(7.2, 3.8, 6, 4.6), gloss=0.8)
         + p("M12 5.4l.85 1.75 1.9.28-1.38 1.34.33 1.9L12 9.8l-1.7.87.33-1.9-1.38-1.34 1.9-.28z", 'fill="#FFF8C8" stroke="#A86A06" stroke-width="0.4"'))

    # Tile Match boosters: undo, magnet and an extra tray slot.
    blue = [(0, "#8CC6FF", 1), (1, "#2F7BF0", 1)]
    icon("icon_undo", chunky("M8.6 8.2h5.6a5 5 0 0 1 0 10H9", "#103C8E", blue, w=2.8, rim=1.6)
         + chunky("M11.6 4.9L8.3 8.2l3.3 3.3", "#103C8E", blue, w=2.8, rim=1.6, shadow=False))
    silver = [(0, "#FFFFFF", 1), (1, "#9AA6BA", 1)]
    icon("icon_magnet", chunky("M6.6 7.4v4.2a5.4 5.4 0 0 0 10.8 0V7.4", "#7A0E08", [(0, "#FF8A70", 1), (1, "#D9281E", 1)], w=4.4, rim=1.8)
         + chunky("M6.6 4.4v2.4M17.4 4.4v2.4", "#3E4656", silver, w=4.4, rim=1.8, shadow=False)
         + p("M10.6 15.2a2.6 2.6 0 0 0 2.8 0", 'fill="none" stroke="#fff" stroke-opacity="0.5" stroke-width="0.8" stroke-linecap="round"'))
    sd, sh = drop()
    slot = "M5 9.5h11a1.8 1.8 0 0 1 1.8 1.8v7.4a1.8 1.8 0 0 1-1.8 1.8H5a1.8 1.8 0 0 1-1.8-1.8v-7.4A1.8 1.8 0 0 1 5 9.5z"
    well_g, well_gd = vgrad([(0, "#7A4A22", 1), (1, "#B9814A", 1)])
    plus_g, plus_gd = rgrad([(0, "#9BEE7E", 1), (0.55, "#30B954", 1), (1, "#167A2E", 1)], cx=0.42, cy=0.32, r=0.72)
    icon("icon_slot", f"<defs>{sd}{well_gd}{plus_gd}</defs>" + p(slot, sh)
         + p(slot, f'fill="url(#{well_g})" stroke="#4E2A0E" stroke-width="1"')
         + rrect(5.4, 11.6, 10.2, 7.2, 1.2, 'fill="none" stroke="#FFF2DC" stroke-opacity="0.75" stroke-width="0.8" stroke-dasharray="1.6 1.2"')
         + p(circ(17.6, 7.4, 5), sh) + p(circ(17.6, 7.4, 5), f'fill="url(#{plus_g})" stroke="#0E5A20" stroke-width="0.9"')
         + p("M17.6 5v4.8M15.2 7.4H20", 'fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round"')
         + '<ellipse cx="16.6" cy="4.6" rx="2.4" ry="1" fill="#fff" fill-opacity="0.45"/>')

with open(os.path.join(OUT, "slices.json"), "w") as f:
    json.dump(slices, f, indent=1)
print(len(slices), "sprites")
