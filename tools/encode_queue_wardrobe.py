"""Encode unedited runtime captures for wardrobe review."""
from pathlib import Path
from PIL import Image
import sys

root=Path(__file__).resolve().parents[1]
kind='battle' if '--battle' in sys.argv else 'close'
source=root/'tmp'/f'queue_wardrobe_{kind}'
output=root/'docs/美术规范/玉虚四套局内形象_20261103'
files=sorted(source.glob('frame_*.png'))
assert 120<=len(files)<=240,len(files)
frames=[]
palette=None
for path in files:
    with Image.open(path) as im:
        frame=im.convert('RGB').resize((1280,720),Image.Resampling.LANCZOS)
        if palette is None:
            palette=frame.quantize(colors=256)
        frames.append(frame.quantize(palette=palette,dither=Image.Dither.NONE))
frames[0].save(output/f'{kind}_motion.gif',save_all=True,append_images=frames[1:],duration=50,loop=0,disposal=1,optimize=False)
print(f'{kind}: {len(frames)} frames, {len(frames)*.05:.2f} seconds')
