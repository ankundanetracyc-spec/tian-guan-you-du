from pathlib import Path
from PIL import Image
import numpy as np
import hashlib
import json
import shutil

ROOT = Path(r'E:/stacklands/mods/appearance/portrait_three_samples_20261103')
FIXED = Path(r'E:/stacklands/mods/appearance/portrait_modular_sample_20261103/parts')
GEN = Path(r'E:/tmp/CodexData/generated_images/01a1a1ae-f744-70a3-8368-2201c0206eae')
SIZE = (1024, 1536)
NAMES = {
    'hair_front': 'exec-976f21f5-9ff1-4308-a353-d5d440ad0d4e.png',
    'hair_back': 'exec-2a4caaee-0043-4de2-b17e-3c499dd5594e.png',
    'eyes': 'exec-4b70808f-efdc-4593-9410-61b7586f425e.png',
    'eyebrows': 'exec-f3191ac4-030a-45bd-a528-5b39c4a1c0a5.png',
    'nose': 'exec-3e9d040a-9fad-4950-8551-a52aae4f72f0.png',
    'mouth': 'exec-c4370982-fc24-43d8-a749-906a4fdcaff7.png',
}

for folder in ['source/set02', 'preview/set02', 'qa/set02']:
    (ROOT / folder).mkdir(parents=True, exist_ok=True)

def paired(src, dst):
    s = (dst[1][0] - dst[0][0]) / (src[1][0] - src[0][0])
    offset = np.asarray(dst).mean(axis=0) - np.asarray(src).mean(axis=0) * s
    return float(s), [float(x) for x in offset]

eye_s, eye_offset = paired([(406.07540983606555, 618.9508196721312), (843.4514637904468, 618.8921417565485)], [(464, 379), (574, 379)])
brow_s, brow_offset = paired([(387.96996165317427, 624.1448657861099), (860.4105632349907, 624.207551062513)], [(457, 354), (582, 353)])
CONFIG = {
    'hair_front': {'scale': .4, 'translation': [313.4, 101.0], 'anchors': {'crown_source': [514, 220], 'crown_target': [519, 189], 'soft_off_center_hairline_source': [549, 440], 'soft_off_center_hairline_target': [533, 277]}, 'method': 'Registration from the anatomical crown and visible hairline; whole-hair bounding-box center is not used. Lateral placement corrected to cover both scalp sides, preserving the slight off-center part.'},
    'hair_back': {'scale': .45, 'translation': [291.05, 90.0], 'anchors': {'crown_source': [512, 215], 'crown_target': [521.45, 186.75], 'low_bun_source': [631, 900], 'low_bun_target': [575, 495]}, 'method': 'Posterior crown plus low nape-bun position; the low bun is allowed to protrude behind the right neck. Not centered on the entire hair bounding box.'},
    'eyes': {'scale': eye_s, 'translation': eye_offset, 'anchors': {'source_pupils': [[406.07540983606555, 618.9508196721312], [843.4514637904468, 618.8921417565485]], 'target_pupils': [[464, 379], [574, 379]]}, 'method': 'Dark-pupil centroid within manually inspected pupil-only windows, followed by actual face composition.'},
    'eyebrows': {'scale': brow_s, 'translation': brow_offset, 'anchors': {'source_brow_centers': [[387.96996165317427, 624.1448657861099], [860.4105632349907, 624.207551062513]], 'target_brow_centers': [[457, 354], [582, 353]]}, 'method': 'Individually inspected brow-body centroids followed by visual placement on the fixed face.'},
    'nose': {'scale': .13, 'translation': [519 - .13 * 625, 446 - .13 * 870], 'anchors': {'source_tip': [625, 870], 'target_tip': [519, 446]}, 'method': 'Nose tip registration, width checked against the fixed face; not the entire source canvas center.'},
    'mouth': {'scale': 60 / 434, 'translation': [519 - 60 / 434 * 625, 483 - 60 / 434 * 624], 'anchors': {'source_center_line': [625, 624], 'target_center_line': [519, 483]}, 'method': 'Lip center-line registration with 60px intended corner width.'},
}

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

report = {'status': 'six_new_generated_layers_pending_visual_selection', 'generation': 'Six separate built-in imagegen calls, no copied set01 artwork and no original-portrait extraction.', 'registration_policy': 'Only a single uniform scale and translation per complete layer; no alpha/color threshold cleanup, warping or fragment repairs.', 'fixed_sources': {n: {'path': str(FIXED / (n + '.png')), 'sha256': sha(FIXED / (n + '.png'))} for n in ['face_shape', 'clothing']}, 'parts': {}}
layers = {}
for name, generated in NAMES.items():
    src_path = ROOT / 'source/set02' / (name + '_attempt_01.png')
    shutil.copy2(GEN / generated, src_path)
    im = Image.open(src_path).convert('RGBA')
    spec = CONFIG[name]
    scale = spec['scale']
    dx, dy = spec['translation']
    layer = im.transform(SIZE, Image.Transform.AFFINE, (1 / scale, 0, -dx / scale, 0, 1 / scale, -dy / scale), resample=Image.Resampling.BICUBIC)
    target = ROOT / 'preview/set02' / (name + '_registered_attempt_01.png')
    layer.save(target)
    layers[name] = layer
    a = np.asarray(layer.getchannel('A'))
    report['parts'][name] = {**spec, 'source': str(src_path), 'generated_original': str(GEN / generated), 'registered_candidate': str(target), 'source_size': im.size, 'target_size': layer.size, 'source_sha256': sha(src_path), 'candidate_sha256': sha(target), 'alpha_extrema': layer.getchannel('A').getextrema(), 'partial_alpha_pixels': int(((a > 0) & (a < 255)).sum()), 'fully_opaque_pixels': int((a == 255).sum())}

face = Image.open(FIXED / 'face_shape.png').convert('RGBA')
clothing = Image.open(FIXED / 'clothing.png').convert('RGBA')

def composite(selected, color=(235, 229, 217, 255)):
    view = Image.new('RGBA', SIZE, color)
    if 'hair_back' in selected:
        view.alpha_composite(layers['hair_back'])
    view.alpha_composite(face)
    view.alpha_composite(clothing)
    for name in ['nose', 'eyes', 'mouth', 'eyebrows', 'hair_front']:
        if name in selected:
            view.alpha_composite(layers[name])
    return view

for name in NAMES:
    v = composite([name])
    v.crop((340, 170, 700, 660)).resize((720, 980)).convert('RGB').save(ROOT / 'preview/set02' / ('check_' + name + '_on_fixed_face.jpg'), quality=94)

view = composite(NAMES.keys())
view.save(ROOT / 'preview/set02/composite_attempt_01.png')
view.convert('RGB').save(ROOT / 'preview/set02/composite_attempt_01.jpg', quality=95)
view.crop((315, 160, 725, 740)).resize((820, 1160)).convert('RGB').save(ROOT / 'preview/set02/head_chest_attempt_01.jpg', quality=95)
view.crop((390, 320, 650, 525)).resize((1040, 820)).convert('RGB').save(ROOT / 'preview/set02/face_close_attempt_01.jpg', quality=95)
composite(['eyes', 'eyebrows', 'nose', 'mouth']).crop((380, 320, 660, 525)).resize((1120, 820)).convert('RGB').save(ROOT / 'preview/set02/facial_only_attempt_01.jpg', quality=95)
for label, color in [('dark', (40, 40, 40, 255)), ('light', (245, 242, 232, 255))]:
    composite(NAMES.keys(), color).convert('RGB').save(ROOT / 'preview/set02' / ('composite_' + label + '_attempt_01.jpg'), quality=94)

(ROOT / 'source/set02/registration_attempt_01.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'status': report['status'], 'parts': list(NAMES), 'preview': str(ROOT / 'preview/set02/composite_attempt_01.jpg')}, ensure_ascii=False))
