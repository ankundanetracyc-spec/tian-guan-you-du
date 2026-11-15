"""Assemble original male clothing 101 from explicit original resource paths.

The original installation remains read-only. Sources are HumanRootMan,
HumanBodyMan, and the four Man/Body/101 component prefabs. Runtime-style
instantiation and naming are recorded rather than represented as a preassembled
prefab found on disk. PNGs are reference/debug assets, not project art.
"""
from __future__ import annotations

import copy
import importlib.util
import json
import struct
from pathlib import Path

import UnityPy
from UnityPy.streams import EndianBinaryReader
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
BASE = PROJECT / "tools/action_battle_test"
OUT = BASE / "output/guigu_source_reference_v2/male101"
DOC = PROJECT / "docs/美术规范/鬼谷原作网格与素材重构_20261104"
GAME = Path("E:/SteamLibrary/steamapps/common/鬼谷八荒/guigubahuang_Data")
ROOT_PATH = "/HumanRootMan/Root"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "textures").mkdir(exist_ok=True)
    DOC.mkdir(parents=True, exist_ok=True)
    spec = importlib.util.spec_from_file_location("original_export", BASE / "guigu_rig/export_guigu_rig.py")
    export = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(export)
    # Male 101 stores some SpriteBones before their parents. The historical
    # female-only exporter assumes parent-first order; resolve by index graph.
    def resolve_sprite_bones(sprite, renderer_path, lookup):
        source_bones = list(sprite.m_Bones or [])
        resolved, visiting, errors = {}, set(), []
        def resolve(index):
            if index in resolved:
                return resolved[index]
            if index in visiting:
                raise ValueError("Cyclic SpriteBone parent graph")
            visiting.add(index)
            bone = source_bones[index]
            parent = int(bone.parentId)
            parent_path = renderer_path if parent < 0 else resolve(parent)
            path = parent_path + "/" + bone.name
            if path not in lookup:
                errors.append(f"{renderer_path}: SpriteBone[{index}] missing {path}")
            resolved[index] = path
            visiting.remove(index)
            return path
        result = []
        for index, bone in enumerate(source_bones):
            path = resolve(index)
            parent = int(bone.parentId)
            result.append({"index": index, "name": bone.name, "parentIndex": parent,
                "parentRigPath": None if parent < 0 else resolve(parent), "length": float(bone.length),
                "position": export.xyz(bone.position), "rotationQuaternionXYZW": export.xyzw(bone.rotation),
                "rigPath": path, "rigNodePathId": lookup.get(path, {}).get("pathId"),
                "restMatrix": lookup.get(path, {}).get("restMatrix")})
        return result, errors
    export.bone_transform_paths = resolve_sprite_bones
    env = UnityPy.Environment()
    asset = env.load_file(str(GAME / "resources.assets"))
    env.load_file(str(GAME / "globalgamemanagers.assets"))
    manager = env.load_file(str(GAME / "globalgamemanagers"))
    resources = dict(manager.objects[13].read(check_read=False).m_Container)
    env.register_cab("resources.assets.resS", EndianBinaryReader(str(GAME / "resources.assets.resS")))
    nodes, renderers, node_by_path, assembly = [], [], {}, []

    def branch(game_object_id, path, parent_path, resource_path=None, rename=None):
        export.RIG_ROOT_GO_ID, export.RIG_ROOT_PATH = game_object_id, path
        new_nodes, new_renderers, _ = export.collect_rig(asset)
        if parent_path:
            parent_matrix = node_by_path[parent_path]["restMatrix"]
            parent_active = node_by_path[parent_path]["activeInHierarchy"]
            for node in new_nodes:
                node["restMatrix"] = export.matrix_multiply(parent_matrix, node["restMatrix"])
                node["activeInHierarchy"] = node["activeInHierarchy"] and parent_active
            new_nodes[0]["parentPath"] = parent_path
        if rename:
            new_nodes[0]["sourceName"] = new_nodes[0]["name"]
            new_nodes[0]["name"] = rename
        for node in new_nodes:
            if node["path"] in node_by_path:
                raise ValueError("Duplicate assembled path " + node["path"])
            nodes.append(node)
            node_by_path[node["path"]] = node
        renderers.extend(new_renderers)
        assembly.append({"sourceResource": resource_path, "sourceGameObjectPathId": game_object_id, "targetPath": path, "parentPath": parent_path, "rootNameOverride": rename})

    def from_resource(resource, path, parent, rename=None):
        if resource not in resources:
            raise KeyError(resource)
        pointer = resources[resource]
        branch(pointer.m_PathID, path, parent, resource, rename)

    branch(120384, ROOT_PATH, None, "battle/human/humanrootman (Root child)")
    body = ROOT_PATH + "/Body"
    garment = body + "/yifu/yifu"
    from_resource("battle/human/man/body/101/yifu", garment, body + "/yifu")
    chest = garment + "/shenti1"
    # HumanBodyMan contains empty attachment slots; the game installs these at
    # the garment's chest bone rather than retaining a HumanBodyMan wrapper.
    body_go = export.read_object(asset.objects[355292])
    body_transform = export.read_object(next(r for kind, _, r in export.component_pairs(body_go) if kind == "Transform"))
    for pointer in body_transform.m_Children:
        child = export.read_object(pointer.deref())
        game_object_id = child.m_GameObject.path_id
        name = export.read_object(asset.objects[game_object_id]).m_Name
        branch(game_object_id, chest + "/" + name, chest, "battle/human/humanbodyman child " + name)
    for arm in ("zuobi", "youbi"):
        from_resource(f"battle/human/man/body/101/{arm}", chest + f"/{arm}/{arm}", chest + "/" + arm)
    for side in ("zuotui", "youtui"):
        from_resource("battle/human/man/body/101/tui", body + f"/Foot/{side}/{side}", body + f"/Foot/{side}", side)
    head_path = chest + "/tou"
    for category, parent in (("head", head_path), ("hair", head_path + "/toufa")):
        resource = f"battle/human/man/{category}/101"
        if resource in resources:
            from_resource(resource, parent + "/101(Clone)", parent)
    # Keep original leg branch disabled status, exclude its empty slots/shadow
    # only at rendering, and preserve the exact source hierarchy in rig.json.
    meshes, bone_paths, mapping_errors = [], set(), []
    for renderer in renderers:
        path = renderer["rendererPath"]
        relevant = {p: n for p, n in node_by_path.items() if p == path or p.startswith(path + "/") or path.startswith(p + "/")}
        pieces, bones, errors, _ = export.extract_skins(asset, [renderer], relevant)
        meshes.extend(pieces)
        bone_paths.update(bones)
        mapping_errors.extend(errors)
    if mapping_errors:
        raise ValueError(mapping_errors)
    source = export.source_metadata(GAME / "resources.assets")
    rig = {"schemaVersion": 1, "source": source, "rigRootPath": ROOT_PATH, "rigRootGameObjectPathId": 120384,
           "assembly": assembly, "assemblyNote": "Original separately stored male prefabs instantiated into explicit male attachment slots. Not an originally preassembled 101 instance.",
           "coordinateSystem": {"position": "Unity local units; X-right, Y-up, Z-depth", "pixelsPerUnit": 100.0, "rotation": "Unity quaternion XYZW"},
           "nodes": nodes, "boneNodes": [node_by_path[path] for path in sorted(bone_paths)], "meshCount": len(meshes),
           "checks": {"nodeCount": len(nodes), "boneNodeCount": len(bone_paths), "spriteRendererCount": len(meshes), "spriteBonePathMappingErrors": mapping_errors}}
    textures = {}
    for mesh in meshes:
        sprite_id = mesh["sourceSpriteId"]
        mesh["sourceImage"] = f"textures/sprite_{sprite_id}.png"
        mesh["referenceOnly"] = True
        if sprite_id not in textures:
            sprite = export.read_object(asset.objects[sprite_id])
            image = sprite.image.convert("RGBA")
            image.save(OUT / mesh["sourceImage"])
            textures[sprite_id] = {"id": sprite_id, "name": sprite.m_Name, "image": image, "size": list(image.size)}
    skins = {"schemaVersion": 1, "source": source, "rigRootPath": ROOT_PATH, "coordinateSystem": rig["coordinateSystem"],
             "meshes": meshes, "checks": {"rendererCount": len(meshes), "skinnedRendererCount": sum(m["skinning"]["isSkinned"] for m in meshes), "totalVertices": sum(len(m["vertices"]) for m in meshes), "totalTriangles": sum(len(m["triangles"]) for m in meshes), "unmappedSpriteBones": mapping_errors}}
    animations = json.loads((BASE / "output/guigu_source_reference_v2/readback/animations.json").read_text(encoding="utf-8"))
    original_animator_root = animations["bodyAnimatorRootPath"]
    animations["rigRootPath"], animations["bodyAnimatorRootPath"], animations["footAnimatorRootPath"] = ROOT_PATH, body, ROOT_PATH + "/Foot2"
    excluded = {}
    for clip in animations["clips"]:
        clip["animatorRoots"] = [body, ROOT_PATH + "/Foot2"]
        excluded[clip["name"]] = []
        for binding in clip["bindings"]:
            old_target = binding.get("targetRigPath")
            target = str(old_target).replace(original_animator_root, body, 1) if old_target else None
            binding["targetRigPath"] = target if target in node_by_path and binding["path"] is not None else None
            if binding["targetRigPath"] is None:
                excluded[clip["name"]].append({k: binding[k] for k in ("path", "pathHash", "attributeId", "curveIndex")})
        for track in clip["tracks"]:
            old_target = track.get("targetRigPath")
            target = str(old_target).replace(original_animator_root, body, 1) if old_target else None
            track["targetRigPath"] = target if target in node_by_path and track["path"] is not None else None
    animator_controller = export.read_object(asset.objects[54804])
    controller_clips = []
    for pointer in animator_controller.m_AnimationClips:
        clip = export.read_object(pointer.deref())
        controller_clips.append({"name": clip.m_Name, "pathId": pointer.path_id})
    animations["maleAssembly"] = {"maleControllerPathId": 54804, "controllerClips": controller_clips, "excludedBindings": excluded,
        "note": "Located source curves remapped to the original male slots; unknown and absent paths remain null. Complete original state-machine and DynamicBone behavior not reproduced."}
    for filename, data in (("rig.json", rig), ("skins.json", skins), ("animations.json", animations)):
        (OUT / filename).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    evidence = {"source": source, "genderEvidence": {"resourceManagerFile": str(GAME / "globalgamemanagers"), "resourceManagerPathId": 13, "maleRootResource": "battle/human/humanrootman", "maleRootPathId": 114095, "maleBodyResource": "battle/human/humanbodyman", "maleBodyPathId": 355292, "clothingResourcePrefix": "battle/human/man/body/101/"}, "assembly": assembly, "textures": [{k:v for k,v in row.items() if k != "image"} for row in textures.values()], "meshes": [{"rendererPath": m["rendererPath"], "spriteId": m["sourceSpriteId"], "spriteName": m["sourceSpriteName"], "vertices": len(m["vertices"]), "triangles": len(m["triangles"]), "bones": [{"name": b["name"], "path": b["rigPath"]} for b in m["skinning"]["spriteBones"]], "referenceOnly": True} for m in meshes], "controllerClips": controller_clips, "excludedAnimationBindings": excluded}
    (OUT / "male101_evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    (DOC / "原作男性101模型与素材复核.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    sheet = Image.new("RGB", (1440, 680), "#f4f0e5")
    draw = ImageDraw.Draw(sheet)
    title_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 25)
    label_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 19)
    draw.text((25, 15), "原作男性101候选 · 性别由原ResourceManager man/body路径确认", fill="#26383d", font=title_font)
    draw.text((25, 56), "原PNG仅作研究；男性长衣、双臂、腿与头发独立网格，不等同此前10002女紫裙。", fill="#526066", font=label_font)
    for index, row in enumerate(textures.values()):
        image = row["image"]
        x,y=22+(index%6)*238,100+(index//6)*280
        panel=Image.new("RGBA",(218,210),(217,224,220,255))
        picture=image.resize((image.width*3,image.height*3),Image.Resampling.NEAREST)
        picture.thumbnail((208,200),Image.Resampling.NEAREST)
        panel.alpha_composite(picture,((218-picture.width)//2,(210-picture.height)//2))
        sheet.paste(panel.convert("RGB"),(x,y))
        draw.text((x,y+217),f"{row['id']} · {row['name']}",fill="#26383d",font=label_font)
        draw.text((x,y+246),f"{image.width} × {image.height} px",fill="#526066",font=label_font)
    sheet.save(DOC / "原作男性候选素材图集.png")
    print(json.dumps({"output": str(OUT), "nodes": len(nodes), "meshes": len(meshes), "textures": len(textures), "maleControllerClips": controller_clips, "excluded": excluded}, ensure_ascii=False),flush=True)


if __name__ == "__main__":
    main()
