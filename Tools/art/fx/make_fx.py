"""Writes the shared effect sprites: white, softly shaded and transparent, so code can tint them per colour.

python3 -I make_fx.py <out>/svg && python3 -I ../render.py <out>/svg <out>/png <out>/sheet.png '#3A2A5C' 5 128
Copy png/*.png to Assets/Games/Shared/Art/Fx/.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from svgkit import blur, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


def write(name, text):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(text)


# A crystal shard for gems: three facets in light, mid and shade, with a bright edge.
write("fx_shard", svg(
    p("M30 4 58 26 40 60 8 44z", 'fill="#9A9A9A"')
    + p("M30 4 40 60 8 44z", 'fill="#FFFFFF"')
    + p("M30 4 58 26 40 60z", 'fill="#CFCFCF"')
    + p("M30 4 36 30 8 44z", 'fill="#FFFFFF" fill-opacity="0.9"')
    + p("M30 4 58 26 40 60 8 44z", 'fill="none" stroke="#FFFFFF" stroke-width="2.5" stroke-linejoin="round"'),
    "0 0 64 64", 64))

# A rounded chunk for cubes and blocks: a squashed glossy pebble.
chunk_g, chunk_gd = vgrad([(0, "#FFFFFF", 1), (1, "#B8B8B8", 1)])
write("fx_chunk", svg(
    f"<defs>{chunk_gd}</defs>"
    + p("M14 18C22 6 46 6 54 18 62 30 56 52 40 56 26 60 6 52 8 36 9 28 10 24 14 18z", 'fill="#8C8C8C"')
    + p("M14 14C22 3 46 3 54 14 62 26 56 47 40 51 26 55 6 47 8 32 9 24 10 20 14 14z", f'fill="url(#{chunk_g})"')
    + '<ellipse cx="28" cy="15" rx="11" ry="4.5" fill="#fff"/>',
    "0 0 64 64", 64))

# A plump cartoon star with a glow, for sparkles and win bursts.
glow, glow_d = blur(3)
star = "M32 4 40 22 60 24 45 37 50 57 32 47 14 57 19 37 4 24 24 22z"
star_g, star_gd = vgrad([(0, "#FFFFFF", 1), (1, "#D4D4D4", 1)])
write("fx_star", svg(
    f"<defs>{glow_d}{star_gd}</defs>"
    + p(star, f'fill="#fff" fill-opacity="0.7" filter="url(#{glow})"')
    + p(star, f'fill="url(#{star_g})" stroke="#fff" stroke-width="2" stroke-linejoin="round"')
    + '<ellipse cx="27" cy="25" rx="6" ry="3" fill="#fff" transform="rotate(-20 27 25)"/>',
    "0 0 64 64", 64))

# A soft smoke puff made of overlapping blurred circles.
puff, puff_d = blur(3.5)
shade, shade_d = rgrad([(0, "#FFFFFF", 1), (0.7, "#EDEDED", 1), (1, "#D0D0D0", 1)], cx=0.42, cy=0.38, r=0.62)
circles = [(40, 70, 24), (64, 52, 30), (90, 68, 24), (64, 82, 26), (48, 50, 20), (82, 48, 20)]
write("fx_smoke", svg(
    f"<defs>{puff_d}{shade_d}</defs>"
    + f'<g filter="url(#{puff})" fill="url(#{shade})">'
    + "".join(f'<circle cx="{x}" cy="{y}" r="{r}"/>' for x, y, r in circles)
    + "</g>",
    "0 0 128 128", 128))

# A soft glowing dot, for trails.
dot, dot_d = rgrad([(0, "#FFFFFF", 1), (0.35, "#FFFFFF", 0.85), (1, "#FFFFFF", 0)])
write("fx_dot", svg(f"<defs>{dot_d}</defs>" + f'<circle cx="32" cy="32" r="31" fill="url(#{dot})"/>', "0 0 64 64", 64))

# A beam for disco rays: drawn left to right, a hot core with a soft halo that fades at both ends; scaled along x.
halo, halo_d = vgrad([(0, "#FFFFFF", 0), (0.5, "#FFFFFF", 0.75), (1, "#FFFFFF", 0)])
core, core_d = vgrad([(0, "#FFFFFF", 0), (0.5, "#FFFFFF", 1), (1, "#FFFFFF", 0)])
ends, ends_d = vgrad([(0, "#000", 1), (0.06, "#fff", 1), (0.94, "#fff", 1), (1, "#000", 1)], x2=1, y2=0)
write("fx_beam", svg(
    f'<defs>{halo_d}{core_d}{ends_d}<mask id="m"><rect width="256" height="64" fill="url(#{ends})"/></mask></defs>'
    + '<g mask="url(#m)">'
    + f'<rect x="0" y="6" width="256" height="52" fill="url(#{halo})"/>'
    + f'<rect x="0" y="24" width="256" height="16" fill="url(#{core})"/>'
    + "</g>",
    "0 0 256 64", (256, 64)))

# Confetti: a paper rectangle, a curled ribbon and a dot. Light top edge so a tint keeps some shape.
paper, paper_d = vgrad([(0, "#FFFFFF", 1), (1, "#C9C9C9", 1)])
write("fx_confetti_a", svg(
    f"<defs>{paper_d}</defs>" + rrect(14, 22, 36, 20, 3, f'fill="url(#{paper})"') + rrect(14, 22, 36, 5, 2.5, 'fill="#fff"'),
    "0 0 64 64", 64))
ribbon, ribbon_d = vgrad([(0, "#FFFFFF", 1), (1, "#BDBDBD", 1)], x2=1, y2=0)
write("fx_confetti_b", svg(
    f"<defs>{ribbon_d}</defs>"
    + p("M10 40C18 18 28 18 32 32 36 46 46 46 54 24", f'fill="none" stroke="url(#{ribbon})" stroke-width="9" stroke-linecap="round"'),
    "0 0 64 64", 64))
disc, disc_d = rgrad([(0, "#FFFFFF", 1), (1, "#C4C4C4", 1)], cx=0.4, cy=0.35, r=0.65)
write("fx_confetti_c", svg(f"<defs>{disc_d}</defs>" + f'<circle cx="32" cy="32" r="14" fill="url(#{disc})"/>', "0 0 64 64", 64))

print(len(os.listdir(OUT)), "svgs")
