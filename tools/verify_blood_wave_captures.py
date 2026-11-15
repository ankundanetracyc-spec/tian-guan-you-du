"""Check actual eight-direction Godot captures against their empty baseline."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageChops


def changed_mask(image: Image.Image, baseline: Image.Image) -> Image.Image:
    channels = ImageChops.difference(image.convert("RGB"), baseline.convert("RGB")).split()
    maximum = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2])
    return maximum.point(lambda value: 255 if value else 0)


def mask_points(mask: Image.Image, offset: tuple[int, int]):
    width, _ = mask.size
    for index, value in enumerate(mask.tobytes()):
        if value:
            yield offset[0] + index % width, offset[1] + index // width


def point_bbox(points: list[tuple[int, int]]) -> tuple[int, int, int, int] | None:
    if not points:
        return None
    return (min(point[0] for point in points), min(point[1] for point in points),
            max(point[0] for point in points) + 1, max(point[1] for point in points) + 1)


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, nargs="?",
                        default=root / "output/blood_wave_eight_directions_20261104")
    parser.add_argument("--log", type=Path,
                        default=root / "output/blood_wave_pixels_20261104.log")
    args = parser.parse_args()
    lines: list[str] = []
    failures: list[str] = []
    checks = 0

    def check(condition: bool, message: str) -> None:
        nonlocal checks
        checks += 1
        if not condition:
            failures.append(message)

    info = json.loads((args.source / "capture_info.json").read_text(encoding="utf-8"))
    files = sorted(args.source.glob("frame_*.png"))
    check(len(files) == 78 and info["frames"] == 78,
          f"Expected 78 captured frames, found files={len(files)}, metadata={info['frames']}")
    with Image.open(args.source / "frame_000.png") as source:
        baseline = source.convert("RGB")
    frames = {}
    for frame_number in (20, 36, 77):
        with Image.open(args.source / f"frame_{frame_number:03d}.png") as source:
            frames[frame_number] = source.convert("RGB")
    final_difference = ImageChops.difference(frames[77], baseline)
    check(final_difference.getbbox() is None,
          f"Final frame differs from baseline; bbox={final_difference.getbbox()}")
    lines.append(f"BASELINE final_frame_77_equals_frame_0={final_difference.getbbox() is None}")
    front_mask = changed_mask(frames[20], baseline)
    wake_mask = changed_mask(frames[36], baseline)
    total_front_pixels = 0
    total_wake_pixels = 0

    for case in info["cases"]:
        left, top, width, height = map(int, case["panel_rect"])
        box = (left, top, left + width, top + height)
        front_points = list(mask_points(front_mask.crop(box), (left, top)))
        wake_points = list(mask_points(wake_mask.crop(box), (left, top)))
        total_front_pixels += len(front_points)
        total_wake_pixels += len(wake_points)
        name = case["name"]
        check(bool(front_points), f"{name}: frame20 has no blood-wave pixels")
        check(bool(wake_points), f"{name}: frame36 has no lingering wake pixels")
        origin_x, origin_y = case["origin"]
        scale = case["visual_scale"]
        projection = case["y_projection"]
        heading = case["direction_angle"]
        maximum_radius = 0.0
        maximum_angle = 0.0
        radius_failures = []
        angle_failures = []
        for pixel_x, pixel_y in front_points:
            # Test the center of the actual rasterized pixel in the ground plane.
            ground_x = (pixel_x + 0.5 - origin_x) / scale
            ground_y = (pixel_y + 0.5 - origin_y) / (scale * projection)
            radius = math.hypot(ground_x, ground_y)
            maximum_radius = max(maximum_radius, radius)
            delta = math.atan2(ground_y, ground_x) - heading
            angle = abs(math.degrees(math.atan2(math.sin(delta), math.cos(delta))))
            if radius > case["max_radius"] + 6.0:
                radius_failures.append((pixel_x, pixel_y))
            if radius >= 20.0:
                maximum_angle = max(maximum_angle, angle)
                if angle > info["fan_degrees"] * 0.5 + 2.0:
                    angle_failures.append((pixel_x, pixel_y))
        check(not radius_failures,
              f"{name}: {len(radius_failures)} pixels exceed r=226; "
              f"max_r={maximum_radius:.4f}, bbox={point_bbox(radius_failures)}")
        check(not angle_failures,
              f"{name}: {len(angle_failures)} pixels exceed half-angle=62 degrees outside r<20; "
              f"max_angle={maximum_angle:.4f}, bbox={point_bbox(angle_failures)}")
        lines.append(f"CASE {name}: frame20_pixels={len(front_points)} "
                     f"bbox={point_bbox(front_points)} max_radius={maximum_radius:.4f} "
                     f"max_half_angle={maximum_angle:.4f} "
                     f"frame36_wake_pixels={len(wake_points)} "
                     f"wake_bbox={point_bbox(wake_points)}")

    lines.append("TOLERANCES radius=6_design_units half_angle=2_degrees "
                 "angle_exclusion_radius=20_design_units changed_pixel_threshold=0")
    lines.extend(f"FAIL {failure}" for failure in failures)
    status = "FAIL" if failures else "OK"
    lines.append(f"BLOOD_WAVE_PIXEL_CHECKS_{status} checks={checks} "
                 f"failures={len(failures)} directions={len(info['cases'])} "
                 f"front_pixels={total_front_pixels} wake_pixels={total_wake_pixels}")
    report = "\n".join(lines) + "\n"
    args.log.parent.mkdir(parents=True, exist_ok=True)
    args.log.write_text(report, encoding="utf-8")
    print(report, end="")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
