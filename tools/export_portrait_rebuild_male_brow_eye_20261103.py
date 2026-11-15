"""Register imagegen-created eye/brow sprites; no drawing or color edits."""
from pathlib import Path
import json
import shutil
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / 'mods/appearance/portrait_rebuild_20261103'
SRC = PACK / 'source/male_brow_eye_final'
ACTIVE = ROOT / 'mods/appearance/ink_three_variants_20261004/male'

def run():
    records = []
    for variant in range(1, 4):
        for part in ['eyes', 'eyebrows']:
            filename = f'{part}_{variant:02d}_generated.png'
            if part == 'eyes' and variant == 1:
                filename = 'eyes_01_alpha_fixed.png'
            source = Image.open(SRC / filename).convert('RGBA')
            if part == 'eyes':
                controls = {1: [(626, 340), (1542, 340)], 2: [(555, 338), (1568, 338)], 3: [(550, 326), (1522, 326)]}[variant]
                target = [(464, 375), (569, 375)]
            else:
                alpha = np.asarray(source.getchannel('A'), dtype=float) / 255
                h, w = alpha.shape
                controls = []
                for x0, x1 in [(0, w // 2), (w // 2, w)]:
                    weight = alpha[:, x0:x1]
                    yy, xx = np.indices(weight.shape)
                    controls.append([float((weight * (xx + x0)).sum() / weight.sum()), float((weight * yy).sum() / weight.sum())])
                target = [(456, 346), (579, 345)]
            scale = (target[1][0] - target[0][0]) / (controls[1][0] - controls[0][0])
            anchor = np.asarray(controls).mean(axis=0)
            destination = np.asarray(target).mean(axis=0)
            origin = np.rint(destination - anchor * scale).astype(int)
            scaled = source.resize((round(source.width * scale), round(source.height * scale)), Image.Resampling.LANCZOS)
            result = Image.new('RGBA', (1024, 1536))
            result.alpha_composite(scaled, tuple(origin))
            output = PACK / 'final/male' / part / f'{variant:02d}.png'
            output.parent.mkdir(parents=True, exist_ok=True)
            result.save(output)
            records.append({'part': part, 'variant': variant, 'source': str(SRC / filename), 'source_controls': controls, 'target_controls': target, 'uniform_scale': scale, 'origin': origin.tolist(), 'output': str(output)})
    (SRC / 'registration.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
    for variant in range(1, 4):
        result = Image.new('RGBA', (1024, 1536), (236, 230, 216, 255))
        for part in ['hair_back', 'face_shape', 'clothing', 'eyes', 'eyebrows', 'nose', 'mouth', 'hair_front']:
            new = PACK / 'final/male' / part / f'{variant:02d}.png'
            path = new if new.exists() else ACTIVE / part / '01.png'
            result.alpha_composite(Image.open(path).convert('RGBA'))
        result.convert('RGB').save(SRC / f'face_check_{variant:02d}.jpg')
        result.crop((330, 210, 710, 560)).resize((760, 700), Image.Resampling.LANCZOS).convert('RGB').save(SRC / f'face_detail_{variant:02d}.jpg')
    print('Exported six independent 1024x1536 eye/brow PNGs.')

if __name__ == '__main__':
    run()
