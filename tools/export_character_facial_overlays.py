"""Export repainted brows and beards, preserving native generated alpha.

Registration is whole-canvas affine only. Invalid paintings are rejected;
this exporter never crops features or erases contamination to pass a check.
"""
from pathlib import Path
from shutil import copy2
import json
from PIL import Image
import numpy as np
from export_character_skin_ears import registered_image, PACKS, ROOT

MANIFEST = ROOT / 'tools/character_facial_overlay_sources.json'

def export(install=False, output_root=None):
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8'))
    base = {(sex, 'brow', i) for sex in PACKS for i in range(5)} | {('male', 'beard', i) for i in range(5)}
    eye_set = {(sex, 'eye', i) for sex in PACKS for i in range(5)}
    actual = {(a['sex'], a['part'], a['index']) for a in manifest['assets']}
    # The user selected the earlier brow/beard source manifest. Its eyes
    # remain the existing exported layers; never replace them with new art.
    assert actual in (base, base | eye_set), actual
    assert len(manifest['assets']) == len(actual), 'duplicate source entries'
    for entry in manifest['assets']:
        pack, assets = PACKS[entry['sex']]
        folder = ROOT / 'mods/appearance' / pack
        preview = output_root / entry['sex'] if output_root else folder / 'preview/portrait_cleanup_v14'
        preview.mkdir(parents=True, exist_ok=True)
        image = registered_image(entry)
        alpha = image.getchannel('A')
        pixels = np.asarray(alpha)
        yy, xx = np.where(pixels > 5)
        bbox = (int(xx.min()), int(yy.min()), int(xx.max()+1), int(yy.max()+1)) if len(xx) else None
        if entry['part'] == 'brow':
            assert bbox and bbox[0] >= 410 and bbox[2] <= 620 and bbox[1] >= 329 and bbox[3] <= 360, (entry['source'], bbox)
            # Check every selectable eye, rather than the default pair alone.
            brow_rows = np.where(pixels[:, 410:620] > 12)[0]
            for eye_index in range(5):
                eye_entry = next((a for a in manifest['assets'] if a['sex'] == entry['sex'] and a['part'] == 'eye' and a['index'] == eye_index), None)
                if eye_entry is None:
                    with Image.open(folder / assets / f'eye_{eye_index}.png') as existing:
                        eye = np.asarray(existing.convert('RGBA').getchannel('A'))
                else:
                    eye = np.asarray(registered_image(eye_entry).getchannel('A'))
                eye_rows = np.where(eye[:, 410:620] > 12)[0]
                assert int(eye_rows.min()) - int(brow_rows.max()) - 1 >= 4, (entry['source'], eye_index)
        elif entry['part'] == 'eye':
            assert bbox and bbox[0] >= 410 and bbox[2] <= 614 and bbox[1] >= 363 and bbox[3] <= 400, (entry['source'], bbox)
            # Native paintings own just the eyelids, iris/sclera and short
            # lashes. Outer temple scars cannot be hidden by another layer.
            import cv2
            count, labels = cv2.connectedComponents((pixels > 16).astype(np.uint8), connectivity=8)
            components = []
            for component in range(1, count):
                y, x = np.where(labels == component)
                if len(x) >= 8:
                    components.append((len(x), int(x.min()), int(y.min()), int(x.max()), int(y.max())))
            assert len(components) == 2, (entry['source'], 'eye islands', components)
            for size, left, top, right, bottom in components:
                assert 45 <= right-left+1 <= 88 and 9 <= bottom-top+1 <= 33, (entry['source'], 'eyelid proportions', components)
        elif entry['index'] == 0:
            assert bbox is None
        else:
            assert alpha.crop((0, 561, 1024, 1536)).getextrema()[1] <= 5, entry['source']
            assert bbox and bbox[0] >= 428 and bbox[2] <= 596 and bbox[1] >= 449, (entry['source'], bbox)
        name = f"{entry['part']}_{entry['index']}.png"
        image.save(preview / name)
        if install:
            target = folder / assets / name
            backup = folder / 'test_sources/before_portrait_cleanup_v14'
            backup.mkdir(parents=True, exist_ok=True)
            if target.exists() and not (backup / name).exists():
                copy2(target, backup / name)
            image.save(target)
    print(f'{len(actual)} facial overlays exported; original alpha preserved; existing eyes retained when not in source manifest.')

if __name__ == '__main__':
    import sys
    export(install='--install' in sys.argv)
