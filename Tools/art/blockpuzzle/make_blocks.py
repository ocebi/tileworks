"""Writes the Block Puzzle block, cell, gem and frame as SVGs at 128 px. Block, cell and frame are greyscale because the skin
tints them; the gem keeps its own colours. The frame is a 9-slice with 48 px borders."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from svgkit import blur, clip, p, rgrad, rrect, svg, vgrad  # noqa: E402

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


def write(name, body, size=128):
    with open(os.path.join(OUT, name + ".svg"), "w") as f:
        f.write(svg(body, f"0 0 {size} {size}", (size, size)))


# Block: lit top face, darker lip, a bright gloss; multiplied by the piece colour it reads as a glossy jelly block.
body, body_d = vgrad([(0, "#EDEDED", 1), (0.5, "#CFCFCF", 1), (1, "#9C9C9C", 1)])
face, face_d = rgrad([(0, "#F2F2F2", 1), (0.7, "#DADADA", 1), (1, "#DADADA", 0)], cx=0.5, cy=0.4, r=0.6)
soft, soft_d = blur(3)
c, cd = clip(rrect(5, 4, 118, 112, 24))
write("block",
      f"<defs>{body_d}{face_d}{soft_d}{cd}</defs>"
      + rrect(5, 10, 118, 114, 24, 'fill="#646464"')
      + rrect(5, 4, 118, 112, 24, f'fill="url(#{body})"')
      + f'<g clip-path="url(#{c})">' + rrect(18, 14, 92, 84, 18, f'fill="url(#{face})" filter="url(#{soft})"')
      + rrect(5, 4, 118, 112, 24, 'fill="none" stroke="#FFFFFF" stroke-width="6" transform="translate(0 3)"') + "</g>"
      + '<ellipse cx="54" cy="22" rx="32" ry="9" transform="rotate(-6 54 22)" fill="#FFFFFF"/>'
      + rrect(5, 4, 118, 112, 24, 'fill="none" stroke="#6A6A6A" stroke-width="2.5"'))

# Cell: a recessed slot, shaded from the top and lit along the bottom edge.
cell, cell_d = vgrad([(0, "#9A9A9A", 1), (1, "#D2D2D2", 1)])
ins, ins_d = blur(4)
c, cd = clip(rrect(5, 5, 118, 118, 22))
write("cell",
      f"<defs>{cell_d}{ins_d}{cd}</defs>"
      + rrect(5, 5, 118, 118, 22, f'fill="url(#{cell})"')
      + f'<g clip-path="url(#{c})">' + rrect(-4, -8, 136, 26, 12, f'fill="#000" fill-opacity="0.35" filter="url(#{ins})"') + "</g>"
      + rrect(5, 5, 118, 118, 22, 'fill="none" stroke="#FFFFFF" stroke-opacity="0.35" stroke-width="2" transform="translate(0 1)"'))

# Gem: a faceted aqua crystal with a sparkle.
facets = [
    ("M64 10L104 50H24z", "#B8F4FF"), ("M24 50H104L64 118z", "#3FB7E8"), ("M64 10L84 50H44z", "#E6FCFF"),
    ("M44 50H84L64 118z", "#6FD4F5"), ("M24 50L44 50L64 118z", "#2A92CC"), ("M104 50L84 50L64 118z", "#1F7DB8"),
]
sh, sh_d = blur(4)
gem = f"<defs>{sh_d}</defs>" + p("M64 14L106 54L64 124L22 54z", f'fill="#000" fill-opacity="0.3" filter="url(#{sh})"')
gem += "".join(p(d, f'fill="{colour}" stroke="#14608F" stroke-width="2" stroke-linejoin="round"') for d, colour in facets)
gem += p("M64 10L104 50L64 118L24 50z", 'fill="none" stroke="#0E4E78" stroke-width="4" stroke-linejoin="round"')
gem += p("M50 26l3 7 7 3-7 3-3 7-3-7-7-3 7-3z", 'fill="#FFFFFF"')
write("gem", gem)

# Frame: a rounded board surround with a lit rim and an inner shadow; tinted by the skin.
fill, fill_d = vgrad([(0, "#C8C8C8", 1), (0.3, "#E0E0E0", 1), (0.7, "#E0E0E0", 1), (1, "#D0D0D0", 1)])
ins, ins_d = blur(5)
c, cd = clip(rrect(14, 14, 100, 100, 26))
write("frame",
      f"<defs>{fill_d}{ins_d}{cd}</defs>"
      + rrect(2, 2, 124, 124, 40, 'fill="#FFFFFF"')
      + rrect(14, 14, 100, 100, 26, f'fill="url(#{fill})"')
      + f'<g clip-path="url(#{c})">' + rrect(0, 2, 128, 22, 10, f'fill="#000" fill-opacity="0.3" filter="url(#{ins})"') + "</g>"
      + rrect(2, 2, 124, 124, 40, 'fill="none" stroke="#9A9A9A" stroke-width="3"'))
print("block svgs")
