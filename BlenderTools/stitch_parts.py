# Contact sheets of the part renders (previews/parts/*.png), one per prefix: stitch_parts.py AK_Muzzle AK_Grip ...
import sys, os, glob
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
for pre in sys.argv[1:]:
    fs = sorted(glob.glob(os.path.join(HERE, "previews", "parts", pre + "*.png")))
    if not fs: continue
    cols = 4; rows = (len(fs) + cols - 1) // cols
    W, H = 640, 360
    sheet = Image.new("RGB", (cols * W, rows * H), (40, 40, 40))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(fs):
        im = Image.open(f).convert("RGB")
        x, y = (i % cols) * W, (i // cols) * H
        sheet.paste(im, (x, y))
        d.text((x + 8, y + 8), os.path.basename(f)[:-4], fill=(255, 255, 120))
    sheet.save(os.path.join(HERE, "previews", "sheet_" + pre + ".png"))
    print(pre, len(fs))
