"""Export repainted head skin and ear pairs with their native alpha intact.

Only a whole-layer affine registration is permitted. Do not consume legacy
head cutouts, erase jaw pixels, or threshold generated alpha to hide defects.
"""
from pathlib import Path
from shutil import copy2
import json
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'tools/character_skin_ear_sources.json'
PACKS = {
    'male': ('腰上男修拆件演示_v2', 'test_assets_v7'),
    'female': ('腰上女修拆件演示_v1', 'test_assets_v5'),
}

def registered_image(entry):
    image = Image.open(ROOT / entry['source']).convert('RGBA')
    assert image.size == (1024, 1536), entry['source']
    r = entry.get('registration', {})
    sx, sy = r.get('x_scale', 1), r.get('y_scale', 1)
    ax, ay = r.get('anchor_x', 512), r.get('anchor_y', 400)
    dx, dy = r.get('offset_x', 0), r.get('offset_y', 0)
    if r:
        image = image.transform(image.size, Image.Transform.AFFINE,
            (1/sx, 0, ax-(ax+dx)/sx, 0, 1/sy, ay-(ay+dy)/sy),
            Image.Resampling.BICUBIC)
    return image

def export(install=False, output_root=None):
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    expected = {(sex, part, i) for sex in PACKS for part in ['face', 'ear'] for i in range(5)}
    assert {(a['sex'], a['part'], a['index']) for a in manifest['assets']} == expected
    assert len(manifest['assets']) == len(expected)
    for entry in manifest['assets']:
        pack, assets = PACKS[entry['sex']]
        folder = ROOT / 'mods/appearance' / pack
        preview = output_root / entry['sex'] if output_root else folder / 'preview/skin_ears_current'
        preview.mkdir(parents=True, exist_ok=True)
        name = f"{entry['part']}_{entry['index']}.png"
        image = registered_image(entry)
        alpha = image.getchannel('A')
        if entry['part'] == 'face':
            # The head remains filled beneath independent facial features and
            # the neck extends continuously underneath the robe collar.
            assert alpha.crop((475, 330, 549, 510)).getextrema()[0] >= 200, name
            assert alpha.crop((490, 550, 534, 665)).getextrema()[0] >= 200, name
            if manifest.get('skin_contract_version') == 'complete_skull_v15':
                pixels = np.asarray(alpha)
                assert 197 <= np.flatnonzero(pixels[:, 512] > 128).min() <= 203, (name, 'bone crown')
                expected_width = 224 if entry['sex'] == 'male' else 216
                row = np.flatnonzero(pixels[378 if entry['sex'] == 'male' else 380] > 128)
                assert abs(len(row) - expected_width) <= 4, (name, 'skin width', len(row))
                assert alpha.crop((480, 230, 544, 300)).getextrema()[0] >= 200, (name, 'missing hidden skull')
        else:
            assert alpha.crop((450, 330, 574, 500)).getextrema()[1] <= 5, name
            if manifest.get('ear_contract') == 'small_attached_muted_skin':
                pixels = np.asarray(alpha)
                ys, xs = np.where(pixels > 64)
                assert len(ys) and 64 <= ys.max()-ys.min()+1 <= 90, (name, 'ear size')
                assert ys.min() >= 348 and ys.max() <= 451, (name, 'ear position')
                for face_entry in manifest['assets']:
                    if face_entry['sex'] != entry['sex'] or face_entry['part'] != 'face':
                        continue
                    face = np.asarray(registered_image(face_entry).getchannel('A'))
                    rig_name = 'half_body_portrait_rig_v2.json' if entry['sex'] == 'male' else 'half_body_portrait_rig_female_v1.json'
                    rig = json.loads((ROOT / 'resources/appearance' / rig_name).read_text(encoding='utf-8'))
                    roots = rig['ear_attachment_contract_px']
                    for root in [roots['left_inner_root'], roots['right_inner_root']]:
                        lo, hi = root[0]-12, root[0]+12
                        overlap = ((face[383:420, lo:hi] > 200) & (pixels[383:420, lo:hi] > 128)).sum()
                        assert overlap > 8, (name, face_entry['index'], 'floating ear root')
        image.save(preview / name)
        if install:
            target = folder / assets / name
            backup = folder / 'test_sources/before_skin_ear_refinement'
            backup.mkdir(parents=True, exist_ok=True)
            if target.exists() and not (backup / name).exists():
                copy2(target, backup / name)
            image.save(target)
    print('20 skin/ear exports: pure head+neck, five independent ear pairs per gender, original alpha.')

if __name__ == '__main__':
    import sys
    export(install='--install' in sys.argv)
