"""Measure native alpha regions without repainting or resampling generated art."""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'textures/world/bai_chuan/ground_details_20261106'
IDS = ['meadow_wash', 'moss_wash', 'sand_wash', 'grass_green', 'grass_dry',
       'wildflowers', 'pebbles', 'flat_stones', 'shore_reeds']


def main():
    source = ART / 'ground_details_native.png'
    pixels = np.asarray(Image.open(source).convert('RGBA'))
    height, width = pixels.shape[:2]
    assets = {}
    for index, ident in enumerate(IDS):
        row, column = divmod(index, 3)
        left, right = column * width // 3, (column + 1) * width // 3
        top, bottom = row * height // 3, (row + 1) * height // 3
        alpha = pixels[top:bottom, left:right, 3]
        yy, xx = np.nonzero(alpha >= 16)
        assert len(xx) and max(alpha[0].max(), alpha[-1].max(), alpha[:, 0].max(), alpha[:, -1].max()) <= 2
        x0, x1 = max(0, int(xx.min()) - 8), min(right-left, int(xx.max()) + 9)
        y0, y1 = max(0, int(yy.min()) - 8), min(bottom-top, int(yy.max()) + 9)
        assets[ident] = {
            'region': [left + x0, top + y0, x1-x0, y1-y0],
            'native_size': [x1-x0, y1-y0], 'pivot': [0.5, 0.5],
            'width_range': [135, 220] if index < 3 else [70, 115],
            'opacity_range': [0.34, 0.56] if index < 3 else [0.72, 0.95],
            'layer': 'ground_decals', 'z': 24,
            'cell': [left, top, right-left, bottom-top],
            'cell_edge_alpha_max': int(max(alpha[0].max(), alpha[-1].max(), alpha[:, 0].max(), alpha[:, -1].max())),
        }
    result = {
        'version': 1, 'mode': 'built_in_imagegen',
        'atlas': 'res://textures/world/bai_chuan/ground_details_20261106/ground_details_native.png',
        'runtime_atlas': 'res://textures/world/bai_chuan/ground_details_20261106/ground_details_native.res',
        'atlas_size': [width, height], 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
        'source_unchanged': True, 'assets': assets,
    }
    (ART / 'catalog.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'assets': len(assets), 'native_atlas': [width, height], 'source_sha256': result['source_sha256']}))


if __name__ == '__main__':
    main()
