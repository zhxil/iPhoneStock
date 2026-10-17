"""Record transparent padding bounds; leaves official product images unchanged."""
import json
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[1]/'native/Stock.WinUI/Assets/Products'
p=root/'index.json'
entries=json.loads(p.read_text(encoding='utf-8-sig'))
info={}
for e in entries:
    if e['file'] not in info:
        with Image.open(root/e['file']) as im:
            info[e['file']]={'width':im.width,'height':im.height,'bounds':list(im.getbbox())}
    e.update(info[e['file']])
p.write_text(json.dumps(entries,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(info)} images, {len(entries)} SKU mappings')
