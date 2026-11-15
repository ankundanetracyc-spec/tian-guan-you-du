"""Audit rendered floor contacts and compare the exact recorded input sequence."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

root = Path(__file__).resolve().parent / 'output'
current = root / 'yuxu_grounded_v20_arena'
previous = root / 'yuxu_arm_motion_v19_arena'
records = [json.loads(line) for line in (current / 'frames.jsonl').read_text().splitlines()]
assert len(records) == 360
assert all(row['facing'] == row['expected_facing'] for row in records)
sole_error = max(abs(foot['sole_y'] + foot['lift']) for row in records for foot in row['feet'].values())
floor_penetration = max(foot['sole_y'] for row in records for foot in row['feet'].values())
worst_support_gap = max(-max(foot['sole_y'] for foot in row['feet'].values()) for row in records)
assert sole_error < .02 and floor_penetration < .02 and worst_support_gap < .02
summary = {
    'rendered_frames': len(records),
    'duration_seconds': 12,
    'all_facings_match': True,
    'maximum_visible_sole_height_error_rig_units': sole_error,
    'maximum_floor_penetration_rig_units': floor_penetration,
    'maximum_support_foot_gap_rig_units': worst_support_gap,
    'forward_run_chest_degrees': [min(r['torso_degrees'] for r in records[36:90]), max(r['torso_degrees'] for r in records[36:90])],
    'backward_walk_chest_degrees': [min(r['torso_degrees'] for r in records[180:204]), max(r['torso_degrees'] for r in records[180:204])],
}
(current / 'ground_contact_review.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
frames = []
for index in range(0, 360, 2):
    frame = Image.new('RGB', (880, 525), '#58646a')
    frame.paste(Image.open(previous / f'detail_{index:04d}.png').convert('RGB'), (0, 25))
    frame.paste(Image.open(current / f'detail_{index:04d}.png').convert('RGB'), (440, 25))
    label = ImageDraw.Draw(frame)
    label.text((12, 8), f'v19 / {index / 30:.2f}s', fill='white')
    label.text((452, 8), 'v20 / overlap, planted soles, weight transfer', fill='white')
    frames.append(frame.quantize(colors=96, dither=Image.Dither.NONE))
durations = [round((i + 1) * 100 / 15) * 10 - round(i * 100 / 15) * 10 for i in range(len(frames))]
frames[0].save(current / 'before_after_12s.gif', save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2)
print(json.dumps(summary, ensure_ascii=False))
