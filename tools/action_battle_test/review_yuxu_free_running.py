"""Keep every rendered frame visible for split-skirt and complete-leg review."""
from pathlib import Path
from PIL import Image, ImageDraw
import json
import sys

root = Path(__file__).resolve().parent / 'output' / (sys.argv[1] if len(sys.argv) > 1 else 'yuxu_free_running_v19_arena')
records = [json.loads(line) for line in (root / 'frames.jsonl').read_text().splitlines()]
assert len(records) == 360
assert all(row['facing'] == row['expected_facing'] for row in records)
for second in range(12):
    sheet = Image.new('RGB', (1320, 1360), '#58646a')
    draw = ImageDraw.Draw(sheet)
    for offset in range(30):
        frame = second * 30 + offset
        shot = Image.open(root / f'detail_{frame:04d}.png').convert('RGB')
        column, row = offset % 6, offset // 6
        sheet.paste(shot.resize((220, 250), Image.Resampling.LANCZOS), (column * 220, row * 272))
        draw.text((column * 220 + 4, row * 272 + 251), f'{frame:03d} / {frame / 30:.3f}s', fill='white')
    sheet.save(root / f'continuous_{second:02d}.jpg', quality=94)
print('Wrote all 360 consecutive poses as twelve review sheets; upper-leg exposure is permitted.')
