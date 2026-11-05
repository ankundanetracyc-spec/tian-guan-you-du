"""Inspect independent portrait replacement PNGs without changing their pixels.

This is a technical/visual-review helper, not an art acceptance test. Skin-color
and hidden-fill heuristics flag possible problems for a human to inspect. It
renders previews with the runtime's exact back-to-front layer order; preview
images are never used as game component textures.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import json
from pathlib import Path

import numpy as np
from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
ACTIVE = PROJECT / "mods/appearance/ink_three_variants_20261004"
PACKAGE = PROJECT / "mods/appearance/portrait_rebuild_20261103"
FINAL = PACKAGE / "final"
PARTS = ("hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth")
ORDER = ("hair_back", "face_shape", "clothing", "eyes", "eyebrows", "nose", "mouth", "face_accessory", "hair_front")
SIZE = (1024, 1536)
FEATURE_ROI = {
    "eyebrows": (355, 312, 680, 379),
    "eyes": (385, 343, 652, 422),
    "nose": (465, 380, 575, 479),
    "mouth": (458, 456, 579, 532),
}
LABELS = {"hair_front": "前发", "hair_back": "后发", "eyebrows": "眉毛", "eyes": "眼睛", "nose": "鼻子", "mouth": "嘴巴"}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def active_mapping(config: dict) -> list[dict]:
    result = []
    for entry in config["components"]:
        part = entry["file"].split("/")[1]
        if part not in PARTS:
            continue
        for logical in range(min(3, entry["variant_count"])):
            physical = entry.get("variant_indices", list(range(1, entry["variant_count"] + 1)))[logical]
            style = entry.get("variant_style_ids", list(range(entry["variant_count"])))[logical]
            result.append({
                "gender": entry["gender"], "part": part, "logical_set": logical + 1,
                "source": str(FINAL / entry["gender"] / part / f"{logical + 1:02}.png"),
                "active_file": entry["file"].replace("{index}", f"{physical:02}"),
                "physical_index": physical, "saved_style_id": style,
                "saved_string_id": f"{entry['id']}_{style:02}",
                "existing_name": entry["variant_names"][logical],
            })
    return result


def active_snapshot(config: dict) -> dict:
    files = {}
    for entry in config["components"]:
        indices = entry.get("variant_indices", list(range(1, entry["variant_count"] + 1)))
        for index in indices:
            relative = entry["file"].replace("{index}", f"{index:02}")
            path = ACTIVE / relative
            files[relative] = digest(path) if path.is_file() else None
    return {"configuration_sha256": digest(ACTIVE / "appearance_parts.json"), "files": files}


def bbox(mask: np.ndarray):
    ys, xs = np.nonzero(mask)
    return [int(xs.min()), int(ys.min()), int(xs.max() + 1), int(ys.max() + 1)] if len(xs) else None


def inspect(path: Path, gender: str, part: str, variant: int) -> tuple[dict, Image.Image | None]:
    row = {"gender": gender, "part": part, "variant": variant, "file": str(path), "errors": [], "review_flags": []}
    if not path.is_file():
        row["errors"].append("missing")
        return row, None
    with Image.open(path) as raw:
        row.update(size=list(raw.size), mode=raw.mode, sha256=digest(path))
        if raw.size != SIZE:
            row["errors"].append("canvas_must_be_1024x1536")
        if raw.mode != "RGBA":
            row["errors"].append("mode_must_be_RGBA")
        im = raw.convert("RGBA")
    if row["errors"]:
        return row, None
    a = np.asarray(im)
    alpha = a[:, :, 3]
    row["nonzero_alpha_bbox"] = bbox(alpha > 0)
    row["visible_alpha_bbox"] = bbox(alpha >= 16)
    row["opaque_pixel_count"] = int((alpha >= 240).sum())
    row["continuous_alpha_pixel_count"] = int(((alpha > 0) & (alpha < 240)).sum())
    if not np.any(alpha):
        row["errors"].append("component_is_empty")
    edge = np.concatenate([alpha[:8].ravel(), alpha[-8:].ravel(), alpha[:, :8].ravel(), alpha[:, -8:].ravel()])
    if np.any(edge >= 16):
        row["review_flags"].append("visible_content_reaches_canvas_edge")
    if part in FEATURE_ROI:
        x0, y0, x1, y1 = FEATURE_ROI[part]
        outside = alpha.copy()
        outside[y0:y1, x0:x1] = 0
        row["outside_feature_roi_alpha_nonzero_pixels"] = int((outside > 0).sum())
        row["outside_feature_roi_visible_pixels"] = int((outside >= 16).sum())
        if np.any(outside >= 16):
            row["errors"].append("visible_art_outside_expected_feature_region")
        elif np.any(outside > 0):
            row["review_flags"].append("faint_alpha_outside_expected_feature_region")
        if part in ("eyebrows", "eyes"):
            row["left_right_visible_pixel_counts"] = [int((alpha[:, :519] >= 16).sum()), int((alpha[:, 519:] >= 16).sum())]
            if min(row["left_right_visible_pixel_counts"]) < 20:
                row["errors"].append("paired_feature_missing_one_side")
    else:
        rgb = a[:, :, :3].astype(np.int16)
        skin = (alpha >= 180) & (rgb[:, :, 0] > 135) & (rgb[:, :, 0] - rgb[:, :, 1] > 8) & (rgb[:, :, 1] - rgb[:, :, 2] > 4)
        row["possible_opaque_skin_pixels"] = int(skin.sum())
        row["possible_opaque_skin_bbox"] = bbox(skin)
        if skin.sum() > 12:
            row["review_flags"].append("inspect_possible_skin_or_ear_pixels_in_hair")
        if part == "hair_front":
            center = alpha[330:515, 465:575]
            row["central_face_region_visible_pixels"] = int((center >= 16).sum())
            if np.any(center >= 16):
                row["review_flags"].append("inspect_hair_front_for_face_skin_or_intruding_hair")
        else:
            axis = 519 if gender == "female" else 517
            samples = {f"{axis},{y}": int(np.median(alpha[y - 3:y + 4, axis - 3:axis + 4])) for y in (260, 330, 400, 450)}
            row["hidden_center_fill_alpha"] = samples
            if min(samples.values()) < 220:
                row["review_flags"].append("inspect_hidden_back_hair_fill_for_face_shaped_hole")
    return row, im


def compose(gender: str, layers: dict[str, Image.Image], hidden=()) -> Image.Image:
    result = Image.new("RGBA", SIZE)
    for part in ORDER:
        if part in hidden:
            continue
        if part in layers:
            layer = layers[part]
        else:
            with Image.open(ACTIVE / gender / part / "01.png") as source:
                layer = source.convert("RGBA")
        result.alpha_composite(layer)
    return result


def on_background(im: Image.Image, color=(232, 227, 218, 255)) -> Image.Image:
    result = Image.new("RGBA", im.size, color)
    result.alpha_composite(im)
    return result.convert("RGB")


def previews(images: dict, out: Path) -> list[dict]:
    records = []
    for gender in ("female", "male"):
        directory = out / "preview" / gender
        directory.mkdir(parents=True, exist_ok=True)
        for variant in range(1, 4):
            if not all((gender, part, variant) in images for part in PARTS):
                continue
            layers = {part: images[gender, part, variant] for part in PARTS}
            full = compose(gender, layers)
            full.save(directory / f"set_{variant:02}_transparent.png")
            on_background(full).save(directory / f"set_{variant:02}.jpg", quality=95)
            on_background(full).crop((300, 95, 735, 600)).save(directory / f"set_{variant:02}_face.jpg", quality=97)
            for hidden in ("hair_front", "hair_back"):
                on_background(compose(gender, layers, (hidden,))).save(directory / f"set_{variant:02}_hide_{hidden}.jpg", quality=94)
            records.append({"type": "set", "gender": gender, "variant": variant, "preview": f"preview/{gender}/set_{variant:02}.jpg"})
        if all((gender, part, variant) in images for part in PARTS for variant in range(1, 4)):
            for front in range(1, 4):
                for back in range(1, 4):
                    layers = {part: images[gender, part, front] for part in PARTS}
                    layers["hair_back"] = images[gender, "hair_back", back]
                    name = f"mix_front_{front:02}_back_{back:02}.jpg"
                    on_background(compose(gender, layers)).save(directory / name, quality=94)
                    records.append({"type": "mix", "gender": gender, "front": front, "back": back, "preview": f"preview/{gender}/{name}"})
    return records


def write_review(out: Path, report: dict) -> None:
    sections = ["<!doctype html><meta charset='utf-8'><title>立绘独立部件检查</title>",
        "<style>body{font:16px system-ui;background:#e8e3da;color:#332e29;margin:32px}section{display:flex;flex-wrap:wrap;gap:16px}figure{margin:0;width:280px}img{max-width:100%;height:auto}small{display:block}a{color:#365f62}h2{margin-top:36px}</style>",
        "<h1>男女三组独立部件检查</h1><p>此页仅用于检查。游戏素材是36张独立透明PNG；预览复用现有脸型、衣服、空面饰，按实际层序叠加。颜色与透明度诊断不能替代目视验收。</p>"]
    for gender in ("female", "male"):
        sections.append(f"<h2>{'女性' if gender == 'female' else '男性'}整套</h2><section>")
        for item in report["previews"]:
            if item["gender"] != gender or item["type"] != "set":
                continue
            variant = item["variant"]
            prefix = f"preview/{gender}/set_{variant:02}"
            sections.append(f"<figure><a href='{item['preview']}'><img src='{item['preview']}'></a><figcaption>第{variant}组 <a href='{prefix}_face.jpg'>脸部</a> <a href='{prefix}_hide_hair_front.jpg'>隐藏前发</a> <a href='{prefix}_hide_hair_back.jpg'>隐藏后发</a></figcaption></figure>")
        sections.append("</section><h2>前后发互换</h2><section>")
        for item in report["previews"]:
            if item["gender"] == gender and item["type"] == "mix":
                sections.append(f"<figure><a href='{item['preview']}'><img src='{item['preview']}'></a><figcaption>前发{item['front']}＋后发{item['back']}</figcaption></figure>")
        sections.append("</section><h2>独立素材与诊断</h2><ul>")
        for row in report["assets"]:
            if row["gender"] != gender:
                continue
            label = f"{LABELS[row['part']]} {row['variant']:02}"
            href = Path(row["file"]).as_uri()
            issues = row["errors"] + row["review_flags"]
            sections.append(f"<li><a href='{href}'>{label}</a> {html.escape(', '.join(issues) if issues else '结构检查通过；画面待目视')}</li>")
        sections.append("</ul>")
    (out / "review.html").write_text("\n".join(sections), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-partial", action="store_true", help="Report missing pending assets without returning failure solely for absence.")
    parser.add_argument("--no-previews", action="store_true")
    parser.add_argument("--out", type=Path, default=PACKAGE / "qa")
    args = parser.parse_args()
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    config = json.loads((ACTIVE / "appearance_parts.json").read_text(encoding="utf-8"))
    mapping = active_mapping(config)
    save_json(out / "replacement_mapping.json", mapping)
    snapshot = active_snapshot(config)
    baseline = out / "active_before.json"
    if not baseline.exists():
        save_json(baseline, snapshot)
    previous = json.loads(baseline.read_text(encoding="utf-8"))
    allowed = {item["active_file"] for item in mapping}
    changed = [path for path, sha in snapshot["files"].items() if previous["files"].get(path) != sha]
    rows, images = [], {}
    for gender in ("female", "male"):
        for part in PARTS:
            for variant in range(1, 4):
                row, im = inspect(FINAL / gender / part / f"{variant:02}.png", gender, part, variant)
                rows.append(row)
                if im is not None:
                    images[gender, part, variant] = im
    errors = [f"{r['gender']}/{r['part']}/{r['variant']:02}: {e}" for r in rows for e in r["errors"] if not (args.allow_partial and e == "missing")]
    report = {
        "canvas": list(SIZE), "required_asset_count": 36, "present_asset_count": len(images),
        "active_variant_counts": {gender: sum(e["variant_count"] for e in config["components"] if e["gender"] == gender) for gender in ("male", "female")},
        "draw_order": list(ORDER), "artwork_acceptance": "not_automatically_determined",
        "changed_active_files": changed, "changed_outside_replacement_scope": [p for p in changed if p not in allowed],
        "assets": rows, "errors": errors, "previews": [] if args.no_previews else previews(images, out),
        "limitations": ["Skin and hidden-fill tests are heuristics requiring visual review.", "These previews use actual game layer order but are not a running-game screenshot.", "No source, final or active image pixels are modified by this script."],
    }
    save_json(out / "asset_report.json", report)
    write_review(out, report)
    print(json.dumps({"present": len(images), "required": 36, "errors": len(errors), "review_flags": sum(len(r['review_flags']) for r in rows), "preview_count": len(report['previews']), "report": str(out / 'asset_report.json')}, ensure_ascii=False))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
