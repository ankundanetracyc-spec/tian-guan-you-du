"""Assemble this batch's clean prompts from the audited subjects, never old style fragments."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / 'docs/美术规范/行囊全物品图标与占地_20261029'


def main():
    base = (DOC / '统一画风提示词.txt').read_text(encoding='utf-8').strip() + ' '
    style = str(DOC / '风格参考_用户指定瓷瓶.png')
    tasks = json.loads((DOC / '生成任务.json').read_text(encoding='utf-8'))
    subjects = json.loads((DOC / '物品形态语义审计.json').read_text(encoding='utf-8'))
    root_jobs = {r['file']: r for r in json.loads((DOC / '生成记录_根任务.json').read_text(encoding='utf-8'))}
    clothing_record = DOC / '生成记录_衣物剑.json'
    clothing_jobs = {r['file']: r for r in json.loads(clothing_record.read_text(encoding='utf-8'))['jobs']} if clothing_record.exists() else {}
    for job in tasks:
        name = job['file']
        w, h = job['cells']
        job['style_reference'] = style
        job['output_source_directory'] = 'textures/items/inventory_density_20261029/raw_unified_final/'
        job['references'] = [style]
        if name in root_jobs:
            job['prompt'] = root_jobs[name]['prompt']
        elif name in subjects:
            job['subject_en'] = subjects[name]['subject_en']
            job['semantic_notes'] = subjects[name]['notes']
            job['prompt'] = base + 'Subject: ' + job['subject_en'] + f' Compose upright within a {w}:{h} canvas. Final inventory footprint {w}x{h}, packed dimensions {w*128}x{h*128}. Keep the material compact, complete and recognizable with broad calm local-color planes, no scenery or extra props.'
            if name == 'mat_frostdew_leaf.png':
                job['prompt'] += ' Frost is a sparse grey-white pigment wash, no glass beads or white glitter.'
        elif name in clothing_jobs:
            job['prompt'] = clothing_jobs[name]['prompt']
            job['references'] = clothing_jobs[name]['references']
        else:
            raise ValueError('Missing clean prompt: ' + name)
    # Persist the actual final prompt after a worker's targeted visual revision.
    by_file = {row['file']: row for row in tasks}
    for filename in ['生成记录_衣物剑.json', '生成记录_材料甲.json', '生成记录_材料乙.json', '生成记录_材料补位.json']:
        path = DOC / filename
        if not path.exists():
            continue
        record = json.loads(path.read_text(encoding='utf-8'))
        entries = record.get('jobs', []) if isinstance(record, dict) else record
        for entry in entries:
            if entry.get('file') in by_file and entry.get('prompt') and entry.get('status') not in ['pending', 'delegated']:
                by_file[entry['file']]['prompt'] = entry['prompt']
                if entry.get('references'):
                    by_file[entry['file']]['references'] = entry['references']
    (DOC / '生成任务.json').write_text(json.dumps(tasks,ensure_ascii=False,indent=2),encoding='utf-8')
    initial_root_materials = {'mat_return_spring_pearl.png', 'mat_moonmist_mushroom_juice.png', 'spirit_stone_1x1.png', 'xuantie_3x3.png'}
    material = [r for r in tasks if r['file'] in subjects and r['file'] not in initial_root_materials]
    assert len(material) == 46
    for label, rows in [('材料甲',material[:23]),('材料乙',material[23:])]:
        (DOC / f'生成分工_{label}.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Clean prompts: 65 inventory assets, material groups 23+23; animation pill stays separate.')


if __name__ == '__main__':
    main()
