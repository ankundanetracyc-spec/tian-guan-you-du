"""Contact sheets and timing-correct loops from native Godot renders."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent / 'output'
for dirname in ['yuxu_unified_v14', 'yuxu_unified_v14_cycle']:
    folder = root / dirname
    for clip in ['Idle', 'Run_New1', 'BackstepWalk', 'AttackIdle', 'Step1']:
        for side in ['right', 'left']:
            paths = sorted(folder.glob(f'{clip}_*_{side}_detail.png'))
            if not paths:
                continue
            # Each tile is a native raster crop: no resampling of the character.
            frames = [Image.open(p).convert('RGB').crop((70, 80, 370, 480)) for p in paths]
            cols = min(6, len(frames))
            rows = (len(frames) + cols - 1) // cols
            sheet = Image.new('RGB', (cols * 300, rows * 420), '#58646a')
            draw = ImageDraw.Draw(sheet)
            for i, (p, frame) in enumerate(zip(paths, frames)):
                x, y = i % cols * 300, i // cols * 420
                sheet.paste(frame, (x, y))
                draw.text((x+6, y+402), p.stem.replace('_detail', ''), fill='white')
            sheet.save(folder / f'{clip}_{side}_contact.png')
            if dirname.endswith('_cycle') and clip == 'Run_New1':
                selected = frames[::2]
                durations = [round((i+1)*58/24)*10-round(i*58/24)*10 for i in range(24)]
                selected[0].save(folder / f'run_{side}_cycle.gif', save_all=True, append_images=selected[1:], duration=durations, loop=0, disposal=2)
                frames[0].save(folder / f'run_{side}_slow.gif', save_all=True, append_images=frames[1:], duration=40, loop=0, disposal=2)
print('Created native contact sheets and full-cycle GIFs.')

