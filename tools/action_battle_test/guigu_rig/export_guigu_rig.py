"""Extract the local Guigu 10002 character rig for the action-battle test area.

Requires UnityPy and Pillow's UnityPy dependencies. This reads resources.assets
and writes reconstructable JSON snapshots; it does not modify game assets.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler


DEFAULT_ASSET = Path(
    "E:/SteamLibrary/steamapps/common/鬼谷八荒/"
    "guigubahuang_Data/resources.assets"
)
UNITY_VERSION = (2020, 3, 0, 0)
CHARACTER_INSTANCE_GO_ID = 181892
RIG_ROOT_GO_ID = 181894
ANIMATION_IDS = {
    "Idle": 38442,
    "Run_New1": 38451,
    "Run_New2": 38452,
    "AttackIdle": 38432,
    "AttackRun": 38433,
    "Step1": 38455,
}
RIG_ROOT_PATH = "/10002/Root"
BODY_ANIMATOR_ROOT = "/10002/Root/Body"
FOOT_ANIMATOR_ROOT = "/10002/Root/Foot2"


def xyz(value: Any) -> list[float]:
    return [float(value.x), float(value.y), float(value.z)]


def xyzw(value: Any) -> list[float]:
    return [float(value.x), float(value.y), float(value.z), float(value.w)]


def matrix_multiply(a: list[list[float]], b: list[list[float]]) -> list[list[float]]:
    return [
        [sum(a[row][k] * b[k][col] for k in range(4)) for col in range(4)]
        for row in range(4)
    ]


def trs_matrix(position: list[float], rotation: list[float], scale: list[float]) -> list[list[float]]:
    x, y, z, w = rotation
    sx, sy, sz = scale
    return [
        [
            (1 - 2 * (y * y + z * z)) * sx,
            (2 * (x * y - z * w)) * sy,
            (2 * (x * z + y * w)) * sz,
            position[0],
        ],
        [
            (2 * (x * y + z * w)) * sx,
            (1 - 2 * (x * x + z * z)) * sy,
            (2 * (y * z - x * w)) * sz,
            position[1],
        ],
        [
            (2 * (x * z - y * w)) * sx,
            (2 * (y * z + x * w)) * sy,
            (1 - 2 * (x * x + y * y)) * sz,
            position[2],
        ],
        [0.0, 0.0, 0.0, 1.0],
    ]


def matrix_rows(matrix: Any) -> list[list[float]]:
    return [
        [float(getattr(matrix, f"e{row}{col}")) for col in range(4)]
        for row in range(4)
    ]


def component_pairs(game_object: Any) -> list[tuple[str, int, Any]]:
    pairs = []
    for item in game_object.m_Component:
        pointer = getattr(item, "component", None)
        if pointer is None:
            pointer = item[1]
        reader = pointer.deref()
        pairs.append((reader.type.name, int(reader.path_id), reader))
    return pairs


def read_object(reader: Any) -> Any:
    return reader.read(check_read=False)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(16 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def source_metadata(asset_path: Path) -> dict[str, Any]:
    stat = asset_path.stat()
    return {
        "path": str(asset_path.resolve()),
        "sizeBytes": stat.st_size,
        "sha256": sha256_file(asset_path),
        "unityVersion": "2020.3f1c1",
    }


def bone_transform_paths(
    sprite: Any,
    renderer_path: str,
    node_by_path: dict[str, dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[str]]:
    result: list[dict[str, Any]] = []
    errors: list[str] = []
    resolved_paths: list[str] = []
    for index, bone in enumerate(sprite.m_Bones or []):
        parent_index = int(bone.parentId)
        parent_path = renderer_path if parent_index < 0 else resolved_paths[parent_index]
        expected = f"{parent_path}/{bone.name}"
        path = expected if expected in node_by_path else None
        if path is None:
            matches = [
                candidate["path"]
                for candidate in node_by_path.values()
                if candidate["name"] == bone.name
                and candidate["parentPath"] == parent_path
            ]
            if len(matches) == 1:
                path = matches[0]
        if path is None:
            errors.append(f"{renderer_path}: SpriteBone[{index}] {bone.name!r} expected {expected}")
            path = expected
        resolved_paths.append(path)
        result.append(
            {
                "index": index,
                "name": bone.name,
                "parentIndex": parent_index,
                "parentRigPath": None if parent_index < 0 else resolved_paths[parent_index],
                "length": float(bone.length),
                "position": xyz(bone.position),
                "rotationQuaternionXYZW": xyzw(bone.rotation),
                "rigPath": path,
                "rigNodePathId": node_by_path.get(path, {}).get("pathId"),
                "restMatrix": node_by_path.get(path, {}).get("restMatrix"),
            }
        )
    return result, errors


def extract_sprite_skin_component(
    asset: Any,
    game_object_id: int,
    renderer_path: str,
    sprite_bones: list[dict[str, Any]],
    node_by_path: dict[str, dict[str, Any]],
) -> dict[str, Any] | None:
    """Read the SpriteSkin root/bone PPtrs from its raw MonoBehaviour payload.

    The release asset omits MonoBehaviour type trees. Its fixed serialized
    prefix is still readable, and the known SpriteSkin fields are validated
    against SpriteBone counts and Transform path IDs before being accepted.
    """
    transform_path_by_id = {node["transformPathId"]: path for path, node in node_by_path.items()}
    game_object = read_object(asset.objects[game_object_id])
    for kind, component_id, reader in component_pairs(game_object):
        if kind != "MonoBehaviour":
            continue
        behaviour = read_object(reader)
        script_reader = behaviour.m_Script.deref() if behaviour.m_Script.path_id else None
        if script_reader is None:
            continue
        script = read_object(script_reader)
        if getattr(script, "m_ClassName", None) != "SpriteSkin":
            continue

        raw = reader.get_raw_data()
        offset = 32  # MonoBehaviour base fields through the empty m_Name.
        if len(raw) < offset + 16:
            raise RuntimeError(f"Truncated SpriteSkin MonoBehaviour at {renderer_path}")
        root_file_id, root_path_id = struct.unpack_from("<iq", raw, offset)
        offset += 12
        (bone_count,) = struct.unpack_from("<i", raw, offset)
        offset += 4
        transforms = []
        for index in range(bone_count):
            if offset + 12 > len(raw):
                raise RuntimeError(f"Truncated SpriteSkin bone array at {renderer_path}")
            file_id, path_id = struct.unpack_from("<iq", raw, offset)
            offset += 12
            transforms.append(
                {
                    "index": index,
                    "fileId": int(file_id),
                    "transformPathId": int(path_id),
                    "rigPath": transform_path_by_id.get(int(path_id)),
                }
            )

        root_path = transform_path_by_id.get(int(root_path_id))
        expected_paths = [bone["rigPath"] for bone in sprite_bones]
        actual_paths = [bone["rigPath"] for bone in transforms]
        array_matches = int(bone_count) == len(sprite_bones) and actual_paths == expected_paths
        hierarchy_matches = (
            bool(expected_paths)
            and all(path in node_by_path for path in expected_paths)
            and root_path == expected_paths[0]
        )
        if root_path not in node_by_path or not (array_matches or (bone_count == 0 and hierarchy_matches)):
            raise RuntimeError(
                f"SpriteSkin path/order mismatch at {renderer_path}: "
                f"root={root_path!r}, actual={actual_paths!r}, expected={expected_paths!r}"
            )
        return {
            "componentPathId": int(component_id),
            "script": {
                "className": script.m_ClassName,
                "namespace": script.m_Namespace,
                "assemblyName": script.m_AssemblyName,
            },
            "rootBone": {
                "fileId": int(root_file_id),
                "transformPathId": int(root_path_id),
                "rigPath": root_path,
            },
            "boneTransforms": transforms,
            "bindingMode": (
                "serialized_bone_transform_array"
                if array_matches
                else "root_bone_with_sprite_bone_hierarchy"
            ),
            "boneTransformOrderMatchesSpriteBones": array_matches if bone_count else None,
            "hierarchyOrderMatchesSpriteBones": hierarchy_matches,
            "hierarchyBoneTransformPaths": expected_paths,
            "rawDataEncoding": "Unity MonoBehaviour serialized bytes, little-endian",
            "rawDataHex": raw.hex(),
            "opaqueTailHex": raw[offset:].hex(),
        }
    return None


def collect_rig(asset: Any) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, dict[str, Any]]]:
    root_reader = asset.objects.get(RIG_ROOT_GO_ID)
    if root_reader is None or root_reader.type.name != "GameObject":
        raise RuntimeError(f"Expected GameObject path ID {RIG_ROOT_GO_ID} for {RIG_ROOT_PATH}")

    nodes: list[dict[str, Any]] = []
    renderers: list[dict[str, Any]] = []
    node_by_path: dict[str, dict[str, Any]] = {}
    visited: set[int] = set()

    def visit(
        game_object_id: int,
        path: str,
        parent_path: str | None,
        parent_matrix: list[list[float]],
        parent_active: bool,
    ) -> None:
        if game_object_id in visited:
            raise RuntimeError(f"Repeated GameObject while walking rig: {game_object_id}")
        visited.add(game_object_id)
        game_object = read_object(asset.objects[game_object_id])
        active_self = bool(getattr(game_object, "m_IsActive", True))
        active_in_hierarchy = parent_active and active_self
        comps = component_pairs(game_object)
        transform_pair = next((pair for pair in comps if pair[0] in ("Transform", "RectTransform")), None)
        if transform_pair is None:
            raise RuntimeError(f"Missing Transform for {path}")
        transform_reader = transform_pair[2]
        transform = read_object(transform_reader)
        position = xyz(transform.m_LocalPosition)
        rotation = xyzw(transform.m_LocalRotation)
        scale = xyz(transform.m_LocalScale)
        local_matrix = trs_matrix(position, rotation, scale)
        rest_matrix = matrix_multiply(parent_matrix, local_matrix)
        node = {
            "pathId": game_object_id,
            "transformPathId": int(transform_reader.path_id),
            "name": game_object.m_Name,
            "path": path,
            "parentPath": parent_path,
            "components": [kind for kind, _pid, _reader in comps],
            "activeSelf": active_self,
            "activeInHierarchy": active_in_hierarchy,
            "localTRS": {
                "position": position,
                "rotationQuaternionXYZW": rotation,
                "scale": scale,
            },
            "localMatrix": local_matrix,
            "restMatrix": rest_matrix,
        }
        nodes.append(node)
        node_by_path[path] = node

        for kind, component_id, component_reader in comps:
            if kind != "SpriteRenderer":
                continue
            renderer = read_object(component_reader)
            if not renderer.m_Sprite.path_id:
                continue
            renderers.append(
                {
                    "rendererPath": path,
                    "rendererGameObjectPathId": game_object_id,
                    "rendererComponentPathId": component_id,
                    "renderer": renderer,
                    "spritePathId": int(renderer.m_Sprite.path_id),
                }
            )

        for child_pointer in transform.m_Children:
            child_transform = read_object(child_pointer.deref())
            child_game_object_id = int(child_transform.m_GameObject.path_id)
            child_game_object = read_object(asset.objects[child_game_object_id])
            visit(
                child_game_object_id,
                f"{path}/{child_game_object.m_Name}",
                path,
                rest_matrix,
                active_in_hierarchy,
            )

    visit(
        RIG_ROOT_GO_ID,
        RIG_ROOT_PATH,
        None,
        trs_matrix([0, 0, 0], [0, 0, 0, 1], [1, 1, 1]),
        True,
    )
    return nodes, renderers, node_by_path


def extract_skins(asset: Any, renderers: list[dict[str, Any]], node_by_path: dict[str, dict[str, Any]]) -> tuple[list[dict[str, Any]], set[str], list[str], dict[str, Any]]:
    meshes: list[dict[str, Any]] = []
    bone_node_paths: set[str] = set()
    mapping_errors: list[str] = []
    checks: dict[str, Any] = {
        "rendererCount": 0,
        "skinnedRendererCount": 0,
        "spriteSkinComponentCount": 0,
        "totalVertices": 0,
        "totalTriangles": 0,
        "unmappedSpriteBones": [],
        "weightSumMin": None,
        "weightSumMax": None,
        "invalidBoneWeightIndices": [],
    }
    all_weight_sums: list[float] = []

    for binding in renderers:
        sprite_reader = asset.objects[binding["spritePathId"]]
        sprite = read_object(sprite_reader)
        render_path = binding["rendererPath"]
        renderer = binding["renderer"]
        render_data = sprite.m_RD
        handler = MeshHandler(render_data, version=UNITY_VERSION)
        handler.process()
        vertices = [list(map(float, vertex)) for vertex in (handler.m_Vertices or [])]
        uv_source = [list(map(float, uv)) for uv in (handler.m_UV0 or [])]
        weight_rows = handler.m_BoneWeights or []
        index_rows = handler.m_BoneIndices or []
        triangle_groups = handler.get_triangles()
        triangles = [list(map(int, tri)) for group in triangle_groups for tri in group]
        bones, errors = bone_transform_paths(sprite, render_path, node_by_path)
        mapping_errors.extend(errors)
        for bone in bones:
            if bone["rigPath"] in node_by_path:
                bone_node_paths.add(bone["rigPath"])
            else:
                checks["unmappedSpriteBones"].append(
                    {"rendererPath": render_path, "boneIndex": bone["index"], "name": bone["name"], "expectedPath": bone["rigPath"]}
                )

        is_skinned = bool(bones) and len(render_data.m_Bindpose or []) == len(bones)
        sprite_skin = extract_sprite_skin_component(
            asset,
            binding["rendererGameObjectPathId"],
            render_path,
            bones,
            node_by_path,
        )
        if is_skinned:
            checks["skinnedRendererCount"] += 1
        if sprite_skin:
            checks["spriteSkinComponentCount"] += 1
        if is_skinned != (sprite_skin is not None):
            raise RuntimeError(
                f"SpriteSkin component/skinned Sprite mismatch at {render_path}: "
                f"mesh={is_skinned}, component={sprite_skin is not None}"
            )
        weights: list[list[dict[str, Any]]] = []
        raw_vertex_influences: list[dict[str, Any]] = []
        if len(vertices) != len(weight_rows) or len(vertices) != len(index_rows):
            raise RuntimeError(
                f"Vertex/influence mismatch for {render_path}: "
                f"{len(vertices)} verts, {len(weight_rows)} weights, {len(index_rows)} indices"
            )
        for vertex_index, (weight_row, index_row) in enumerate(zip(weight_rows, index_rows)):
            raw_vertex_influences.append(
                {"boneIndices": [int(index) for index in index_row], "weights": [float(weight) for weight in weight_row]}
            )
            influences: list[dict[str, Any]] = []
            if is_skinned:
                for influence_index, (bone_index, weight) in enumerate(zip(index_row, weight_row)):
                    bone_index = int(bone_index)
                    weight = float(weight)
                    if weight <= 0.0:
                        continue
                    if bone_index < 0 or bone_index >= len(bones):
                        checks["invalidBoneWeightIndices"].append(
                            {"rendererPath": render_path, "vertex": vertex_index, "slot": influence_index, "boneIndex": bone_index, "weight": weight}
                        )
                        continue
                    influences.append(
                        {"boneIndex": bone_index, "weight": weight, "rigPath": bones[bone_index]["rigPath"]}
                    )
                weight_sum = sum(influence["weight"] for influence in influences)
                if influences:
                    all_weight_sums.append(weight_sum)
                weights.append(influences)
            else:
                weights.append([])

        tex_reader = render_data.texture.deref() if render_data.texture.path_id else None
        texture = read_object(tex_reader) if tex_reader else None
        tex_width = int(getattr(texture, "m_Width", 0) or 0)
        tex_height = int(getattr(texture, "m_Height", 0) or 0)
        rect = sprite.m_Rect
        pivot = sprite.m_Pivot
        ppu = float(sprite.m_PixelsToUnits)
        uv_transform = render_data.uvTransform
        if uv_transform is not None:
            uv_scale_x, uv_offset_x, uv_scale_y, uv_offset_y = xyzw(uv_transform)
        else:
            uv_scale_x, uv_scale_y = ppu, ppu
            uv_offset_x, uv_offset_y = float(rect.width * pivot.x), float(rect.height * pivot.y)
        texture_rect = render_data.textureRect
        uv_texture: list[list[float]] = []
        uv_crop: list[list[float]] = []
        if tex_width and tex_height and texture_rect.width and texture_rect.height:
            for vertex in vertices:
                pixel_x = vertex[0] * uv_scale_x + uv_offset_x
                pixel_y = vertex[1] * uv_scale_y + uv_offset_y
                u = pixel_x / tex_width
                v = pixel_y / tex_height
                uv_texture.append([u, v])
                uv_crop.append(
                    [
                        (u * tex_width - float(texture_rect.x)) / float(texture_rect.width),
                        (v * tex_height - float(texture_rect.y)) / float(texture_rect.height),
                    ]
                )

        mesh = {
            "rendererPath": render_path,
            "rendererGameObjectPathId": binding["rendererGameObjectPathId"],
            "rendererComponentPathId": binding["rendererComponentPathId"],
            "activeSelf": node_by_path[render_path]["activeSelf"],
            "activeInHierarchy": node_by_path[render_path]["activeInHierarchy"],
            "rendererRestTRS": node_by_path[render_path]["localTRS"],
            "rendererToRigRoot": node_by_path[render_path]["restMatrix"],
            "sourceSpriteId": binding["spritePathId"],
            "sourceSpriteName": sprite.m_Name,
            "sourceImage": f"../art/guigu_reference/sprite_{binding['spritePathId']}.png",
            "category": "shadow" if "/Shadow" in render_path else ("effect" if "/Effect/" in render_path else "character"),
            "enabled": bool(renderer.m_Enabled),
            "sortingLayerId": int(renderer.m_SortingLayerID),
            "sortingOrder": int(renderer.m_SortingOrder),
            "spriteRectPixels": [float(rect.x), float(rect.y), float(rect.width), float(rect.height)],
            "spritePivotFraction": [float(pivot.x), float(pivot.y)],
            "pixelsPerUnit": ppu,
            "vertexSpace": "source Sprite local units, Unity X-right Y-up Z-depth",
            "vertices": vertices,
            "submeshes": [
                {"indexCount": len(group) * 3, "triangles": [list(map(int, tri)) for tri in group]}
                for group in triangle_groups
            ],
            "triangles": triangles,
            "uvConvention": {
                "origin": "lower-left",
                "sourceTextureName": getattr(texture, "m_Name", None),
                "sourceTextureSizePixels": [tex_width, tex_height],
                "sourceTextureRectPixels": [
                    float(texture_rect.x),
                    float(texture_rect.y),
                    float(texture_rect.width),
                    float(texture_rect.height),
                ],
                "sourceUvTransform": [uv_scale_x, uv_offset_x, uv_scale_y, uv_offset_y],
                "textureUvFormula": "((vertex.x * scaleX + offsetX) / textureWidth, (vertex.y * scaleY + offsetY) / textureHeight)",
                "cropUvFormula": "((textureU * textureWidth - rectX) / rectWidth, (textureV * textureHeight - rectY) / rectHeight)",
                "uvTextureLowerLeft": uv_texture,
                "uvCropLowerLeft": uv_crop,
                "rawVertexDataUv0": uv_source,
            },
            "skinning": {
                "isSkinned": is_skinned,
                "spriteSkinComponent": sprite_skin,
                "weightNormalization": "stored as Unity source values; not renormalized",
                "boneIndexSpace": "index into spriteBones and bindposeMatrices",
                "spriteBones": bones,
                "boneWeights": weights,
                "bindposeMatrices": [matrix_rows(matrix) for matrix in (render_data.m_Bindpose or [])],
                "rawVertexInfluences": raw_vertex_influences,
            },
        }
        meshes.append(mesh)
        checks["rendererCount"] += 1
        checks["totalVertices"] += len(vertices)
        checks["totalTriangles"] += len(triangles)

    if all_weight_sums:
        checks["weightSumMin"] = min(all_weight_sums)
        checks["weightSumMax"] = max(all_weight_sums)
    return meshes, bone_node_paths, mapping_errors, checks


def jsonable_binding(binding: Any) -> dict[str, Any]:
    script = getattr(binding, "script", None)
    return {
        "pathHash": int(binding.path),
        "attributeId": int(binding.attribute),
        "customType": int(binding.customType),
        "typeId": int(binding.typeID),
        "isPPtrCurve": bool(binding.isPPtrCurve),
        "isIntCurve": getattr(binding, "isIntCurve", None),
        "scriptPathId": int(getattr(script, "path_id", 0) or 0),
    }


def extract_animations(asset: Any, curve_dump_path: Path, node_by_path: dict[str, dict[str, Any]]) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    curve_dump = json.loads(curve_dump_path.read_text(encoding="utf-8"))
    clips: list[dict[str, Any]] = []
    checks: dict[str, Any] = {"clipCount": 0, "unresolvedBindingPaths": [], "streamWordCounts": {}}

    for clip_name, path_id in ANIMATION_IDS.items():
        reader = asset.objects.get(path_id)
        if reader is None or reader.type.name != "AnimationClip":
            raise RuntimeError(f"Missing AnimationClip {clip_name} at path ID {path_id}")
        clip = read_object(reader)
        decoded = curve_dump.get(clip_name)
        if decoded is None or int(decoded["source_path_id"]) != path_id:
            raise RuntimeError(f"Parsed curve dump does not match source clip {clip_name} ({path_id})")
        if clip.m_Name != clip_name:
            raise RuntimeError(f"Clip ID {path_id} names {clip.m_Name!r}, expected {clip_name!r}")

        muscle = clip.m_MuscleClip
        raw_clip = muscle.m_Clip.data
        streamed = raw_clip.m_StreamedClip
        dense = raw_clip.m_DenseClip
        constant = raw_clip.m_ConstantClip
        animation_bindings = []
        unresolved = []
        for row in decoded["bindings"]:
            target = f"{BODY_ANIMATOR_ROOT}/{row['path']}" if row["path"] else BODY_ANIMATOR_ROOT
            target_exists = target in node_by_path
            if not target_exists:
                unresolved.append({"clip": clip_name, "path": row["path"], "targetRigPath": target})
            animation_bindings.append(
                {
                    "curveIndex": int(row["index"]),
                    "width": int(row["width"]),
                    "path": row["path"],
                    "pathHash": int(row["path_hash"]),
                    "attributeId": int(row["attribute"]),
                    "targetRigPath": target if target_exists else None,
                }
            )

        sparse_samples = []
        for binding in animation_bindings:
            path_name = binding["path"]
            track = {
                "path": path_name,
                "targetRigPath": binding["targetRigPath"],
                "attributeId": binding["attributeId"],
                "firstCurveIndex": binding["curveIndex"],
                "width": binding["width"],
                "samples": [],
            }
            sparse_samples.append(track)

        for frame_time, raw_keys in decoded["frames"]:
            by_curve_index = {int(key[0]): [float(value) for value in key[1:5]] for key in raw_keys}
            for binding, track in zip(animation_bindings, sparse_samples):
                component_coefficients = [
                    by_curve_index.get(binding["curveIndex"] + component_index)
                    for component_index in range(binding["width"])
                ]
                if any(coefficients is not None for coefficients in component_coefficients):
                    track["samples"].append(
                        {
                            "time": float(frame_time),
                            "components": [
                                None if coefficients is None else coefficients[3]
                                for coefficients in component_coefficients
                            ],
                            "rawCoefficients": component_coefficients,
                        }
                    )

        direct_bindings = [jsonable_binding(item) for item in (clip.m_ClipBindingConstant.genericBindings or [])]
        clips.append(
            {
                "name": clip_name,
                "sourcePathId": path_id,
                "duration": float(decoded["duration"]),
                "sampleRate": float(clip.m_SampleRate),
                "wrapMode": int(clip.m_WrapMode),
                "loopTime": bool(getattr(muscle, "m_LoopTime", False)),
                "animatorRoots": [BODY_ANIMATOR_ROOT, FOOT_ANIMATOR_ROOT],
                "bindingSemantics": "AnimationClip generic binding paths are relative to each listed Animator root; targetRigPath is resolved against Body when present.",
                "bindings": animation_bindings,
                "tracks": sparse_samples,
                "decodedStreamFrames": decoded["frames"],
                "rawClipBindingConstant": direct_bindings,
                "rawMuscleMetadata": {
                    "startTime": float(muscle.m_StartTime),
                    "stopTime": float(muscle.m_StopTime),
                    "loopTime": bool(getattr(muscle, "m_LoopTime", False)),
                    "cycleOffset": float(muscle.m_CycleOffset),
                    "loopBlend": bool(muscle.m_LoopBlend),
                    "mirror": bool(muscle.m_Mirror),
                },
                "rawStreamedClip": {
                    "curveCount": int(streamed.curveCount),
                    "dataUInt32": [int(value) for value in streamed.data],
                },
                "rawDenseClip": {
                    "beginTime": float(dense.m_BeginTime),
                    "curveCount": int(dense.m_CurveCount),
                    "frameCount": int(dense.m_FrameCount),
                    "sampleRate": float(dense.m_SampleRate),
                    "sampleArray": [float(value) for value in dense.m_SampleArray],
                },
                "rawConstantClip": {
                    "data": [float(value) for value in (constant.data if constant else [])],
                },
            }
        )
        checks["streamWordCounts"][clip_name] = len(streamed.data)
        checks["unresolvedBindingPaths"].extend(unresolved)
        checks["clipCount"] += 1

    return clips, checks


def write_json(path: Path, value: Any) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, default=DEFAULT_ASSET)
    parser.add_argument("--out", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument(
        "--curve-dump",
        type=Path,
        default=Path(__file__).resolve().parent / "decoded_animation_curves.json",
    )
    args = parser.parse_args()
    asset_path = args.assets.resolve()
    output_dir = args.out.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    if not asset_path.is_file():
        raise FileNotFoundError(asset_path)
    if not args.curve_dump.is_file():
        raise FileNotFoundError(args.curve_dump)

    environment = UnityPy.Environment()
    asset = environment.load_file(str(asset_path))
    global_managers = asset_path.parent / "globalgamemanagers.assets"
    if global_managers.is_file():
        environment.load_file(str(global_managers))
    if str(asset.version) != "2020.3f1c1":
        raise RuntimeError(f"Unexpected Unity version {asset.version!r}; extraction schema is pinned to 2020.3f1c1")

    outer_reader = asset.objects.get(CHARACTER_INSTANCE_GO_ID)
    if outer_reader is None or outer_reader.type.name != "GameObject":
        raise RuntimeError(f"Missing 10002 outer GameObject path ID {CHARACTER_INSTANCE_GO_ID}")
    outer = read_object(outer_reader)
    if outer.m_Name != "10002":
        raise RuntimeError(f"Expected outer GameObject 10002 at {CHARACTER_INSTANCE_GO_ID}, got {outer.m_Name!r}")
    outer_transform = read_object(next(pair[2] for pair in component_pairs(outer) if pair[0] == "Transform"))

    nodes, renderers, node_by_path = collect_rig(asset)
    if RIG_ROOT_PATH not in node_by_path or f"{RIG_ROOT_PATH}/Body" not in node_by_path:
        raise RuntimeError("Expected /10002/Root/Body hierarchy was not found")
    meshes, bone_node_paths, mapping_errors, skin_checks = extract_skins(asset, renderers, node_by_path)
    clips, animation_checks = extract_animations(asset, args.curve_dump.resolve(), node_by_path)

    source = source_metadata(asset_path)
    bone_nodes = [node_by_path[path] for path in sorted(bone_node_paths)]
    rig = {
        "schemaVersion": 1,
        "source": source,
        "rigRootPath": RIG_ROOT_PATH,
        "rigRootGameObjectPathId": RIG_ROOT_GO_ID,
        "characterInstance": {
            "pathId": CHARACTER_INSTANCE_GO_ID,
            "localTRSExcludedFromRigRoot": {
                "position": xyz(outer_transform.m_LocalPosition),
                "rotationQuaternionXYZW": xyzw(outer_transform.m_LocalRotation),
                "scale": xyz(outer_transform.m_LocalScale),
            },
            "reason": "This is a placed game instance transform, not the reusable character rig origin.",
        },
        "coordinateSystem": {
            "position": "Unity local units; X-right, Y-up, Z-depth",
            "rotation": "Unity quaternion XYZW",
            "matrixStorage": "Nested rows in e00..e33 order; column-vector multiplication; translation at e03/e13/e23",
            "pixelsPerUnit": 100.0,
            "vertices": "Source Sprite local units; not Y-reflected",
        },
        "animationRoots": [BODY_ANIMATOR_ROOT, FOOT_ANIMATOR_ROOT],
        "animationRootStates": {
            BODY_ANIMATOR_ROOT: {
                "activeSelf": node_by_path[BODY_ANIMATOR_ROOT]["activeSelf"],
                "activeInHierarchy": node_by_path[BODY_ANIMATOR_ROOT]["activeInHierarchy"],
            },
            FOOT_ANIMATOR_ROOT: {
                "activeSelf": node_by_path[FOOT_ANIMATOR_ROOT]["activeSelf"],
                "activeInHierarchy": node_by_path[FOOT_ANIMATOR_ROOT]["activeInHierarchy"],
            },
        },
        "nodes": nodes,
        "boneNodes": bone_nodes,
        "meshCount": len(meshes),
        "checks": {
            "nodeCount": len(nodes),
            "spriteRendererCount": len(renderers),
            "spriteBonePathMappingErrors": mapping_errors,
            "boneNodeCount": len(bone_nodes),
        },
    }
    skins = {
        "schemaVersion": 1,
        "source": source,
        "rigRootPath": RIG_ROOT_PATH,
        "coordinateSystem": rig["coordinateSystem"],
        "matrixStorage": rig["coordinateSystem"]["matrixStorage"],
        "meshes": meshes,
        "checks": skin_checks,
    }
    animations = {
        "schemaVersion": 1,
        "source": source,
        "rigRootPath": RIG_ROOT_PATH,
        "bodyAnimatorRootPath": BODY_ANIMATOR_ROOT,
        "footAnimatorRootPath": FOOT_ANIMATOR_ROOT,
        "decodedCurveSource": {
            "path": str(args.curve_dump.resolve()),
            "sha256": sha256_file(args.curve_dump.resolve()),
            "description": "Prior local decode of source AnimationClip stream coefficients; raw source streams are also embedded below.",
        },
        "streamCoefficientSemantics": "decodedStreamFrames preserve each source frame time and its per-curve four coefficients. tracks.samples.components uses coefficient[3] at that frame; rawStreamedClip.dataUInt32 preserves Unity's serialized stream words for exact re-decoding.",
        "clips": clips,
        "checks": animation_checks,
    }

    # Basic integrity checks before writing the snapshots.
    if mapping_errors:
        raise RuntimeError("Unmapped SpriteBone transforms:\n" + "\n".join(mapping_errors))
    if skin_checks["invalidBoneWeightIndices"]:
        raise RuntimeError(f"Invalid weighted bone indexes: {skin_checks['invalidBoneWeightIndices'][:5]}")
    if skin_checks["unmappedSpriteBones"]:
        raise RuntimeError(f"Unmapped SpriteBones: {skin_checks['unmappedSpriteBones'][:5]}")
    if skin_checks["weightSumMin"] is not None and abs(skin_checks["weightSumMin"] - 1.0) > 1e-3:
        raise RuntimeError(f"Unexpected minimum skin weight sum {skin_checks['weightSumMin']}")
    if skin_checks["weightSumMax"] is not None and abs(skin_checks["weightSumMax"] - 1.0) > 1e-3:
        raise RuntimeError(f"Unexpected maximum skin weight sum {skin_checks['weightSumMax']}")

    write_json(output_dir / "rig.json", rig)
    write_json(output_dir / "skins.json", skins)
    write_json(output_dir / "animations.json", animations)
    write_json(output_dir / "export_check.json", {"rig": rig["checks"], "skins": skin_checks, "animations": animation_checks})
    print(
        json.dumps(
            {
                "output": str(output_dir),
                "sourceSha256": source["sha256"],
                "nodes": len(nodes),
                "spriteRenderers": len(meshes),
                "skinnedRenderers": skin_checks["skinnedRendererCount"],
                "vertices": skin_checks["totalVertices"],
                "triangles": skin_checks["totalTriangles"],
                "clips": animation_checks["clipCount"],
                "weightSumMinMax": [skin_checks["weightSumMin"], skin_checks["weightSumMax"]],
                "unresolvedAnimationBindings": len(animation_checks["unresolvedBindingPaths"]),
            },
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
