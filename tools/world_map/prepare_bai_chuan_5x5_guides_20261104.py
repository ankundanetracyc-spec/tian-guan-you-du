"""Exact core/context references for 25 native AI regional refinements."""
from pathlib import Path
import json
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/free_world_8000_20261104'
OUT = BASE / 'references_5x5/shared_guides'
SOURCE = BASE / 'master_1254_native_v1.png'
STYLE = BASE / 'references/style_user_20261104.png'

PROMPT = '''Use case: stylized-concept. Asset: production 2D ink-wash overworld terrain tile for an 8000 km square cultivation world. This request is ONE precise square tile in a shared 5 by 5 map. Input image 1 is the exact CORE GEOMETRY and edit target for this tile. Input image 2 is its surrounding geographic context, for understanding river continuity only. Input image 3 is the full continuous world reference. Input image 4 is the user's desired STYLE reference; ignore every UI panel, text, building, dotted road and person in it.
Produce a newly painted native high-detail square terrain tile, at the largest native square resolution the tool supports. Refine the exact scene of image 1 into crisp, delicate traditional Chinese ink and subtle mineral watercolor on warm ivory-grey rice paper. Fine dark grey-green mountain outlines and rock striations, restrained forest canopies, very pale blue rivers, spacious plains with gentle light ground washes. Improve native brush detail, paper fibers and clear contours without changing cartographic feature scale. Tiny distant mountains remain tiny; preserve the height and footprints of all existing hills. The view remains a high oblique cartographic landscape, not a close-up illustration.
GEOGRAPHIC INVARIANTS: exactly preserve river and coast centerlines, river widths, lake outlines, mountain-group positions, forest placement, proportions and camera framing from CORE image 1. The output must depict precisely that same crop, including all four boundary cuts. Water crossing an edge must meet the identical normalized edge position with the same width and tangent as the core. No new branch, lake, island, coastline, bridge or road. Preserve spacious walkable pale ground. Use shared full-map lighting, paper colour and restrained saturation. Do not enlarge mountains or create a wall of peaks.
The outermost boundary is for seamless adjacency to the same full world, so keep boundary geometry and tone particularly faithful. Do not include a border, vignette, frame, tiled grid, labels, letters, numerals, symbols, city or sect buildings, characters, interface, path nodes, or dotted road lines. Deliver only the clean terrain artwork. Tile identity (for coordination only; never paint it): {tile}, row {row} column {col}.'''


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    image = Image.open(SOURCE).convert('RGB')
    width, height = image.size
    jobs = []
    for row in range(5):
        for col in range(5):
            rect = (round(col * width / 5), round(row * height / 5), round((col + 1) * width / 5), round((row + 1) * height / 5))
            tile = f'bc_r{row}c{col}'
            core = OUT / f'{tile}_core.png'
            context = OUT / f'{tile}_context.png'
            image.crop(rect).save(core)
            bleed = 25
            context_rect = (max(0, rect[0] - bleed), max(0, rect[1] - bleed), min(width, rect[2] + bleed), min(height, rect[3] + bleed))
            image.crop(context_rect).save(context)
            jobs.append({'tile': tile, 'row': row, 'col': col, 'rect': rect, 'context_rect': context_rect,
                         'core': str(core), 'context': str(context), 'full_reference': str(SOURCE), 'style_reference': str(STYLE),
                         'prompt': PROMPT.format(tile=tile, row=row, col=col),
                         'native_output': str(BASE / 'tiles_5x5_native' / f'{tile}.png')})
    (OUT / 'jobs.json').write_text(json.dumps(jobs, ensure_ascii=False, indent=2), encoding='utf-8')
    print(OUT / 'jobs.json')


if __name__ == '__main__':
    main()
