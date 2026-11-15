"""Repackage the approved painting into nine exact tiles; never repaint/resample."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'textures/world/bai_chuan/free_world_8000_20261104'
DESTINATION = ROOT / 'textures/world/bai_chuan/free_world_8000_3x3_20261104'
EXPECTED_SHA256 = '385f5ebf0898111767b4738c14e3deb071c1a32873aa05a633d027af88574cb9'


def main():
    master = SOURCE / 'master_8000.png'
    digest = hashlib.sha256(master.read_bytes()).hexdigest()
    if digest != EXPECTED_SHA256:
        raise ValueError('Approved source painting changed; inspect before repackaging.')
    tiles_dir = DESTINATION / 'tiles'
    tiles_dir.mkdir(parents=True, exist_ok=True)
    target_master = DESTINATION / 'master_8000.png'
    shutil.copy2(master, target_master)
    records = []
    with Image.open(master) as source:
        if source.size != (6270, 6270):
            raise ValueError(f'Unexpected source dimensions {source.size}')
        for row in range(3):
            for column in range(3):
                box = (column * 2090, row * 2090, (column + 1) * 2090, (row + 1) * 2090)
                tile_path = tiles_dir / f'bc_r{row}c{column}.png'
                tile = source.crop(box)
                tile.save(tile_path)
                with Image.open(tile_path) as saved:
                    differences = ImageChops.difference(tile, saved)
                    if differences.getbbox() is not None:
                        raise ValueError(f'Tile pixel mismatch: {tile_path.name}')
                records.append({'file': tile_path.relative_to(ROOT).as_posix(),
                                'size': [2090, 2090], 'source_rectangle': box,
                                'sha256': hashlib.sha256(tile_path.read_bytes()).hexdigest(),
                                'pixel_mismatches': 0})
    shutil.copy2(SOURCE / 'preview_2090.png', DESTINATION / 'preview_2090.png')
    record = {
        'purpose': 'User requests a more compact 3x3 world using the same painting style.',
        'world_kilometres': [8000, 8000],
        'previous_virtual_canvas': [12288, 12288],
        'virtual_canvas': [7372.8, 7372.8],
        'linear_display_scale': 0.6,
        'grid': [3, 3],
        'tile_pixels': [2090, 2090],
        'assembled_pixels': [6270, 6270],
        'operation': 'Exact crop/retiling only, without image resizing or new image generation.',
        'painting_source': master.relative_to(ROOT).as_posix(),
        'master_sha256': digest,
        'original_generation': 'Built-in image_gen; 25 independently generated 1254px regions, previously assembled and seam-treated.',
        'original_prompt_record': (SOURCE / 'generation_prompts_final_5x5.json').relative_to(ROOT).as_posix(),
        'original_assembly_record': (SOURCE / 'final_5x5_assembly_audit.json').relative_to(ROOT).as_posix(),
        'tiles': records,
    }
    (DESTINATION / 'retiling_3x3_manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'path': str(DESTINATION), 'tiles': len(records), 'tile_pixels': 2090,
                      'world_km': 8000, 'virtual_canvas': 7372.8, 'pixel_mismatches': 0}))


if __name__ == '__main__':
    main()
