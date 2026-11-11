"""Import the approved-colour native head while preserving the frozen head Alpha."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt


PACK = Path(__file__).resolve().parent.parent
CANVAS = (1024, 1536)
APPROVED_SHA = "d97b79e3372df42db24b4dd5c3ec42f376d652616d9fa25d78a393b004da81a8"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rgba(path: Path) -> np.ndarray:
    image = Image.open(path).convert("RGBA")
    if image.size != CANVAS:
        raise ValueError(f"Unexpected native canvas {image.size}: {path}")
    return np.array(image)


def backdrop(kind: str) -> Image.Image:
    if kind != "checker":
        return Image.new("RGBA", CANVAS, "#e8e1d6" if kind == "light" else "#253136")
    image = Image.new("RGBA", CANVAS, "#dedbd5")
    draw = ImageDraw.Draw(image)
    for y in range(0, CANVAS[1], 24):
        for x in range(0, CANVAS[0], 24):
            if (x // 24 + y // 24) % 2:
                draw.rectangle((x, y, x + 23, y + 23), fill="#a8a6a1")
    return image


def main() -> None:
    raw_path = PACK / "raw/head.png"
    frozen_path = PACK / "reference/current_male/face_shape/01.png"
    approved_path = PACK / "source/approved_male.png"
    output_path = PACK / "male/face_shape/01.png"
    qa = PACK / "qa"
    if sha(approved_path) != APPROVED_SHA:
        raise ValueError("Approved source must be the user's exact whole01 attachment")
    native = rgba(raw_path)
    frozen = rgba(frozen_path)
    solid = native[:, :, 3] >= 230
    if not solid.any():
        raise ValueError("Native head has no solid skin pixels")

    # RGB always comes from this new native head. No previous skin RGB is read.
    distance, nearest = distance_transform_edt(~solid, return_indices=True)
    output = native.copy()
    output[:, :, 3] = frozen[:, :, 3]
    visible = output[:, :, 3] > 0
    extend = visible & ~solid
    output[extend, :3] = native[nearest[0][extend], nearest[1][extend], :3]
    output[~visible, :3] = 0
    alpha_changed = int(np.count_nonzero(output[:, :, 3] != frozen[:, :, 3]))
    native_core = visible & solid
    core_rgb_changed = int(np.count_nonzero(np.any(output[native_core, :3] != native[native_core, :3], axis=1)))
    if alpha_changed or core_rgb_changed:
        raise AssertionError("Frozen Alpha or native solid skin RGB changed")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    qa.mkdir(parents=True, exist_ok=True)
    head = Image.fromarray(output)
    head.save(output_path)

    neck_rows = []
    for y in (500, 510, 520, 530, 540, 550, 560, 570, 580, 590, 600):
        xs = np.flatnonzero(output[y, :, 3] >= 230)
        old_xs = np.flatnonzero(frozen[y, :, 3] >= 230)
        neck_rows.append({"y": y, "new": [int(xs.min()), int(xs.max())],
                          "frozen": [int(old_xs.min()), int(old_xs.max())],
                          "identical": np.array_equal(xs, old_xs)})
    approved = np.array(Image.open(approved_path).convert("RGB"))
    samples = []
    for name, (x0, y0, x1, y1) in {
        "forehead": (496, 292, 526, 320),
        "left_cheek": (435, 402, 453, 427),
        "right_cheek": (573, 402, 591, 427),
        "neck": (490, 570, 530, 590),
    }.items():
        new_rgb = np.median(output[y0:y1, x0:x1, :3], axis=(0, 1))
        approved_rgb = np.median(approved[y0:y1, x0:x1], axis=(0, 1))
        samples.append({"name": name, "rect_xyxy": [x0, y0, x1, y1],
                        "native_head_rgb_median": new_rgb.tolist(),
                        "approved_whole01_rgb_median": approved_rgb.tolist(),
                        "rgb_delta": (new_rgb - approved_rgb).tolist()})
    for kind in ("light", "dark", "checker"):
        image = backdrop(kind)
        image.alpha_composite(head)
        image.save(qa / f"head_{kind}.png")
        image.crop((320, 140, 710, 760)).save(qa / f"head_close_{kind}.png")

    guard = {
        "native_source": str(raw_path), "native_sha256": sha(raw_path),
        "frozen_alpha_source": str(frozen_path), "frozen_sha256": sha(frozen_path),
        "approved_source": str(approved_path), "approved_sha256": sha(approved_path),
        "output": str(output_path), "output_sha256": sha(output_path),
        "canvas": list(CANVAS), "transform": "none; same native origin and dimensions",
        "head_neck_alpha_changed_pixels": alpha_changed,
        "native_solid_rgb_changed_pixels": core_rgb_changed,
        "native_solid_rgb_preserved_pixels": int(native_core.sum()),
        "edge_rgb_extended_from_new_native_only_pixels": int(extend.sum()),
        "max_extension_distance_alpha_ge128": float(distance[extend & (output[:, :, 3] >= 128)].max(initial=0)),
        "old_head_rgb_used": False,
        "alpha_sha256": hashlib.sha256(output[:, :, 3].tobytes()).hexdigest(),
        "frozen_alpha_sha256": hashlib.sha256(frozen[:, :, 3].tobytes()).hexdigest(),
        "neck_alpha230_rows": neck_rows, "skin_samples": samples,
        "visual_limit": "Native blank head is AI generated; inspect unfeatured skin, ears, jaw and collar contact. Exact Alpha does not certify artistic likeness.",
    }
    (qa / "head_guard.json").write_text(json.dumps(guard, ensure_ascii=False, indent=2), encoding="utf-8")
    geometry = {"canvas": list(CANVAS), "head_neck_alpha_unchanged": True,
                "no_translation_scaling_or_warping": True,
                "neck_alpha230_rows": neck_rows,
                "skin_samples": samples,
                "geometry_reference": "qa/approved_geometry.json",
                "note": "Fixed silhouette is exact; the native painted inner jaw is not an Alpha boundary."}
    (qa / "head_geometry.json").write_text(json.dumps(geometry, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"head": str(output_path), "alpha_changed": alpha_changed,
                      "native_core_rgb_changed": core_rgb_changed,
                      "skin_samples": samples}, ensure_ascii=False))


if __name__ == "__main__":
    main()
