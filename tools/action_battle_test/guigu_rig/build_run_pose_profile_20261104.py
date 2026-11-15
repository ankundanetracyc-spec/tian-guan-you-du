"""Build a compact, read-only-source motion reference for the project skeleton.

Only the requested new profile is written. The existing source exports and
analysis snapshots are never regenerated or changed. This samples located
source curves; it does not reproduce the original Animator or DynamicBone.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[3]
READBACK_DIR = PROJECT_ROOT / "output/guigu_cloth_readback_20261104"
DEFAULT_OUTPUT = PROJECT_ROOT / "tools/action_battle_test/guigu_run_pose_profile_20261104.json"
SAMPLE_COUNT = 64
PHASE_MEASUREMENT_COUNT = 240


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def wrap_angle(value: float) -> float:
    return (value + math.pi) % math.tau - math.pi


def load_source_functions() -> tuple[dict, Path]:
    analysis_path = READBACK_DIR / "analyze_full_gait.py"
    source = analysis_path.read_text(encoding="utf-8")
    marker = "\nreports={}\n"
    if source.count(marker) != 1:
        raise ValueError("Cannot safely isolate the read-only source evaluator")
    prefix = source.split(marker, 1)[0]
    namespace = {"__file__": str(analysis_path), "__name__": "source_gait_functions"}
    exec(compile(prefix, str(analysis_path), "exec"), namespace)
    return namespace, analysis_path


def arm_sample(frame: dict, side: str) -> tuple[list[float], float]:
    arm = frame[side]
    shoulder, elbow, wrist = arm["shoulder"], arm["elbow"], arm["wrist"]
    chain_length = math.dist(shoulder, elbow) + math.dist(elbow, wrist)
    if chain_length <= 0.0:
        raise ValueError("Degenerate source arm chain")
    target = [
        float(wrist[0] - shoulder[0]) / chain_length,
        -float(wrist[1] - shoulder[1]) / chain_length,
    ]
    upper_angle = math.atan2(-float(elbow[1] - shoulder[1]), float(elbow[0] - shoulder[0]))
    if math.hypot(*target) > 1.000001:
        raise ValueError("Normalized source wrist exceeds its arm chain length")
    return target, upper_angle


def make_profile() -> dict:
    source, evaluator_path = load_source_functions()
    rig, animations = source["rig"], source["animations"]
    if rig["source"]["sha256"] != animations["source"]["sha256"]:
        raise ValueError("Rig and animation source hashes differ")
    clips = {}
    excluded = {}
    for clip in animations["clips"]:
        name = clip["name"]
        if name not in ("Run_New1", "Run_New2"):
            continue
        curves = source["curves_for"](clip)
        duration = float(clip["duration"])
        samples = []
        for index in range(SAMPLE_COUNT):
            phase = index / SAMPLE_COUNT
            frame = source["frame"](clip, curves, phase * duration)
            near_wrist, near_upper = arm_sample(frame, "right")
            far_wrist, far_upper = arm_sample(frame, "left")
            torso = frame["neck"] - frame["waist"]
            cloth = frame["cloth_mid"] - frame["cloth_root"]
            sample = {
                "phase": phase,
                "near_wrist": near_wrist,
                "far_wrist": far_wrist,
                "near_upper_angle": near_upper,
                "far_upper_angle": far_upper,
                "body_lean": math.atan2(float(torso[0]), float(torso[1])),
                "cloth_root_angle": wrap_angle(math.atan2(-float(cloth[1]), float(cloth[0])) - math.pi / 2),
            }
            values = [phase, *near_wrist, *far_wrist, near_upper, far_upper, sample["body_lean"], sample["cloth_root_angle"]]
            if not all(math.isfinite(value) for value in values):
                raise ValueError(f"Non-finite source sample: {name} phase {phase}")
            samples.append(sample)
        dense_frames = [
            source["frame"](clip, curves, duration * index / PHASE_MEASUREMENT_COUNT)
            for index in range(PHASE_MEASUREMENT_COUNT)
        ]
        frontmost = {}
        for target_side, source_side in (("near", "right"), ("far", "left")):
            front_index = max(
                range(PHASE_MEASUREMENT_COUNT),
                key=lambda index: float(dense_frames[index][source_side]["ankle"][0] - dense_frames[index][source_side]["hip"][0]),
            )
            frontmost[target_side] = front_index / PHASE_MEASUREMENT_COUNT
        clips[name] = {
            "sourcePathId": clip["sourcePathId"],
            "duration": duration,
            "sampleCount": SAMPLE_COUNT,
            "nearFrontmostPhase": frontmost["near"],
            "farFrontmostPhase": frontmost["far"],
            "frontmostPhaseMeasurementCount": PHASE_MEASUREMENT_COUNT,
            "samples": samples,
        }
        excluded[name] = [
            {key: binding.get(key) for key in ("pathHash", "attributeId", "curveIndex", "width")}
            for binding in clip["bindings"]
            if binding["path"] is None or not binding["targetRigPath"]
        ]
    if set(clips) != {"Run_New1", "Run_New2"}:
        raise ValueError("Required source movement clips are missing")
    return {
        "schemaVersion": 1,
        "source": {
            "assetPath": animations["source"]["path"],
            "assetSha256": animations["source"]["sha256"],
            "assetSizeBytes": animations["source"]["sizeBytes"],
            "unityVersion": animations["source"]["unityVersion"],
            "character": "10002",
            "rigRootPath": animations["rigRootPath"],
            "readbackDirectory": READBACK_DIR.relative_to(PROJECT_ROOT).as_posix(),
            "inputHashes": {name: file_hash(READBACK_DIR / name) for name in ("rig.json", "animations.json")},
            "evaluator": evaluator_path.relative_to(PROJECT_ROOT).as_posix(),
            "evaluatorSha256": file_hash(evaluator_path),
            "evaluation": "Existing evaluator definitions only, before reports={}; complete located hierarchy, cubic streamed curves and constant channels; unknown path bindings excluded.",
            "excludedUnknownBindings": excluded,
            "limitations": [
                "Not a reproduction of the original Animator state machine, blending, ground contacts, or DynamicBone solver.",
                "Run_New2 is a source pose reference; its in-game state selection is not established by its name.",
                "Normalized targets and measured angles are references for retargeting; do not copy source bone lengths or animated local translations.",
            ],
        },
        "coordinates": {
            "x": "forward/right in unmirrored actor space",
            "y": "down",
            "angles": "radians in Godot y-down actor space",
            "near": "source right/youbi, held forward, foreground render layer",
            "far": "source left/zuobi, swinging arm, rear render layer",
            "wrist": "wrist minus same-side shoulder, divided by that frame's same-side shoulder-elbow-wrist chain length",
            "upper_angle": "absolute projected shoulder-to-elbow direction; use to choose IK bend side",
            "body_lean": "atan2(neck.x-waist.x, neck.y-waist.y) in source y-up; positive is forward lean",
            "cloth_root_angle": "wrapped Godot root-to-mid direction minus PI/2; positive trails backward",
            "phase": "source clip time divided by duration, samples [0,1); interpolate cyclically",
            "alignment": "source phase = current near-leg phase + nearFrontmostPhase; far-leg phase supplied separately includes an additional half-cycle",
        },
        "clips": clips,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    profile = make_profile()
    args.output.write_text(json.dumps(profile, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.output), "sampleCounts": {key: value["sampleCount"] for key, value in profile["clips"].items()}, "assetSha256": profile["source"]["assetSha256"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
