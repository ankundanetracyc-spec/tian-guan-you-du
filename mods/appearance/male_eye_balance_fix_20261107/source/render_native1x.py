"""Read-only native-pixel eye comparison; no candidate mutation."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
P = Path(__file__).resolve().parents[1]
S = P / 'reference/current_male'
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
def layer(part, index):
    return Image.open(S / part / f'{index:02}.png').convert('RGBA')
old = layer('eyes', 4)
new = Image.open(P / 'candidate/new_male_eyes04.png').convert('RGBA')
for theme, bg, fg in [('light', '#e8e1d6', '#33362e'), ('dark', '#202b31', '#eee8dc')]:
    sheet = Image.new('RGB', (680, 600), bg)
    draw = ImageDraw.Draw(sheet)
    for row, hair in enumerate((None, 1, 4, 5)):
        for col, (eye, word) in enumerate(((old, 'Before'), (new, 'After'))):
            out = Image.new('RGBA', (1024, 1536), bg)
            seq = [layer('hair_back', 4)] if hair else []
            seq += [layer('face_shape', 1), layer('nose', 1), eye, layer('eyebrows', 4), layer('mouth', 1)]
            if hair:
                seq += [layer('hair_front', hair)]
            for im in seq:
                out.alpha_composite(im)
            sheet.paste(out.crop((350, 318, 690, 433)).convert('RGB'), (col*340, row*150+32))
            label = 'no hair' if hair is None else f'front{hair:02}'
            draw.text((col*340+8, row*150+5), f'{word} / {label} / native1x', font=FONT, fill=fg)
    sheet.save(P / 'qa' / f'before_after_eye_hair_native_1x_{theme}.png')
print('Native1x light/dark QA refreshed; candidate unchanged.')
