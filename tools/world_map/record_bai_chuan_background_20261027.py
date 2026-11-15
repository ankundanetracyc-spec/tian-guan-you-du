import json
import shutil
import sys
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
manifest = root / 'docs/美术规范/百川九张底图_20261027/制作清单.json'
data = json.loads(manifest.read_text(encoding='utf-8'))
key, source = sys.argv[1:3]
job = next(j for j in data['jobs'] if j['key'] == key)
source = Path(source)
target = Path(job['destination'])
shutil.copy2(source, target)
with Image.open(target) as im:
    job['nativeSize'] = list(im.size)
job.update(status='generated-v01', source=str(source), generation='native imagegen; original preserved; no resampling')
manifest.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
print(key, job['nativeSize'])
