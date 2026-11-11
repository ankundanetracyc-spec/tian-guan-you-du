"""Technical whole-layer import of the v4 image_gen eye pair; never warp either eye."""
from pathlib import Path
import runpy
import hashlib
import json
import numpy as np
import cv2
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
SOURCE = PACK / 'candidates/eyes/extraction_v4.png'
OUTPUT = PACK / 'candidates/eyes/registered_v4_10point.png'
TARGET = PACK / 'source/reference/approved_02_long_eyes.png'
fit = runpy.run_path(str(ROOT / 'mods/appearance/selected_ink_portrait_rig_20261003_v1/register_variant.py'))['fit_similarity']
source_controls = [[386,385],[475,400],[552,400],[638,384],[436,389],[588,389], [436,382],[436,400],[588,381],[588,400]]
target_controls = [[429,377],[491,389],[548,389],[610,377],[465,381],[575,381], [465,375],[465,389],[575,375],[575,389]]
matrix, errors, scale = fit(source_controls, target_controls)
if float(errors.max()) > 2:
    raise SystemExit('Whole-layer eye controls exceed 2px; no export')
if OUTPUT.exists():
    raise SystemExit('Candidate already exists; retain the reviewed export')
rgba = np.asarray(Image.open(SOURCE).convert('RGBA'), dtype=np.float32) / 255
rgba[:,:,:3] *= rgba[:,:,3:4]
shifted = cv2.warpAffine(rgba, matrix, (1024,1536), flags=cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
shifted = np.clip(shifted, 0, 1)
shifted[:,:,:3] /= np.maximum(shifted[:,:,3:4], 1e-8)
result = np.round(np.clip(shifted,0,1)*255).astype(np.uint8)
result[result[:,:,3] == 0,:3] = 0
Image.fromarray(result).save(OUTPUT)
record = {
    'status':'registered_candidate_pending_visual_fidelity_review',
    'source':str(SOURCE), 'target_approved_portrait':str(TARGET), 'output':str(OUTPUT),
    'canvas':[1024,1536], 'export_offset_px':[0,0],
    'control_names':['left_outer_lid_stroke_end','left_inner_eye_corner','right_inner_eye_corner','right_outer_lid_stroke_end','left_iris_center','right_iris_center','left_upper_lid','left_lower_lid','right_upper_lid','right_lower_lid'],
    'source_controls':source_controls, 'target_controls':target_controls,
    'control_measurement':'Visible painted landmarks, manual original-pixel reading; approximately 1px reading uncertainty. Visual fidelity remains a separate requirement.',
    'similarity_matrix':matrix.tolist(), 'uniform_scale':scale,
    'residuals_px':errors.tolist(), 'maximum_residual_px':float(errors.max()),
    'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    'target_sha256':hashlib.sha256(TARGET.read_bytes()).hexdigest(),
    'output_sha256':hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
    'method':'Existing project whole-layer similarity fit and premultiplied-alpha resampling. No separate-eye transforms, local warps, recoloring or repainting.',
    'fidelity_limit':'image_gen is generative; this export does not assert original-pixel identity.'
}
(PACK/'source/eye_import_v4.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(record,ensure_ascii=False))
