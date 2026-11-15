"""Read original male101 DynamicBone records; never modify the game installation.

The field order is the installation's Mod SDK DynamicBone.cs. AnimationCurve
records are Unity 2020 keyframes (including weighted mode/weights). Exact byte
consumption and a fresh comparison against the existing cloth payload guard the
manual, named layout. No unidentified float is promoted to a physical parameter.
"""
from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

import UnityPy

PROJECT = Path(__file__).resolve().parents[2]
GAME = Path("E:/SteamLibrary/steamapps/common/鬼谷八荒")
DATA = GAME / "guigubahuang_Data"
SDK = GAME / "Mod/modFQA/资源修改教程/ResBuildABProject/Assets/Scripts/Packages/DynamicBone/Scripts"
RIG = PROJECT / "tools/action_battle_test/output/guigu_source_reference_v2/male101/rig.json"
OUT = PROJECT / "resources/appearance/guigu_native_first_20261105/dynamic_bone_config.json"


class Reader:
    def __init__(self, data):
        self.data, self.offset = data, 0

    def read(self, fmt):
        values = struct.unpack_from("<" + fmt, self.data, self.offset)
        self.offset += struct.calcsize("<" + fmt)
        return values[0] if len(values) == 1 else list(values)

    def align(self):
        self.offset = (self.offset + 3) & ~3

    def pointer(self):
        return {"fileID": self.read("i"), "pathID": self.read("q")}

    def curve(self):
        count = self.read("i")
        assert 0 <= count < 10000
        keys = []
        for _ in range(count):
            time, value, incoming, outgoing = self.read("4f")
            weighted = self.read("i")
            in_weight, out_weight = self.read("2f")
            keys.append(dict(time=time, value=value, inTangent=incoming,
                             outTangent=outgoing, weightedMode=weighted,
                             inWeight=in_weight, outWeight=out_weight))
        return dict(keys=keys, preWrapMode=self.read("i"),
                    postWrapMode=self.read("i"), rotationOrder=self.read("i"))


def decode(data):
    r = Reader(data)
    header = dict(gameObject=r.pointer(), enabled=bool(r.read("B")))
    r.align()
    header["script"] = r.pointer()
    name_length = r.read("i")
    header["name"] = data[r.offset:r.offset + name_length].decode("utf-8")
    r.offset += name_length
    r.align()
    config = dict(root=r.pointer(), updateRate=r.read("f"), updateMode=r.read("i"))
    for key in ("damping", "elasticity", "stiffness", "inert", "radius"):
        config[key] = r.read("f")
        config[key + "Distrib"] = r.curve()
    config["endLength"] = r.read("f")
    for key in ("endOffset", "gravity", "force"):
        config[key] = r.read("3f")
    for key in ("colliders", "exclusions"):
        config[key] = [r.pointer() for _ in range(r.read("i"))]
    config["freezeAxis"] = r.read("i")
    config["distantDisable"] = bool(r.read("B"))
    r.align()
    config["referenceObject"] = r.pointer()
    config["distanceToObject"] = r.read("f")
    config["isEnbale"] = bool(r.read("B"))  # Original public-field spelling.
    r.align()
    assert r.offset == len(data), (r.offset, len(data))
    return header, config


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    rig = json.loads(RIG.read_text(encoding="utf-8"))
    by_transform = {n["transformPathId"]: n["path"] for n in rig["nodes"]}
    env = UnityPy.Environment()
    asset = env.load_file(str(DATA / "resources.assets"))
    env.load_file(str(DATA / "globalgamemanagers.assets"))
    records = []
    for node in rig["nodes"]:
        if "MonoBehaviour" not in node["components"]:
            continue
        game_object = asset.objects[node["pathId"]].read(check_read=False)
        for pair in game_object.m_Component:
            pointer = pair.component
            obj = pointer.deref()
            if obj.type.name != "MonoBehaviour":
                continue
            mono = obj.read(check_read=False)
            script = mono.m_Script.deref().read(check_read=False)
            if script.m_ClassName != "DynamicBone":
                continue
            raw = obj.get_raw_data()
            header, config = decode(raw)
            config["componentPath"] = node["path"]
            config["rootPath"] = by_transform[config["root"]["pathID"]]
            config["exclusionPaths"] = [by_transform[p["pathID"]] for p in config["exclusions"]]
            config["enabled"] = header["enabled"]
            config["componentPathID"] = obj.path_id
            config["rawPayloadHex"] = raw.hex()
            config["rawPayloadSha256"] = hashlib.sha256(raw).hexdigest()
            config["rawPayloadBytes"] = len(raw)
            config["bytesConsumed"] = len(raw)
            config["scriptClass"] = script.m_ClassName
            config["assemblyName"] = script.m_AssemblyName
            records.append(config)
    old = json.loads((RIG.parent / "male_secondary_components.json").read_text(encoding="utf-8"))
    cloth = next(c for c in records if c["componentPathID"] == 1253484)
    assert cloth["rawPayloadHex"] == old[0]["rawPayloadHex"]
    result = dict(schemaVersion=1, sourceRig=str(RIG.relative_to(PROJECT)).replace("\\", "/"),
                  sourceAsset=rig["source"], sourceUnityVersion=asset.unity_version,
                  algorithmSourceFiles=[dict(path=str(p), sha256=sha(p)) for p in
                    (SDK / "DynamicBone.cs", SDK / "DynamicBoneCollider.cs",
                     SDK / "DynamicBoneColliderBase.cs", SDK / "DynamicBonePlaneCollider.cs")],
                  coordinateSystem="Unity full 3D local units; physics before XY projection; no HD rescaling",
                  extraction="Fresh selected male101 GameObject components, Mod SDK named field order, complete byte consumption; original installation read-only.",
                  verification=dict(clothPayloadMatchesEarlierReadback=True,
                      componentCount=len(records), allBytesConsumed=True,
                      runtimeBinaryAlgorithmIdentity="SDK source is supplied with this installation; exact native-code identity has not been established.",
                      originalGameStateMachineAndRuntimeOverrides="Not captured; restoring serialized setup and supplied algorithm is not proof of complete original runtime state."),
                  components=records)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(output=str(OUT), components=[{k:c[k] for k in
        ("componentPath", "rootPath", "updateRate", "updateMode", "damping", "elasticity",
         "stiffness", "inert", "radius", "endLength", "endOffset", "gravity", "force",
         "freezeAxis", "colliders", "exclusions", "isEnbale", "rawPayloadBytes")} for c in records]), ensure_ascii=False))


if __name__ == "__main__":
    main()
