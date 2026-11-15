"""Native arena replay and independently rendered detail; no discarded limbs."""
from pathlib import Path
from PIL import Image, ImageDraw
import json
import sys

root = Path(__file__).resolve().parent / 'output' / (sys.argv[1] if len(sys.argv)>1 else 'yuxu_natural_v16_arena')
records = [json.loads(line) for line in (root / 'frames.jsonl').read_text().splitlines()]
assert len(records) == 360
assert all(row['facing'] == row['expected_facing'] for row in records)
for prefix, name in [('frame','arena'), ('detail','detail')]:
    paths = sorted(root.glob(prefix + '_*.' + ('jpg' if prefix=='frame' else 'png')))
    if len(paths) != 360:
        continue
    frames = [Image.open(p).convert('RGB') for p in paths[::2]]
    durations = [round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(len(frames))]
    if prefix == 'frame':
        frames[0].save(root / 'arena_12s.webp', save_all=True, append_images=frames[1:], duration=durations, loop=0, quality=78, method=4)
        # Full camera view, reduced to 960 for a compact inline GIF. The
        # original 1280 JPG sequence and full-resolution WebP are retained.
        gif_frames = [frame.resize((960,540),Image.Resampling.LANCZOS).quantize(colors=64,dither=Image.Dither.NONE) for frame in frames]
    else:
        gif_frames = frames
    gif_frames[0].save(root / (name + '_12s.gif'), save_all=True, append_images=gif_frames[1:], duration=durations, loop=0, disposal=2)
    for start,end,label in [(20,44,'start'),(84,108,'turn'),(122,146,'stop'),(142,166,'attack'),(202,226,'moving_attack'),(236,260,'dash'),(302,326,'settle')]:
        if prefix!='detail':
            continue
        selected = list(range(start,end,3))
        sheet = Image.new('RGB',(4*440,2*525),'#58646a')
        draw = ImageDraw.Draw(sheet)
        for i,idx in enumerate(selected):
            x,y = (i%4)*440,(i//4)*525
            shot = Image.open(paths[idx]).convert('RGB')
            sheet.paste(shot,(x,y))
            draw.text((x+8,y+503),f"{idx/30:.2f}s / {records[idx]['label']} / {records[idx]['clip']}",fill='white')
        sheet.save(root / (label+'_contact.png'))
print('Verified 360 rendered frames and their real facing; wrote 12-second loops and transition sheets.')
