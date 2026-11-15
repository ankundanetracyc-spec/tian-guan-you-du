"""Encode unmodified Godot capture frames as a review animation."""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = root / 'tmp/queue_strikes_capture'
output = root / 'docs/美术规范/队列拳击手刀重制_20261103'
frames = []
for index in range(96):
    with Image.open(source / f'{index:03}.png') as im:
        frames.append(im.convert('RGB').resize((960, 540), Image.Resampling.LANCZOS))
palette = frames[5].quantize(colors=256)
frames = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in frames]
frames[0].save(output / '拳击与手刀实机.gif', save_all=True, append_images=frames[1:],
               duration=[60, 60, 60, 70] * 24, loop=0, optimize=False)
print('Encoded 96 Godot frames, 6 seconds at 16 fps.')
