"""Assemble contact sheets from actual Godot viewport captures, never source art."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent / 'output'
prefix = 'yuxu_run_motion_final'
phases = {'idle': [0,408,817,1225], 'run': [30,145,267,385,500], 'attack':[20,67,130], 'step':[30,83,140]}
for action, samples in phases.items():
    contact = Image.new('RGB',(330*len(samples), 450*2),(234,231,220))
    draw = ImageDraw.Draw(contact)
    for row, side in enumerate(['','_left']):
        for col, phase in enumerate(samples):
            capture = Image.open(root / f'{prefix}_standalone_review_{action}_t{phase:03d}{side}.png')
            crop = capture.crop((345,382,455,522)).resize((330,420), Image.Resampling.NEAREST)
            contact.paste(crop, (col*330,row*450))
            draw.text((col*330+10,row*450+426),f'{action} {phase/1000:.3f}s / {"left" if side else "right"}',fill=(35,40,38))
    contact.save(root/f'{prefix}_{action}_contact.png')
for side in ['right','left']:
    frames=[Image.open(root/f'{prefix}_cycle_{side}_{frame:02d}.png').resize((360,420),Image.Resampling.NEAREST) for frame in range(24)]
    frames[0].save(root/f'{prefix}_run_{side}.gif',save_all=True,append_images=frames[1:],duration=[20,30,20,30,20,30]*4,loop=0,disposal=2)
print('Created four direction/phase sheets and two 24-frame run GIFs from native Godot screenshots.')
