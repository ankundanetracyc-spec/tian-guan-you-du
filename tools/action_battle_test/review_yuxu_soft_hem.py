"""Compare identical live-input replays and retain every frame for cloth review."""
from pathlib import Path
from PIL import Image, ImageDraw

base = Path(__file__).resolve().parent / 'output'
before = base / 'yuxu_natural_v16_arena'
after = base / 'yuxu_soft_hem_v17_arena'
comparison = []
for frame in range(360):
    shot = Image.open(after / f'detail_{frame:04d}.png').convert('RGB')
    if frame % 30 == 0:
        sheet = Image.new('RGB', (1320, 1360), '#58646a')
        draw = ImageDraw.Draw(sheet)
    column, row = frame % 6, (frame % 30) // 6
    sheet.paste(shot.resize((220,250),Image.Resampling.LANCZOS), (column*220,row*272))
    draw.text((column*220+4,row*272+251),f'{frame:03d} / {frame/30:.3f}s',fill='white')
    if frame % 30 == 29:
        sheet.save(after / f'continuous_{frame//30:02d}.jpg',quality=94)
    if frame % 2 == 0:
        pair = Image.new('RGB',(880,530),'#58646a')
        pair.paste(Image.open(before / f'detail_{frame:04d}.png').convert('RGB'),(0,30))
        pair.paste(shot,(440,30))
        labels=ImageDraw.Draw(pair)
        labels.text((15,9),f'BEFORE v16 / {frame/30:.2f}s',fill='white')
        labels.text((455,9),'SOFTER HEM v17 / same live replay',fill='white')
        comparison.append(pair)
durations=[round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(len(comparison))]
comparison[0].save(after/'before_after_12s.gif',save_all=True,append_images=comparison[1:],duration=durations,loop=0,disposal=2)
print('Wrote synchronized before/after GIF and twelve consecutive 30-frame review sheets.')
