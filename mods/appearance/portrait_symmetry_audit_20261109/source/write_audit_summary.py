"""Summarize a read-only visual audit and arrange actual-game evidence."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[4]
PACK=Path(__file__).resolve().parents[1]
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004'
QA=PACK/'qa'
QA.mkdir(exist_ok=True)
sha=lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
runtime_path=PACK/'installation/runtime_check.json'
runtime=json.loads(runtime_path.read_text(encoding='utf-8-sig'))
assert runtime['pass'] and not runtime['failures']
inventory_path=PACK/'source/active_inventory.json'
inventory=json.loads(inventory_path.read_text(encoding='utf-8-sig'))
assert all(sha(ROOT/r['file'])==r['sha256'] for r in inventory['files'])
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
background=(232,226,214,255)
sheet=Image.new('RGBA',(660,365),background)
sources=[]
for position,(filename,label) in enumerate((
    ('female_bald_b02_e03_face_1x.png','无发检查：弯挑细眉＋舒展长目'),
    ('female_hair_01_eye_03_face_1x.png','带发检查：舒展长目'),
)):
    path=PACK/'installation'/filename
    actual=Image.open(path).convert('RGBA')
    assert actual.size==(330,325)
    sheet.alpha_composite(actual,(position*330,40))
    ImageDraw.Draw(sheet).text((position*330+7,10),label,font=font,fill=(42,48,44,255))
    sources.append({'path':str(path.resolve()),'sha256':sha(path),'resized':False})
sheet.convert('RGB').save(QA/'confirmed_female_issues_actual_1x.png')
summary={
    'schema':'active-portrait-symmetry-readonly-audit/v1',
    'scope':'Current male3eyes/3brows, female3eyes/4brows, face axes, nose/mouth and male5nonempty beards; current enabled catalog and runtime selections.',
    'active_assets_or_configuration_modified':False,
    'active_inventory_sha256':sha(inventory_path),
    'runtime_report_sha256':sha(runtime_path),
    'runtime_checks':runtime['checks'],
    'technical_results_are_not_aesthetic_approval':True,
    'confirmed_findings':[
        {'file':'female/eyes/03.png','name':'舒展长目','style_id':3,
         'severity':'clear_local_shape_difference',
         'observation':'Screen-left eye is rounder and exposes more iris; screen-right upper lid is flatter and outer half narrower. Repeats with all4brows and3hair choices in actual game output.',
         'source_sha256':sha(ACTIVE/'female/eyes/03.png'),
         'cause_scope':'Shape is in the current eye layer; it is not caused by hair occlusion or old-package fallback.'},
        {'file':'female/eyebrows/02.png','name':'弯挑细眉','style_id':1,
         'severity':'mild_visible_brow_imbalance',
         'observation':'Screen-right peak/tail sit higher and the outer half carries more solid pigment; one side reads more lifted at normal face size.',
         'source_sha256':sha(ACTIVE/'female/eyebrows/02.png'),
         'cause_scope':'Current brow shape itself; actual normal-scale bald combinations confirm it.'},
    ],
    'not_same_level_findings':[
        'Female eyes01/02 and brows01/03/04 have smaller local painted differences; they are not classified as equally obvious size mismatch.',
        'Female eyes02 are intentionally rounder/larger as a separate style; cross-style size is not bilateral asymmetry.',
        'Female eye center proxy is slightly right of the female head axis; no normal-size major whole-face offset confirmed. Female axis is approximately518..518.5, not male513.',
        'Small original cheek hairs in male beard06 remain visible without hair; no continuous hard skin seam or comparable imbalance found.',
    ],
    'negative_results':[
        'All6current male eye/brow PNGs match the selected mirror hashes and exact RGBA reflection aboutx513; actual3matching portraits use those files.',
        'Only the intended ink_three_variants package is enabled; numeric/full IDs and retirement aliases resolve to the expected current files.',
        'No comparable obvious male nose/mouth/beard center shift or new skin seam found in normal-size review.',
        'All67active PNGs and2active configs in the root inventory remain identical; runtime additionally verifies other scanned configs and existing user preset unchanged.',
    ],
    'delivery_image':'qa/confirmed_female_issues_actual_1x.png',
    'delivery_sources':sources,
    'status':'readonly_audit_complete_two_findings_open_no_replacements',
}
(QA/'audit_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'findings':2,'checks':runtime['checks'],'failures':runtime['failures'],'active_unchanged':True},ensure_ascii=False))
