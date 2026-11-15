"""Historical rejected v2 experiment. Do not run against the current eye layer."""
raise SystemExit("Retired after the user rejected oversized eyes; current eye source is approved-source extraction, without this transform.")
from pathlib import Path
import importlib.util
import json
import hashlib
import numpy as np
import cv2
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
SOURCE = PACK / "candidates/eyes/03.png"
TARGET = ROOT / "mods/appearance/female_aligned_features_20261106/preview/02_soft_straight_long_eyes.png"
OUTPUT = PACK / "female/eyes/03.png"

def controls(path):
    rgba = np.array(Image.open(path).convert("RGBA"))
    lum = rgba[:, :, :3] @ np.array([.2126, .7152, .0722])
    centers = []
    lower = None
    for x0, x1 in [(448, 485), (555, 595)]:
        mask = ((lum[374:390, x0:x1] < 45) & (rgba[374:390, x0:x1, 3] > 128)).astype("uint8")
        count, labels, stats, centroids = cv2.connectedComponentsWithStats(mask, 8)
        if count < 2:
            raise ValueError("Cannot inspect the painted pupil controls")
        label = 1 + int(np.argmax(stats[1:, 4]))
        centers.append([float(centroids[label, 0] + x0), float(centroids[label, 1] + 374)])
        if lower is None:
            ys, xs = np.where(labels == label)
            bottom = ys.max()
            lower = [float(xs[ys == bottom].mean() + x0), float(bottom + 374)]
    return np.array(centers + [lower], dtype=np.float64)

spec = importlib.util.spec_from_file_location("portrait_registration", ROOT / "mods/appearance/selected_ink_portrait_rig_20261003_v1/register_variant.py")
registration = importlib.util.module_from_spec(spec)
spec.loader.exec_module(registration)
source_controls = controls(SOURCE)
target_controls = controls(TARGET)
matrix, residuals, scale = registration.fit_similarity(source_controls, target_controls)
if residuals.max() > 2:
    raise ValueError("Painted controls exceed the 2px whole-frame registration limit")
source = np.array(Image.open(SOURCE).convert("RGBA"), dtype=np.float32) / 255
source[:, :, :3] *= source[:, :, 3:4]
shifted = cv2.warpAffine(source, matrix, (1024, 1536), flags=cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_CONSTANT, borderValue=0)
shifted = np.clip(shifted, 0, 1)
shifted[:, :, :3] /= np.maximum(shifted[:, :, 3:4], 1e-8)
pixels = np.round(np.clip(shifted, 0, 1) * 255).astype(np.uint8)
pixels[pixels[:, :, 3] == 0, :3] = 0
Image.fromarray(pixels).save(OUTPUT)
report = {
    "source": str(SOURCE), "target_approved_portrait": str(TARGET), "output": str(OUTPUT),
    "canvas": [1024, 1536], "export_offset": [0, 0],
    "control_names": ["left_dark_pupil_region_center", "right_dark_pupil_region_center", "left_dark_pupil_lower_edge"],
    "source_controls": source_controls.tolist(), "target_controls": target_controls.tolist(),
    "similarity_matrix": matrix.tolist(), "uniform_scale": scale,
    "control_residuals_px": residuals.tolist(), "maximum_residual_px": float(residuals.max()),
    "source_sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    "output_sha256": hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
    "method": "Three actual non-collinear painted pupil-region controls from the approved image; one whole-layer similarity transform using the established importer; premultiplied-alpha resampling; no per-eye edits, shape warps or recoloring.",
    "semantic_and_style_review": "Still required after registration"
}
(PACK / "source/eye_registration.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))

