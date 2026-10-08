"""Render isolated reference portrait parts for review; never edit source art."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / 'mods/appearance/reference_face_sample_20260930'
REVIEW = PACK / 'modular_review'
SIZE = (1024, 1536)
ORDER = ['hair_back', 'face', 'ear', 'clothes', 'eye', 'brow', 'nose', 'mouth', 'extra', 'hair_front']

def render(sex, background, hidden=()):
    picture = Image.new('RGBA', SIZE, background)
    for name in ORDER:
        name = ('beard' if sex == 'male' else 'makeup') if name == 'extra' else name
        if name in hidden:
            continue
        with Image.open(PACK / 'modular' / sex / f'{name}_0.png') as layer:
            picture.alpha_composite(layer.convert('RGBA'))
    return picture

def main():
    REVIEW.mkdir(exist_ok=True)
    report = {'stage': 'isolated_standard_modular_review', 'canvas': list(SIZE), 'layers': {}, 'runtime_unchanged': {}}
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 19)
    comparison = Image.new('RGB', (1260, 1180), '#ccc8bd')
    draw = ImageDraw.Draw(comparison)
    for row, sex in enumerate(['male', 'female']):
        parts = [p if p != 'extra' else ('beard' if sex == 'male' else 'makeup') for p in ORDER]
        measurements = {}
        for part in parts:
            file = PACK / 'modular' / sex / f'{part}_0.png'
            with Image.open(file) as image:
                assert image.size == SIZE, (file, image.size)
                assert image.mode == 'RGBA', (file, image.mode)
                alpha = np.asarray(image.getchannel('A'))
                assert alpha.min() == 0, (file, 'no transparent pixels')
                ys, xs = np.where(alpha > 16)
                measurements[part] = {'visible_bbox': [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1] if len(xs) else None,
                                      'alpha_max': int(alpha.max()), 'transparent_pixels': int((alpha == 0).sum())}
                if part in ['beard', 'makeup']:
                    assert alpha.max() == 0, (file, 'standard extra must be empty')
                if part == 'face':
                    eye_y = 378 if sex == 'male' else 390
                    face_x = np.flatnonzero(alpha[eye_y] > 64)
                    measurements[part]['span_at_eye_y'] = int(face_x[-1] - face_x[0] + 1) if len(face_x) else 0
                if part in ['eye', 'brow']:
                    measurements[part]['visible_rows'] = [int(ys.min()), int(ys.max())] if len(ys) else None
        brow_bottom = measurements['brow']['visible_rows'][1]
        eye_top = measurements['eye']['visible_rows'][0]
        measurements['brow_eye_transparent_gap'] = eye_top - brow_bottom - 1
        assert measurements['brow_eye_transparent_gap'] >= 4, (sex, 'brow/eye overlap', measurements['brow_eye_transparent_gap'])
        report['layers'][sex] = measurements
        for name, color in [('light', '#ccc8bd'), ('dark', '#202526')]:
            render(sex, color).save(REVIEW / f'{sex}_{name}.png')
            render(sex, color, hidden=('hair_back',)).save(REVIEW / f'{sex}_without_rear_{name}.png')
            render(sex, color, hidden=('hair_back', 'hair_front')).save(REVIEW / f'{sex}_without_hair_{name}.png')
        render(sex, (0, 0, 0, 0)).save(REVIEW / f'{sex}_transparent.png')
        reference = Image.open(PACK / f'{sex}_brows_raised_sample.png').convert('RGB')
        light = render(sex, '#ccc8bd').convert('RGB')
        dark = render(sex, '#202526').convert('RGB')
        for col, (label, image) in enumerate([('已确认样稿', reference), ('实际拆件 · 浅底', light), ('实际拆件 · 深底', dark)]):
            draw.text((col * 420 + 12, row * 590 + 9), ('男修' if sex == 'male' else '女修') + ' · ' + label, font=font, fill='#222222')
            comparison.paste(image.crop((302, 120, 722, 670)), (col * 420, row * 590 + 40))
        pack, folder = ('腰上男修拆件演示_v2', 'test_assets_v7') if sex == 'male' else ('腰上女修拆件演示_v1', 'test_assets_v5')
        before = json.loads((PACK / f'{sex}_runtime_before_sha256.json').read_text(encoding='utf-8'))
        current = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT / 'mods/appearance' / pack / folder).glob('*.png')}
        assert before == current, (sex, 'runtime art changed unexpectedly')
        report['runtime_unchanged'][sex] = len(current)
    comparison.save(REVIEW / 'standard_comparison.png')
    (REVIEW / 'qa_report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False))

if __name__ == '__main__':
    main()
