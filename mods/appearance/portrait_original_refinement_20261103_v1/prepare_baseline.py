"""Copy provenance-preserving inputs; no image editing or active asset writes."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
ACTIVE = ROOT / 'mods/appearance/ink_three_variants_20261004'
PARTS = ['hair_back', 'face_shape', 'clothing', 'eyes', 'eyebrows', 'nose', 'mouth', 'face_accessory', 'hair_front']
records = []
sha = lambda b: hashlib.sha256(b).hexdigest()
snapshot = {str(p.relative_to(ACTIVE)): sha(p.read_bytes()) for p in ACTIVE.rglob('*') if p.is_file() and p.suffix in ('.png', '.json')}
(OUT / 'qa').mkdir(parents=True, exist_ok=True)
(OUT / 'qa/active_before.json').write_text(json.dumps(snapshot, indent=2), encoding='utf-8')
backup_path = ROOT / 'mods/appearance/portrait_rebuild_20261103/three_style_parts/installation/before_install.zip'
with zipfile.ZipFile(backup_path) as backup:
    for gender in ('male', 'female'):
        for part in PARTS:
            target = OUT / 'baseline' / gender / (part + '.png')
            target.parent.mkdir(parents=True, exist_ok=True)
            if gender == 'female' and part in ('hair_front', 'hair_back', 'eyebrows', 'eyes', 'nose', 'mouth'):
                index = '02' if part == 'eyes' else '01'
                member = f'female/{part}/{index}.png'
                data = backup.read(member)
                source = str(backup_path.relative_to(ROOT)) + '::' + member
            else:
                index = '04' if gender == 'female' and part == 'face_shape' else '01'
                path = ACTIVE / gender / part / (index + '.png')
                data = path.read_bytes()
                source = str(path.relative_to(ROOT))
            target.write_bytes(data)
            records.append({'gender': gender, 'part': part, 'source': source, 'copy': str(target.relative_to(OUT)), 'sha256': sha(data)})
        for part in PARTS:
            if part in ('hair_front', 'hair_back'):
                continue
            dest = OUT / 'parts' / gender / (part + '.png')
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(OUT / 'baseline' / gender / (part + '.png'), dest)
(OUT / 'source_records.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'copied_baseline': len(records), 'retained_nongenerated_parts': 14, 'active_hashes': len(snapshot), 'root': str(ROOT)}))
