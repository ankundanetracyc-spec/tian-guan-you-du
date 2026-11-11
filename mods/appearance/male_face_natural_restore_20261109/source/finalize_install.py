"""Record successful scoped runtime checks without declaring aesthetic approval."""
import hashlib
import json
from pathlib import Path

from install_natural_restore import ACTIVE, META, PACK, ROOT, read, replace, sha, write_json

runtime_path = PACK/'installation/runtime_check.json'
runtime = read(runtime_path)
assert runtime['pass'] and not runtime['failures']
assert runtime['restored_png_count'] == 6 and runtime['retained_pngs'] == 61
assert runtime['active_png_count'] == 67 and runtime['active_choice_count'] == 60
assert runtime['user_preset_unchanged']
spec = read(PACK/'source/natural_restore_candidates.json')
restored = {r['candidate']: r for r in spec['records']}
baseline = read(PACK/'source/baseline.json')
for record in baseline['files']:
    path = ROOT/record['file']
    relative = path.relative_to(ACTIVE).as_posix()
    if relative == 'portrait_manifest.json':
        continue
    expected = restored[relative]['candidate_sha256'] if relative in restored else record['sha256']
    assert sha(path) == expected, f'Changed since runtime verification: {relative}'

manifest_path = ACTIVE/'portrait_manifest.json'
manifest_sha = sha(manifest_path)
manifest = read(manifest_path)
entry = manifest[META]
entry['runtime_status'] = 'technical_loading_and_preservation_verified'
entry['runtime_report_sha256'] = sha(runtime_path)
entry['aesthetic_status'] = 'original_brushwork_recovered_local_shape_differences_pending'
entry['user_approval'] = False
entry['geometry_status'] = 'whole_pair_centered_to_nearest_pixel_not_full_bilateral_repair'
encoded = (json.dumps(manifest, ensure_ascii=False, indent=2)+'\n').encode('utf-8')
replace(manifest_path, encoded, manifest_sha)
report_path = PACK/'installation/install_report.json'
report = read(report_path)
report.update({
    'status': 'installed_visual_recovery_baseline_runtime_verified',
    'runtime_status': entry['runtime_status'],
    'runtime_report_sha256': sha(runtime_path),
    'after_manifest_sha256': sha(manifest_path),
    'aesthetic_status': entry['aesthetic_status'],
    'user_approval': False,
})
write_json(report_path, report)
print('Original brushwork recovery installed; loading/preservation verified. Local shape differences remain pending.')
