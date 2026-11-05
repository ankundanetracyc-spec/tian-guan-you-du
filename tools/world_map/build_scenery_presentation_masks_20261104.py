"""Build data masks for the native Godot terrain presentation, never gameplay.

The PNGs contain scalar weights, not replacement landscape artwork. Source
paintings, runtime crops, layout and navigation remain read-only inputs.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "textures/world/bai_chuan/scenery_ink_20261104/presentation"
ART = ROOT / "textures/world/bai_chuan/free_world_8000_3x3_20261104/master_8000.png"
LAYOUT = ROOT / "resources/world/bai_chuan/free_layout_3x3_20261104.json"
NAVIGATION = ROOT / "resources/world/bai_chuan/free_navigation_3x3_20261104.json"
PLAN = ROOT / "docs/prototypes/百川宗门城镇资源规划数据_20261027.json"
SIZE = 2048
WORLD_SIZE = 7372.8
LEGACY_ART_SHA256 = "385f5ebf0898111767b4738c14e3deb071c1a32873aa05a633d027af88574cb9"
WATER_GEOMETRY_SOURCE = ART
WATER_SDF_RANGE_WORLD = 96.0


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def path_label(path: Path) -> str:
    path = path.resolve()
    return (str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path)).replace("\\", "/")


def smoothstep(lo, hi, value):
    t = np.clip((value - lo) / (hi - lo), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def remove_small(mask, minimum):
    labels, _ = ndimage.label(mask)
    counts = np.bincount(labels.ravel())
    accepted = counts >= minimum
    accepted[0] = False
    return accepted[labels]


def save_data(name, channels):
    data = np.clip(np.stack(channels, axis=-1) * 255.0 + 0.5, 0, 255).astype(np.uint8)
    path = OUT / name
    Image.fromarray(data, "RGBA").save(path, optimize=True)
    return {"path": path_label(path), "sha256": digest(path),
            "size": [SIZE, SIZE], "mode": "RGBA8 scalar data; no sRGB conversion"}


def prepare_water_geometry(source_path, candidate_water):
    # Extract the actual old painted water, not bridge-cleared navigation rows.
    # Classification occurs at the old native resolution before mask reduction.
    with Image.open(source_path) as source:
        rgb = np.asarray(source.convert("RGB"), dtype=np.int16)
    r, g, b = np.moveaxis(rgb, -1, 0)
    native_water = (b - r >= 20) & (b - g >= 7) & (rgb.mean(axis=2) >= 108)
    del rgb, r, g, b
    coverage = np.asarray(Image.fromarray(native_water.astype(np.uint8) * 255, "L").resize(
        (SIZE, SIZE), Image.Resampling.BOX), dtype=np.float32) / 255.0
    del native_water
    target = coverage >= 0.5
    candidate = candidate_water >= 0.5

    def signed_distance(mask):
        signed = (ndimage.distance_transform_edt(mask) - ndimage.distance_transform_edt(~mask)) * WORLD_SIZE / SIZE
        return np.clip(0.5 + signed / (2.0 * WATER_SDF_RANGE_WORLD), 0.0, 1.0).astype(np.float32)

    return save_data("water_geometry_lock_data.png", [coverage, signed_distance(target), candidate_water,
                                                        signed_distance(candidate)])


def landmark_field(layout, nodes):
    # Feathered asymmetric oblongs share the surrounding paper tone. They have
    # no opaque white centre, disk rim, shadow, decal, road or icon of their own.
    result = np.zeros((SIZE, SIZE), np.float32)
    scale = SIZE / WORLD_SIZE
    for ident, place in layout["locations"].items():
        node = nodes[ident]
        built = node.get("type") != "wild"
        rx, ry = (92.0, 59.0) if built else (45.0, 34.0)
        cx, cy = place["drawPos"]
        cy -= 13.0 if built else 0.0
        x0, x1 = max(0, int((cx - rx * 2.2) * scale)), min(SIZE, int((cx + rx * 2.2) * scale) + 1)
        y0, y1 = max(0, int((cy - ry * 2.2) * scale)), min(SIZE, int((cy + ry * 2.2) * scale) + 1)
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        dx, dy = (xx / scale - cx) / rx, (yy / scale - cy) / ry
        # The broad irregular feather avoids a visible repeated ellipse.
        phase = int(hashlib.sha256(ident.encode()).hexdigest()[:8], 16) / 0xFFFFFFFF * np.pi * 2
        q = (dx + 0.12 * dy) ** 2 + dy ** 2
        feather = np.exp(-q * 1.55) * (0.94 + 0.06 * np.sin(dx * 3.1 + dy * 2.2 + phase))
        result[y0:y1, x0:x1] = np.maximum(result[y0:y1, x0:x1], feather * (0.94 if built else 0.62))
    return result


def regional_fields(plan, water, grove):
    # Existing natural-area polygons are only colour guidance. The 5% world
    # feather removes polygon edges; their old river/ridge geometry is ignored.
    weights = {
        "镇岳西岭": (0.14, 0.86, 0.06), "玄都台原": (0.04, 1.00, 0.09),
        "归藏盆地": (0.24, 0.54, 0.15), "涵虚水乡": (0.38, 0.08, 0.74),
        "沧溟河港": (0.14, 0.17, 0.90), "玉虚叠嶂": (0.76, 0.28, 0.12),
        "百药南林": (1.00, 0.08, 0.15), "古寺东陵": (0.11, 0.65, 0.27),
        "幽泉深谷": (0.48, 0.15, 0.74),
    }
    total = np.zeros((SIZE, SIZE), np.float32)
    fields = np.zeros((3, SIZE, SIZE), np.float32)
    for area in plan["terrainPlanning"]["areas"]:
        mask = Image.new("L", (SIZE, SIZE), 0)
        points = [(x * SIZE / 100.0, y * SIZE / 100.0) for x, y in area["polygon"]]
        ImageDraw.Draw(mask).polygon(points, fill=255)
        feather = ndimage.gaussian_filter(np.asarray(mask).astype(np.float32) / 255.0, SIZE * 0.035)
        for channel, weight in enumerate(weights[area["name"]]):
            fields[channel] += feather * weight
        total += feather
    # Where the old planning polygons have gaps, gently extend their palette;
    # never draw their outlines or invent corresponding playable terrain.
    fields /= np.maximum(total, 0.28)
    bank = np.exp(-ndimage.distance_transform_edt(water < 0.25) / (SIZE * 0.009))
    fields[2] = np.maximum(fields[2] * 0.88, bank * 0.72)
    return [np.clip(field, 0.0, 1.0) for field in fields] + [grove]


def main():
    global ART, OUT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--art", type=Path, default=ART, help="Read-only square source artwork, any native resolution.")
    parser.add_argument("--output-dir", type=Path, default=OUT)
    parser.add_argument("--profile", choices=("auto", "legacy", "clean"), default="auto",
                        help="auto uses strong cleanup only for the known old master; all new paintings use restrained adaptation.")
    parser.add_argument("--write-parameters", action="store_true",
                        help="Install the profile's suggested presentation.json as well as data masks.")
    parser.add_argument("--water-style", choices=("auto", "blue", "gray-teal"), default="auto")
    parser.add_argument("--water-mask", type=Path, help="Optional explicit candidate water mask, white=water; read-only.")
    parser.add_argument("--prepare-water-geometry-lock", action="store_true",
                        help="Prepare optional old-water authority/SDF; shader remains disabled until reviewed.")
    parser.add_argument("--water-geometry-source", type=Path, default=WATER_GEOMETRY_SOURCE,
                        help="Original native blue-water painting; never use bridge-cleared navigation rows.")
    args = parser.parse_args()
    ART, OUT = args.art.resolve(), args.output_dir.resolve()
    OUT.mkdir(parents=True, exist_ok=True)
    input_paths = [ART, LAYOUT, NAVIGATION, PLAN]
    if args.prepare_water_geometry_lock:
        input_paths.append(args.water_geometry_source.resolve())
    if args.water_mask:
        input_paths.append(args.water_mask.resolve())
    inputs = {path_label(path): digest(path) for path in input_paths}
    source_sha256 = inputs[path_label(ART)]
    profile = ("legacy" if source_sha256 == LEGACY_ART_SHA256 else "clean") if args.profile == "auto" else args.profile
    water_style = ("blue" if profile == "legacy" else "gray-teal") if args.water_style == "auto" else args.water_style
    layout = json.loads(LAYOUT.read_text(encoding="utf-8"))
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    assert layout["worldSize"] == WORLD_SIZE and len(layout["locations"]) == 123
    nodes = {node["id"]: node for node in plan["nodes"]}
    with Image.open(ART) as source:
        source_size = source.size
        if source.width != source.height:
            raise ValueError("Presentation needs the complete square world painting, not a local crop.")
        rgb = np.asarray(source.convert("RGB").resize((SIZE, SIZE), Image.Resampling.BOX), dtype=np.float32) / 255.0
    r, g, b = np.moveaxis(rgb, -1, 0)
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    water = smoothstep(0.055, 0.105, b - r) * smoothstep(0.012, 0.035, b - g)
    if water_style == "gray-teal":
        teal = (smoothstep(0.022, 0.035, g - r) * smoothstep(-0.040, -0.025, b - r)
                * smoothstep(-0.130, -0.105, b - g) * (1.0 - smoothstep(0.80, 0.87, lum)))
        water = np.maximum(water, teal)
    if args.water_mask:
        with Image.open(args.water_mask) as source_mask:
            water = np.asarray(source_mask.convert("L").resize((SIZE, SIZE), Image.Resampling.BOX), dtype=np.float32) / 255.0
    water = ndimage.gaussian_filter(water, 0.42)
    dry = water < 0.15

    # Protect connected ink masses and their pale interiors. Flat paper grain
    # lacks the local density required to seed a protected mountain/woodland.
    density = ndimage.uniform_filter(((lum < 0.705) & dry).astype(np.float32), size=7)
    mass = remove_small((density > 0.16) & dry, 20)
    mass = ndimage.binary_fill_holes(ndimage.binary_closing(mass, iterations=3))
    mass = ndimage.binary_dilation(mass, iterations=2)
    protection = ndimage.gaussian_filter(mass.astype(np.float32), 1.7)
    # Never suppress a prominent dark source stroke even outside mass seeds.
    protection = np.maximum(protection, (1.0 - smoothstep(0.55, 0.73, lum)) * dry)
    protection = np.maximum(protection, np.exp(-ndimage.distance_transform_edt(water < 0.25) / 1.6) * 0.92)

    grove_seed = (g - b > 0.025) & (lum < 0.69) & (g - r > 0.012) & dry
    grove_density = ndimage.uniform_filter(grove_seed.astype(np.float32), size=7)
    grove = ndimage.gaussian_filter(smoothstep(0.07, 0.38, grove_density), 1.5) * dry
    # B is a low-frequency luminance measurement, not a colour image. It lets
    # the shader reduce flat-ground contrast without filtering source rock ink.
    paper_luminance = ndimage.gaussian_filter(lum, 7.0)
    clearing = landmark_field(layout, nodes) * (1.0 - water)
    eligible = (protection < 0.20) & dry & (lum > 0.73)
    fine_contrast = float(np.std((lum - ndimage.gaussian_filter(lum, 1.2))[eligible])) if eligible.any() else 0.0
    colourfulness = float((rgb.max(axis=2) - rgb.min(axis=2))[dry].mean()) if dry.any() else 0.0
    if profile == "legacy":
        suggested = {"plain_cleanup": 0.68, "landmark_cleanup": 0.74, "regional_strength": 0.80,
                     "forest_strength": 0.72, "rock_strength": 0.66, "water_strength": 0.84,
                     "water_detail": 0.32, "paper_color": "#eae6d9", "forest_color": "#67815b",
                     "rock_color": "#8c826d", "water_color": "#9eb1af"}
    else:
        # A newly painted clean/coloured map supplies its own art direction.
        # Only faint residual filler is reduced; the old strong recolour is off.
        cleanup = float(np.clip(0.06 + fine_contrast * 9.0, 0.06, 0.22))
        region = float(np.clip(0.20 - colourfulness * 1.20, 0.06, 0.16))
        paper_sample = np.median(rgb[eligible], axis=0) if eligible.any() else np.array([0.916, 0.901, 0.850])
        paper_hex = "#" + "".join(f"{int(np.clip(channel * 255 + 0.5, 0, 255)):02x}" for channel in paper_sample)
        water_sample = np.median(rgb[water > 0.8], axis=0) if np.any(water > 0.8) else np.array([0.55, 0.62, 0.58])
        water_hex = "#" + "".join(f"{int(np.clip(channel * 255 + 0.5, 0, 255)):02x}" for channel in water_sample)
        suggested = {"plain_cleanup": round(cleanup, 4), "landmark_cleanup": 0.18,
                     "regional_strength": round(region, 4), "forest_strength": 0.30,
                     "rock_strength": 0.26, "water_strength": 0.12, "water_detail": 0.72,
                     "paper_color": paper_hex, "forest_color": "#67815b", "rock_color": "#8c826d",
                     "water_color": water_hex}
    suggested.update(plain_region_strength=0.0 if profile == "legacy" else 0.16,
                     plain_region_green_color="#8daa70", plain_region_earth_color="#b39c77", plain_region_cool_color="#9badae",
                     water_native_mode=0.0 if water_style == "blue" else 1.0,
                     water_geometry_lock=0.0, water_geometry_max_repair_world=24.0,
                     water_geometry_sdf_range_world=WATER_SDF_RANGE_WORLD)
    profile_config = {"version": 2, "role": "Display-only parameters; clean paintings retain their own native colour.",
                      "profile": profile, "source_art_path": path_label(ART), "source_art_sha256": source_sha256,
                      "shader_parameters": suggested,
                      "adaptation_measurements": {"flat_fine_contrast": fine_contrast, "land_colourfulness": colourfulness},
                      "adjustment_notes": {"plain_region_strength": "New painted map: 0.10–0.20 faint regional chroma on bright plains, luminance preserved; source rock and water excluded.",
                                           "regional_strength": "Separate older rock/woodland grading, deliberately weak on the new coloured painting.",
                                           "water_geometry_lock": "Default 0. Enable only after comparing actual shore views; requires prepared original-water/SDF data.",
                                           "water_geometry_max_repair_world": "World units, independent of 6270/5016 native raster sizes; 24 world units is about 16.3 new 5016-pixel texels."},
                      "geography": "Colour/cleanup only. Navigation, 123 locations and continuous paths are unchanged."}
    (OUT / "suggested_presentation.json").write_text(json.dumps(profile_config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.write_parameters:
        (OUT / "presentation.json").write_text(json.dumps(profile_config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    terrain = save_data("terrain_presentation_data.png", [water, protection, paper_luminance, clearing])
    region_channels = regional_fields(plan, water, grove)
    regional = save_data("regional_presentation_data.png", region_channels)
    geometry = prepare_water_geometry(args.water_geometry_source.resolve(), water) if args.prepare_water_geometry_lock else None
    # A diagnostic of scalar fields only, never a modified copy of the artwork.
    diagnostic = Image.new("RGB", (1024, 1024))
    for channel, offset in ((protection, (0, 0)), (water, (512, 0)), (clearing, (0, 512))):
        panel = Image.fromarray(np.clip(channel * 255, 0, 255).astype(np.uint8), "L")
        diagnostic.paste(panel.resize((512, 512), Image.Resampling.BOX).convert("RGB"), offset)
    regional_rgb = np.stack(region_channels[:3], axis=-1)
    panel = Image.fromarray(np.clip(regional_rgb * 255, 0, 255).astype(np.uint8), "RGB")
    diagnostic.paste(panel.resize((512, 512), Image.Resampling.BOX), (512, 512))
    ImageDraw.Draw(diagnostic).text((10, 10), "SOURCE MASS PROTECTION", fill="#ff5830")
    ImageDraw.Draw(diagnostic).text((522, 10), "NATIVE WATER COVERAGE", fill="#ff5830")
    ImageDraw.Draw(diagnostic).text((10, 522), "EXISTING 123 ANCHORS", fill="#ff5830")
    ImageDraw.Draw(diagnostic).text((522, 522), "GREEN / EARTH / COOL WEIGHTS", fill="#ff5830")
    diagnostic.save(OUT / "mask_diagnostic.png", optimize=True)
    manifest = {
        "version": 2, "purpose": "Native Godot display-only scalar masks. No gameplay consumer.",
        "world_size": WORLD_SIZE, "world_km": 8000.0, "named_locations": 123,
        "source_art_path": path_label(ART), "source_art_sha256": source_sha256,
        "source_art_pixels": list(source_size), "mask_pixels": [SIZE, SIZE],
        "profile": profile, "suggested_shader_parameters": suggested,
        "water_style": water_style,
        "water_geometry_lock": {"prepared": geometry is not None, "enabled": False,
                                "source": path_label(args.water_geometry_source) if geometry else None,
                                "sdf_range_world_units": WATER_SDF_RANGE_WORLD,
                                "maximum_repair_world_units": 24.0,
                                "authority": "Original painted blue-water coverage, including actual water below later bridge sprites. Never nav rows.",
                                "scope": "Only discrepancies close to BOTH target and candidate shores. Large deviations require local art review."},
        "parameters_installed": args.write_parameters,
        "sampling": "clamp((tile_origin + UV * tile_size) / map_size, 0, 1); linear; repeat disabled; not sRGB",
        "inputs_sha256": inputs, "generator_sha256": digest(Path(__file__)),
        "files": [terrain, regional] + ([geometry] if geometry else []),
        "channels": {
            "terrain_presentation_data.png": {"R": "Source blue or low-confidence gray-teal water coverage, or explicit --water-mask; shader checks native texel again",
                "G": "preserve connected rock/woodland ink masses, pale interiors, dark contours and banks",
                "B": "7 mask-pixel Gaussian source luminance measurement for flat-ground contrast compression",
                "A": "123 existing named-location soft asymmetric clearing weights; not a white decal"},
            "regional_presentation_data.png": {"R": "green wash from existing Yuxu/Baiyao woodland/valley regions",
                "G": "earth wash from existing western/northern/eastern rock/highland regions",
                "B": "cool grey wash from existing water/valley regions plus actual source-bank proximity",
                "A": "native source woodland tendency from warm-green dark crown clusters"},
            "water_geometry_lock_data.png": {"R": "Original native blue-water scalar coverage, normalized to world coordinates",
                "G": "Original water signed distance; positive inside, encoded 0.5 + world_distance/(2*96)",
                "B": "Candidate painted water evidence, blue or low-confidence gray-teal or author supplied in later audits",
                "A": "Candidate water signed distance; positive inside, same world-unit encoding"},
        },
        "rules": {"source_resampling": "Only logical measurements use BOX reduction. Original art and nine tiles are never written or resampled.",
            "protection": "Local dark density >0.16 over 7x7 at 2048; connected components >=20; close/fill/dilate and feather; native dark lines remain protected.",
            "regions": "Nine existing terrainPlanning.areas polygons, 3.5% world Gaussian feather; artistic weights only. Original planning waters/ridges are not applied.",
            "clearings": "Built 92x59 world-unit e-folding field, wild 45x34; feathered, asymmetric, low opacity, same nearby paper tone; water excluded.",
            "navigation": "Input SHA recorded only. No navigation/layout/path writes and no playable climate/terrain definitions."},
        "coverage": {"water_mean": float(water.mean()), "protected_mean": float(protection.mean()),
            "clearings_mean": float(clearing.mean()), "grove_mean": float(grove.mean())},
    }
    # Data PNG imports must retain literal scalar values in all four channels.
    for file in (terrain, regional, *([geometry] if geometry else [])):
        png = OUT / Path(file["path"]).name
        import_path = png.with_suffix(png.suffix + ".import")
        if import_path.exists():
            text = import_path.read_text(encoding="utf-8")
            import_path.write_text(text.replace("process/fix_alpha_border=true", "process/fix_alpha_border=false"), encoding="utf-8")
    (OUT / "mask_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    # Prove the read-only art/geography inputs remained untouched.
    assert all(digest(ROOT / path) == sha for path, sha in inputs.items())
    print(json.dumps({"mask_size": SIZE, "inputs_unchanged": True, "profile": profile,
                      "source_art_sha256": source_sha256, "suggested_shader_parameters": suggested, "coverage": manifest["coverage"],
                      "files": manifest["files"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
