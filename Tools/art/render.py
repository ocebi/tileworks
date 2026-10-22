"""render.py <svg_dir> <png_dir> <sheet.png> <bg> [cols] [cell] : renders every SVG at its own size with headless Chrome, then a contact sheet."""
import os, re, subprocess, sys, tempfile

src, dst, sheet, bg = sys.argv[1:5]
cols = int(sys.argv[5]) if len(sys.argv) > 5 else 9
cell = int(sys.argv[6]) if len(sys.argv) > 6 else 128
chrome = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
profile = tempfile.mkdtemp(prefix="chrome-art-")
os.makedirs(dst, exist_ok=True)
base = [chrome, "--headless=new", "--disable-gpu", "--no-first-run", f"--user-data-dir={profile}", "--hide-scrollbars", "--force-device-scale-factor=1"]
names = sorted(f[:-4] for f in os.listdir(src) if f.endswith(".svg"))
for n in names:
    head = open(os.path.join(src, n + ".svg")).read(400)
    w, h = (int(float(x)) for x in re.search(r'width="([\d.]+)" height="([\d.]+)"', head).groups())
    subprocess.run(base + ["--default-background-color=00000000", f"--window-size={w},{h}", f"--screenshot={dst}/{n}.png", f"file://{src}/{n}.svg"], capture_output=True)
figs = "".join(f'<figure><img src="file://{dst}/{n}.png"><figcaption>{n}</figcaption></figure>' for n in names)
rows = (len(names) + cols - 1) // cols
html = os.path.join(os.path.dirname(sheet), os.path.basename(sheet)[:-4] + ".html")
open(html, "w").write(f'''<!doctype html><html><body style="margin:0;background:{bg};font:11px sans-serif;color:#fff">
<div style="display:grid;grid-template-columns:repeat({cols},{cell + 4}px);gap:6px;padding:8px">{figs}</div>
<style>figure{{margin:0;text-align:center}}img{{max-width:{cell}px;max-height:{cell}px}}</style></body></html>''')
subprocess.run(base + [f"--window-size={cols * (cell + 10) + 16},{rows * (cell + 26) + 16}", f"--screenshot={sheet}", f"file://{html}"], capture_output=True)
print(len(names), "rendered")
