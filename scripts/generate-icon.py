"""Render the repository's simple phone mark into Windows icon sizes."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
assets = root / "native/Stock.WinUI/Assets"
scale = 4
image = Image.new("RGBA", (256 * scale, 256 * scale))
draw = ImageDraw.Draw(image)

def rect(box, radius, color):
    draw.rounded_rectangle(tuple(round(v * scale) for v in box), radius=round(radius * scale), fill=color)

rect((8, 8, 248, 248), 56, "#1765D1")
rect((76, 38, 180, 218), 22, "white")
rect((87, 51, 169, 193), 13, "#1765D1")
rect((109, 55, 147, 62), 3.5, "white")
rect((111, 202, 145, 207), 2.5, "#1765D1")
image = image.resize((256, 256), Image.Resampling.LANCZOS)
image.save(assets / "AppIcon.png")
image.save(assets / "AppIcon.ico", sizes=[(s, s) for s in [16, 20, 24, 32, 40, 48, 64, 128, 256]])
