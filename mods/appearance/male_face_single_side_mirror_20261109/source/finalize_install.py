"""Bind current installed mirror assets to their actual runtime verification."""
from install_mirrored_features import ACTIVE, META, PACK, ROOT, encoded, read, replace, sha, write

runtime_path=PACK/'installation/runtime_check.json'
runtime=read(runtime_path)
assert runtime['pass'] and not runtime['failures']
assert runtime['mirrored_png_count']==6 and runtime['retained_pngs']==61
assert runtime['active_png_count']==67 and runtime['active_choice_count']==60
assert runtime['user_preset_unchanged']
geometry=runtime['mirror_geometry']
assert len(geometry)==6
for row in geometry:
    assert row['different_RGBA_pixels']==0 and row['source_half_RGBA_unchanged']
    assert row['source_center_column_RGBA_unchanged']
spec=read(PACK/'source/selected_features.json')
selected={r['candidate']:r for r in spec['records']}
for record in read(PACK/'source/baseline.json')['files']:
    path=ROOT/record['file']
    relative=path.relative_to(ACTIVE).as_posix()
    if relative=='portrait_manifest.json':continue
    expected=selected[relative]['candidate_sha256'] if relative in selected else record['sha256']
    assert sha(path)==expected, f'Changed after runtime: {relative}'
manifest_path=ACTIVE/'portrait_manifest.json'
before=sha(manifest_path)
manifest=read(manifest_path)
entry=manifest[META]
entry['runtime_status']='actual_loading_and_full_rgba_mirror_verified'
entry['runtime_report_sha256']=sha(runtime_path)
entry['geometry_status']='all_six_exact_reflections_about_axis513'
entry['aesthetic_status']='original_paint_preserved_and_actual_face_reviewed_user_final_review_pending'
replace(manifest_path,encoded(manifest),before)
report_path=PACK/'installation/install_report.json'
report=read(report_path)
report.update({
    'status':'installed_single_side_mirror_runtime_verified',
    'runtime_status':entry['runtime_status'],
    'runtime_report_sha256':sha(runtime_path),
    'geometry_status':entry['geometry_status'],
    'after_manifest_sha256':sha(manifest_path),
})
write(report_path,report)
print('Six exact mirrored eye/brow layers installed and runtime verified; other61 PNGs/config/preset retained.')
