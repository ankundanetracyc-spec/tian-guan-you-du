"""Publish 25 native tiles with bounded paper correction and 16px seam bands."""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/free_world_8000_20261104'
NATIVE = BASE / 'tiles_5x5_native'
TILES = BASE / 'tiles'
SIDE = 1254
BAND = 16


def main():
    medians = []
    sources = []
    for row in range(5):
        for col in range(5):
            path = NATIVE / f'bc_r{row}c{col}.png'
            with Image.open(path) as image:
                if image.size != (SIDE, SIDE): raise ValueError(f'Invalid native dimensions: {path}')
                sample = np.asarray(image.convert('RGB').resize((256, 256)), np.float32)
            luminance = sample.mean(2)
            paper = (luminance >= np.percentile(luminance, 78)) & (sample[:, :, 0] >= sample[:, :, 2] - 3)
            medians.append(np.median(sample[paper], axis=0))
            sources.append({'tile': path.stem, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    target = np.median(np.asarray(medians), axis=0)
    canvas = np.empty((SIDE * 5, SIDE * 5, 3), np.uint8)
    corrections = []
    for index, source in enumerate(sources):
        row, col = divmod(index, 5)
        shift = np.clip(target - medians[index], -6, 6)
        with Image.open(NATIVE / f'{source["tile"]}.png') as image:
            pixels = np.asarray(image.convert('RGB'), np.int16)
        pixels = np.clip(pixels + np.rint(shift).astype(np.int16), 0, 255).astype(np.uint8)
        canvas[row * SIDE:(row + 1) * SIDE, col * SIDE:(col + 1) * SIDE] = pixels
        corrections.append({'tile': source['tile'], 'rgb_additive_shift': np.rint(shift).astype(int).tolist()})
    for boundary in range(SIDE, SIDE * 5, SIDE):
        target_edge = (canvas[:, boundary - 1].astype(np.float32) + canvas[:, boundary].astype(np.float32)) * .5
        for distance in range(BAND):
            alpha = (1 - distance / BAND) ** 2
            for column in [boundary - 1 - distance, boundary + distance]:
                canvas[:, column] = np.rint(canvas[:, column].astype(np.float32) * (1 - alpha) + target_edge * alpha).astype(np.uint8)
    for boundary in range(SIDE, SIDE * 5, SIDE):
        target_edge = (canvas[boundary - 1].astype(np.float32) + canvas[boundary].astype(np.float32)) * .5
        for distance in range(BAND):
            alpha = (1 - distance / BAND) ** 2
            for row in [boundary - 1 - distance, boundary + distance]:
                canvas[row] = np.rint(canvas[row].astype(np.float32) * (1 - alpha) + target_edge * alpha).astype(np.uint8)
    image = Image.fromarray(canvas)
    image.save(BASE / 'master_8000.png')
    image.resize((2090, 2090), Image.Resampling.LANCZOS).save(BASE / 'preview_2090.png')
    TILES.mkdir(exist_ok=True)
    mismatches = 0
    for row in range(5):
        for col in range(5):
            box = (col * SIDE, row * SIDE, (col + 1) * SIDE, (row + 1) * SIDE)
            path = TILES / f'bc_r{row}c{col}.png'
            image.crop(box).save(path)
            with Image.open(path) as tile:
                mismatches += int(np.count_nonzero(np.asarray(tile) != canvas[box[1]:box[3], box[0]:box[2]]))
    if mismatches: raise RuntimeError('Published tiles differ from the master')
    audit = {'native_tile_count': 25, 'native_tile_dimensions': [SIDE, SIDE], 'master_dimensions': list(image.size),
             'spatial_resampling': False, 'paper_target_rgb': target.tolist(), 'maximum_channel_shift': 6,
             'paper_corrections': corrections, 'seam_band_pixels_each_side': BAND,
             'feathered_boundaries': {'vertical': 4, 'horizontal': 4},
             'master_to_tile_pixel_mismatches': mismatches, 'native_sources': sources,
             'master_sha256': hashlib.sha256((BASE / 'master_8000.png').read_bytes()).hexdigest()}
    (BASE / 'final_5x5_assembly_audit.json').write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in audit.items() if k not in ['native_sources', 'paper_corrections']}))


if __name__ == '__main__':
    main()
