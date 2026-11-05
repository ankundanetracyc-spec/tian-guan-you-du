"""Pack generated art without changing its design, and bind the revised storage footprints.

Only crop transparent margins, downsample uniformly, and pad to 128 source pixels
per cell. Original art is retained beside the review record. No save is touched.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re
import shutil

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / 'docs/美术规范/储物戒指与行囊尺度_20261104'
CATALOG = ROOT / 'docs/美术规范/行囊全物品图标与占地_20261029'


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def dump(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    jobs = load(REVIEW / '生成记录.json')
    assert len(jobs) == 13 and len({j['file'] for j in jobs}) == 13
    packed = []
    for job in jobs:
        source = Image.open(job['generated_file']).convert('RGBA')
        alpha = source.getchannel('A')
        assert alpha.getextrema()[0] == 0 and alpha.getpixel((0, 0)) == 0, job['file']
        bounds = alpha.point(lambda a: 255 if a >= 2 else 0).getbbox()
        assert bounds is not None
        cropped = source.crop(bounds)
        width, height = (n * 128 for n in job['cells'])
        scale = min(width * .88 / cropped.width, height * .88 / cropped.height)
        assert scale <= 1, 'Upscaling prohibited: ' + job['file']
        fitted = (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale)))
        resized = cropped.resize(fitted, Image.Resampling.LANCZOS)
        result = Image.new('RGBA', (width, height))
        result.paste(resized, ((width - fitted[0]) // 2, (height - fitted[1]) // 2))
        packed.append((job, result, dict(
            file=job['file'], cells_w=job['cells'][0], cells_h=job['cells'][1],
            pixels_w=width, pixels_h=height, pixels_per_cell=128,
            source='res://' + job['source'], source_w=source.width, source_h=source.height,
            source_used_x=bounds[0], source_used_y=bounds[1], source_used_w=cropped.width, source_used_h=cropped.height,
            fitted_w=fitted[0], fitted_h=fitted[1], usable_fraction=.88, bounds_alpha_threshold=2)))
    if not args.apply:
        print(f'INVENTORY_SCALE_PREFLIGHT_OK: {len(packed)} generated assets; no upscaling; true alpha')
        return

    for job, result, metadata in packed:
        target = ROOT / job['output']; target.parent.mkdir(parents=True, exist_ok=True)
        before = REVIEW / 'before' / job['file']
        if target.exists() and not before.exists():
            before.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(target, before)
        raw = ROOT / job['source']; raw.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(job['generated_file'], raw)
        result.save(target)

    by_name = {j['file']: j for j in jobs}
    manifest_path = CATALOG / '物品占地与图标清单.json'
    manifest = load(manifest_path)
    for row in manifest:
        job = by_name.get(row['new_icon'].rsplit('/', 1)[-1])
        if not job:
            continue
        row['width'], row['height'] = job['cells']; row['area'] = row['width'] * row['height']
        row['pixels_w'], row['pixels_h'] = row['width'] * 128, row['height'] * 128
    dump(manifest_path, manifest)

    tasks_path = CATALOG / '生成任务.json'; tasks = load(tasks_path)
    for task in tasks:
        job = by_name.get(task['file'])
        if job:
            task['cells'] = job['cells']; task['source'] = 'res://' + job['source']
            task['prompt'] = job['prompt']; task['references'] = job['references']
            task['output_source_directory'] = 'textures/items/inventory_scale_20261104/raw/'
    dump(tasks_path, tasks)

    density_path = ROOT / 'textures/items/inventory_density_20261029/density_manifest.json'
    density = load(density_path)
    metadata = {j['file']: m for j, _, m in packed}
    density = [metadata.get(row['file'], row) for row in density]
    dump(density_path, density)

    changed = []; checked = 0
    for path in (ROOT / 'resources').rglob('*.tres'):
        original = path.read_text(encoding='utf-8-sig')
        icon = re.search(r'^icon_path\s*=\s*"([^"]+)"', original, re.M)
        job = by_name.get(icon[1].rsplit('/', 1)[-1]) if icon else None
        if job is None:
            continue
        checked += 1; updated = original
        for field, value in zip(('inventory_width', 'inventory_height'), job['cells']):
            pattern = rf'(?m)^{field}\s*=\s*\d+'
            replacement = f'{field} = {value}'
            if re.search(pattern, updated):
                updated = re.sub(pattern, replacement, updated)
            else:
                updated += '\n' + replacement + '\n'
        if updated != original:
            path.write_text(updated, encoding='utf-8', newline='\n'); changed.append(path.relative_to(ROOT).as_posix())

    plan = ROOT / 'docs/plans/行囊全物品图标与占地规划_20261029.md'
    content = plan.read_text(encoding='utf-8-sig')
    content = content.replace('丹药瓷瓶改1×1', '丹药瓷瓶改1×2')
    note = '2026-11-04修订：以[储物戒指与行囊尺度修订](储物戒指与行囊尺度修订_20261104.md)为当前补充。书册正俯视，丹药瓷瓶改1×2、折衣改2×3；下表已同步。'
    if note not in content:
        content = content.replace('# 全物品图标与占地规划\n', '# 全物品图标与占地规划\n\n' + note + '\n', 1)
    content = content.replace('瓷瓶图（2×3）', '瓷瓶图（1×2）').replace('瓷瓶图（1×1）', '瓷瓶图（1×2）').replace('衣物按折叠形态3×4', '衣物按折叠形态2×3')
    row_by_name = {r['name']: r for r in manifest}
    lines = content.splitlines()
    for i, line in enumerate(lines):
        if not line.startswith('| '):
            continue
        cols = line.split('|')
        row = row_by_name.get(cols[1].strip())
        if row and row['new_icon'].rsplit('/', 1)[-1] in by_name:
            cols[3] = ' 方形 ' if row['width'] == row['height'] else ' 竖长矩形 '
            cols[4] = f" {row['width']}×{row['height']} "; cols[5] = f" {row['area']} "
            cols[6] = f" {row['pixels_w']}×{row['pixels_h']} "
            lines[i] = '|'.join(cols)
    plan.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    dump(REVIEW / '接入结果.json', {'generated_assets': len(packed), 'checked_resources': checked,
        'changed_resources': changed, 'pixels_per_cell': 128, 'design_cell': 48,
        'source_mode': 'built-in image_gen', 'packed_assets': [m for _, _, m in packed]})
    print(f'INVENTORY_SCALE_APPLIED_OK: {len(packed)} assets, {checked} resource bindings checked, {len(changed)} footprints changed')


if __name__ == '__main__':
    main()
