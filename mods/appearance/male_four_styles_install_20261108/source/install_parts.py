"""Append the four user-selected hair pairs; default performs read-only checks."""
from pathlib import Path
import argparse
import copy
import hashlib
import json
import os
import tempfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / 'mods/appearance/ink_three_variants_20261004'
META = 'male_four_hair_styles_20261108'
ORDER = ['hair_front', 'hair_back', 'eyebrows', 'eyes', 'nose', 'mouth', 'face_shape', 'clothing', 'face_accessory']
SPEC = [
    ('low_coil', 6, 5, '后收拢发', '后颈低髻'),
    ('high_tail', 7, 6, '露额拢发', '长尾高束'),
    ('loose_back', 8, 7, '露耳后梳', '自然松披'),
    ('temple_plait', 9, 8, '偏分鬓辫', '半束长披'),
]

def sha(data):
    return hashlib.sha256(data).hexdigest()

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')

def resource(path):
    return 'res://' + path.relative_to(ROOT).as_posix()

def atomic(path, data):
    fd, temporary = tempfile.mkstemp(dir=path.parent, prefix=path.name + '.', suffix='.tmp')
    try:
        with os.fdopen(fd, 'wb') as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    baseline = read(PACK / 'source/baseline.json')
    config_path, manifest_path = ACTIVE / 'appearance_parts.json', ACTIVE / 'portrait_manifest.json'
    before_config, before_manifest = config_path.read_bytes(), manifest_path.read_bytes()
    config, manifest = read(config_path), read(manifest_path)
    existing_report = PACK / 'installation/install_report.json'
    if META in manifest:
        report = read(existing_report)
        for item in report['records']:
            assert sha((ACTIVE / item['target']).read_bytes()) == item['sha256']
        assert sha(before_config) == report['after_config_sha256']
        print(json.dumps({'status': 'already_installed_verified', 'additions': len(report['records'])}))
        return
    for item in baseline['files']:
        if item['file'].startswith('mods/appearance/ink_three_variants_20261004/'):
            assert sha((ROOT / item['file']).read_bytes()) == item['sha256'], item['file']
    updated = copy.deepcopy(config)
    records, content = [], {}
    for key, physical, style, front_name, back_name in SPEC:
        approved = PACK / 'source/approved' / (key + '.png')
        for slot, name in [('hair_front', front_name), ('hair_back', back_name)]:
            relative = f'male/{slot}/{physical:02d}.png'
            source, target = PACK / relative, ACTIVE / relative
            assert source.is_file() and not target.exists(), str(target)
            data = source.read_bytes()
            with Image.open(source) as image:
                assert image.mode == 'RGBA' and image.size == (1024, 1536), relative
                alpha = image.getchannel('A')
                assert alpha.getbbox() and alpha.getpixel((0, 0)) == 0, relative
                if slot == 'hair_front':
                    assert alpha.getpixel((513, 400)) == 0, 'Baked central face in ' + relative
                bbox = list(alpha.getbbox())
            entry = next(x for x in updated['components'] if x['gender'] == 'male' and x['part'] == slot)
            assert physical not in entry['variant_indices'] and style not in entry['variant_style_ids']
            entry['variant_indices'].append(physical)
            entry['variant_style_ids'].append(style)
            entry['variant_names'].append(name)
            entry['variant_count'] = len(entry['variant_indices'])
            record = {'key': key, 'target': relative, 'part': slot, 'catalog_part': slot,
                      'component_id': entry['id'], 'full_component_id': f"{entry['id']}_{style:02d}",
                      'name': name, 'index': physical, 'style_id': style, 'ui_index': entry['variant_count'] - 1,
                      'resource_path': resource(target), 'candidate_path': resource(source),
                      'sha256': sha(data), 'size': [1024, 1536], 'alpha_bbox': bbox,
                      'approved_source': resource(approved), 'approved_source_sha256': sha(approved.read_bytes())}
            records.append(record)
            content[relative] = data
    # Only the two male hair components can change. All old values are exact prefixes.
    for old, new in zip(config['components'], updated['components']):
        if old['gender'] != 'male' or old['part'] not in ['hair_front', 'hair_back']:
            assert old == new
        else:
            assert old['variant_aliases'] == new['variant_aliases']
            for field in ['variant_indices', 'variant_style_ids', 'variant_names']:
                assert new[field][:len(old[field])] == old[field]
    assert sum(x['variant_count'] for x in updated['components']) == 60
    slots = {}
    for gender in ['male', 'female']:
        slots[gender] = [next(x['variant_count'] for x in updated['components']
                           if x['gender'] == gender and x['file'].split('/')[1] == slot) for slot in ORDER]
    dedup = [{'key': key, 'reused_physical_01': ['eyebrows', 'eyes', 'nose', 'mouth', 'face_shape'],
              'reason': 'Near-identical master features; user requested no duplicate facial parts.'} for key, *_ in SPEC]
    manifest[META] = {'source': resource(PACK), 'append_only': True, 'records': records,
                      'approved_whole_portraits': True, 'pixel_exact_extraction': False,
                      'deduplication': dedup, 'runtime_status': 'pending_verification'}
    manifest['variants_per_slot'] = slots
    manifest['active_variant_count'] = 60
    manifest['total_pngs'] = 67
    config_after, manifest_after = encoded(updated), encoded(manifest)
    report = {'schema': 'male-four-hair-styles/v1', 'status': 'preflight_passed', 'installed': False,
              'append_only': True, 'records': records, 'slot_counts': slots, 'png_count': 67,
              'active_variant_count': 60, 'baseline_config': config, 'installed_config': updated,
              'before_config_sha256': sha(before_config), 'after_config_sha256': sha(config_after),
              'before_manifest_sha256': sha(before_manifest), 'after_manifest_sha256': sha(manifest_after),
              'deduplication': dedup, 'old_pngs_preserved': 59, 'female_pngs_preserved': 24,
              'code_change': 'AppearanceEditor.normalize honors configured old aliases for front/back hair.'}
    if not args.apply:
        atomic(PACK / 'installation/preflight.json', encoded(report))
        print(json.dumps({'status': 'preflight_passed', 'new_png': 8, 'active_choices': 60, 'total_png': 67}))
        return
    assert config_path.read_bytes() == before_config and manifest_path.read_bytes() == before_manifest
    created = []
    try:
        for relative, data in content.items():
            target = ACTIVE / relative
            with target.open('xb') as handle:
                created.append(target)
                handle.write(data)
        atomic(config_path, config_after)
        atomic(manifest_path, manifest_after)
        for item in baseline['files']:
            if item['file'].endswith('.png'):
                assert sha((ROOT / item['file']).read_bytes()) == item['sha256'], item['file']
        for item in records:
            assert sha((ACTIVE / item['target']).read_bytes()) == item['sha256']
        report.update(status='installed_pending_runtime_verification', installed=True)
        atomic(existing_report, encoded(report))
    except Exception as error:
        recovery_errors = []
        for path, data in [(config_path, before_config), (manifest_path, before_manifest)]:
            try:
                atomic(path, data)
            except Exception as recovery_error:
                recovery_errors.append(f'{path.name}: {recovery_error}')
        for target in created:
            try:
                if target.parent.parent.parent == ACTIVE:
                    target.unlink(missing_ok=True)
            except Exception as recovery_error:
                recovery_errors.append(f'{target}: {recovery_error}')
        if recovery_errors:
            error.add_note('Recovery requires attention: ' + '; '.join(recovery_errors))
        raise
    print(json.dumps({'status': report['status'], 'new_png': 8, 'old_png_preserved': 59,
                      'active_choices': 60, 'total_png': 67}))

if __name__ == '__main__':
    main()
