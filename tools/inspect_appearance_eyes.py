"""Render the interchangeable brows and eyes at inspection scale."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1] / 'mods/appearance/腰上男修拆件演示_v2'
assets = root / 'test_assets_v3'
order = ['hair_back', 'face', 'clothes', 'eye', 'brow', 'nose', 'mouth', 'beard', 'hair_front']
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
sheet = Image.new('RGB', (2500, 880), '#ddd9d0')
draw = ImageDraw.Draw(sheet)

for row, changed in enumerate(['eye', 'brow']):
    for variant in range(5):
        portrait = Image.new('RGBA', (1024, 1536))
        for kind in order:
            selected = variant if kind == changed else 0
            portrait.alpha_composite(Image.open(assets / f'{kind}_{selected}.png').convert('RGBA'))
        crop = portrait.crop((390, 270, 634, 470)).resize((488, 400), Image.Resampling.LANCZOS)
        x, y = variant * 500 + 6, row * 440 + 38
        sheet.paste(crop, (x, y), crop)
        draw.text((x, row * 440 + 5), f'{changed} {variant}', font=font, fill='#111111')

target = root / 'preview/eye_brow_cleanup_v8.png'
sheet.save(target)
print(target)
for kind in ['brow', 'eye']:
    for variant in range(5):
        alpha = Image.open(assets / f'{kind}_{variant}.png').getchannel('A')
        strong = alpha.point(lambda v: 255 if v >= 32 else 0)
        print(kind, variant, strong.getbbox())

matrix = Image.new('RGB', (2440, 1230), '#ddd9d0')
matrix_draw = ImageDraw.Draw(matrix)
cache = {}
for eye in range(5):
    for brow in range(5):
        portrait = Image.new('RGBA', (1024, 1536))
        for kind in order:
            selected = eye if kind == 'eye' else brow if kind == 'brow' else 0
            key = (kind, selected)
            if key not in cache:
                cache[key] = Image.open(assets / f'{kind}_{selected}.png').convert('RGBA')
            portrait.alpha_composite(cache[key])
        crop = portrait.crop((390, 310, 634, 420)).resize((488, 220), Image.Resampling.LANCZOS)
        x, y = brow * 488, eye * 246
        matrix.paste(crop, (x, y + 26), crop)
        matrix_draw.text((x + 8, y + 2), f'眼 {eye} / 眉 {brow}', font=font, fill='#111111')
matrix_target = root / 'preview/eye_brow_combinations_v8.png'
matrix.save(matrix_target)
print(matrix_target)
