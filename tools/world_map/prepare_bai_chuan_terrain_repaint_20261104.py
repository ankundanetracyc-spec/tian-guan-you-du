"""Prepare precise geographic references for a new painting, rendered as 3x3."""
from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'textures/world/bai_chuan/free_world_8000_3x3_20261104/master_8000.png'
BASE = ROOT / 'textures/world/bai_chuan/terrain_repaint_3x3_20261104'

STYLE = '''Use case: style-transfer. Asset: original terrain background for a Chinese cultivation RPG world map.
The single input image is an EXACT geographic crop, not a style example. Completely redraw its painting, keeping its exact framing, all river/lake/coast silhouettes and their edge-crossing positions, and the positions/footprints of mountain masses and open passes. Do not zoom, pan, expand the view, change river widths or move coastlines.
New art direction: refined hand-painted oblique cartography on warm light parchment. A high bird's-eye map view with no horizon. Simplified irregular connected rocky massifs, earthy grey/ochre stone planes, mineral jade-green foothill washes, very restrained crisp dark-ink contours, soft atmospheric feet. Give rock faces several broad confident brush planes, not dense etched hatching. Mountain shapes should feel naturally varied while remaining in their geographic footprints.
Remove all baked-in individual trees, dense tree clusters, bushes and ground clutter. Where woodland existed, leave broad quiet pale sage-green meadow/soil washes suitable for separate tree sprites. Keep the existing mountain masses; clearing woodland must not remove mountains. Valleys and plains should be calm low-detail areas with large readable open space and sparse broad wash variation. No uniform stippling, repeated grass flecks, pebbles carpet, diagonal filler hatching, scratches or noisy paper texture.
Water stays within the exact reference channels: muted mineral grey-teal, lower contrast than buildings would be, almost flat gentle wash with a few broad currents. Preserve natural banks and their silhouette. Avoid bright cyan, white wave stripes and shining water.
Palette: warm ivory paper, subdued moss/sage and jade greens, earthy grey-brown rock, quiet grey-teal water. Coloured hand-painted terrain with elegant ink edges; no monochrome grey filter, no neon, no photorealism, no 3D render. Keep source edge features in place so adjacent crops can join. Artwork extends to every image edge, without any border.
No buildings, people, characters, animals, signs, icons, text, numbers, map labels, UI, roads, bridges or paths. These are placed by the game separately. Do not add new subjects, new water or isolated rocks in existing open travel corridors.
Output one square, detailed but deliberately calm game terrain crop. Clear silhouettes and large colour shapes should remain readable at gameplay size.'''


def main():
    (BASE / 'references').mkdir(parents=True, exist_ok=True)
    (BASE / 'native_4x4').mkdir(exist_ok=True)
    (BASE / 'prompts').mkdir(exist_ok=True)
    with Image.open(SOURCE) as source:
        width, height = source.size
        boundaries = [round(i * width / 4) for i in range(5)]
        jobs = []
        for row in range(4):
            for column in range(4):
                name = f'r{row}c{column}'
                box = (boundaries[column], boundaries[row], boundaries[column + 1], boundaries[row + 1])
                reference = BASE / 'references' / f'{name}.png'
                source.crop(box).save(reference)
                prompt = STYLE + f'\nGeographic crop identifier: {name}. The reference is exactly this crop only.'
                job = {'id': name, 'row': row, 'column': column,
                       'reference': str(reference), 'source_box': box,
                       'prompt': prompt, 'transparent_background': False,
                       'native_output': str(BASE / 'native_4x4' / f'{name}.png')}
                jobs.append(job)
                (BASE / 'prompts' / f'{name}.json').write_text(json.dumps(job, ensure_ascii=False, indent=2), encoding='utf-8')
    record = {'source': str(SOURCE), 'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
              'runtime_grid': [3, 3], 'source_detail_grid': [4, 4],
              'world_kilometres': [8000, 8000], 'virtual_canvas': [7372.8, 7372.8],
              'reason': 'Keep sufficient original raster detail while delivering nine runtime textures.',
              'jobs': jobs}
    (BASE / 'jobs.json').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'base': str(BASE), 'jobs': len(jobs), 'runtime_grid': [3, 3]}))


if __name__ == '__main__':
    main()
