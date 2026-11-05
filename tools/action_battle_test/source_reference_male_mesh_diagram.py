"""Plot the newly extracted male 101 mesh, UV and source weight evidence."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
DATA = PROJECT / "tools/action_battle_test/output/guigu_source_reference_v2/male101"
DOC = PROJECT / "docs/美术规范/鬼谷原作网格与素材重构_20261104"
COLORS = [(221,65,65), (42,126,213), (35,155,100), (222,161,25), (141,75,191), (31,159,174), (211,91,154)]


def main():
    skins = json.loads((DATA / "skins.json").read_text(encoding="utf-8"))
    selected = [85649,84807,79543,87557]
    labels = {85649:"连续男长袍",84807:"左臂长袖",79543:"右臂长袖",87557:"长裤与靴（双腿共图）"}
    sheet = Image.new("RGB", (1920, 990), "#f6f2e8")
    draw = ImageDraw.Draw(sheet)
    title = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 27)
    normal = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 20)
    small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
    draw.text((24,18), "原作男性101 · 连续衣袖网格与真实蒙皮权重", fill="#25373a", font=title)
    draw.text((24,60), "由原ResourceManager男性路径确认；原蒙皮索引保持。颜色表示权重混合，不是重做后的新分片。", fill="#516064", font=normal)
    summaries=[]
    for index,sprite_id in enumerate(selected):
        mesh=next(m for m in skins["meshes"] if m["sourceSpriteId"]==sprite_id)
        image=Image.open(DATA/mesh["sourceImage"]).convert("RGBA")
        factor=4
        panel=Image.new("RGBA", (438,480), (217,224,221,255))
        pd=ImageDraw.Draw(panel)
        for py in range(0,480,16):
            for px in range(0,438,16):
                if (px//16+py//16)%2:pd.rectangle((px,py,px+15,py+15),fill=(231,235,233,255))
        texture=image.resize((image.width*factor,image.height*factor),Image.Resampling.NEAREST)
        ox,oy=(438-texture.width)//2,(480-texture.height)//2
        panel.alpha_composite(texture,(ox,oy))
        pd=ImageDraw.Draw(panel)
        points=[(ox+uv[0]*texture.width,oy+(1-uv[1])*texture.height) for uv in mesh["uvConvention"]["uvCropLowerLeft"]]
        for tri in mesh["triangles"]:
            pd.line([points[v] for v in tri]+[points[tri[0]]],fill=(47,85,95,190),width=2)
        for point,weights in zip(points,mesh["skinning"]["boneWeights"]):
            color=tuple(round(sum(COLORS[w["boneIndex"]%len(COLORS)][channel]*w["weight"] for w in weights)) for channel in range(3))
            pd.ellipse((point[0]-4,point[1]-4,point[0]+4,point[1]+4),fill=color+(255,),outline="white",width=1)
        x=25+477*index
        sheet.paste(panel.convert("RGB"),(x,110))
        draw.text((x,606),labels[sprite_id],fill="#26383d",font=normal)
        draw.text((x,636),f"{len(mesh['vertices'])}顶点／{len(mesh['triangles'])}三角／{len(mesh['skinning']['spriteBones'])}骨",fill="#506065",font=normal)
        bones=[]
        for b in mesh["skinning"]["spriteBones"]:
            bi=b["index"]
            count=sum(any(w["boneIndex"]==bi and w["weight"]>0 for w in row) for row in mesh["skinning"]["boneWeights"])
            y=680+bi*34
            draw.ellipse((x,y+4,x+16,y+20),fill=COLORS[bi])
            draw.text((x+25,y),f"[{bi}] {b['name']} · {count}点",fill="#35464a",font=small)
            bones.append({"index":bi,"name":b["name"],"parentIndex":b["parentIndex"],"influencedVertices":count})
        summaries.append({"spriteId":sprite_id,"vertices":len(mesh["vertices"]),"triangles":len(mesh["triangles"]),"blendedVertices":sum(len(row)>1 for row in mesh["skinning"]["boneWeights"]),"bones":bones})
    draw.text((24,950), "男袍骨链：shenti2 → bone_5 → shenti2_1 → shenti2_2；另分 bone_3 → bone_4。双臂长袖独立蒙皮，没有沿用女修的前臂外挂布片。", fill="#4b5a60",font=small)
    sheet.save(DOC/"原作男性101衣袖网格与权重.png")
    (DOC/"原作男性101网格权重统计.json").write_text(json.dumps({"source":skins["source"],"meshes":summaries},ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(summaries,ensure_ascii=False))


if __name__=="__main__":main()
