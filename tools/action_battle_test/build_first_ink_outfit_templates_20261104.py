"""Create full-Sprite-canvas drawing guides, never production art or alpha.

All joints originate in source bindpose/renderer coordinates. Crop UV is
mapped through the serialized texture rectangle and UV transform into the
logical full Sprite canvas before multiplying the shared guide density.
"""
from __future__ import annotations
import hashlib
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image,ImageDraw,ImageFont,ImageFilter

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/"tools/action_battle_test/output/guigu_source_reference_v2/male101"
RESEARCH=ROOT/"docs/美术规范/鬼谷局内素材拼接研究_20261104"
OUTPUT=ROOT/"tools/action_battle_test/art/ink_outfit_01_20261104/assets/template"
DOC=ROOT/"docs/美术规范/首套高清水墨长衣_20261104"
K=24
PARTS={"body":85649,"sleeve_near":79543,"sleeve_far":84807,"leg":87557}
NAMES={"body":"连续衣身与袍摆","sleeve_near":"近侧完整长袖臂与手","sleeve_far":"远侧完整长袖臂与手","leg":"完整裤腿、束口、靴筒与足部"}

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def role(name):
    return {"youbi1":"shoulder","youbi2":"elbow","youbi3":"wrist","zuobi1":"shoulder","zuobi2":"elbow","zuobi3":"wrist","tui1":"hip","tui2":"knee","tui3":"ankle","shenti1":"upper_body","shenti2":"cloth_root"}.get(name,name)

def masks(image):
    alpha=image.getchannel("A")
    return alpha.point(lambda v:255 if v>0 else 0),alpha.point(lambda v:255 if v>=128 else 0)

def scaled_bbox(box):return [v*K for v in box] if box else None

def contour_segments(mask):
    values=np.array(mask)>0;H,W=values.shape;segments=[]
    for y,x in zip(*np.nonzero(values)):
        if y==0 or not values[y-1,x]:segments.append([[int(x),int(y)],[int(x+1),int(y)]])
        if x==W-1 or not values[y,x+1]:segments.append([[int(x+1),int(y)],[int(x+1),int(y+1)]])
        if y==H-1 or not values[y+1,x]:segments.append([[int(x+1),int(y+1)],[int(x),int(y+1)]])
        if x==0 or not values[y,x-1]:segments.append([[int(x),int(y+1)],[int(x),int(y)]])
    return segments

def create_source_overview(components):
    scale=8;column=500;top=100
    panel=Image.new("RGB",(column*4,1050),"#f1eee4")
    draw=ImageDraw.Draw(panel);font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",23);small=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",17)
    draw.text((18,12),"四件原素材 · 完整Sprite画布 · 同像素密度8倍只读放大",font=font,fill="#263942")
    draw.text((18,48),"原色只用于读位置和结构；最终首套为自有高清水墨。骨点、纹理分区、透明形变余量分别标注。",font=small,fill="#405460")
    regions={}
    for index,(key,c) in enumerate(components.items()):
        x=index*column+10;W,H=c["sourceCanvasSize"]
        im=Image.open(ROOT/c["sourceFullTexture"]).convert("RGBA")
        up=im.resize((W*scale,H*scale),Image.Resampling.NEAREST)
        panel.paste(up,(x,top),up)
        draw.rectangle((x,top,x+W*scale,top+H*scale),outline="#9aa7a8",width=2)
        draw.text((x,top+H*scale+12),f"{key}: {W}×{H} → {c['templateCanvasSize'][0]}×{c['templateCanvasSize'][1]}",font=small,fill="#263942")
        anchors={a["name"]:a for a in c["anchors"]}
        if key=="body":
            p=anchors["cloth_root"]["sourceCanvasPx"];px,py=x+p[0]*scale,top+p[1]*scale
            draw.line((x+10*scale,py,x+50*scale,py),fill="#e45651",width=2)
            draw.ellipse((px-4,py-4,px+4,py+4),fill="#e45651")
            draw.text((x+2,py-28),"waist / cloth root",font=small,fill="#a72e35",stroke_width=1,stroke_fill="#f1eee4")
            draw.text((x,945),"腰点：完整画布Y≈43.11",font=small,fill="#263942")
            draw.text((x,973),"原件未见独立横向宽腰带",font=small,fill="#263942")
            regions[key]={"waistRigPivot":p,"separateHorizontalBeltConfirmed":False}
        elif key.startswith("sleeve"):
            rows=(34,40) if key=="sleeve_near" else (35,42)
            alpha=np.array(im.getchannel("A"))>=128;yy,xx=np.nonzero(alpha[rows[0]:rows[1]])
            box=[int(xx.min()),int(yy.min()+rows[0]),int(xx.max()+1),int(yy.max()+rows[0]+1)]
            draw.rectangle((x+box[0]*scale,top+box[1]*scale,x+box[2]*scale,top+box[3]*scale),outline="#3f9aba",width=3)
            wrist=anchors["wrist"]["sourceCanvasPx"]
            draw.ellipse((x+wrist[0]*scale-4,top+wrist[1]*scale-4,x+wrist[0]*scale+4,top+wrist[1]*scale+4),fill="#e45651")
            draw.text((x+4,top+rows[0]*scale-30),"cuff / hand transition",font=small,fill="#245d78",stroke_width=1,stroke_fill="#f1eee4")
            draw.text((x,945),"袖口色带与腕pivot分开",font=small,fill="#263942")
            draw.text((x,973),"近远袖分别画，不镜像复制",font=small,fill="#263942")
            regions[key]={"observedCuffToHandRows":list(rows),"observedCoreBBox":box,"note":"Observed source cloth/trim-to-hand transition band, not another skeleton joint"}
        else:
            cuff=anchors["boot_cuff_top"]["sourceCanvasPx"];bottom=anchors["boot_cuff_bottom"]["sourceCanvasPx"]
            draw.rectangle((x+14*scale,top+cuff[1]*scale,x+31*scale,top+bottom[1]*scale),outline="#3f9aba",width=3)
            draw.text((x+3,top+cuff[1]*scale-28),"trouser cuff / boot top",font=small,fill="#245d78",stroke_width=1,stroke_fill="#f1eee4")
            instep=anchors["instep"]["sourceCanvasPx"]
            draw.ellipse((x+instep[0]*scale-4,top+instep[1]*scale-4,x+instep[0]*scale+4,top+instep[1]*scale+4),fill="#e08e32")
            draw.text((x+2,top+instep[1]*scale-29),"instep / toe volume",font=small,fill="#9c4d18",stroke_width=1,stroke_fill="#f1eee4")
            for triangle in c.get("sourceFootMeshTriangles",[]):
                points=[(x+p[0]*scale,top+p[1]*scale) for p in triangle]
                draw.line(points+[points[0]],fill="#d4a45f",width=1)
            draw.text((x,945),"脚背/鞋头可在网格余量内圆厚",font=small,fill="#263942")
            draw.text((x,973),"不能把全部三角形涂成实心楔",font=small,fill="#263942")
            regions[key]={"trouserCuffTop":cuff,"trouserCuffBottom":bottom,"instep":instep,"bootMarginUse":"Rounded volume may be redesigned inside the actual source foot triangle domain; do not fill the entire triangle envelope"}
        for name in (["shoulder","elbow","wrist"] if key.startswith("sleeve") else (["hip","knee","ankle"] if key=="leg" else [])):
            p=anchors[name]["sourceCanvasPx"];px,py=x+p[0]*scale,top+p[1]*scale
            draw.ellipse((px-3,py-3,px+3,py+3),fill="#e45651")
    draw.text((18,1010),"红点为骨pivot；蓝框为原纹理分区；橙点/细线为脚背观察与源网格余量。工程图不进入正式美术或Alpha。",font=small,fill="#405460")
    name="four_original_parts_fullcanvas_regions.png"
    panel.save(OUTPUT/name);panel.save(DOC/"四件原素材完整画布与实际分区.png")
    return {"guideImage":name,"densityMultiplier":scale,"regions":regions,"productionArt":False}

def create_guides(key,record,image,anchors,overlap):
    size=tuple(record["templateCanvasSize"])
    soft,core=masks(image)
    soft_up=soft.resize(size,Image.Resampling.NEAREST)
    core_up=core.resize(size,Image.Resampling.NEAREST)
    # Deliberately RGB with a drawn grid: these cannot be mistaken for the
    # transparent production mask or a texture to load into the renderer.
    shape=Image.new("RGB",size,"#f7f4ec")
    shape.paste("#deded5",(0,0,*size),soft_up)
    shape.paste("#818d90",(0,0,*size),core_up)
    edge=core_up.filter(ImageFilter.MaxFilter(5))
    edge_array=np.array(edge,dtype=np.int16)-np.array(core_up,dtype=np.int16)
    shape.paste("#334e59",(0,0,*size),Image.fromarray(np.clip(edge_array,0,255).astype(np.uint8)))
    draw=ImageDraw.Draw(shape)
    for x in range(0,size[0],K*5):draw.line((x,0,x,size[1]),fill="#d1d2c9",width=1)
    for y in range(0,size[1],K*5):draw.line((0,y,size[0],y),fill="#d1d2c9",width=1)
    small=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",19)
    draw.text((16,10),"DRAWING GUIDE ONLY · SOURCE FULL CANVAS · NOT ART / ALPHA",font=small,fill="#435660")
    shape_name=key+"_shape_guide.png"
    shape.save(OUTPUT/shape_name)
    marked=shape.copy();draw=ImageDraw.Draw(marked)
    rectangle=record["sourceCropRectTopLeftInCanvas"]
    rect=[rectangle[0]*K,rectangle[1]*K,rectangle[2]*K,rectangle[3]*K]
    draw.rectangle(rect,outline="#4a91bc",width=3)
    large=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
    draw.text((16,40),NAMES[key]+f" · {size[0]}×{size[1]} · k=24",font=large,fill="#263b47")
    byname={a["name"]:a for a in anchors}
    chain=["shoulder","elbow","wrist"] if key.startswith("sleeve") else (["hip","knee","ankle"] if key=="leg" else ["upper_body","cloth_root","bone_5","shenti2_1","shenti2_2"])
    available=[byname[n] for n in chain if n in byname]
    for a,b in zip(available,available[1:]):draw.line((*a["templatePx"],*b["templatePx"]),fill="#28b0cc",width=4)
    if key=="body":
        for pair in [("cloth_root","bone_5"),("bone_5","bone_3"),("bone_3","bone_4")]:
            if all(n in byname for n in pair):draw.line((*byname[pair[0]]["templatePx"],*byname[pair[1]]["templatePx"]),fill="#28b0cc",width=4)
    for anchor in anchors:
        x,y=anchor["templatePx"]
        if not (0<=x<size[0] and 0<=y<size[1]):continue
        joint=anchor["kind"] in ["source_skin_bindpose_pivot","external_source_rig_mount"]
        color="#ed594e" if joint else "#d49b31"
        draw.ellipse((x-7,y-7,x+7,y+7),fill=color,outline="#fff9eb",width=2)
        # Exact values remain in JSON; guide labels do not create another
        # hand-maintained set of target coordinates.
        if anchor["name"] in chain or anchor["name"] in ["head_neck","near_shoulder_mount","far_shoulder_mount","boot_cuff_top","toe_tip","sole_center"]:
            label=anchor["name"]
            lx=max(5,min(size[0]-250,x+13));ly=max(75,min(size[1]-35,y-23))
            draw.text((lx,ly),label,font=large,fill="#142e3a",stroke_width=2,stroke_fill="#f7f4ec")
    if overlap["sourceAlphaIntersectionBBox"]:
        box=scaled_bbox(overlap["sourceAlphaIntersectionBBox"])
        draw.rectangle(box,outline="#b34e9d",width=4)
        draw.text((max(8,min(size[0]-400,box[0])),min(size[1]-55,max(80,box[1]-35))),"attachment allowance / underlap",font=large,fill="#9c317f",stroke_width=2,stroke_fill="#f7f4ec")
    guide_name=key+"_registration_guide.png"
    marked.save(OUTPUT/guide_name)
    return {"shapeGuide":shape_name,"registrationGuide":guide_name,"imageMode":"RGB","productionAlpha":False,
            "notes":"Pixel masks and contour/grid/axes are technical guides only. Native HD art must redraw a continuous transparent silhouette; never use these RGB guides or enlarged low-resolution alpha as production textures."}

def main():
    OUTPUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
    rig=json.loads((SOURCE/"rig.json").read_text(encoding="utf-8"))
    skins=json.loads((SOURCE/"skins.json").read_text(encoding="utf-8"))
    original_fields=json.loads((RESEARCH/"原Sprite裁剪偏移与完整纹理字段.json").read_text(encoding="utf-8"))
    fields={r["spriteId"]:r for r in original_fields["records"]}
    world={n["path"]:np.array(n["restMatrix"],float) for n in rig["nodes"]}
    foot_observations=json.loads((RESEARCH/"男性101原靴分区与源锚点.json").read_text(encoding="utf-8"))
    components={};max_errors=[]
    for key,id in PARTS.items():
        mesh=next(m for m in skins["meshes"] if m["sourceSpriteId"]==id)
        field=fields[id];W,H=float(field["spriteRect"]["width"]),float(field["spriteRect"]["height"])
        ppu=float(mesh["pixelsPerUnit"]);pivot=mesh["spritePivotFraction"]
        if ppu!=100 or field["decodedSettings"]["packed"] or field["decodedSettings"]["packingRotation"]["value"]!=0:raise ValueError("Unexpected source canvas/packing convention")
        rx,ry,rw,rh=mesh["uvConvention"]["sourceTextureRectPixels"]
        sx,ox,sy,oy=mesh["uvConvention"]["sourceUvTransform"]
        # Top-left crop UV -> top-left full-canvas physical source pixels.
        A=np.array([[ppu*rw/sx,0,ppu*(rx-ox)/sx+W*pivot[0]],
                    [0,ppu*rh/sy,H*(1-pivot[1])-ppu*(ry+rh-oy)/sy]])
        normalized=A/np.array([[W],[H]])
        target=A*K
        crop_rect=[A[0,2],A[1,2],A[0,2]+A[0,0],A[1,2]+A[1,1]]
        source_image=ROOT/field["fullTexturePng"]
        image=Image.open(source_image).convert("RGBA")
        if image.size!=(int(W),int(H)):raise ValueError("Full source texture does not match logical Sprite canvas")
        soft,core=masks(image)
        anchors=[]
        def anchor_local(name,point,kind,path=None):
            full=np.array([point[0]*ppu+W*pivot[0],H*(1-pivot[1])-point[1]*ppu])
            # Derive the matching source crop UV by the serialized source
            # vertex-UV relation, not by integer crop PNG width/height.
            crop=np.array([(point[0]*sx+ox-rx)/rw,1-(point[1]*sy+oy-ry)/rh])
            mapped=A@np.array([*crop,1.])
            if not np.allclose(mapped,full,atol=1e-9):raise ValueError("Anchor conversion mismatch")
            anchors.append({"name":name,"kind":kind,"sourceRigPath":path,"sourceSpriteLocal":point[:3].tolist(),
                "sourceCropUV":crop.tolist(),"sourceCanvasPx":full.tolist(),"sourceCanvasNormalized":(full/np.array([W,H])).tolist(),"templatePx":(full*K).tolist()})
        for bone,bp in zip(mesh["skinning"]["spriteBones"],mesh["skinning"]["bindposeMatrices"]):
            point=np.linalg.inv(np.array(bp,float))@np.array([0.,0.,0.,1.]);point/=point[3]
            anchor_local(role(bone["name"]),point,"source_skin_bindpose_pivot",bone["rigPath"])
        if key=="body":
            inv=np.linalg.inv(np.array(mesh["rendererToRigRoot"],float))
            suffixes={"head_neck":"/shenti1/tou","near_shoulder_mount":"/youbi/youbi/youbi1","far_shoulder_mount":"/zuobi/zuobi/zuobi1"}
            for name,suffix in suffixes.items():
                path=next(p for p in world if p.endswith(suffix))
                anchor_local(name,inv@world[path]@np.array([0.,0.,0.,1.]),"external_source_rig_mount",path)
        if key=="leg":
            for observation in foot_observations["anchors"]:
                if observation["kind"]=="source_rig_bindpose_pivot":continue
                uv=np.array([*observation["source_uv"],1.]);full=A@uv
                anchors.append({"name":observation["name"],"kind":"source_texture_observation_not_joint","definition":observation["definition"],
                    "sourceCropUV":observation["source_uv"],"sourceCanvasPx":full.tolist(),"sourceCanvasNormalized":(full/np.array([W,H])).tolist(),"templatePx":(full*K).tolist()})
        # Verify every original mesh vertex using an independent local->canvas path.
        error=0.
        for vertex,uv in zip(mesh["vertices"],mesh["uvConvention"]["uvCropLowerLeft"]):
            full=np.array([vertex[0]*ppu+W*pivot[0],H*(1-pivot[1])-vertex[1]*ppu])
            mapped=A@np.array([uv[0],1-uv[1],1.])
            error=max(error,float(np.abs(full-mapped).max()))
        if error>1e-9:raise ValueError("Fullcanvas map does not reproduce source vertices")
        max_errors.append(error)
        attachment="shoulder" if key.startswith("sleeve") else ("hip" if key=="leg" else "head_neck")
        pivot_anchor=next(a for a in anchors if a["name"]==attachment)
        cutoff=float(pivot_anchor["sourceCanvasPx"][1])
        alpha=np.array(core)>0
        above=alpha & (np.arange(image.height)[:,None]+.5<cutoff)
        ys,xs=np.nonzero(above)
        bbox=[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)] if len(xs) else None
        overlap={"attachmentAnchor":attachment,"definition":"Observed source core-alpha above the attachment pivot in this Sprite's undeformed canvas. This is connection allowance, not a claim that all marked pixels are hidden in every pose.",
            "sourceCanvasCutoffY":cutoff,"sourceAlphaIntersectionBBox":bbox,"templateIntersectionBBox":scaled_bbox(bbox),"sourceCorePixelCount":int(len(xs)),
            "artDirection":"Keep a rounded sleeve cap and connected upper-leg/collar allowance. Upper-sleeve and leg-root coverage must continue under adjacent clothing; do not cut the image at the pivot or paint grid/labels into the final art."}
        record={"sourceSpriteId":id,"logicalPart":NAMES[key],"sourceCanvasSize":[int(W),int(H)],"templateCanvasSize":[int(W*K),int(H*K)],
            "sourceFullTexture":source_image.relative_to(ROOT).as_posix(),"sourceFullTextureSha256":sha(source_image),"sourceSpritePivot":pivot,
            "sourceTextureRectPixels":[rx,ry,rw,rh],"sourceTextureRectOffset":field["rdFields"]["textureRectOffset"]["value"],
            "sourceCropRectTopLeftInCanvas":crop_rect,"sourceCropPngIntegerSize":list(Image.open(SOURCE/"textures"/f"sprite_{id}.png").size),
            "sourcePPU":ppu,"cropUvToFullCanvasNormalized":normalized.tolist(),"cropUvToTemplatePixels":target.tolist(),
            "softAlphaBBoxSourceCanvas":list(soft.getbbox()),"coreAlphaBBoxSourceCanvas":list(core.getbbox()),"coreAlphaThreshold":128,
            "visibleContour":{"sourceCoreBoundarySegments":contour_segments(core),"sourceSoftBoundarySegments":contour_segments(soft),
                "coordinateConvention":"Pixel outer edges in the logical full source canvas; multiply by24 only for this guide. Final art redraws continuous native-HD edges.",
                "templateCoreBBox":scaled_bbox(core.getbbox()),"visibility":"Texture alpha defines the drawn domain; final rendered visibility also intersects source triangles and neighboring part occlusion. Guide masks are not production alpha."},
            "anchors":anchors,"overlapRegion":overlap,"sourceVertexCanvasMaxError":error,
            "finalArtFilename":key+".png","guidesOnlyNotProductionArt":True}
        if key=="leg":
            instep=next(a for a in anchors if a["name"]=="instep")["sourceCanvasPx"]
            vertex_canvas=[[float(v[0]*ppu+W*pivot[0]),float(H*(1-pivot[1])-v[1]*ppu)] for v in mesh["vertices"]]
            record["sourceFootMeshTriangles"]=[[vertex_canvas[int(i)] for i in tri] for tri in mesh["triangles"] if any(vertex_canvas[int(i)][1]>=instep[1]-1.0 for i in tri)]
            record["bootShapeAllowance"]="Foot alpha may be redesigned for a rounded instep/toe and appropriate thickness within source triangle support, instead of copying pixel stairs. Do not fill the entire transparent mesh envelope or flatten the shoe into a wedge."
        record["guideImages"]=create_guides(key,record,image,anchors,overlap)
        components[key]=record
    overview=create_source_overview(components)
    report={"schemaVersion":1,"status":"Mathematical drawing guides complete; native HD art and assembled visual verification pending",
        "templateOnly":True,"sourcePPU":100,"densityMultiplier":K,"targetPPU":100*K,
        "coordinateConvention":"Top-left full logical Sprite canvas, X right/Y down. Crop UV comes from source fractional TextureRect; all matrices are row-major2x3 applied to column [u,v,1].",
        "source":skins["source"],"components":components,"sourceOriginalFullCanvasOverview":overview,
        "rules":["Never use a guide PNG or enlarged source alpha as production art.","Render native high-resolution transparent ink art into each complete logical Sprite canvas.","Do not align to a tight PNG crop center or multiply integer crop pixels by24.","All four parts share sourcePPU100 -> nominal drawingPPU2400; geometry scale is not inferred from opaque art height.","Bone pivots may be transparent; contour observations and underlap allowance have separate meanings.","The final fullcanvas art keeps these normalized anchors. Any actual exported image dimensions must be recorded and converted from full-canvas normalized coordinates, not assumed from the requested size."],
        "validation":{"components":len(components),"maxSourceVertexCanvasError":max(max_errors),"allPivotsRoundTrip":True,"oldRuntimeAndArtModified":False},
        "inputHashes":{"rig.json":sha(SOURCE/"rig.json"),"skins.json":sha(SOURCE/"skins.json"),"sourceRectFields":sha(RESEARCH/"原Sprite裁剪偏移与完整纹理字段.json")}}
    path=OUTPUT/"fullcanvas_template_anchors.json"
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    (DOC/"完整画布模板与像素锚点.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"template":str(path),"guideImages":8,"validation":report["validation"]},ensure_ascii=False))

if __name__=="__main__":main()
