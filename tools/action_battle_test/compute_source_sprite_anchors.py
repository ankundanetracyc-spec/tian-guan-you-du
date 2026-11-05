"""Recover exact male101 source UV joint anchors for high-resolution registration.

Reads original bindposes, bone pivots, mesh UVs and original alpha. It never
changes the runtime adapter, source model, or generated replacement images.
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / "tools/action_battle_test/output/guigu_source_reference_v2/male101"
DEST = ROOT / "docs/美术规范/鬼谷原作网格与素材重构_20261104/源模型局部锚点.json"


def alpha_landmarks(alpha: np.ndarray) -> dict:
    ys, xs = np.nonzero(alpha)
    if not len(xs):
        raise ValueError("Empty source alpha")
    top, bottom = int(ys.min()), int(ys.max())
    top_x = np.nonzero(alpha[top])[0]
    bottom_x = np.nonzero(alpha[bottom])[0]
    top_center = float(np.average(top_x + .5, weights=alpha[top, top_x]))
    bottom_center = float(np.average(bottom_x + .5, weights=alpha[bottom, bottom_x]))
    return {
        "bbox": [int(xs.min()), top, int(xs.max())+1, bottom+1],
        "top": [top_center, float(top)],
        "bottom": [bottom_center, float(bottom+1)],
        "bottom_left": [float(bottom_x.min()), float(bottom+1)],
        "bottom_right": [float(bottom_x.max()+1), float(bottom+1)],
    }


def role_for_bone(name: str) -> str:
    for prefix in ("youbi", "zuobi"):
        if name == prefix + "1": return "shoulder"
        if name == prefix + "2": return "elbow"
        if name == prefix + "3": return "wrist"
    return {"tui1":"hip", "tui2":"knee", "tui3":"ankle", "shenti1":"upper_body", "shenti2":"cloth_root"}.get(name, name)


def main() -> None:
    rig_path, skins_path = MODEL / "rig.json", MODEL / "skins.json"
    rig = json.loads(rig_path.read_text(encoding="utf-8"))
    skins = json.loads(skins_path.read_text(encoding="utf-8"))
    world = {node["path"]: np.array(node["restMatrix"], dtype=float) for node in rig["nodes"]}
    head_path = next(path for path in world if path.endswith("/shenti1/tou"))
    hair_mount = next(path for path in world if path.endswith("/shenti1/tou/toufa"))
    sprites, residuals = {}, []
    for mesh in skins["meshes"]:
        if mesh["category"] != "character": continue
        sprite_id = str(mesh["sourceSpriteId"])
        image_path = MODEL / "textures" / f"sprite_{sprite_id}.png"
        image = Image.open(image_path).convert("RGBA")
        width, height = image.size
        source_xy = np.array(mesh["vertices"], dtype=float)[:, :2]
        design = np.c_[source_xy, np.ones(len(source_xy))]
        source_uv_lower = np.array(mesh["uvConvention"]["uvCropLowerLeft"], dtype=float)
        fit, _, rank, _ = np.linalg.lstsq(design, source_uv_lower, rcond=None)
        if rank != 3: raise ValueError(f"Non-invertible source UV mapping: {sprite_id}")
        error = float(np.max(np.abs(design @ fit - source_uv_lower)))
        if error > 1e-10: raise ValueError(f"Source localXY -> UV is not affine: {sprite_id}: {error}")
        residuals.append(error)
        # Column-vector mapping [sprite_local_x, sprite_local_y, 1] -> top-left UV.
        to_uv = np.array([fit[:, 0], -fit[:, 1]])
        to_uv[1, 2] += 1.0
        landmarks = alpha_landmarks(np.array(image)[:, :, 3])
        anchors = []

        def from_local(name: str, local: np.ndarray, kind: str, path: str | None = None, extra: dict | None = None) -> None:
            uv = to_uv @ np.array([local[0], local[1], 1.0])
            anchor = {
                "name": name,
                "kind": kind,
                "source_uv": uv.tolist(),
                "source_pixels": (uv * np.array([width, height])).tolist(),
                "sprite_local": local[:3].tolist(),
                "rigPath": path,
            }
            if extra: anchor.update(extra)
            if not all(math.isfinite(value) for value in anchor["source_uv"]): raise ValueError("Nonfinite anchor")
            anchors.append(anchor)

        def from_pixel(name: str, xy: list[float], definition: str) -> None:
            uv = [xy[0]/width, xy[1]/height]
            anchors.append({"name":name, "kind":"visible_alpha_extent", "source_uv":uv, "source_pixels":xy, "definition":definition})

        for index, (bone, bindpose) in enumerate(zip(mesh["skinning"]["spriteBones"], mesh["skinning"]["bindposeMatrices"])):
            bp = np.array(bindpose, dtype=float)
            pivot = np.linalg.inv(bp) @ np.array([0., 0., 0., 1.])
            pivot /= pivot[3]
            if not np.allclose(bp @ pivot, [0,0,0,1], atol=1e-9): raise ValueError("Inverse-bindpose pivot round trip failed")
            role = role_for_bone(bone["name"])
            if sprite_id == "87679": role = "hair_" + bone["name"]
            from_local(role, pivot, "inverse_source_bindpose_pivot", bone["rigPath"], {"boneName":bone["name"], "boneIndex":index})

        if sprite_id in ("85649", "65806", "87679"):
            inverse_renderer = np.linalg.inv(np.array(mesh["rendererToRigRoot"], dtype=float))
            for role, path in (("head_neck", head_path), ("hair_mount", hair_mount)):
                local = inverse_renderer @ world[path] @ np.array([0.,0.,0.,1.])
                from_local(role, local, "inverse_renderer_times_rig_bone_pivot", path,
                           {"poseBasis":"source assembled prefab matrices; external mount anchor, not a skinned bindpose pivot"})
        if sprite_id == "85649":
            inverse_renderer = np.linalg.inv(np.array(mesh["rendererToRigRoot"], dtype=float))
            for side, stem in (("near","youbi"),("far","zuobi")):
                path = next(path for path in world if path.endswith(f"/{stem}/{stem}/{stem}1"))
                from_local(side+"_shoulder_mount", inverse_renderer @ world[path] @ np.array([0.,0.,0.,1.]),
                           "inverse_renderer_times_rig_bone_pivot", path,
                           {"poseBasis":"external shoulder mount in the assembled prefab; not a source cloth bindpose bone"})

        if sprite_id == "87557":
            from_pixel("sole_center", landmarks["bottom"], "At last nonzero-alpha row: alpha-weighted pixel-center X, bottom pixel edge Y=max_nonzero_y+1")
            from_pixel("sole_left", landmarks["bottom_left"], "Left outer pixel edge on the last nonzero-alpha row")
            from_pixel("sole_right", landmarks["bottom_right"], "Right outer pixel edge on the last nonzero-alpha row")
        elif sprite_id == "65806":
            from_pixel("head_crown", landmarks["top"], "First nonzero-alpha row: alpha-weighted pixel-center X, top pixel edge Y")
            bbox = landmarks["bbox"]
            from_pixel("head_alpha_center", [(bbox[0]+bbox[2])/2, (bbox[1]+bbox[3])/2], "Center of the nonzero-alpha bounding box; a visible-extent landmark, not a skeletal pivot")
        elif sprite_id == "87679":
            from_pixel("hair_crown", landmarks["top"], "First nonzero-alpha row: alpha-weighted pixel-center X, top pixel edge Y")
            from_pixel("hair_tip", landmarks["bottom"], "Last nonzero-alpha row: alpha-weighted pixel-center X, bottom pixel edge Y")
            alpha = np.array(image)[:, :, 3]
            neck = next(anchor for anchor in anchors if anchor["name"]=="head_neck")
            end = min(height, max(1, int(math.ceil(neck["source_pixels"][1]))))
            ys, xs = np.nonzero(alpha[:end])
            right = int(xs.max())
            right_ys = np.nonzero(alpha[:end, right])[0]
            y = float(np.average(right_ys+.5, weights=alpha[right_ys,right]))
            from_pixel("hair_front_edge", [float(right+1), y], "Rightmost nonzero-alpha column above the head/neck mount; alpha-weighted pixel-center Y, right outer pixel edge X")

        record = {
            "sprite_id":int(sprite_id), "source_name":mesh["sourceSpriteName"],
            "image_size":[width,height], "alpha_bbox_pixels":landmarks["bbox"],
            "alpha_bbox_uv":[landmarks["bbox"][0]/width,landmarks["bbox"][1]/height,landmarks["bbox"][2]/width,landmarks["bbox"][3]/height],
            "renderer_paths":[mesh["rendererPath"]], "is_skinned":mesh["skinning"]["isSkinned"],
            "localXYToUvTopLeft":to_uv.tolist(), "maxUvFitResidual":error,
            "anchors":anchors,
        }
        if sprite_id in sprites:
            previous = sprites[sprite_id]
            for left,right in zip(previous["anchors"],anchors):
                if left["name"] != right["name"] or not np.allclose(left["source_uv"],right["source_uv"],atol=1e-10):
                    raise ValueError(f"Shared leg Sprite has inconsistent local anchor coordinates: {sprite_id}")
            previous["renderer_paths"].append(mesh["rendererPath"])
        else: sprites[sprite_id] = record

    report = {
        "schemaVersion":1,
        "source":rig["source"], "rigRootPath":rig["rigRootPath"],
        "coordinates":{
            "source_uv":"Normalized crop PNG UV, top-left origin, X right, Y down. Not spriteRect/full-atlas UV.",
            "source_pixels":"Original cropped PNG edge coordinates: source_uv multiplied by image_size. Fractional coordinates are retained.",
            "alpha_bbox_pixels":"Nonzero source-alpha bounding rectangle [left,top,right,bottom], right/bottom exclusive. Alpha threshold >0, no threshold cleanup.",
            "bone_pivot_method":"inverse(source bindpose) * [0,0,0,1], then least-squares exact affine Sprite-local XY -> uvCropLowerLeft and Y flip.",
            "external_mount_method":"inverse(rendererToRigRoot) * rigBoneWorldMatrix * [0,0,0,1], then the same affine. Used for rigid face and head/neck mounts outside a mesh's own skin bones.",
            "registration":"Measure matching joint/mount/visible-extent landmarks in each new HD image and fit a mapping from these source UV anchors to those measured HD pixels. Multiplying source UV by arbitrary HD width/height assumes identical internal layout and does not perform registration.",
            "limitations":"Alpha crown/sole/front-edge values describe raster silhouette extents, not skeletal joints. External-mount anchors use the assembled prefab pose and must not replace inverse-bindpose anchors for skinned joints. Do not clamp anchors merely because they fall outside a crop.",
        },
        "sprites":sprites,
        "validation":{"spriteCount":len(sprites),"rendererCount":sum(len(s["renderer_paths"]) for s in sprites.values()),"maxSourceAffineResidual":max(residuals),"sharedLegAnchorsAgree":True,"inverseBindposeRoundTripsPass":True},
        "inputHashes":{"rig.json":hashlib.sha256(rig_path.read_bytes()).hexdigest(),"skins.json":hashlib.sha256(skins_path.read_bytes()).hexdigest()},
    }
    DEST.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"output":str(DEST),**report["validation"]},ensure_ascii=False))


if __name__ == "__main__": main()
