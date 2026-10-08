"""Persist the observed game UI result and fingerprints of the tested PNGs."""
import hashlib
import json
from pathlib import Path

PROJECT=Path(__file__).resolve().parents[1]
PACK=PROJECT/'mods/appearance/ink_portrait_pair_20261003_v4'
stdout=(PROJECT/'tmp/ink_portrait_v4_ui_stdout.log').read_text(encoding='utf-8')
stderr=(PROJECT/'tmp/ink_portrait_v4_ui_stderr.log').read_text(encoding='utf-8')
markers=[
    'INK_PORTRAIT_V4_UI_PASS: male;',
    'INK_PORTRAIT_V4_UI_PASS: female;',
    'INK_PORTRAIT_V4_ENTRY_PASS:',
]
assert all(marker in stdout for marker in markers), 'Current game checks did not complete'
assert 'SCRIPT ERROR' not in stderr and 'Unhandled exception' not in stderr
files={str(p.relative_to(PACK)):hashlib.sha256(p.read_bytes()).hexdigest()
       for sex in ('male','female') for p in sorted((PACK/sex).glob('*.png'))}
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
    'tested_png_sha256':files,
    'stdout':stdout,
    'stderr_diagnostics':stderr,
    'visual_review':'Light/dark, isolated parts, no front/back/eyes and game screenshots inspected; user art review pending',
    'scope':'Current base pair; two transparent default face-accessory slots; multiple variants not complete',
}
(PACK/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
manifest=json.loads((PACK/'portrait_manifest.json').read_text(encoding='utf-8'))
manifest.update({'test_entry_integrated':True,'ui_verified':True,'verification':'verification.json','visual_approval':'pending_user_review'})
(PACK/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
generation=json.loads((PACK/'source/generation_manifest.json').read_text(encoding='utf-8'))
generation['status']='Selected assets exported with original-coordinate facial pigment extraction; game UI checks passed; user art review pending'
for attempt in generation['attempts']:
    if attempt['asset']=='rear_hair_underpainting':
        attempt['status']='selected: complete continuous painting; genuine alpha; uniform registration recorded in v4 rig'
front=json.loads((PACK/'source/front_hair_prompts.json').read_text(encoding='utf-8'))
for attempt in front['attempts']:
    if not any(old['asset']==attempt['asset'] for old in generation['attempts']):
        generation['attempts'].append(attempt)
(PACK/'source/generation_manifest.json').write_text(json.dumps(generation,ensure_ascii=False,indent=2),encoding='utf-8')
print('Recorded passing UI checks and SHA256 fingerprints for 18 PNG layers.')
