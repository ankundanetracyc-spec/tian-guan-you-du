"""Read-only original male101 same-phase part attribution for source research."""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / "tools/action_battle_test/output/guigu_source_reference_v2/male101"
AUDIT = ROOT / "tools/action_battle_test/output/foot_registration_audit"
DEST = ROOT / "docs/美术规范/鬼谷局内素材拼接研究_20261104"
WIDTH,HEIGHT = 440,500
PALETTE = [(89,176,211),(85,203,147),(170,122,221),(232,184,75),(230,112,104),(231,231,216),(73,83,94)]


def main() -> None:
    DEST.mkdir(parents=True,exist_ok=True)
    data = json.loads((MODEL/"source_reference_vertex_frames.json").read_text(encoding="utf-8"))
    skins = json.loads((MODEL/"skins.json").read_text(encoding="utf-8"))
    diag = json.loads((AUDIT/"Run_New1_042_same_phase.json").read_text(encoding="utf-8"))
    phase = float(diag["original"]["source_phase"])
    clip = data["clips"]["Run_New1"]
    position = phase*int(clip["sampleCount"])
    first,blend = int(position),position%1
    second = (first+1)%int(clip["sampleCount"])
    idle_points = np.array([point for mesh in data["clips"]["Idle"]["frames"][0] for point in mesh])
    center_x = float((idle_points[:,0].min()+idle_points[:,0].max())/2)
    source_foot = float(diag["original"]["source_foot_y"])
    scale = float(diag["original"]["fixed_source_scale"])*.72*4
    frames = clip["secondaryFrames"] if diag["original"]["secondary_surrogate"] else clip["frames"]
    vertices = [(np.array(a)*(1-blend)+np.array(b)*blend-np.array([center_x,source_foot]))*scale+np.array([220,440])
                for a,b in zip(frames[first],frames[second])]
    images = [np.array(Image.open(MODEL/"textures"/f"sprite_{mesh['spriteId']}.png").convert("RGBA")) for mesh in data["meshes"]]
    raw_mesh = {mesh["rendererPath"]:mesh for mesh in skins["meshes"]}
    owners = np.full((HEIGHT,WIDTH),-1,dtype=np.int16)

    def trace(point: tuple[float,float]) -> list[dict]:
        hits = []
        for index,mesh in enumerate(data["meshes"]):
            for triangle in mesh["triangles"]:
                p = vertices[index][triangle]
                basis = np.column_stack((p[1]-p[0],p[2]-p[0]))
                if abs(np.linalg.det(basis))<1e-10: continue
                u,v = np.linalg.solve(basis,np.array(point)-p[0])
                barycentric = np.array([1-u-v,u,v])
                if float(barycentric.min()) < -1e-6: continue
                uv = barycentric @ np.array(mesh["uv"])[triangle]
                image = images[index]
                px = min(image.shape[1]-1,max(0,int(uv[0]*image.shape[1])))
                py = min(image.shape[0]-1,max(0,int(uv[1]*image.shape[0])))
                color = image[py,px]
                if color[3]<128: break
                source = raw_mesh[mesh["rendererPath"]]
                weights = {}
                for vertex_index,amount in zip(triangle,barycentric):
                    for influence in source["skinning"]["boneWeights"][vertex_index]:
                        path = influence["rigPath"]
                        weights[path] = weights.get(path,0.)+float(amount)*float(influence["weight"])
                hits.append({"meshIndex":index,"spriteId":mesh["spriteId"],"rendererPath":mesh["rendererPath"],
                             "sourceUV":uv.tolist(),"sourcePixel":[px,py],"sourceRGBA":color.tolist(),
                             "triangle":triangle,"barycentric":barycentric.tolist(),
                             "interpolatedSourceBoneWeights":dict(sorted(weights.items(),key=lambda row:-row[1]))})
                break
        return hits

    for index,mesh in enumerate(data["meshes"]):
        image = images[index]
        for triangle in mesh["triangles"]:
            p = vertices[index][triangle]
            basis = np.column_stack((p[1]-p[0],p[2]-p[0]))
            if abs(np.linalg.det(basis))<1e-10: continue
            x0,y0 = np.maximum([0,0],np.floor(p.min(axis=0)).astype(int))
            x1,y1 = np.minimum([WIDTH,HEIGHT],np.ceil(p.max(axis=0)).astype(int)+1)
            if x0>=x1 or y0>=y1:continue
            xx,yy = np.meshgrid(np.arange(x0,x1)+.5,np.arange(y0,y1)+.5)
            points = np.stack((xx-p[0,0],yy-p[0,1]),axis=-1)
            coordinates = points @ np.linalg.inv(basis).T
            b = np.stack((1-coordinates.sum(axis=-1),coordinates[:,:,0],coordinates[:,:,1]),axis=-1)
            inside = (b>=-1e-6).all(axis=-1)
            uv = b @ np.array(mesh["uv"])[triangle]
            px = np.clip((uv[:,:,0]*image.shape[1]).astype(int),0,image.shape[1]-1)
            py = np.clip((uv[:,:,1]*image.shape[0]).astype(int),0,image.shape[0]-1)
            visible = inside & (image[py,px,3]>=128)
            owners[y0:y1,x0:x1][visible] = index

    samples = []
    for point in [(245,290),(250,300),(255,310),(260,320),(265,330)]:
        hits = trace(point)
        if not hits or hits[-1]["spriteId"] != 87557 or "/Foot/zuotui/" not in hits[-1]["rendererPath"]:
            raise ValueError("Dark upper-leg source attribution changed")
        samples.append({"screenPixel":list(point),"visibleHit":hits[-1],"allOpaqueHitsBackToFront":hits})

    original = Image.open(AUDIT/"Run_New1_042_same_phase.png").convert("RGB").crop((0,0,WIDTH,HEIGHT))
    original.save(DEST/"男性101原作_Run1同相位.png")
    idmap = np.full((HEIGHT,WIDTH,3),[88,100,106],dtype=np.uint8)
    for index,color in enumerate(PALETTE[:len(data["meshes"])]): idmap[owners==index] = color
    ids = Image.fromarray(idmap)
    ids.save(DEST/"男性101原作_网格覆盖域.png")
    board = Image.new("RGB",(1120,620),"#f3efe4")
    board.paste(original,(0,55))
    board.paste(ids,(440,55))
    draw = ImageDraw.Draw(board)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",18)
    small = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",14)
    draw.text((14,12),"男101原作原纹理 · Run1相位0.228571",font=font,fill="#253239")
    draw.text((453,12),"同几何／UV的部件覆盖域（研究着色）",font=font,fill="#253239")
    for offset in [0,440]:
        draw.rectangle((239+offset,280+55,279+offset,344+55),outline="#ee5650",width=2)
    leg = Image.open(MODEL/"textures/sprite_87557.png").convert("RGBA")
    enlarged = leg.resize((144,408),Image.Resampling.NEAREST)
    board.paste(enlarged,(925,70),enlarged)
    draw.rectangle((925+8*6,70+19*6,925+12*6,70+33*6),outline="#ee5650",width=2)
    draw.text((890,493),"裤腿原裁图87557",font=font,fill="#253239")
    draw.text((890,520),"24×68，整数放大",font=small,fill="#253239")
    draw.text((890,543),"红框来自原图19–32行",font=small,fill="#253239")
    draw.text((14,566),"红框深色块：/Body/Foot/zuotui/zuotui 的裤腿；不是衣身85649的内衬。",font=font,fill="#253239")
    draw.text((14,594),"原游戏PNG未改；右图按原三角、原UV与alpha≥128标示实际覆盖域。",font=small,fill="#253239")
    board.save(DEST/"深色大腿块_原网格与源像素归属.png")
    legend = [{"meshIndex":index,"spriteId":mesh["spriteId"],"rendererPath":mesh["rendererPath"],
               "researchColorRGB":PALETTE[index],"visiblePixelsAtAlpha128":int((owners==index).sum())}
              for index,mesh in enumerate(data["meshes"])]
    report = {"scope":"Original male101 only; no generated art, runtime or registration modified",
              "clip":"Run_New1","clock":43/60,"sourcePhase":phase,"sourceFrames":[first,second],"interpolation":blend,
              "referenceImage":"男性101原作_Run1同相位.png","screenCanvas":[WIDTH,HEIGHT],
              "pixelCoordinates":"Original 440x500 reproduction image coordinates, not arbitrary user screenshot scaling",
              "sourceProjection":{"centerX":center_x,"footY":source_foot,"sourceScaleTimesDisplayTimesZoom":scale},
              "partLegend":legend,"darkRegionSamples":samples,
              "conclusion":"The sampled dark upper-leg region is the original 87557 trouser sprite on the zuotui leg chain; it is not a third garment or coat lining.",
              "limitations":"Source binding/located clip reconstruction, not the original game's full Animator or DynamicBone state; owner diagram uses original alpha>=128 with nearest source-pixel sampling for attribution.",
              "inputHashes":{name:hashlib.sha256((MODEL/name).read_bytes()).hexdigest() for name in ["source_reference_vertex_frames.json","skins.json"]}}
    (DEST/"深色大腿块与源覆盖域证据.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"samples":len(samples),"sourcePhase":phase,"darkSprite":87557,"renderer":"/Body/Foot/zuotui/zuotui","output":str(DEST)},ensure_ascii=False))


if __name__=="__main__": main()
