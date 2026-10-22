"""Writes the Tile Match tile base, overlays and tray as glossy SVGs. Sizes match TileArtBuilder: the face is 288 x 320 (top face
288 px plus a 32 px side), overlays are 288 px square, the tray is a 128 px 9-slice with 40 px borders."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from svgkit import blur, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


def write(name, body, w, h):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, f"0 0 {w} {h}", (w, h)))


# Tile: a cream ceramic face on a caramel side, bevelled and lightly glossed.
side, side_d = vgrad([(0, "#EBCF9F", 1), (1, "#C29360", 1)])
face, face_d = vgrad([(0, "#FFFFFF", 1), (0.55, "#FFF8EA", 1), (1, "#F3E2C2", 1)])
centre, centre_d = rgrad([(0, "#FFFFFF", 1), (0.7, "#FFFBF2", 1), (1, "#FFFBF2", 0)], cx=0.5, cy=0.42, r=0.6)
gloss, gloss_d = vgrad([(0, "#FFFFFF", 0.9), (1, "#FFFFFF", 0)])
soft, soft_d = blur(6)
c, cd = clip(rrect(4, 4, 280, 280, 44))
write("tile_face",
      f"<defs>{side_d}{face_d}{centre_d}{gloss_d}{soft_d}{cd}</defs>"
      + rrect(4, 34, 280, 282, 44, f'fill="url(#{side})" stroke="#9A6A38" stroke-width="4"')
      + rrect(4, 4, 280, 280, 44, f'fill="url(#{face})"')
      + f'<g clip-path="url(#{c})">' + rrect(22, 20, 244, 236, 34, f'fill="url(#{centre})" filter="url(#{soft})"')
      + rrect(4, 4, 280, 280, 44, 'fill="none" stroke="#FFFFFF" stroke-width="10" stroke-opacity="0.9" transform="translate(0 4)"') + "</g>"
      + rrect(48, 12, 192, 30, 15, f'fill="url(#{gloss})"')
      + rrect(4, 4, 280, 280, 44, 'fill="none" stroke="#B98A52" stroke-width="4"'),
      288, 320)

sh, sh_d = blur(9)
write("tile_shadow", f"<defs>{sh_d}</defs>" + rrect(14, 22, 260, 284, 48, f'fill="#1A2A10" fill-opacity="0.38" filter="url(#{sh})"'), 288, 320)

# Hidden tile: a glossy blue back with a soft diamond pattern and a star seal.
back, back_d = vgrad([(0, "#8CC0FF", 1), (0.5, "#4C86E8", 1), (1, "#2F5FC4", 1)])
seal, seal_d = rgrad([(0, "#FFFFFF", 0.95), (1, "#D6E6FF", 0.9)], cx=0.4, cy=0.35, r=0.7)
c, cd = clip(rrect(18, 18, 252, 252, 32))
pattern = "".join(p(f"M{x} {y}l22 22-22 22-22-22z", 'fill="#FFFFFF" fill-opacity="0.09"')
                  for x in range(18, 290, 44) for y in range(-4, 290, 44))
STAR = "M144 96l13.6 27.5 30.4 4.4-22 21.4 5.2 30.3L144 165.3l-27.2 14.3 5.2-30.3-22-21.4 30.4-4.4z"
write("tile_hidden",
      f"<defs>{back_d}{seal_d}{gloss_d}{cd}</defs>"
      + rrect(18, 18, 252, 252, 32, f'fill="url(#{back})"')
      + f'<g clip-path="url(#{c})">{pattern}</g>'
      + p(STAR, f'fill="url(#{seal})" stroke="#2A55B0" stroke-width="5" stroke-linejoin="round"')
      + rrect(52, 26, 184, 26, 13, f'fill="url(#{gloss})"')
      + rrect(18, 18, 252, 252, 32, 'fill="none" stroke="#1E4AA8" stroke-width="6"'),
      288, 288)


def ice(stage):
    body, body_d = vgrad([(0, "#E6F6FF", 0.75), (0.5, "#A8DCFF", 0.5), (1, "#7CC4F5", 0.6)])
    edge, edge_d = blur(2)
    cracks = ["M60 40L118 120L96 170L150 230", "M118 120L190 108L238 150"]
    if stage > 1:
        cracks += ["M96 170L40 214", "M190 108L210 40", "M150 230L230 262"]
    lines = "".join(p(d, 'fill="none" stroke="#FFFFFF" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"')
                    + p(d, 'fill="none" stroke="#5FA8DC" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" transform="translate(2 3)"')
                    for d in cracks)
    return (f"<defs>{body_d}{edge_d}</defs>"
            + rrect(6, 6, 276, 276, 42, f'fill="url(#{body})"')
            + p("M40 70L90 30H130L50 120z M210 250L250 200V240L230 260z", 'fill="#FFFFFF" fill-opacity="0.55"')
            + lines
            + rrect(6, 6, 276, 276, 42, f'fill="none" stroke="#FFFFFF" stroke-width="10" stroke-opacity="0.85" filter="url(#{edge})"')
            + rrect(6, 6, 276, 276, 42, 'fill="none" stroke="#6DB4E8" stroke-width="3"'))


write("tile_ice_1", ice(1), 288, 288)
write("tile_ice_2", ice(2), 288, 288)

# Chain: two steel links in the top-right corner.
steel, steel_d = vgrad([(0, "#FFFFFF", 1), (0.5, "#C2CBD8", 1), (1, "#7E8AA0", 1)])
link_sh, link_sh_d = blur(3)
links = ""
for cx, cy, rot in ((228, 56, -40), (258, 86, 50)):
    shape = f'<ellipse cx="{cx}" cy="{cy}" rx="32" ry="20" transform="rotate({rot} {cx} {cy})"'
    links += (f'<g transform="translate(0 4)" filter="url(#{link_sh})">' + shape + ' fill="none" stroke="#000" stroke-opacity="0.3" stroke-width="18"/></g>'
              + shape + ' fill="none" stroke="#3E4656" stroke-width="18"/>'
              + shape + f' fill="none" stroke="url(#{steel})" stroke-width="11"/>')
write("tile_link", f"<defs>{steel_d}{link_sh_d}</defs>{links}", 288, 288)

# Tray: a varnished wooden dock board; the stretched centre stays flat.
wood, wood_d = vgrad([(0, "#D9A060", 1), (0.5, "#B87638", 1), (1, "#8E5629", 1)])
well, well_d = vgrad([(0, "#5E3416", 1), (0.35, "#734420", 1), (0.65, "#734420", 1), (1, "#7E4C24", 1)])
ins, ins_d = blur(4)
c, cd = clip(rrect(12, 12, 104, 102, 22))
write("tray",
      f"<defs>{wood_d}{well_d}{ins_d}{cd}</defs>"
      + rrect(2, 4, 124, 122, 30, 'fill="#4A2810"')
      + rrect(2, 2, 124, 120, 30, f'fill="url(#{wood})"')
      + rrect(4, 4, 120, 116, 28, 'fill="none" stroke="#FFFFFF" stroke-opacity="0.4" stroke-width="3"')
      + rrect(12, 12, 104, 102, 22, f'fill="url(#{well})"')
      + f'<g clip-path="url(#{c})">' + rrect(0, 2, 128, 16, 8, f'fill="#000" fill-opacity="0.45" filter="url(#{ins})"') + "</g>"
      + rrect(12, 12, 104, 102, 22, 'fill="none" stroke="#3E200C" stroke-width="2"')
      + rrect(2, 2, 124, 120, 30, 'fill="none" stroke="#4A2810" stroke-width="3"'),
      128, 128)
print("tile svgs")
