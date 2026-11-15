"""Assemble original detail crops, then exactly cut the runtime 3x3 textures."""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/terrain_repaint_3x3_20261104'
DRAFT = ROOT / 'output/bai_chuan_scenery_20261104/repaint_draft'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paper_median(pixels):
    brightness = pixels.mean(2)
    saturation = pixels.max(2).astype(np.int16) - pixels.min(2)
    mask = (brightness > 200) & (saturation < 65)
    return np.median(pixels[mask], axis=0) if mask.sum() > 250 else np.median(pixels.reshape(-1, 3), axis=0)


def soften_seam(pixels, boundary, axis, radius=14):
    # A narrow interpolation band connects edge colours without rescaling art.
    region = pixels[:, boundary-radius:boundary+radius] if axis == 1 else pixels[boundary-radius:boundary+radius]
    line = np.linspace(0, 1, radius*2, dtype=np.float32)
    weight = np.sin(line * np.pi) ** 2
    if axis == 1:
        reference = region[:, :1] * (1-line)[None, :, None] + region[:, -1:] * line[None, :, None]
        region[:] = np.rint(region * (1-weight)[None, :, None] + reference * weight[None, :, None])
    else:
        reference = region[:1] * (1-line)[:, None, None] + region[-1:] * line[:, None, None]
        region[:] = np.rint(region * (1-weight)[:, None, None] + reference * weight[:, None, None])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--draft', action='store_true')
    args = parser.parse_args()
    jobs = json.loads((BASE / 'jobs.json').read_text(encoding='utf-8'))
    source_paths = [Path(job['native_output']) for job in jobs['jobs']]
    present = [path.exists() for path in source_paths]
    if not args.draft and not all(present):
        raise ValueError('All sixteen original image crops are required before publishing.')
    destination = DRAFT if args.draft else BASE
    destination.mkdir(parents=True, exist_ok=True)
    records, originals = [], []
    with Image.open(jobs['source']) as geographic_source:
        for job, path in zip(jobs['jobs'], source_paths):
            if path.exists():
                with Image.open(path) as image:
                    if image.size != (1254, 1254):
                        raise ValueError(f'Unexpected actual native image dimensions: {path}: {image.size}')
                    pixels = np.array(image.convert('RGB'))
                records.append({'id': job['id'], 'native_path': str(path), 'native_size': [1254,1254],
                                'sha256': digest(path), 'draft_fallback': False})
            elif args.draft:
                # Reference-only fill makes incomplete geography review possible.
                pixels = np.array(geographic_source.crop(job['source_box']).resize((1254,1254), Image.Resampling.LANCZOS).convert('RGB'))
                records.append({'id': job['id'], 'draft_fallback': True})
            originals.append(pixels)
    medians = [paper_median(pixels) for pixels, record in zip(originals, records) if not record['draft_fallback']]
    target = np.median(medians, axis=0) if medians else np.array([232,225,207])
    composite = np.empty((5016,5016,3), dtype=np.uint8)
    for job, pixels, record in zip(jobs['jobs'], originals, records):
        offset = np.zeros(3, dtype=np.int16) if record['draft_fallback'] else np.rint(np.clip(target-paper_median(pixels), -6, 6)).astype(np.int16)
        coloured = np.clip(pixels.astype(np.int16)+offset, 0, 255).astype(np.uint8)
        y, x = job['row']*1254, job['column']*1254
        composite[y:y+1254,x:x+1254] = coloured
        record['paper_rgb_offset'] = offset.tolist()
    for boundary in (1254,2508,3762): soften_seam(composite, boundary, 1)
    for boundary in (1254,2508,3762): soften_seam(composite, boundary, 0)
    image = Image.fromarray(composite)
    master = destination / ('draft_master_5016.png' if args.draft else 'master_8000.png')
    image.save(master)
    preview = image.resize((2090,2090), Image.Resampling.LANCZOS)
    preview.save(destination / 'preview_2090.png')
    tiles = []
    if not args.draft:
        tiles_folder = destination / 'tiles'
        tiles_folder.mkdir(exist_ok=True)
        for row in range(3):
            for column in range(3):
                box = (column*1672,row*1672,(column+1)*1672,(row+1)*1672)
                tile = image.crop(box)
                path = tiles_folder / f'bc_r{row}c{column}.png'
                tile.save(path)
                with Image.open(path) as saved:
                    if ImageChops.difference(tile, saved).getbbox() is not None:
                        raise ValueError('Published tile differs from assembled original pixels.')
                tiles.append({'file': str(path), 'size': [1672,1672], 'source_box': box, 'sha256': digest(path), 'pixel_mismatches': 0})
    record = {'draft': args.draft, 'tool': 'built-in image_gen', 'generated_crops': sum(present),
              'source_detail_grid': [4,4], 'runtime_grid': [3,3], 'native_crop_size': [1254,1254],
              'assembled_pixels': [5016,5016], 'runtime_tile_size': [1672,1672],
              'world_kilometres': [8000,8000], 'virtual_canvas': [7372.8,7372.8],
              'spatial_upscaling_used': False, 'paper_channel_offset_limit': 6,
              'seam_feather_pixels_per_side': 14, 'master_sha256': digest(master),
              'source_records': records, 'runtime_tiles': tiles}
    (destination/'assembly_manifest.json').write_text(json.dumps(record, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'draft': args.draft, 'generated': sum(present), 'master': str(master), 'master_sha256': record['master_sha256']}))


if __name__ == '__main__':
    main()
