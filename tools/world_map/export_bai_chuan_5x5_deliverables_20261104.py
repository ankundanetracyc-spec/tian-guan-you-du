"""Copy selected map deliverables and exact prompt records to the chat outputs."""
from pathlib import Path
import hashlib
import json
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/free_world_8000_20261104'
DESTINATION = Path(r'E:\新建文件夹 (2)\2026-11-04\new-chat\outputs\百川8000公里_5x5')


def main():
    master = BASE / 'master_8000.png'
    with Image.open(master) as image:
        if image.size != (6270, 6270):
            raise RuntimeError('Final 5x5 master has not been published.')
    jobs = json.loads((BASE / 'references_5x5/shared_guides/jobs.json').read_text(encoding='utf-8'))
    final_prompt_file = BASE / 'generation_prompts_final_5x5.json'
    exact_jobs = {job['tile']: job for job in json.loads(final_prompt_file.read_text(encoding='utf-8'))['jobs']} if final_prompt_file.exists() else {}
    records = []
    for job in jobs:
        native = Path(job['native_output'])
        with Image.open(native) as image:
            size = list(image.size)
            if size != [1254, 1254]:
                raise RuntimeError(f'Unexpected native tile: {native}: {size}')
        records.append({'tile': job['tile'], 'native_dimensions': size,
                        'native_sha256': hashlib.sha256(native.read_bytes()).hexdigest(),
                        'native_file': str(native), 'tool': 'built-in image_gen',
                        'prompt': exact_jobs.get(job['tile'], job)['prompt'],
                        'referenced_image_paths': exact_jobs.get(job['tile'], job)['referenced_image_paths']})
    DESTINATION.mkdir(parents=True, exist_ok=True)
    shutil.copy2(master, DESTINATION / '百川全图_6270.png')
    preview = BASE / 'preview_2090.png'
    if preview.exists(): shutil.copy2(preview, DESTINATION / '百川全图_预览.png')
    result = {'tool': 'built-in image_gen', 'grid': [5, 5], 'world_kilometres': [8000, 8000],
              'virtual_map_pixels': [12288, 12288], 'master_native_dimensions': [6270, 6270],
              'master_sha256': hashlib.sha256(master.read_bytes()).hexdigest(),
              'note': '25 independently generated 1254px native tiles. Final assembly colour and seam processing are documented in the project art record.',
              'tiles': records}
    (DESTINATION / '生成提示词与尺寸记录.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    if final_prompt_file.exists(): shutil.copy2(final_prompt_file, DESTINATION / '最终逐块提示词.json')
    master_prompt = BASE / 'generation_prompt_master_v1.txt'
    if master_prompt.exists(): shutil.copy2(master_prompt, DESTINATION / '母图生成提示词.txt')
    print(DESTINATION)


if __name__ == '__main__':
    main()
