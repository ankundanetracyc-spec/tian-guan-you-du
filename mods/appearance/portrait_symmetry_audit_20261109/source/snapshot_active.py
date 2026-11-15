"""Record the read-only portrait audit inventory; do not alter active assets."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[4]
PACK=Path(__file__).resolve().parents[1]
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004'
files=sorted(p for sex in ('male','female') for p in (ACTIVE/sex).rglob('*.png'))
files.extend([ACTIVE/'appearance_parts.json',ACTIVE/'portrait_manifest.json'])
records=[{'file':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]
assert len(records)==69
result={'schema':'active-portrait-readonly-audit-inventory/v1','active_png_count':67,
        'scope':'Current67 active PNGs and2 configs, not archived exploration images.',
        'files':records}
path=PACK/'source/active_inventory.json'
if path.exists():
    assert json.loads(path.read_text(encoding='utf-8-sig'))==result, 'Active files changed during audit'
else:
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('69 active asset/config hashes recorded; active files unchanged.')
