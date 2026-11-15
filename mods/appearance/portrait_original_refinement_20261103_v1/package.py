"""Package candidate assets and provenance without modifying image data."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

HERE = Path(__file__).resolve().parent
qa = json.loads((HERE / 'qa/assets.json').read_text(encoding='utf-8'))
manifest = {
    'package_id': HERE.name, 'enabled': False,
    'status': 'visual_candidate_not_installed',
    'canvas': [1024,1536], 'origin': [0,0], 'runtime_offset': [0,0],
    'draw_order_back_to_front': ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front'],
    'new_generated_parts': 4, 'retained_source_parts': 14,
    'tool': 'built-in image_gen',
    'registration_certification': 'not passed: side contours differ from reference; full three-control-point residual <=2px is not asserted',
    'documentation': '../../../docs/美术规范/原版基础立绘优化_20261103/README.md',
    'assets': [{k:r[k] for k in ['gender','part','file','width','height','sha256','retained_bytes_equal']} for r in qa['records']],
    'prompt_sets': ['work_male/prompts.json','work_male/tone_prompt.json','work_female/prompts.json'],
}
(HERE / 'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
browser = {
    'browser': 'Microsoft Edge via Playwright CLI', 'pass': True,
    'verified': ['male/female switching','warm/dark/checker backdrop','reference anchors','eight nonempty visible layers','old/new front and back switches','reset restores identical pixels','transparent PNG download'],
    'empty_accessory': 'retained blank default; no visual difference expected',
    'export': {'file':'browser_export.png','width':1024,'height':1536,'rgba':True},
    'latest_smoke': {'ready':True,'gender':'female','backdrop':True,'anchors':True,'reset':True,'canvases':2},
    'screenshot':'preview_browser.png', 'game_runtime_tested':False,
}
(HERE / 'qa/browser.json').write_text(json.dumps(browser,indent=2),encoding='utf-8')
shot = HERE.parents[2] / 'output/playwright/portrait_original_refinement/preview.png'
if shot.exists(): shutil.copy2(shot,HERE / 'qa/preview_browser.png')
files = list((HERE/'parts').rglob('*.png'))
files += [HERE/'预览.html',HERE/'portrait_manifest.json',HERE/'source_records.json']
files += [HERE/'preview'/n for n in ['pair.png','pair_dark.png','face_compare.png','face_compare_dark.png','before_after.png']]
files += [HERE/n for n in ['work_male/prompts.json','work_male/tone_prompt.json','work_male/measurement.json','work_female/prompts.json','work_female/qa.json','qa/assets.json','qa/preview_sources.json','qa/browser.json','qa/browser_export.png','qa/preview_browser.png']]
archive = HERE/'男女基础立绘新版_独立部件.zip'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files:
        if p.exists(): z.write(p,p.relative_to(HERE).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert sum(n.startswith('parts/') and n.endswith('.png') for n in z.namelist()) == 18
    count=len(z.namelist())
print(json.dumps({'zip':str(archive),'entries':count,'bytes':archive.stat().st_size,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest()},ensure_ascii=False))
