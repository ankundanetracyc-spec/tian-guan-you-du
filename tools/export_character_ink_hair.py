"""Export complete generated ink hair without cutting hidden rear regions.

Only whole-canvas registration is allowed. Generated alpha is preserved;
legacy rear snippets and semantic cutout masks are deliberately not consumed.
"""
from pathlib import Path
from shutil import copy2
import json
import sys
from PIL import Image
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'tools/character_ink_hair_sources.json'
PACKS = {
    'male': ('腰上男修拆件演示_v2', 'test_assets_v7'),
    'female': ('腰上女修拆件演示_v1', 'test_assets_v5'),
}

def validate_front_envelope(image, sex, name):
    rig_name = 'half_body_portrait_rig_v2.json' if sex == 'male' else 'half_body_portrait_rig_female_v1.json'
    rig = json.loads((ROOT / 'resources/appearance' / rig_name).read_text(encoding='utf-8'))
    envelope = rig.get('front_scalp_envelope_px')
    if not envelope:
        return
    rows = envelope['rows']
    alpha = np.asarray(image.getchannel('A'))
    for y in range(rows[0][0], rows[-1][0] + 1):
        left = np.interp(y, [r[0] for r in rows], [r[1] for r in rows])
        right = np.interp(y, [r[0] for r in rows], [r[2] for r in rows])
        visible = np.flatnonzero(alpha[y] >= envelope['visible_alpha_min'])
        if len(visible):
            assert visible.min() >= left and visible.max() <= right, (name, y, int(visible.min()), int(visible.max()), left, right)

def export(install=False, output_root=None, require_complete=True):
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    if install or require_complete:
        assert len(manifest['assets']) == 20
        assert len({(a['sex'], a['part'], a['index']) for a in manifest['assets']}) == 20
    for entry in manifest['assets']:
        pack, assets = PACKS[entry['sex']]
        folder = ROOT / 'mods/appearance' / pack
        target = folder / assets
        preview = output_root / entry['sex'] if output_root else folder / 'preview/ink_hair_current'
        preview.mkdir(parents=True, exist_ok=True)
        name = f"hair_{entry['part']}_{entry['index']}.png"
        image = Image.open(ROOT / entry['source']).convert('RGBA')
        assert image.size == (1024, 1536), entry['source']
        registration = entry.get('registration', {})
        if 'scalp_crown' in entry:
            scalp = entry['scalp_crown']
            sy = registration.get('y_scale', 1)
            ay = registration.get('anchor_y', 279)
            dy = registration.get('offset_y', 0)
            registered_crown = ay + (scalp['source_y'] - ay) * sy + dy
            assert abs(registered_crown - manifest['shared_scalp_crown_y']) < .01, entry['source']
        if registration:
            sx = registration.get('x_scale', 1)
            sy = registration.get('y_scale', 1)
            ax = registration.get('anchor_x', 512)
            ay = registration.get('anchor_y', 279)
            dx = registration.get('offset_x', 0)
            dy = registration.get('offset_y', 0)
            image = image.transform(image.size, Image.Transform.AFFINE,
                (1/sx, 0, ax - (ax+dx)/sx, 0, 1/sy, ay - (ay+dy)/sy),
                Image.Resampling.BICUBIC)
        if manifest.get('front_contract') == 'complete_frontal_crown':
            alpha = image.getchannel('A')
            if entry['part'] == 'front':
                # A front layer must own a real scalp, independently of the rear.
                crown_y = manifest['shared_scalp_crown_y']
                assert alpha.crop((480, crown_y + 35, 544, crown_y + 70)).getextrema()[0] >= 200, name
                assert alpha.crop((470, 330, 554, 440)).getextrema()[1] == 0, name
                validate_front_envelope(image, entry['sex'], name)
            else:
                # The covered head area is painted, never cut into a face-shaped hole.
                # Check the common hidden skull core, above the natural neck
                # taper. The previous wider rectangle extended into the neck
                # silhouette of the low-tied female variant after fitting.
                assert alpha.crop((470, 350, 554, 475)).getextrema()[0] >= 200, (entry['sex'], name)
        image.save(preview / name)
        if install:
            backup = folder / 'test_sources/before_ink_hair'
            backup.mkdir(parents=True, exist_ok=True)
            if not (backup / name).exists():
                copy2(target / name, backup / name)
            image.save(target / name)
    print(f"{len(manifest['assets'])} ink hair exports; original alpha, shared 1024x1536 canvas.")

if __name__ == '__main__':
    export(install='--install' in sys.argv)
