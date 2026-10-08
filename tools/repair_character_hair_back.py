"""Rebuild independent hair from complete painted strands.

Male rear style zero uses a single complete hair-only painting with a recorded
whole-layer translation, and standard ears belong to the head layer. Other
styles still use the older upper paint and need individual visual acceptance.
Front crowns are confined to the face; selected tapered cheek locks remain
long enough to reach the collar.
"""

from pathlib import Path
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import prepare_character_hair_independent as base


def solid_hair_source(source):
    """Keep the generated PNG's actual hair matte, not its dark RGB haze."""
    rgba = np.asarray(source).copy()
    alpha = rgba[:, :, 3].astype(np.float32) / 255.0
    rgba[:, :, 3] = np.uint8(np.clip(base.smoothstep((alpha - 0.12) / 0.55) * 255, 0, 255))
    return Image.fromarray(rgba, "RGBA")


def complete_back(old, complete, sex, index, folder):
    if sex == "female" and index == 3:
        # This painting is a rear view of a short style.  Its broad sides are
        # useful as hidden completion, but cannot replace the front silhouette.
        face = np.asarray(base.open_png(folder / "face_0.png").getchannel("A")) > 250
        robe = np.logical_and.reduce([
            np.asarray(base.open_png(folder / f"clothes_{i}.png").getchannel("A")) > 250
            for i in range(5)
        ])
        seam_cover = np.asarray(base.ellipse_mask((347, 344, 383, 401)), dtype=np.float32) / 255
        seam_cover += np.asarray(base.ellipse_mask((642, 344, 678, 401)), dtype=np.float32) / 255
        seam_cover = np.clip(seam_cover, 0, 1)
        seam_cover = np.asarray(
            Image.fromarray(np.uint8(seam_cover * 255), "L").filter(base.ImageFilter.GaussianBlur(2)),
            dtype=np.float32,
        ) / 255
        hidden = base.masked(complete, base.mask_from_array(np.maximum(face | robe, seam_cover)))
        hidden.alpha_composite(old)
        return hidden
    source_alpha = np.asarray(complete.getchannel("A"), dtype=np.float32) / 255
    old_alpha = np.asarray(old.getchannel("A"), dtype=np.float32) / 255
    if sex == "male" and index == 4:
        # The prior upper paint contains the removed braid.  Keep only its
        # aligned crown; all lengths come from the new unbraided painting.
        unbraided_length = base.smoothstep((base.YY - 345) / 100)
        left_tail = 1 - base.smoothstep((base.XX - 420) / 80)
        old_alpha *= 1 - unbraided_length * left_tail
    fade = base.smoothstep((base.YY - 350) / 170)
    fade *= base.smoothstep((source_alpha - 0.2) / 0.6)
    old = old.copy()
    old.putalpha(base.mask_from_array(old_alpha * (1 - fade)))
    out = complete.copy()
    out.alpha_composite(old)
    if sex == "female" and index == 4:
        # The braided cutout has empty wedges beside the ears.  Add strands
        # from the matching complete side-hair painting behind that braid.
        bridge = solid_hair_source(base.open_png(base.FEMALE / "source/back_fill_full_0.png"))
        left = base.smoothstep((base.XX - 340) / 25) * (1 - base.smoothstep((base.XX - 415) / 25))
        right = base.smoothstep((base.XX - 580) / 25) * (1 - base.smoothstep((base.XX - 670) / 25))
        vertical = base.smoothstep((base.YY - 365) / 50) * (1 - base.smoothstep((base.YY - 575) / 65))
        alpha = np.asarray(bridge.getchannel("A"), dtype=np.float32) / 255
        bridge.putalpha(base.mask_from_array(alpha * np.maximum(left, right) * vertical))
        bridge.alpha_composite(out)
        out = bridge
    return out


def complete_below_ear(back, sex, index, preview):
    """Fill exposed holes behind ears with aligned strands, under each style."""
    if (sex, index) == ("female", 2):
        donor = base.open_png(preview / "hair_back_0.png")
        side = base.smoothstep((base.XX - 325) / 30) * (1 - base.smoothstep((base.XX - 470) / 30))
    elif (sex, index) == ("male", 3):
        donor = base.open_png(preview / "hair_back_1.png")
        side = base.smoothstep((base.XX - 555) / 30) * (1 - base.smoothstep((base.XX - 735) / 30))
    else:
        return back
    vertical = base.smoothstep((base.YY - 390) / 45)
    alpha = np.asarray(donor.getchannel("A"), dtype=np.float32) / 255
    donor.putalpha(base.mask_from_array(alpha * side * vertical))
    donor.alpha_composite(back)
    return donor


def front_on_face(front, face, sex, index):
    """Keep the hairline and preserve selected tapered cheek locks."""
    alpha = np.asarray(front.getchannel("A"), dtype=np.float32) / 255
    face_alpha = np.asarray(face.getchannel("A"), dtype=np.float32) / 255
    if (sex, index) in {("male", 1), ("male", 2), ("female", 1), ("female", 2)}:
        # These four source fronts have usable individual locks below the
        # cheeks.  Fade in only after the old rectangular crown has ended.
        length = base.smoothstep((base.YY - 330) / 100)
        mask = np.maximum(face_alpha, length)
    else:
        mask = face_alpha
    front = front.copy()
    front.putalpha(base.mask_from_array(alpha * mask))
    return front


REGISTRATION = [
    (.82, [(30, 35), (445, 279), (1515, 1280)]),
    (.86, [(57, 116), (425, 280), (1515, 1280)]),
    (.84, [(28, 55), (460, 279), (1518, 1280)]),
    (.86, [(30, 110), (420, 280), (1525, 1280)]),
    (.90, [(30, 90), (435, 280), (1536, 1280)]),
]


def resize_about_anchor(im, x_scale, y_scale, x_anchor=512, y_anchor=400):
    """Match a completed painting to the established face and robe rig."""
    pixels = np.asarray(im)
    src_x = x_anchor + (base.XX - x_anchor) / x_scale
    src_y = y_anchor + (base.YY - y_anchor) / y_scale
    return Image.fromarray(base.cv2.remap(
        pixels, src_x.astype(np.float32), src_y.astype(np.float32),
        base.cv2.INTER_LINEAR, borderMode=base.cv2.BORDER_CONSTANT,
    ), "RGBA")


def complete_male_head(face):
    """Register both ears on the skin layer, retaining the existing face."""
    support = base.open_png(base.MALE / "test_sources/head_skin_complete_v8.png")
    support = resize_about_anchor(support, .53, .55, y_anchor=85 / .45)
    support_alpha = np.asarray(support.getchannel("A"), dtype=np.float32) / 255
    # Only the two anatomically registered ears extend the existing skin
    # silhouette.  Extra generated scalp must not cover the rear hairline.
    left_ear = [(365, 345), (392, 345), (402, 357), (405, 370),
                (408, 395), (413, 422), (419, 445), (395, 453), (365, 425)]
    right_ear = [(1024 - x, y) for x, y in left_ear]
    ears = np.asarray(base.polygon_mask([left_ear, right_ear]), dtype=np.float32) / 255
    support.putalpha(base.mask_from_array(support_alpha * ears))
    support.alpha_composite(face)
    return support


def complete_male_back_zero():
    """Use one complete painting, with a recorded whole-layer translation."""
    source = base.open_png(base.MALE / "test_sources/hair_back_0_complete_v8.png")
    assert source.size == base.SIZE
    back = Image.new("RGBA", base.SIZE)
    back.alpha_composite(source, (0, 35))
    return back


def complete_source(sex, index):
    if sex == "male":
        if index == 0:
            full = base.open_png(base.MALE / "test_sources/hair_back_unbraided_front_0.png")
            return resize_about_anchor(full, .82, .82, y_anchor=450)
        scale, stops = REGISTRATION[index]
        name = "hair_cut_4_unbraided.png" if index == 4 else f"hair_cut_{index}.png"
        cutout = base.open_png(base.MALE / "test_sources" / name)
        return base.register_male_cutout(cutout, scale, stops)
    if index == 4:
        return base.open_png(base.FEMALE / "source/hair_4_cutout.png")
    full = base.open_png(base.FEMALE / "source" / f"back_fill_full_{index}.png")
    if index == 2:
        return resize_about_anchor(full, .80, .90, y_anchor=240)
    return full


def build(output_root, install=False):
    # The complete replacement paintings supersede all legacy cutout repairs.
    ink_manifest = base.ROOT / "tools/character_ink_hair_sources.json"
    if ink_manifest.exists():
        import json
        if len(json.loads(ink_manifest.read_text(encoding="utf-8"))["assets"]) == 20:
            import export_character_front_ears
            export_character_front_ears.export(install=install, output_root=output_root)
            return
    # Recreate the pre-repair export from its v6/v4 source on every run.
    # Reading the installed v7/v5 hair here would compound paint on rerun.
    baseline_root = base.ROOT / "tmp/hair_repair_baseline"
    (baseline_root / "male").mkdir(parents=True, exist_ok=True)
    (baseline_root / "female").mkdir(parents=True, exist_ok=True)
    base.build_male(destination=baseline_root / "male", export_preview=False, copy_nonhair=False)
    base.build_female(destination=baseline_root / "female", export_preview=False, copy_nonhair=False)
    for sex, root, package in (
        ("male", base.MALE, "test_assets_v7"),
        ("female", base.FEMALE, "test_assets_v5"),
    ):
        folder = root / package
        preview = output_root / sex
        preview.mkdir(parents=True, exist_ok=True)
        if sex == "male":
            for i in range(5):
                # Always start from the unchanged face variants, never from
                # already extended heads in the installed package.
                head = complete_male_head(base.open_png(root / "test_assets_v6" / f"face_{i}.png"))
                head.save(preview / f"face_{i}.png", optimize=True)
                if install:
                    head.save(folder / f"face_{i}.png", optimize=True)
        for index in range(5):
            old = base.open_png(baseline_root / sex / f"hair_back_{index}.png")
            full = solid_hair_source(complete_source(sex, index))
            repaired = complete_below_ear(complete_back(old, full, sex, index, folder), sex, index, preview)
            if sex == "male" and index == 0:
                repaired = complete_male_back_zero()
            repaired.save(preview / f"hair_back_{index}.png", optimize=True)
            if install:
                repaired.save(folder / f"hair_back_{index}.png", optimize=True)
            front = base.open_png(baseline_root / sex / f"hair_front_{index}.png")
            previous = "test_assets_v6" if sex == "male" else "test_assets_v4"
            face = base.open_png(root / previous / "face_0.png")
            front = front_on_face(front, face, sex, index)
            front.save(preview / f"hair_front_{index}.png", optimize=True)
            if install:
                front.save(folder / f"hair_front_{index}.png", optimize=True)
        print(f"{sex}: 5 repaired front/back pairs -> {preview}")
    # Keep later complete-front and independent-ear exports on rebuild too.
    import export_character_front_ears
    export_character_front_ears.export(install=install, output_root=output_root)


if __name__ == "__main__":
    output = base.ROOT / "tmp/hair_repair_candidate"
    build(output, install="--install" in sys.argv)
