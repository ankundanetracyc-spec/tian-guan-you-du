import hashlib
import json
import shutil
import sys
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
doc = root / 'docs/美术规范/百川地图融合修订_20261028'
out = root / 'textures/world/bai_chuan/nine_v02'
out.mkdir(parents=True, exist_ok=True)
log_path = doc / '生成记录.json'
records = json.loads(log_path.read_text(encoding='utf-8')) if log_path.exists() else {}
key, source = sys.argv[1:3]
target = out / ('places_v02.png' if key == 'places' else f'bc_{key}.png')
shutil.copy2(source, target)
with Image.open(target) as im:
    size, mode = list(im.size), im.mode
records[key] = {'path': str(target.relative_to(root)), 'source': source, 'nativeSize': size, 'mode': mode, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
log_path.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
print(key, size, mode)
