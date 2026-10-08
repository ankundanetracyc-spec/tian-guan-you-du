"""Export current complete hair, repainted skin and independent ear variants.

Current paintings keep their generated alpha and whole-canvas registration.
Legacy head demultiplexing remains a fallback only without the current manifest.
"""
from pathlib import Path
import json
import sys
from shutil import copy2
import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import repair_character_hair_back as repair

ROOT = repair.base.ROOT
PACKAGES = {
    "male": (repair.base.MALE, "test_assets_v7"),
    "female": (repair.base.FEMALE, "test_assets_v5"),
}


def register_front(source, sex):
    sy = .55 if sex == "male" else .36
    src_x = 512 + (repair.base.XX - 512) / .8
    src_y = 370 + (repair.base.YY - 279) / sy
    pixels = repair.base.cv2.remap(
        np.asarray(source), src_x, src_y, repair.base.cv2.INTER_LINEAR,
        borderMode=repair.base.cv2.BORDER_CONSTANT,
    )
    return Image.fromarray(pixels, "RGBA")


def standard_ears(sex):
    if sex == "male":
        return repair.complete_male_head(Image.new("RGBA", repair.base.SIZE))
    source = repair.base.open_png(repair.base.FEMALE / "source/ear_standard_clean_v6.png")
    return repair.resize_about_anchor(source, .91, .84, y_anchor=400 - 6 / .16)


def skin_without_ears(face, sex):
    if sex == "male":
        # v6 is the unchanged earless head, preserving every original jaw.
        return face
    # Ear islands in the old female head also carry rear-hair pixels. They
    # lie outside the continuous central face. Only these islands are removed;
    # no forehead, jaw, neck or other skin is repainted.
    yy, xx = repair.base.YY, repair.base.XX
    left_edge = np.interp(yy, [335, 360, 410, 452, 478], [407, 416, 420, 426, 431])
    right_edge = 1024 - left_edge
    islands = (yy >= 335) & (yy <= 478) & ((xx < left_edge) | (xx > right_edge))
    out = face.copy()
    alpha = np.asarray(face.getchannel("A")).copy()
    alpha[islands] = 0
    out.putalpha(Image.fromarray(alpha, "L"))
    return out


def export(install=False, output_root=None):
    # Current repainted skin must never be rebuilt from the old heads with
    # baked hair/collars or collapsed back to one ear variant.
    if (ROOT / "tools/character_skin_ear_sources.json").exists():
        import export_character_skin_ears
        import export_character_ink_hair
        export_character_skin_ears.export(install=install, output_root=output_root)
        export_character_ink_hair.export(install=install, output_root=output_root)
        if (ROOT / "tools/character_facial_overlay_sources.json").exists():
            import export_character_facial_overlays
            export_character_facial_overlays.export(install=install, output_root=output_root)
        return
    manifest = json.loads((ROOT / "tools/character_front_ear_sources.json").read_text(encoding="utf-8"))
    ink_manifest = ROOT / "tools/character_ink_hair_sources.json"
    use_ink_hair = ink_manifest.exists() and len(json.loads(ink_manifest.read_text(encoding="utf-8"))["assets"]) == 20
    for sex, (root, package) in PACKAGES.items():
        target = root / package
        preview = output_root / sex if output_root else root / "preview/front_ears_current"
        preview.mkdir(parents=True, exist_ok=True)
        backup = root / "test_sources/before_front_ears_export"
        backup.mkdir(parents=True, exist_ok=True)

        def save(name, image):
            assert image.size == repair.base.SIZE
            image.save(preview / name)
            if install:
                if (target / name).exists() and not (backup / name).exists():
                    copy2(target / name, backup / name)
                image.save(target / name)

        for entry in manifest["assets"]:
            if use_ink_hair or entry["sex"] != sex or entry.get("part") == "ear":
                continue
            source = repair.base.open_png(ROOT / entry["source"])
            save(f'hair_front_{entry["index"]}.png', register_front(source, sex))
        save("ear_0.png", standard_ears(sex))
        for i in range(5):
            source = root / "test_assets_v6" / f"face_{i}.png" if sex == "male" else backup / f"face_{i}.png"
            if sex == "female" and not source.exists():
                # Capture the existing shape before the first demultiplexing.
                copy2(target / f"face_{i}.png", source)
            save(f"face_{i}.png", skin_without_ears(repair.base.open_png(source), sex))
        print(f"{sex}: 5 clean front layers, standard ear pair, 5 original head shapes")
    if use_ink_hair:
        import export_character_ink_hair
        export_character_ink_hair.export(install=install, output_root=output_root)


if __name__ == "__main__":
    export(install="--install" in sys.argv)
