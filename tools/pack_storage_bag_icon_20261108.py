"""Pack the generated storage pouch at 128 source pixels per cell; preserve native alpha."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'textures/items/storage_bag_20261108'
REVIEW = ROOT / 'docs/美术规范/储物袋迁移与四格图标_20261108'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    args = parser.parse_args()
    with Image.open(args.source) as image:
        source = image.convert('RGBA')
    alpha = source.getchannel('A')
    if alpha.getextrema() != (0, 255) or alpha.getpixel((0, 0)) != 0:
        raise ValueError('Generated source must have native transparency')
    bounds = alpha.point(lambda a: 255 if a >= 2 else 0).getbbox()
    if bounds is None:
        raise ValueError('Empty source')
    cropped = source.crop(bounds)
    factor = min(256 * .88 / cropped.width, 256 * .88 / cropped.height)
    if factor > 1:
        raise ValueError('Upscaling is prohibited')
    fitted = (max(1, round(cropped.width * factor)), max(1, round(cropped.height * factor)))
    resized = cropped.resize(fitted, Image.Resampling.LANCZOS)
    output = Image.new('RGBA', (256, 256))
    output.paste(resized, ((256 - fitted[0]) // 2, (256 - fitted[1]) // 2))
    (ASSETS / 'raw').mkdir(parents=True, exist_ok=True)
    REVIEW.mkdir(parents=True, exist_ok=True)
    shutil.copy2(args.source, ASSETS / 'raw/storage_bag.png')
    output.save(ASSETS / 'storage_bag_2x2.png')
    metadata = dict(file='storage_bag_2x2.png', cells_w=2, cells_h=2, pixels_w=256, pixels_h=256,
                    pixels_per_cell=128, source='res://textures/items/storage_bag_20261108/raw/storage_bag.png',
                    source_w=source.width, source_h=source.height, source_bounds=list(bounds),
                    fitted_w=fitted[0], fitted_h=fitted[1], usable_fraction=.88,
                    bounds_alpha_threshold=2, alpha_extrema=list(output.getchannel('A').getextrema()))
    (REVIEW / '图标装配.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('STORAGE_BAG_ICON_PACK_OK: 2x2 cells, 256x256 RGBA; uniform downsample, native alpha')


if __name__ == '__main__':
    main()
