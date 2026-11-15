from pathlib import Path
import hashlib
import json
import shutil
import zipfile
from datetime import datetime, timezone

pack = Path(__file__).resolve().parent.parent
project = pack.parents[2]
active = project / 'mods/appearance/ink_three_variants_20261004'
installation = pack / 'installation'
snapshot = installation / 'backup_before'
archive = installation / 'before_install.zip'
if archive.exists() or (installation / 'backup_report.json').exists():
    raise SystemExit('A baseline snapshot already exists; refusing to overwrite it.')
config = json.loads((active / 'appearance_parts.json').read_text(encoding='utf-8'))
pngs = []
for entry in config['components']:
    for index in entry.get('variant_indices', range(1, entry['variant_count'] + 1)):
        pngs.append(active / entry['file'].replace('{index}', f'{index:02d}'))
if len(pngs) != 44 or len(set(pngs)) != 44:
    raise SystemExit(f'Expected exactly 44 registered PNGs, got {len(pngs)}')
files = pngs + [active / 'appearance_parts.json', active / 'portrait_manifest.json']
missing = [str(file) for file in files if not file.is_file()]
if missing:
    raise SystemExit('Missing baseline files: ' + ', '.join(missing))
installation.mkdir(parents=True, exist_ok=True)
sha = lambda file: hashlib.sha256(file.read_bytes()).hexdigest()
resource = lambda file: 'res://' + file.relative_to(project).as_posix()
# Keep the immutable enabled config out of the recursive appearance-package scan.
# The ZIP member name remains the original appearance_parts.json.
snapshot_name = lambda relative: relative.with_name('appearance_parts.snapshot.json') if relative.name == 'appearance_parts.json' else relative
hashes = {}
with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
    for file in files:
        relative = file.relative_to(active)
        destination = snapshot / snapshot_name(relative)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file, destination)
        output.write(destination, relative.as_posix())
        hashes[resource(file)] = sha(destination)
        if sha(file) != sha(destination):
            raise SystemExit('Baseline changed while copying: ' + str(file))
configuration = {name: sha(snapshot / snapshot_name(Path(name))) for name in ['appearance_parts.json', 'portrait_manifest.json']}
report = {'created_at': datetime.now(timezone.utc).isoformat(), 'active_png_count': 44,
          'archive_file_count': 46, 'archive_sha256': sha(archive),
          'sha256': hashes, 'configuration_sha256': configuration,
          'snapshot_path': resource(snapshot), 'active_directory': resource(active),
          'archive_members': [file.relative_to(active).as_posix() for file in files],
          'snapshot_configuration_files': {'appearance_parts.json': 'appearance_parts.snapshot.json', 'portrait_manifest.json': 'portrait_manifest.json'}}
(installation / 'backup_report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
baseline = {'baseline_sha256': {resource(file): hashes[resource(file)] for file in pngs},
            'configuration_sha256': configuration, 'active_png_count': 44}
(installation / 'preserved_baseline.json').write_text(json.dumps(baseline, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'backed_up_pngs': 44, 'archived_files': 46, 'archive': str(archive)}, ensure_ascii=False))
