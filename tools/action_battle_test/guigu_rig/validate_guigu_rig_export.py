"""Validate the extracted 10002 rig JSON without reopening the game asset."""

from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Any


HERE = Path(__file__).resolve().parent


def load(name: str) -> dict[str, Any]:
    return json.loads((HERE / name).read_text(encoding="utf-8"))


def close(a: float, b: float, tolerance: float = 1e-3) -> bool:
    return math.isfinite(a) and math.isfinite(b) and abs(a - b) <= tolerance


def main() -> None:
    rig = load("rig.json")
    skins = load("skins.json")
    animations = load("animations.json")
    node_by_path = {node["path"]: node for node in rig["nodes"]}
    assert len(node_by_path) == len(rig["nodes"]), "Rig paths must be unique"
    assert rig["rigRootPath"] == "/10002/Root"
    assert rig["coordinateSystem"]["pixelsPerUnit"] == 100.0
    assert skins["rigRootPath"] == rig["rigRootPath"]
    for node in rig["nodes"]:
        parent = node["parentPath"]
        expected_active = node["activeSelf"] and (
            True if parent is None else node_by_path[parent]["activeInHierarchy"]
        )
        assert node["activeInHierarchy"] == expected_active, f"Active hierarchy mismatch: {node['path']}"
    assert rig["animationRootStates"]["/10002/Root/Body"]["activeInHierarchy"] is True
    assert rig["animationRootStates"]["/10002/Root/Foot2"]["activeSelf"] is False

    weight_sums: list[float] = []
    vertex_count = 0
    triangle_count = 0
    skinned_mesh_count = 0
    for mesh in skins["meshes"]:
        vertices = mesh["vertices"]
        triangles = mesh["triangles"]
        skin = mesh["skinning"]
        uv = mesh["uvConvention"]["uvCropLowerLeft"]
        assert mesh["rendererPath"] in node_by_path
        assert mesh["activeSelf"] == node_by_path[mesh["rendererPath"]]["activeSelf"]
        assert mesh["activeInHierarchy"] == node_by_path[mesh["rendererPath"]]["activeInHierarchy"]
        assert len(uv) == len(vertices), f"UV count mismatch: {mesh['rendererPath']}"
        assert len(skin["rawVertexInfluences"]) == len(vertices)
        assert len(skin["boneWeights"]) == len(vertices)
        assert all(len(triangle) == 3 for triangle in triangles)
        assert all(0 <= index < len(vertices) for triangle in triangles for index in triangle)
        assert all(
            math.isfinite(component)
            for row in vertices + uv
            for component in row
        ), f"Non-finite coordinate: {mesh['rendererPath']}"
        assert all(-0.01 <= component <= 1.01 for row in uv for component in row), (
            f"Crop UV out of expected range: {mesh['rendererPath']}"
        )

        if skin["isSkinned"]:
            skinned_mesh_count += 1
            bones = skin["spriteBones"]
            bindposes = skin["bindposeMatrices"]
            sprite_skin = skin["spriteSkinComponent"]
            assert len(bones) == len(bindposes), f"Bone/bindpose mismatch: {mesh['rendererPath']}"
            assert sprite_skin is not None, f"Missing SpriteSkin component: {mesh['rendererPath']}"
            assert sprite_skin["script"]["className"] == "SpriteSkin"
            assert sprite_skin["rootBone"]["rigPath"] in node_by_path
            assert bytes.fromhex(sprite_skin["rawDataHex"])
            assert sprite_skin["opaqueTailHex"] is not None
            for bone in bones:
                assert bone["rigPath"] in node_by_path, f"Unmapped bone {bone['rigPath']}"
                assert bone["parentRigPath"] is None or bone["parentRigPath"] in node_by_path
            if sprite_skin["bindingMode"] == "serialized_bone_transform_array":
                assert sprite_skin["boneTransformOrderMatchesSpriteBones"] is True
                assert [row["rigPath"] for row in sprite_skin["boneTransforms"]] == [
                    bone["rigPath"] for bone in bones
                ]
            else:
                assert sprite_skin["bindingMode"] == "root_bone_with_sprite_bone_hierarchy"
                assert sprite_skin["boneTransforms"] == []
                assert sprite_skin["hierarchyOrderMatchesSpriteBones"] is True
                assert sprite_skin["hierarchyBoneTransformPaths"] == [
                    bone["rigPath"] for bone in bones
                ]
            for vertex_index, influences in enumerate(skin["boneWeights"]):
                assert influences, f"Missing influences: {mesh['rendererPath']} vertex {vertex_index}"
                total = sum(influence["weight"] for influence in influences)
                assert close(total, 1.0), f"Bad weight sum {total}: {mesh['rendererPath']} vertex {vertex_index}"
                for influence in influences:
                    assert 0 <= influence["boneIndex"] < len(bones)
                    assert influence["rigPath"] == bones[influence["boneIndex"]]["rigPath"]
                weight_sums.append(total)
        else:
            assert all(not influences for influences in skin["boneWeights"])

        matrix = mesh["rendererToRigRoot"]
        assert len(matrix) == 4 and all(len(row) == 4 for row in matrix)
        vertex_count += len(vertices)
        triangle_count += len(triangles)

    clips = animations["clips"]
    assert len(clips) == 6
    assert [clip["name"] for clip in clips] == [
        "Idle", "Run_New1", "Run_New2", "AttackIdle", "AttackRun", "Step1"
    ]
    unresolved_bindings = []
    for clip in clips:
        assert clip["duration"] > 0
        assert clip["sampleRate"] > 0
        assert clip["rawStreamedClip"]["curveCount"] > 0
        assert clip["rawStreamedClip"]["dataUInt32"]
        assert clip["decodedStreamFrames"]
        assert len(clip["tracks"]) == len(clip["bindings"])
        for binding in clip["bindings"]:
            if binding["targetRigPath"] is None:
                unresolved_bindings.append((clip["name"], binding["path"]))
            else:
                assert binding["targetRigPath"] in node_by_path
    assert not unresolved_bindings, f"Unresolved animation paths: {unresolved_bindings}"

    layout_path = HERE.parent / "art" / "guigu_reference" / "layout.json"
    layout = json.loads(layout_path.read_text(encoding="utf-8"))
    max_layout_error = 0.0
    for row in layout:
        node = node_by_path[row["path"]]
        matrix = node["restMatrix"]
        computed = [matrix[0][3] * 100.0, -matrix[1][3] * 100.0]
        delta = [computed[i] - float(row["position"][i]) for i in range(2)]
        error = max(abs(value) for value in delta)
        max_layout_error = max(max_layout_error, error)
        assert error <= 0.002, f"Legacy layout mismatch at {row['path']}: {delta}"

    report = {
        "status": "passed",
        "nodeCount": len(rig["nodes"]),
        "boneNodeCount": len(rig["boneNodes"]),
        "spriteRendererCount": len(skins["meshes"]),
        "activeSpriteRendererCount": sum(1 for mesh in skins["meshes"] if mesh["activeInHierarchy"]),
        "inactiveSpriteRendererCount": sum(1 for mesh in skins["meshes"] if not mesh["activeInHierarchy"]),
        "skinnedRendererCount": skinned_mesh_count,
        "spriteSkinComponentCount": sum(
            1 for mesh in skins["meshes"] if mesh["skinning"]["spriteSkinComponent"] is not None
        ),
        "serializedSpriteSkinBoneArrayCount": sum(
            1
            for mesh in skins["meshes"]
            if mesh["skinning"]["spriteSkinComponent"]
            and mesh["skinning"]["spriteSkinComponent"]["bindingMode"] == "serialized_bone_transform_array"
        ),
        "hierarchyBoundSpriteSkinCount": sum(
            1
            for mesh in skins["meshes"]
            if mesh["skinning"]["spriteSkinComponent"]
            and mesh["skinning"]["spriteSkinComponent"]["bindingMode"] == "root_bone_with_sprite_bone_hierarchy"
        ),
        "vertexCount": vertex_count,
        "triangleCount": triangle_count,
        "animationClipCount": len(clips),
        "unresolvedAnimationBindingCount": len(unresolved_bindings),
        "skinWeightSumMin": min(weight_sums),
        "skinWeightSumMax": max(weight_sums),
        "legacyLayoutRowsCompared": len(layout),
        "legacyLayoutMaxPixelError": max_layout_error,
    }
    (HERE / "validation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
