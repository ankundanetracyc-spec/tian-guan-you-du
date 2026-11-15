"""Encode actual engine capture without synthesizing animation frames."""
from pathlib import Path
from PIL import Image
root = Path(__file__).resolve().parents[1]
source = root / 'tmp/blood_idle_capture'
out = root / 'docs/美术规范/气血手臂透明待机_20261103'
frames = []
close = []
for i in range(64):
    with Image.open(source / f'{i:03}.png') as im:
        frames.append(im.convert('RGB').resize((960,540),Image.Resampling.LANCZOS))
        close.append(im.convert('RGB').crop((120,200,600,590)))
for name, images in [('实机待机',frames),('待机局部',close)]:
    palette=images[0].quantize(colors=256)
    images=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in images]
    images[0].save(out / (name+'.gif'),save_all=True,append_images=images[1:],duration=[60,60,60,70]*16,loop=0,optimize=False)
print('Encoded 64 runtime frames and native-pixel crop; 4 seconds at 16fps.')
