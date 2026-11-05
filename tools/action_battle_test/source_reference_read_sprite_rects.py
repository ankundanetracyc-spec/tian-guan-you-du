"""Read only seven selected original Sprite rect fields and full tiny textures."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import UnityPy
from UnityPy.streams import EndianBinaryReader
from UnityPy.export.SpriteHelper import SpriteSettings


ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / "tools/action_battle_test/output/guigu_source_reference_v2/male101"
DEST = ROOT / "docs/美术规范/鬼谷局内素材拼接研究_20261104"


def simple(value):
    if value is None or isinstance(value,(bool,int,float,str)): return value
    if hasattr(value,"x") and hasattr(value,"y"):
        return {key:float(getattr(value,key)) for key in ("x","y","z","w") if hasattr(value,key)}
    if hasattr(value,"value"): return value.value
    if hasattr(value,"__dict__"):
        return {key:simple(item) for key,item in vars(value).items() if not key.startswith("_")}
    return str(value)


def main() -> None:
    skins = json.loads((MODEL/"skins.json").read_text(encoding="utf-8"))
    source = Path(skins["source"]["path"])
    env = UnityPy.Environment()
    asset = env.load_file(str(source))
    env.register_cab("resources.assets.resS",EndianBinaryReader(str(source.with_name("resources.assets.resS"))))
    texture_dir = DEST/"source_full_textures"
    texture_dir.mkdir(parents=True,exist_ok=True)
    records = []
    for sprite_id in [65806,79543,84807,85649,87557,87679,60765]:
        sprite = asset.objects[sprite_id].read(check_read=False)
        rd = sprite.m_RD
        texture = rd.texture.deref().read(check_read=False)
        image = texture.image.convert("RGBA")
        output = texture_dir/f"sprite_{sprite_id}_full_texture.png"
        image.save(output)
        record = {
            "spriteId":sprite_id,"spriteName":sprite.m_Name,"texturePathId":int(rd.texture.path_id),
            "textureName":texture.m_Name,"fullTextureSize":list(image.size),
            "spriteRect":{name:float(getattr(sprite.m_Rect,name)) for name in ("x","y","width","height")},
            "spritePivot":simple(sprite.m_Pivot),"pixelsPerUnit":float(sprite.m_PixelsToUnits),
            "rdTextureRect":{name:float(getattr(rd.textureRect,name)) for name in ("x","y","width","height")},
            "rdFields":{},"spriteFields":{},
            "fullTexturePng":output.relative_to(ROOT).as_posix(),
            "fullTexturePngSha256":hashlib.sha256(output.read_bytes()).hexdigest(),
        }
        for name in ["textureRectOffset","atlasRectOffset","settingsRaw","packingRotation","uvTransform"]:
            record["rdFields"][name]={"present":hasattr(rd,name),"value":simple(getattr(rd,name,None))}
        for name in ["m_Offset","m_Border","m_IsPolygon"]:
            record["spriteFields"][name]={"present":hasattr(sprite,name),"value":simple(getattr(sprite,name,None))}
        settings=SpriteSettings(int(rd.settingsRaw))
        record["decodedSettings"]={"method":"UnityPy.export.SpriteHelper.SpriteSettings bit decoding of the exact serialized settingsRaw",
            "packed":settings.packed,"packingMode":{"value":int(settings.packingMode),"name":settings.packingMode.name},
            "packingRotation":{"value":int(settings.packingRotation),"name":settings.packingRotation.name},
            "meshType":{"value":int(settings.meshType),"name":settings.meshType.name}}
        records.append(record)
        print(json.dumps({"spriteId":sprite_id,"size":image.size,"RD":record["rdFields"],"offset":record["spriteFields"]["m_Offset"]},ensure_ascii=False),flush=True)
    report={"scope":"Only seven known male101 Sprite objects and their selected Texture2D stream ranges; original installation unchanged",
            "source":skins["source"],"coordinateNote":"rdTextureRect and explicit offset fields are separate original serialized fields. No equality or trim meaning is inferred from matching numbers.",
            "records":records}
    (DEST/"原Sprite裁剪偏移与完整纹理字段.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")


if __name__=="__main__": main()
