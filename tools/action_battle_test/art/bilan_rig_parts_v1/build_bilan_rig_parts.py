from pathlib import Path
import json

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "source_bilan_rig_parts_v1.png"
WEIGHTED_ROBE_SOURCE = ROOT / "source_bilan_robe_body_v3.png"
WEIGHTED_LEG_SOURCE = ROOT / "source_bilan_weighted_leg_v2.png"
FRONT_PANEL_SOURCE = ROOT / "source_bilan_front_panel_generated.png"
PADDING = 6

# Regions are measured on the generated 1536x1024 transparent component sheet.
# Each region deliberately excludes adjacent parts; the subsequent alpha trim
# removes the generous spacing without changing the artwork.
PARTS = {
    "head_hair": (70, 55, 455, 515),
    "robe_body": (445, 20, 865, 580),
    "far_upper_arm": (980, 100, 1160, 525),
    "far_forearm": (1260, 125, 1490, 545),
    "near_upper_arm": (120, 600, 350, 1005),
    "near_forearm": (430, 585, 720, 1010),
    # The robe panels are already part of robe_body. Keep only trousers and
    # boots from these two lower-leg cutouts.
    "far_lower_leg": (850, 825, 1135, 990),
    "near_lower_leg": (1220, 825, 1475, 990),
}

RIG_BINDINGS = {
    "robe_body": {
        "parentPath": "/10002/Root/Body/yifu/yifu/shenti1",
        "scale": [0.095, 0.158],
        "rotationDegrees": 97.91,
        "position": [-8.4, -5.8],
        "zIndex": 0,
        "anchor": "center",
    },
    "head_hair": {
        "parentPath": "/10002/Root/Body/yifu/yifu/shenti1/tou",
        "scale": [0.076, 0.105],
        "rotationDegrees": 97.91,
        "position": [-8.0, -8.3],
        "zIndex": 5,
        "anchor": "center",
    },
    "far_upper_arm": {
        "parentPath": "/10002/Root/Body/yifu/yifu/shenti1/zuobi/zuobi/zuobi1",
        "scale": [0.0473, 0.0473],
        "rotationDegrees": -90.0,
        "position": [0.0, 0.0],
        "zIndex": -2,
        "anchor": "top_center",
    },
    "far_forearm": {
        "parentPath": "/10002/Root/Body/yifu/yifu/shenti1/zuobi/zuobi/zuobi1/zuobi2",
        "scale": [0.0212, 0.0212],
        "rotationDegrees": -90.0,
        "position": [0.0, 0.0],
        "zIndex": -2,
        "anchor": "top_center",
    },
    "near_upper_arm": {
        "parentPath": "/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi/youbi1",
        "scale": [0.0444, 0.0444],
        "rotationDegrees": -90.0,
        "position": [0.0, 0.0],
        "zIndex": 3,
        "anchor": "top_center",
    },
    "near_forearm": {
        "parentPath": "/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi/youbi1/youbi2",
        "scale": [0.0310, 0.0310],
        "rotationDegrees": -90.0,
        "position": [0.0, 0.0],
        "zIndex": 3,
        "anchor": "top_center",
    },
    "far_lower_leg": {
        "parentPath": "/10002/Root/Body/Foot/youtui/youtui/tui1/tui2",
        "scale": [0.082, 0.168],
        "rotationDegrees": -90.0,
        "position": [0.0, 0.0],
        "zIndex": -2,
        "anchor": "top_center",
    },
    "near_lower_leg": {
        "parentPath": "/10002/Root/Body/Foot/zuotui/zuotui/tui1/tui2",
        "scale": [0.082, 0.168],
        "rotationDegrees": -90.0,
        "position": [0.0, 0.0],
        "zIndex": 2,
        "anchor": "top_center",
    },
}


def trim_alpha(image: Image.Image) -> tuple[Image.Image, tuple[int, int, int, int]]:
    alpha = image.getchannel("A")
    # Remove only the faint ambient-generation haze while keeping antialiased
    # garment edges. The component sheet itself remains preserved unchanged.
    alpha.point(lambda value: 0 if value < 24 else value, mode="L")
    pixels = alpha.load()
    for y in range(alpha.height):
        for x in range(alpha.width):
            if pixels[x, y] < 24:
                pixels[x, y] = 0
    bbox = alpha.point(lambda value: 255 if value >= 24 else 0, mode="L").getbbox()
    if bbox is None:
        raise RuntimeError("A component crop contains no visible pixels")
    left, top, right, bottom = bbox
    left = max(0, left - PADDING)
    top = max(0, top - PADDING)
    right = min(image.width, right + PADDING)
    bottom = min(image.height, bottom + PADDING)
    image.putalpha(alpha)
    return image.crop((left, top, right, bottom)), (left, top, right, bottom)


def main() -> None:
    source = Image.open(SOURCE).convert("RGBA")
    if source.size != (1536, 1024):
        raise RuntimeError(f"Unexpected source dimensions: {source.size}")

    parts_dir = ROOT / "parts"
    parts_dir.mkdir(exist_ok=True)
    _build_weighted_experiment_textures(parts_dir)
    manifest = {
        "schema": "bilan-bone-parts/v1",
        "source": SOURCE.name,
        "sourceSize": list(source.size),
        "coordinateNotes": "Texture crops retain their transparent silhouette. Runtime scaling, bone paths, local rotations and offsets are listed per part.",
        # In the new-part experiment, the torso and lower legs use the original
        # weighted SpriteSkin meshes. These project-owned textures replace the
        # rigid full-length robe and short shin stickers while preserving the
        # source mesh UVs and three-bone leg weights.
        "experimentWeightedTextures": {
            "79450": "parts/bilan_robe_body_weighted.png",
            "61872": "parts/bilan_weighted_leg_v2.png",
        },
        "bindings": RIG_BINDINGS,
        "parts": [],
    }
    for name, region in PARTS.items():
        crop = source.crop(region)
        trimmed, local_bbox = trim_alpha(crop)
        png_name = f"bilan_{name}.png"
        trimmed.save(parts_dir / png_name, optimize=True)
        manifest["parts"].append({
            "name": name,
            "png": f"parts/{png_name}",
            "sourceRegion": list(region),
            "trimmedSourceBounds": [
                region[0] + local_bbox[0],
                region[1] + local_bbox[1],
                region[0] + local_bbox[2],
                region[1] + local_bbox[3],
            ],
            "size": list(trimmed.size),
        })

    (ROOT / "parts_manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def _build_weighted_experiment_textures(parts_dir: Path) -> None:
    # The SpriteSkin mesh for the original robe has a 39:87 logical canvas.
    # Bake the rigid sprite's non-uniform horizontal scale into this texture so
    # its image coordinates match the weighted mesh UV shape.
    if not WEIGHTED_ROBE_SOURCE.is_file():
        raise FileNotFoundError(f"Missing weighted robe source: {WEIGHTED_ROBE_SOURCE}")
    robe = Image.open(WEIGHTED_ROBE_SOURCE).convert("RGBA")
    robe_width = round(robe.height * 39.0 / 87.0)
    robe = robe.resize((robe_width, robe.height), Image.Resampling.LANCZOS)
    robe.save(parts_dir / "bilan_robe_body_weighted.png", optimize=True)

    # Foreground includes the inner lapel and the central robe panel.  The
    # flaring left-hand tail stays on the base mesh, behind the legs.  Both
    # textures share the same UV canvas and deformation weights.
    mask = Image.new("L", robe.size, 0)
    points = [(0.76, 0.28), (1.0, 0.28), (1.0, 1.0), (0.76, 1.0)]
    ImageDraw.Draw(mask).polygon([(round(x * robe.width), round(y * robe.height))
                                  for x, y in points], fill=255)
    removed_front = robe.copy()
    removed_front.putalpha(ImageChops.multiply(robe.getchannel("A"), mask))
    back = robe.copy()
    back.putalpha(ImageChops.subtract(robe.getchannel("A"), removed_front.getchannel("A")))
    back.save(parts_dir / "bilan_robe_back_panel.png", optimize=True)
    panel_source = Image.open(FRONT_PANEL_SOURCE).convert("RGBA")
    panel = panel_source.crop((560, 100, 840, 1680)).resize(
        (round(robe.width * 0.34), round(robe.height * 0.55)), Image.Resampling.LANCZOS
    )
    front = Image.new("RGBA", robe.size, (0, 0, 0, 0))
    front.alpha_composite(panel, (round(robe.width * 0.48), round(robe.height * 0.20)))
    front.save(parts_dir / "bilan_robe_front_panel.png", optimize=True)

    if not WEIGHTED_LEG_SOURCE.is_file():
        raise FileNotFoundError(f"Missing weighted leg source: {WEIGHTED_LEG_SOURCE}")
    leg = Image.open(WEIGHTED_LEG_SOURCE).convert("RGBA")
    bounds = leg.getchannel("A").getbbox()
    if bounds is None:
        raise RuntimeError("The weighted leg source has no visible pixels")
    leg = leg.crop(bounds).resize((60, 276), Image.Resampling.LANCZOS)
    fitted_leg = Image.new("RGBA", (76, 292), (0, 0, 0, 0))
    fitted_leg.alpha_composite(leg, (8, 8))
    fitted_leg.save(parts_dir / "bilan_weighted_leg_v2.png", optimize=True)
    upper_leg = fitted_leg.copy()
    upper_alpha = upper_leg.getchannel("A")
    upper_alpha.paste(0, (0, round(upper_leg.height * 0.64), upper_leg.width, upper_leg.height))
    upper_leg.putalpha(upper_alpha)
    upper_leg.save(parts_dir / "bilan_weighted_leg_upper.png", optimize=True)


if __name__ == "__main__":
    main()
