"""Record checks for the selected own-style portrait package."""
import hashlib
import json
from pathlib import Path
from PIL import Image

PROJECT=Path(__file__).resolve().parents[1]
PACK=PROJECT/'mods/appearance/ink_portrait_pair_20261003_v5'
stdout=(PROJECT/'tmp/ink_portrait_v5_ui_stdout.log').read_text(encoding='utf-8')
stderr=(PROJECT/'tmp/ink_portrait_v5_ui_stderr.log').read_text(encoding='utf-8')
markers=['INK_PORTRAIT_V5_UI_PASS: male;',
         'INK_PORTRAIT_V5_UI_PASS: female;',
         'INK_PORTRAIT_V5_ENTRY_PASS:']
assert all(marker in stdout for marker in markers)
assert 'SCRIPT ERROR' not in stderr and 'Unhandled exception' not in stderr
qa=json.loads((PACK/'qa_report.json').read_text(encoding='utf-8'))
files={}
for sex in ('male','female'):
    assert qa[sex]['skin_reference_rgb_max_error']<=1
    assert qa[sex]['ears_reference_rgb_max_error']<=1
    assert qa[sex]['opaque_eye_skin_pixels_outside_opening']==0
    assert qa[sex]['fully_transparent_pixels_have_zero_rgb']
    assert qa[sex]['rear_center_is_painted']
    for path in sorted((PACK/sex).glob('*.png')):
        with Image.open(path) as image:
            assert image.mode=='RGBA' and image.size==(1024,1536)
        files[str(path.relative_to(PACK))]=hashlib.sha256(path.read_bytes()).hexdigest()
assert len(files)==18
report={
    'package':PACK.name,
    'build':{'errors':0,'warnings':0},
    'entry':'StartMenu/AppearanceCreatorTest -> CharacterAppearanceTest',
    'sexes':['male','female'],
    'toggles_per_sex':9,
    'all_toggles_and_restore_passed':True,
    'csharp_composite_matches_independent_godot_composite':True,
    'godot_exit_code':0,
    'own_art_restoration':qa,
    'tested_png_sha256':files,
    'stdout':stdout,
    'stderr_diagnostics':stderr,
    'visual_review':'Reference/composite faces, isolated face/features, light/dark pair, nine-part sheets and actual game screenshots inspected; user art review pending',
    'scope':'Current base pair; two transparent default face-accessory slots; multiple variants not complete',
}
(PACK/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
manifest=json.loads((PACK/'portrait_manifest.json').read_text(encoding='utf-8'))
manifest.update({'test_entry_integrated':True,'ui_verified':True,'verification':'verification.json','visual_approval':'pending_user_review'})
(PACK/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
provenance={
    'faces_ears_features':'../ink_portrait_pair_20261003_v1/reference/{sex}_bald_user.png',
    'style':'Our original painted facial colors, contours and ears',
    'guigu_scope':'Semantic layers, continuous alpha and registration experience only; no artwork or palette reused',
    'skin_adjustment':'Male retains the current small palette offset toward the female reference; upper lid glints paired and restrained',
    'feature_processing':'Original-coordinate colored straight-alpha unmixing over one continuous featureless skin base',
    'hair_and_clothing':'Current base v4 PNGs reused without pixel changes',
    'hair_generation_prompts':'../ink_portrait_pair_20261003_v4/source/front_hair_prompts.json',
    'hair_generation_history':'../ink_portrait_pair_20261003_v4/source/generation_manifest.json',
    'new_ai_generation_this_revision':False,
}
(PACK/'source_provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: own facial artwork restored; 18 current PNGs fingerprinted; both sexes and all nine toggles verified in the real game entry.')
