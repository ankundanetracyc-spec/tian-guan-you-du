"""Render-reference vertex snapshots from freshly re-read original Unity data.

Reads the original vertices, weights, bind poses, and complete located curves.
Writes only source_reference data; no existing runtime files are changed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np


PROJECT = Path(__file__).resolve().parents[2]
BASE = PROJECT / "tools/action_battle_test"
DEFAULT_OUTPUT = BASE / "output/guigu_source_reference_v2"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--readback", type=Path, default=DEFAULT_OUTPUT / "readback")
parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
parser.add_argument("--sample-count", type=int, default=48)
parser.add_argument("--all-clips", action="store_true")
ARGS = parser.parse_args()
OUTPUT = ARGS.output.resolve()
READBACK = ARGS.readback.resolve()
ANALYSIS = PROJECT / "output/guigu_cloth_readback_20261104/analyze_full_gait.py"
SAMPLES = ARGS.sample_count
if SAMPLES < 8:
    raise ValueError("At least eight samples are required")


def load_evaluator() -> dict:
    text = ANALYSIS.read_text(encoding="utf-8").split("\nreports={}\n", 1)[0]
    namespace = {"__file__": str(READBACK / "source_reference_evaluator.py"), "__name__": "source_reference_evaluator"}
    exec(compile(text, str(ANALYSIS), "exec"), namespace)
    return namespace


E = load_evaluator()
RIG = E["rig"]
ANIMATIONS = E["animations"]
SKINS = json.loads((READBACK / "skins.json").read_text(encoding="utf-8"))
NODES = RIG["nodes"]
MID = next((node["path"] for node in NODES if node["path"].endswith("/shenti2_1")), "")
TIP = MID + "/shenti2_2"
BONE_PATHS = [node["path"] for node in NODES]


def evaluate_world(clip: dict, curves: dict, time: float, secondary_delta: float = 0.0) -> dict:
    local = {node["path"]: {
        "p": list(node["localTRS"]["position"]),
        "r": E["quat_matrix"](node["localTRS"]["rotationQuaternionXYZW"]),
        "s": list(node["localTRS"]["scale"]),
    } for node in NODES}
    for binding in clip["bindings"]:
        path = binding["targetRigPath"]
        if binding["path"] is None or not path:
            continue
        attribute = binding["attributeId"]
        if attribute not in (1, 2, 3, 4):
            continue
        values = [E["channel"](clip, curves, binding["curveIndex"] + component, time) for component in range(binding["width"])]
        if attribute == 1:
            local[path]["p"] = values
        elif attribute == 2:
            local[path]["r"] = E["quat_matrix"](values)
        elif attribute == 3:
            local[path]["s"] = values
        else:
            local[path]["r"] = E["euler_matrix"](values)
    if secondary_delta and MID in local:
        local[MID]["r"] = local[MID]["r"] @ E["euler_matrix"]([0, 0, math.degrees(secondary_delta)])
    world = {}
    for node in NODES:
        path, data = node["path"], local[node["path"]]
        matrix = np.eye(4)
        matrix[:3, :3] = data["r"] @ np.diag(data["s"])
        matrix[:3, 3] = data["p"]
        world[path] = world[node["parentPath"]] @ matrix if node["parentPath"] else matrix
    return world


def project(vertex: np.ndarray) -> list[float]:
    return [round(float(vertex[0]) * 100, 5), round(-float(vertex[1]) * 100, 5)]


MESHES = [mesh for mesh in SKINS["meshes"] if mesh["activeInHierarchy"] and mesh["enabled"] and mesh["category"] == "character"]
MESHES.sort(key=lambda mesh: float(mesh["rendererToRigRoot"][2][3]), reverse=True)


def deform(mesh: dict, world: dict) -> list[list[float]]:
    skin = mesh["skinning"]
    original = np.array([[*vertex[:3], 1.0] for vertex in mesh["vertices"]], dtype=float)
    if not skin["isSkinned"]:
        return [project(world[mesh["rendererPath"]] @ vertex) for vertex in original]
    matrices = [world[bone["rigPath"]] @ np.array(bindpose) for bone, bindpose in zip(skin["spriteBones"], skin["bindposeMatrices"])]
    positions = []
    for vertex, influences in zip(original, skin["boneWeights"]):
        posed = np.zeros(4)
        for influence in influences:
            posed += float(influence["weight"]) * (matrices[int(influence["boneIndex"])] @ vertex)
        positions.append(project(posed))
    return positions


def sample_clips() -> dict:
    clips = {}
    for clip in ANIMATIONS["clips"]:
        if not ARGS.all_clips and clip["name"] not in ("Idle", "Run_New1", "Run_New2"):
            continue
        curves = E["curves_for"](clip)
        duration = float(clip["duration"])
        originals, targets = [], []
        for index in range(SAMPLES):
            world = evaluate_world(clip, curves, duration * index / SAMPLES)
            originals.append(world)
            tip_delta = world[TIP][:3, 3] - world[MID][:3, 3] if TIP in world and MID in world else np.array([0., -1., 0.])
            targets.append(math.atan2(float(tip_delta[1]), float(tip_delta[0])))
        # A separately labelled surrogate, never presented as the original solver.
        # Root/mid attachment follows the original clip; the final segment has
        # damped angular lag and a modest gravitational return.
        angle, speed, offsets = targets[0], 0.0, []
        dt = duration / SAMPLES
        for step in range(SAMPLES * 5):
            target = targets[step % SAMPLES]
            for _ in range(6):
                error = (target - angle + math.pi) % math.tau - math.pi
                speed += (error * 95.0 - speed * 13.0 - math.cos(angle) * 9.0) * dt / 6
                angle += speed * dt / 6
            if step >= SAMPLES * 4:
                offsets.append((angle - target + math.pi) % math.tau - math.pi)
        frames, secondary_frames, bone_frames, bone_transforms = [], [], [], []
        for index, world in enumerate(originals):
            frames.append([deform(mesh, world) for mesh in MESHES])
            bone_frames.append([project(world[path][:, 3]) for path in BONE_PATHS])
            bone_transforms.append([[
                *project(world[path][:, 3]),
                round(math.atan2(-float(world[path][1, 0]), float(world[path][0, 0])), 7),
            ] for path in BONE_PATHS])
            secondary_world = evaluate_world(clip, curves, duration * index / SAMPLES, offsets[index])
            secondary_frames.append([deform(mesh, secondary_world) for mesh in MESHES])
        clips[clip["name"]] = {
            "duration": duration,
            "sampleCount": SAMPLES,
            "loop": bool(clip.get("loopTime", False)),
            "frames": frames,
            "secondaryFrames": secondary_frames,
            "boneFrames": bone_frames,
            "boneTransforms": bone_transforms,
            "secondaryAngleOffsets": offsets,
            "excludedBindings": [{key: binding.get(key) for key in ("pathHash", "attributeId", "curveIndex")} for binding in clip["bindings"] if binding["path"] is None or not binding["targetRigPath"]],
        }
    return clips


def main() -> None:
    source = ANIMATIONS["source"]
    assert source["sha256"] == SKINS["source"]["sha256"] == RIG["source"]["sha256"]
    data = {
        "schemaVersion": 1,
        "source": source,
        "rigRootPath": RIG["rigRootPath"],
        "textureRoot": "textures",
        "bonePaths": BONE_PATHS,
        "method": "Source world bone matrices * source bindpose matrices * original Sprite-local vertices, original unmodified weights. Cubic streamed channels and constant channels, unknown paths excluded, 3D source transforms projected to XY.",
        "visibility": "Only active, enabled character meshes; hidden second legs and transient effects excluded. Back to front by original renderer root-space Z (higher Z first).",
        "secondaryLimit": "secondaryFrames adds a project-authored damped final-cloth-segment surrogate; neither DynamicBone parameters nor the original Animator were reconstructed.",
        "meshes": [{
            "spriteId": mesh["sourceSpriteId"],
            "name": mesh["sourceSpriteName"],
            "rendererPath": mesh["rendererPath"],
            "sourceZ": mesh["rendererToRigRoot"][2][3],
            "uv": [[float(uv[0]), 1.0-float(uv[1])] for uv in mesh["uvConvention"]["uvCropLowerLeft"]],
            "triangles": mesh["triangles"],
            "sourceSkinBoneCount": len(mesh["skinning"]["spriteBones"]),
            "sourceImage": "textures/sprite_%d.png" % int(mesh["sourceSpriteId"]),
            "category": "face" if mesh["rendererPath"].endswith("/lian") else ("hair" if "/toufa/" in mesh["rendererPath"] else ("leg" if "/Foot/" in mesh["rendererPath"] else ("sleeve" if "/youbi/" in mesh["rendererPath"] or "/zuobi/" in mesh["rendererPath"] else "body"))),
        } for mesh in MESHES],
        "clips": sample_clips(),
        "inputHashes": {name: hashlib.sha256((READBACK/name).read_bytes()).hexdigest() for name in ("rig.json", "skins.json", "animations.json")},
    }
    output = OUTPUT / "source_reference_vertex_frames.json"
    OUTPUT.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(json.dumps({"output": str(output), "meshes": len(MESHES), "clips": {name: clip["sampleCount"] for name, clip in data["clips"].items()}}, ensure_ascii=False))


if __name__ == "__main__":
    main()
