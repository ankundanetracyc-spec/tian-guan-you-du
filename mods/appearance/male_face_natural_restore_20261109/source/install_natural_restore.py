"""Withdraw six rejected redraws; recover original RGBA with integer translation.

This is a visual recovery baseline, not bilateral-geometry approval.
The --apply switch replaces only six PNGs and the manifest audit record.
"""
import argparse
import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / 'mods/appearance/ink_three_variants_20261004'
META = 'male_face_natural_restore_20261109'
BAD = {
    'male/eyes/01.png': '98026ce638f1c880ec3bb1c6e690ad7b15c0184b77a24561cef180dec746d642',
    'male/eyes/04.png': 'cf1e14f61b5ac2904cddcaad14eb38d3da6328673fa3e90a019e1e7629b99f05',
    'male/eyes/05.png': 'a69e053c0f0af4be64b10a157c0f076aede96ab44ccc0915665f74c50a2560da',
    'male/eyebrows/01.png': '0b81852f026929d204972a43f9c0e88f9b15396e02941342fb761a615795f3cf',
    'male/eyebrows/04.png': '530d5cf4e2fb3094269b6493ca7e9c552c30d27de5fe389a3a2012440adfcccf',
    'male/eyebrows/05.png': 'a8ef833cc1bfaadb9c0ee55f5c27806adfae05991216162d21a8ae6f492b6ef4',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def replace(path, data, expected):
    assert sha(path) == expected, f'Concurrent change: {path}'
    fd, temporary = tempfile.mkstemp(dir=path.parent, suffix='.tmp')
    try:
        with os.fdopen(fd, 'wb') as out:
            out.write(data)
        assert sha(path) == expected, f'Concurrent change: {path}'
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def main(apply):
    spec = read(PACK/'source/natural_restore_candidates.json')
    records = {r['candidate']: r for r in spec['records']}
    assert set(records) == set(BAD)
    for name, row in records.items():
        original = ROOT/row['source']
        candidate = PACK/name
        assert sha(original) == row['source_sha256']
        assert sha(candidate) == row['candidate_sha256']
        dx, dy = row['translation']
        assert isinstance(dx, int) and dy == 0 and abs(dx) <= 3
        assert not row['interpolation'] and not row['reflection'] and not row['generation']
        src = np.asarray(Image.open(original).convert('RGBA'))
        got = np.asarray(Image.open(candidate).convert('RGBA'))
        assert src.shape == got.shape == (1536, 1024, 4)
        expected = np.zeros_like(src)
        if dx < 0:
            expected[:, :dx] = src[:, -dx:]
            assert not src[:, :-dx, 3].any()
        else:
            expected[:, dx:] = src[:, :-dx]
            assert not src[:, -dx:, 3].any()
        assert np.array_equal(expected, got), f'Original RGBA changed: {name}'

    pngs = sorted(p for sex in ('male', 'female') for p in (ACTIVE/sex).rglob('*.png'))
    assert len(pngs) == 67
    baseline_path = PACK/'source/baseline.json'
    if not baseline_path.exists():
        for name, digest in BAD.items():
            assert sha(ACTIVE/name) == digest, f'Unexpected active state: {name}'
        files = []
        for source in pngs + [ACTIVE/'appearance_parts.json', ACTIVE/'portrait_manifest.json']:
            relative = source.relative_to(ACTIVE)
            backup = PACK/'source/frozen_rejected'/relative
            if source.suffix == '.json':
                backup = backup.with_suffix('.snapshot.json')
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, backup)
            files.append({'file': source.relative_to(ROOT).as_posix(), 'sha256': sha(source),
                          'backup': backup.relative_to(PACK).as_posix()})
        write_json(baseline_path, {'schema': 'withdraw-rejected-features-baseline/v1', 'files': files})
    baseline = read(baseline_path)
    before = {str(Path(r['file']).relative_to(ACTIVE.relative_to(ROOT))): r for r in baseline['files']}
    # Normalize Windows separators for the explicit inventory.
    before = {name.replace('\\', '/'): row for name, row in before.items()}
    assert len(before) == 69
    for name, row in before.items():
        assert sha(PACK/row['backup']) == row['sha256']
        assert sha(ACTIVE/name) == row['sha256'], f'Active changed since snapshot: {name}'
    if not apply:
        print('Preflight passed; six exact original-RGBA translations ready.')
        return

    manifest = read(ACTIVE/'portrait_manifest.json')
    previous = manifest['male_face_alignment_fix_20261109']
    previous['visual_status'] = 'user_rejected_redrawn_eyes_and_brows'
    previous['visual_approval_withdrawn'] = True
    previous['superseded_features'] = sorted(records)
    previous['superseded_by'] = META
    previous['historical_technical_results_only'] = True
    manifest[META] = {
        'status': 'installed_visual_recovery_baseline',
        'scope': 'Restore six original eye/brow PNGs with exact integer whole-pair translation.',
        'not_full_symmetry_repair': True,
        'retained': 'Current centered nose, mouth and five nonempty beards; other61 PNGs unchanged.',
        'records': [{**r, 'before_sha256': before[name]['sha256']} for name, r in records.items()],
        'appearance_parts_byte_identical': True,
        'runtime_status': 'pending',
        'runtime_report': 'res://mods/appearance/'+META+'/installation/runtime_check.json',
    }
    written = []
    try:
        for name in records:
            replace(ACTIVE/name, (PACK/name).read_bytes(), before[name]['sha256'])
            written.append(name)
        encoded = (json.dumps(manifest, ensure_ascii=False, indent=2)+'\n').encode('utf-8')
        replace(ACTIVE/'portrait_manifest.json', encoded, before['portrait_manifest.json']['sha256'])
        written.append('portrait_manifest.json')
        for name, row in before.items():
            if name == 'portrait_manifest.json':
                continue
            target_sha = records[name]['candidate_sha256'] if name in records else row['sha256']
            assert sha(ACTIVE/name) == target_sha, f'Unexpected output: {name}'
    except Exception:
        for name in reversed(written):
            if name in records:
                current_expected = records[name]['candidate_sha256']
            else:
                current_expected = hashlib.sha256(encoded).hexdigest()
            replace(ACTIVE/name, (PACK/before[name]['backup']).read_bytes(), current_expected)
        raise
    report = {
        'status': 'installed_visual_recovery_baseline', 'geometry_status': 'residual_shape_differences_retained',
        'six_restored': sorted(records), 'other_pngs_unchanged': 61, 'total_pngs': 67,
        'appearance_parts_unchanged': True, 'before_config_sha256': before['appearance_parts.json']['sha256'],
        'after_config_sha256': sha(ACTIVE/'appearance_parts.json'),
        'after_manifest_sha256': sha(ACTIVE/'portrait_manifest.json'),
        'candidate_record_sha256': sha(PACK/'source/natural_restore_candidates.json'),
        'runtime_status': 'pending',
    }
    write_json(PACK/'installation/install_report.json', report)
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    main(parser.parse_args().apply)
