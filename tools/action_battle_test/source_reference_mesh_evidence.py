"""Read only the selected original 10002 sprites and document mesh evidence."""
from __future__ import annotations

import hashlib
import json
import math
import struct
from pathlib import Path

import UnityPy
from UnityPy.streams import EndianBinaryReader
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tools/action_battle_test/output/guigu_source_reference_v2/readback"
DOC = ROOT / "docs/美术规范/鬼谷原作网格与素材重构_20261104"
TEXTURES = OUT / "textures"
FONT = "C:/Windows/Fonts/msyh.ttc"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def checker(size):
    image = Image.new("RGBA", size, (231, 235, 232, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 16):
        for x in range(0, size[0], 16):
            if (x // 16 + y // 16) % 2:
                draw.rectangle((x, y, x + 15, y + 15), fill=(214, 220, 218, 255))
    return image


def font(size):
    return ImageFont.truetype(FONT, size)


def parse_raw_frames(clip):
    words = clip["rawStreamedClip"]["dataUInt32"]
    data = struct.pack("<" + "I" * len(words), *words)
    frames, offset, total = [], 0, 0
    while offset < len(data):
        time, count = struct.unpack_from("<fI", data, offset)
        offset += 8
        rows = []
        for _ in range(count):
            rows.append(list(struct.unpack_from("<I4f", data, offset)))
            offset += 20
        total += 1
        if math.isfinite(time) and time >= 0:
            frames.append([time, rows])
    if offset != len(data):
        raise ValueError("Partial clip stream")
    decoded = clip["decodedStreamFrames"]
    same = frames == decoded
    if not same:
        raise ValueError(f"Fresh raw stream differs from decoded coefficients for {clip['name']}")
    return {"rawFrames": total, "finiteFrames": len(frames), "wordCount": len(words), "decodedCoefficientsEqual": same}


def main():
    DOC.mkdir(parents=True, exist_ok=True)
    TEXTURES.mkdir(parents=True, exist_ok=True)
    skins = json.loads((OUT / "skins.json").read_text(encoding="utf-8"))
    animations = json.loads((OUT / "animations.json").read_text(encoding="utf-8"))
    layout = json.loads((ROOT / "tools/action_battle_test/art/guigu_reference/layout.json").read_text(encoding="utf-8"))
    ids = list(dict.fromkeys(int(Path(row["sprite"]).stem.split("_")[-1]) for row in layout))
    env = UnityPy.Environment()
    asset = env.load_file(skins["source"]["path"])
    # The 25 GB resource stream stays on disk; only selected Sprite ranges read.
    stream_path = Path(skins["source"]["path"]).with_name("resources.assets.resS")
    env.register_cab("resources.assets.resS", EndianBinaryReader(str(stream_path)))
    images, image_checks = {}, []
    for sprite_id in ids:
        sprite = asset.objects[sprite_id].read(check_read=False)
        image = sprite.image.convert("RGBA")
        target = TEXTURES / f"sprite_{sprite_id}.png"
        image.save(target)
        previous_path = ROOT / f"tools/action_battle_test/art/guigu_reference/sprite_{sprite_id}.png"
        previous = Image.open(previous_path).convert("RGBA")
        pixel_equal = image.size == previous.size and image.tobytes() == previous.tobytes()
        images[sprite_id] = image
        image_checks.append({"spriteId": sprite_id, "spriteName": sprite.m_Name, "size": list(image.size), "freshPng": target.relative_to(ROOT).as_posix(), "existingPng": previous_path.relative_to(ROOT).as_posix(), "freshPngSha256": sha(target), "existingPngSha256": sha(previous_path), "rgbaPixelsEqual": pixel_equal, "alphaPixelsEqual": image.size == previous.size and image.getchannel("A").tobytes() == previous.getchannel("A").tobytes()})
    stream_checks = {clip["name"]: parse_raw_frames(clip) for clip in animations["clips"]}
    mesh_checks = []
    for mesh in skins["meshes"]:
        if mesh["sourceSpriteId"] not in ids:
            continue
        skin = mesh["skinning"]
        bones = skin["spriteBones"]
        influences = {str(b["index"]): sum(any(w["boneIndex"] == b["index"] and w["weight"] > 0 for w in row) for row in skin["boneWeights"]) for b in bones}
        mesh_checks.append({"spriteId": mesh["sourceSpriteId"], "rendererPath": mesh["rendererPath"], "spriteName": mesh["sourceSpriteName"], "activeInHierarchy": mesh["activeInHierarchy"], "vertices": len(mesh["vertices"]), "triangles": len(mesh["triangles"]), "bones": [{"name": b["name"], "path": b["rigPath"], "influencedVertexCount": influences[str(b["index"])]} for b in bones], "blendedVertices": sum(len(row) > 1 for row in skin["boneWeights"]), "weights": skin["boneWeights"]})
    labels = {79450: "连续衣服／袍摆", 78517: "左臂：主要摆动", 89721: "右臂：持架", 71535: "前臂挂接布片（5骨）", 56808: "头发", 76416: "脸", 61872: "腿：左右共用同图"}
    sheet = Image.new("RGB", (1440, 1010), "#f6f3eb")
    draw = ImageDraw.Draw(sheet)
    draw.text((30, 15), "本机原资源：10002人物活动贴图（原PNG尺寸，统一3倍显示）", fill="#202c32", font=font(27))
    draw.text((30, 58), "仅局部读取7张独立Sprite；左右腿复用同一图。透明边缘、形状与现有参考PNG逐像素核对。", fill="#48565b", font=font(20))
    for index, sprite_id in enumerate(ids):
        x, y = 26 + (index % 4) * 353, 102 + (index // 4) * 444
        image = images[sprite_id]
        panel = checker((330, 352))
        displayed = image.resize((image.width * 3, image.height * 3), Image.Resampling.NEAREST)
        if displayed.width > 320 or displayed.height > 340:
            displayed.thumbnail((320, 340), Image.Resampling.NEAREST)
        panel.alpha_composite(displayed, ((330-displayed.width)//2, (352-displayed.height)//2))
        sheet.paste(panel.convert("RGB"), (x, y))
        draw.text((x, y+359), f"{sprite_id} · {labels.get(sprite_id,'Sprite')}", fill="#26363b", font=font(19))
        draw.text((x, y+388), f"原尺寸 {image.width} × {image.height} px", fill="#526066", font=font(18))
    sheet.save(DOC / "原作活动素材图集.png")
    palette = [(225,75,75), (44,136,221), (49,164,108), (224,167,35), (150,86,197)]
    targets = [79450,78517,89721,71535]
    sheet2 = Image.new("RGB", (1920, 790), "#f6f3eb")
    draw2 = ImageDraw.Draw(sheet2)
    draw2.text((24, 18), "原作连续网格与逐顶点权重（原图UV位置；圆点为混合权重颜色）",fill="#202c32",font=font(26))
    for index, sprite_id in enumerate(targets):
        mesh = next(m for m in skins["meshes"] if m["sourceSpriteId"]==sprite_id)
        image=images[sprite_id]
        skin=mesh["skinning"]
        factor=4
        panel=checker((430,440))
        texture=image.resize((image.width*factor,image.height*factor),Image.Resampling.NEAREST)
        ox,oy=(430-texture.width)//2,(440-texture.height)//2
        panel.alpha_composite(texture,(ox,oy))
        pd=ImageDraw.Draw(panel)
        points=[(ox+uv[0]*image.width*factor,oy+(1-uv[1])*image.height*factor) for uv in mesh["uvConvention"]["uvCropLowerLeft"]]
        for tri in mesh["triangles"]:
            pd.line([points[v] for v in tri]+[points[tri[0]]],fill=(57,91,101,180),width=2)
        for pos,weights in zip(points,skin["boneWeights"]):
            color=tuple(round(sum(palette[w["boneIndex"]%len(palette)][channel]*w["weight"] for w in weights)) for channel in range(3))
            pd.ellipse((pos[0]-5,pos[1]-5,pos[0]+5,pos[1]+5),fill=color+(255,),outline="white",width=1)
        x=25+index*477
        sheet2.paste(panel.convert("RGB"),(x,80))
        draw2.text((x,530),f"{labels[sprite_id]} · {len(mesh['vertices'])}点 / {len(mesh['triangles'])}三角",fill="#23363b",font=font(20))
        for j,b in enumerate(skin["spriteBones"]):
            y=565+j*37
            draw2.ellipse((x,y+3,x+17,y+20),fill=palette[j])
            count=sum(any(w["boneIndex"]==j and w["weight"]>0 for w in row) for row in skin["boneWeights"])
            draw2.text((x+26,y),f"{b['name']}：影响{count}顶点",fill="#36464a",font=font(19))
    sheet2.save(DOC / "原作袍摆双臂网格与权重.png")
    report={"source":skins["source"],"scope":"Only character 10002 active layout's seven unique sprites; no broad texture sweep", "images":image_checks,"meshChecks":mesh_checks,"freshRawStreamChecks":stream_checks,"allSourcePngPixelsEqual":all(row["rgbaPixelsEqual"] for row in image_checks),"checks":skins["checks"]}
    (OUT/"source_mesh_evidence.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    (DOC/"原作素材与网格复核.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"sourceSha256":skins["source"]["sha256"],"sprites":len(images),"allSourcePngPixelsEqual":report["allSourcePngPixelsEqual"],"rawStreamsVerified":list(stream_checks),"docs":str(DOC)},ensure_ascii=False))


if __name__=="__main__":
    main()
