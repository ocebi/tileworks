"""Writes the illustrated blast backdrops (a toy-block valley; Home sharp, Game blurred and dimmed)."""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from scenekit import CUBES, bush, cloud, hill, path_road, toy_cube, tower, tree  # noqa: E402
from svgkit import blur, circ, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
W, H = 1080, 2340
EDGE = "#2F8A2C"  # bottom edge colour, used as the camera clear colour

def scene():
    sky, skyd = vgrad([(0, "#3FA9EE", 1), (0.32, "#7FD0FF", 1), (0.5, "#BFE8FF", 1), (0.56, "#FFEFD2", 1)])
    sun, sund = rgrad([(0, "#FFF6D8", 1), (0.25, "#FFF0BF", 0.85), (1, "#FFF0BF", 0)], cx=0.5, cy=0.5, r=0.5)
    fade, faded = rgrad([(0, "#fff", 1), (0.5, "#fff", 0.4), (1, "#fff", 0)], cx=0.5, cy=640 / H, r=0.55)
    mount, mountd = vgrad([(0, "#C9D6F7", 1), (1, "#A9BCEB", 1)])
    haze, hazed = vgrad([(0, "#FFFFFF", 0), (1, "#FFFFFF", 0.55)])
    rays = ""
    for i in range(20):
        a1 = 2 * math.pi * i / 20
        a2 = a1 + math.pi / 20
        rays += f'<path d="M540 640L{540 + 2400 * math.cos(a1):.0f} {640 + 2400 * math.sin(a1):.0f}L{540 + 2400 * math.cos(a2):.0f} {640 + 2400 * math.sin(a2):.0f}z"/>'
    body = (f"<defs>{skyd}{sund}{fade.join(['', ''])}{faded}{mountd}{hazed}"
            f'<mask id="rays"><rect width="{W}" height="{H}" fill="url(#{fade})"/></mask></defs>'
            + f'<rect width="{W}" height="{H}" fill="url(#{sky})"/>'
            + f'<g mask="url(#rays)" fill="#FFFFFF" fill-opacity="0.13">{rays}</g>'
            + f'<circle cx="540" cy="640" r="430" fill="url(#{sun})"/>'
            + cloud(150, 330, 1.1) + cloud(930, 470, 1.25) + cloud(740, 190, 0.7, 0.9) + cloud(300, 900, 0.8, 0.85) + cloud(880, 1010, 0.6, 0.8))
    # Distant mountains and the horizon haze.
    body += p("M-20 1330L120 1150L230 1240L380 1080L520 1230L640 1120L790 1250L930 1110L1100 1290L1100 1400L-20 1400z",
              f'fill="url(#{mount})" stroke="#B2C3EE" stroke-width="4" stroke-linejoin="round"')
    body += p("M380 1080L340 1126L362 1120L380 1140L400 1118L420 1124z M930 1110L895 1150L915 1146L932 1162L950 1144L966 1150z",
              'fill="#FFFFFF" fill-opacity="0.9"')
    body += f'<rect y="1180" width="{W}" height="240" fill="url(#{haze})"/>'
    # Far hills with block towers, mid hills with trees, near hills with the path and flowers.
    body += hill([(-40, 1360), (180, 1300), (420, 1350), (680, 1290), (900, 1330), (1120, 1300)], "#A8E07C", "#7CC85A", "#D6F5B4", H + 20)
    body += tower(120, 1352, 70, ["blue", "yellow", "red"]) + tower(840, 1335, 62, ["green", "purple", "teal", "yellow"])
    body += toy_cube(560, 1290, 46, "red", -8) + toy_cube(612, 1300, 40, "blue", 10)
    body += hill([(-40, 1520), (220, 1440), (520, 1500), (800, 1430), (1120, 1480)], "#86D35F", "#4EA93C", "#C6F09A", H + 20)
    body += tree(90, 1478, 120) + tree(250, 1460, 95) + tree(870, 1450, 110) + tree(1010, 1468, 90) + bush(700, 1462, 70) + bush(400, 1488, 60)
    body += toy_cube(520, 1420, 64, "yellow", 6)
    body += hill([(-40, 1700), (260, 1620), (560, 1680), (860, 1600), (1120, 1650)], "#6CC64A", "#3E9E35", "#B9EC8C", H + 20)
    body += path_road([(560, H + 40), (470, 2050), (640, 1840), (560, 1690)], [420, 300, 170, 70])
    body += tree(70, 1740, 160) + tree(1000, 1700, 150) + bush(230, 1770, 110) + bush(860, 1800, 90)
    body += tower(150, 2230, 120, ["purple", "red"]) + tower(860, 2260, 110, ["teal", "green", "yellow"])
    rng = random.Random(7)
    flowers = ""
    for _ in range(46):
        x, y = rng.uniform(20, 1060), rng.uniform(1760, 2320)
        if abs(x - 560 - (y - 2000) * -0.2) < 200:
            continue
        colour = rng.choice(["#FFFFFF", "#FFD43B", "#FF7A8F", "#C9A4FF"])
        r = rng.uniform(6, 11)
        flowers += p(circ(x, y, r), f'fill="{colour}" stroke="#2E7A2A" stroke-width="2"') + p(circ(x, y, r * 0.35), 'fill="#FFB02E"')
    body += flowers
    return body


home = scene()
with open(os.path.join(OUT, "bg_blast_home.svg"), "w") as f:
    f.write(svg(home, f"0 0 {W} {H}", (W, H)))
b, bd = blur(7)
dim, dimd = vgrad([(0, "#0B1A3A", 0.22), (1, "#0B2A10", 0.35)])
with open(os.path.join(OUT, "bg_blast_game.svg"), "w") as f:
    f.write(svg(f"<defs>{bd}{dimd}</defs><g filter=\"url(#{b})\">{home}</g>" + f'<rect width="{W}" height="{H}" fill="url(#{dim})"/>',
                f"0 0 {W} {H}", (W, H)))
print("scene svgs")
