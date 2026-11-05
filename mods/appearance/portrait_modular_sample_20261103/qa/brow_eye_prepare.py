from pathlib import Path
from PIL import Image
import json
import shutil
import hashlib
import numpy as np

ROOT = Path(r'E:/stacklands/mods/appearance/portrait_modular_sample_20261103')
FIXED = Path(r'E:/stacklands/mods/appearance/portrait_generated_anchors_20261103/parts')
GENERATED = Path(r'E:/tmp/CodexData/generated_images/01a1a1ae-f744-70a3-8368-2201c0206eae')
CANVAS = (1024, 1536)
SOURCES = {
    'eyes': GENERATED / 'exec-5ab0be7e-1d33-47e2-ab10-095b023ec045.png',
    'eyebrows': GENERATED / 'exec-f6e1b3ff-eea6-49b9-9e04-7c6fcd2667da.png',
}
ANCHORS = {
    'eyes': {
        'source': [(459.36386138613864, 509.970297029703), (1076.858832807571, 509.74526813880124)],
        'target': [(464, 379), (574, 379)],
        'method': 'Pupil dark-pigment centroid in manually inspected pupil-only windows; not the whole asset bounding box.',
    },
    'eyebrows': {
        'source': [(406.5, 624.5), (852, 623.5)],
        'target': [(457, 354), (582, 353)],
        'method': 'Individually inspected brow-body midpoints, then visually checked on the fixed face.',
    },
}

for folder in ['source/eyes', 'source/eyebrows', 'parts', 'preview', 'qa']:
    (ROOT / folder).mkdir(parents=True, exist_ok=True)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def alpha_stats(im):
    a = np.asarray(im.getchannel('A'))
    return {
        'size': im.size,
        'mode': im.mode,
        'nonzero_alpha_pixels': int((a > 0).sum()),
        'partially_transparent_pixels': int(((a > 0) & (a < 255)).sum()),
        'alpha_extrema': im.getchannel('A').getextrema(),
    }

report = {
    'status': 'registered_candidates_pending_visual_review',
    'generation': 'Built-in imagegen, two fresh independent drawings after the local game assembly review. No previous rejected draft reused.',
    'transform_policy': 'One uniform scale and translation for each complete paired layer; no separate left/right reshaping, color cleanup, alpha thresholding, skin extraction or source pixel patching.',
    'fixed_face': str(FIXED / 'face_shape.png'),
    'fixed_clothing': str(FIXED / 'clothing.png'),
    'fixed_face_sha256': sha(FIXED / 'face_shape.png'),
    'fixed_clothing_sha256': sha(FIXED / 'clothing.png'),
    'parts': {},
}

registered = {}
for name, source_path in SOURCES.items():
    saved_source = ROOT / 'source' / name / 'attempt_01.png'
    shutil.copy2(source_path, saved_source)
    im = Image.open(saved_source).convert('RGBA')
    src = np.asarray(ANCHORS[name]['source'], dtype=float)
    dst = np.asarray(ANCHORS[name]['target'], dtype=float)
    scale = float((dst[1, 0] - dst[0, 0]) / (src[1, 0] - src[0, 0]))
    offset = dst.mean(axis=0) - src.mean(axis=0) * scale
    inverse = (1 / scale, 0, -float(offset[0]) / scale, 0, 1 / scale, -float(offset[1]) / scale)
    layer = im.transform(CANVAS, Image.Transform.AFFINE, inverse, resample=Image.Resampling.BICUBIC)
    layer_path = ROOT / 'preview' / (name + '_registered_attempt_01.png')
    layer.save(layer_path)
    registered[name] = layer
    report['parts'][name] = {
        'source_path': str(saved_source),
        'generated_original_path': str(source_path),
        'registered_candidate': str(layer_path),
        'source_sha256': sha(saved_source),
        'registered_sha256': sha(layer_path),
        'registration': {
            **ANCHORS[name],
            'uniform_scale': scale,
            'translation': [float(v) for v in offset],
            'mapped_source_points': (src * scale + offset).tolist(),
            'maximum_anchor_distance_px': float(np.linalg.norm(src * scale + offset - dst, axis=1).max()),
        },
        'source_alpha': alpha_stats(im),
        'registered_alpha': alpha_stats(layer),
    }

face = Image.open(FIXED / 'face_shape.png').convert('RGBA')
clothing = Image.open(FIXED / 'clothing.png').convert('RGBA')
base = Image.new('RGBA', CANVAS, (235, 229, 217, 255))
base.alpha_composite(face)
base.alpha_composite(clothing)
for name, names in [('eyes_only', ['eyes']), ('eyebrows_only', ['eyebrows']), ('brow_eye', ['eyes', 'eyebrows'])]:
    view = base.copy()
    for key in names:
        view.alpha_composite(registered[key])
    view.convert('RGB').save(ROOT / 'preview' / (name + '_face_and_clothing_attempt_01.jpg'), quality=95)
    view.crop((365, 280, 675, 480)).resize((930, 600), Image.Resampling.NEAREST).convert('RGB').save(ROOT / 'preview' / (name + '_face_close_attempt_01.png'))

swatches = Image.new('RGB', (960, 640), (235, 229, 217))
for i, color in enumerate([(235, 229, 217, 255), (48, 48, 48, 255)]):
    v = Image.new('RGBA', CANVAS, color)
    for key in ['eyes', 'eyebrows']:
        v.alpha_composite(registered[key])
    v = v.crop((398, 325, 638, 405)).resize((960, 320), Image.Resampling.NEAREST)
    swatches.paste(v.convert('RGB'), (0, i * 320))
swatches.save(ROOT / 'preview' / 'brow_eye_alpha_light_dark_attempt_01.png')
(ROOT / 'qa' / 'brow_eye_registration_attempt_01.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'paths': {name: data['registered_candidate'] for name, data in report['parts'].items()}, 'registration': {name: data['registration'] for name, data in report['parts'].items()}}, ensure_ascii=False))
