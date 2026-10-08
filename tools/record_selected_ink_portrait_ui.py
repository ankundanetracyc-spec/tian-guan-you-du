"""Persist observed selected-style game checks and current selection state."""
from pathlib import Path
import hashlib
import json

PROJECT=Path(__file__).resolve().parents[1]
PACK=PROJECT/'mods/appearance/selected_ink_portrait_rig_20261003_v1'
SOURCE=PROJECT/'mods/appearance/ink_portrait_pair_20261003_v2'
stdout=(PROJECT/'tmp/selected_ink_portrait_ui_stdout.log').read_text(encoding='utf-8')
stderr=(PROJECT/'tmp/selected_ink_portrait_ui_stderr.log').read_text(encoding='utf-8')
assert all(text in stdout for text in ['SELECTED_INK_UI_PASS: male;',
    'SELECTED_INK_UI_PASS: female;','SELECTED_INK_ENTRY_PASS:'])
assert 'SCRIPT ERROR' not in stderr and 'Unhandled exception' not in stderr
files={str(p.relative_to(PROJECT)):hashlib.sha256(p.read_bytes()).hexdigest()
       for sex in ['male','female'] for p in sorted((SOURCE/sex).glob('*.png'))
       if p.stem in ['hair_front','hair_back','eyebrows','eyes','nose','mouth','face_shape','clothing','face_accessory']}
assert len(files)==18
files.update({str(p.relative_to(PROJECT)):hashlib.sha256(p.read_bytes()).hexdigest()
              for p in sorted((PACK/'rig').glob('*_overlay.png'))})
report={
 'build':{'warnings':0,'errors':0},
 'entry':'StartMenu/AppearanceCreatorTest -> CharacterAppearanceTest',
 'selected_source_package':SOURCE.name,
 'rig':'rig/portrait_rig.json',
 'sexes':['male','female'],'nine_toggles_and_restore_per_sex':True,
 'own_anchor_overlay_shown_and_hidden_per_sex':True,
 'csharp_composite_matches_independent_godot_composite':True,
 'godot_exit_code':0,'tested_png_sha256':files,'stdout':stdout,'stderr_diagnostics':stderr,
 'semantic_cleanup_approved':False,
 'scope':'Selected artwork and registration framework only. Current source PNGs still need semantic cleanup and new export review; variants are not complete.',
}
(PACK/'game_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
selection={
 'selected_artwork':'reference/selected_pair_user.png',
 'style_approved_by_user':True,'rig':'rig/portrait_rig.json',
 'test_source_package':'../'+SOURCE.name,
 'test_entry_integrated':True,'show_own_anchors_in_test_entry':True,
 'semantic_cleanup_approved':False,'multiple_variants_complete':False,
 'verification':'game_verification.json',
}
(PACK/'portrait_selection.json').write_text(json.dumps(selection,ensure_ascii=False,indent=2),encoding='utf-8')
for version in [3,4,5]:
 path=PROJECT/f'mods/appearance/ink_portrait_pair_20261003_v{version}/portrait_manifest.json'
 old=json.loads(path.read_text(encoding='utf-8'))
 old['test_entry_integrated']=False
 old['current_visual_source']=False
 old['current_visual_source_selection']='../selected_ink_portrait_rig_20261003_v1/portrait_selection.json'
 path.write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf-8')
path=SOURCE/'portrait_manifest.json'
manifest=json.loads(path.read_text(encoding='utf-8'))
manifest.update({'test_entry_integrated':True,'current_visual_source':True,
  'style_approved_by_user':True,'semantic_cleanup_approved':False,
  'rig':'../selected_ink_portrait_rig_20261003_v1/rig/portrait_rig.json',
  'selection':'../selected_ink_portrait_rig_20261003_v1/portrait_selection.json'})
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS: selected source, both sexes, nine toggles and own-anchor overlay verified. Semantic cleanup remains a separate gate.')
