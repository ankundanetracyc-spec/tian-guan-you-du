"""Read-only source-model analysis. Writes evidence only beside this script."""
from pathlib import Path
import json
import hashlib
import numpy as np
import cv2
from PIL import Image
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

DEST=Path(__file__).resolve().parent
ROOT=DEST.parents[2]
SOURCE=ROOT/"tools/action_battle_test/output/guigu_source_reference_v2/male101"
SKINS=json.loads((SOURCE/"skins.json").read_text(encoding="utf-8"))
FRAMES=json.loads((SOURCE/"source_reference_vertex_frames.json").read_text(encoding="utf-8"))
ANCHORS=json.loads((ROOT/"docs/美术规范/鬼谷原作网格与素材重构_20261104/源模型局部锚点.json").read_text(encoding="utf-8"))["sprites"]
FONT=FontProperties(fname="C:/Windows/Fonts/msyh.ttc")
matplotlib.rcParams["font.family"]=FONT.get_name()
matplotlib.rcParams["axes.unicode_minus"]=False
COLORS=["#df7195","#797dd6","#9faeeb","#e6bd55","#8bd2c7","#ef8961","#66b37a"]
source_meshes={str(m["sourceSpriteId"]):m for m in SKINS["meshes"] if m["category"]=="character"}

def image(sprite):return Image.open(SOURCE/f"textures/sprite_{sprite}.png").convert("RGBA")
def measure(sprite):
    m=source_meshes[sprite];u=m["uvConvention"]
    width,height=u["sourceTextureSizePixels"];x,y,w,h=u["sourceTextureRectPixels"]
    sx,ox,sy,oy=u["sourceUvTransform"]
    # This source's UV transform is PPU plus the central canvas offset.
    top=height-(y+h)
    pivot_local=np.array([0.,0.])
    pivot_uv=[(ox-x)/w,1-(oy-y)/h]
    return dict(sprite=int(sprite),sprite_rect=m["spriteRectPixels"],sprite_pivot_fraction=m["spritePivotFraction"],ppu=m["pixelsPerUnit"],full_texture_size=[width,height],trim_texture_rect_y_up=[x,y,w,h],trim_top_left_in_full_canvas=[x,top],decoded_crop_png_size=list(image(sprite).size),sprite_origin_in_crop_uv=pivot_uv,alpha_bbox_crop_pixels=ANCHORS[sprite]["alpha_bbox_pixels"],renderer_depth=m["rendererToRigRoot"][2][3],sorting_layer=m["sortingLayerId"],sorting_order=m["sortingOrder"],uv_transform=[sx,ox,sy,oy])

all_measurements={sprite:measure(sprite) for sprite in source_meshes}
fig,axes=plt.subplots(2,3,figsize=(13,10),dpi=160)
for ax,sprite in zip(axes.flat,["79543","84807","87557","85649","65806","87679"]):
    data=all_measurements[sprite];m=source_meshes[sprite];W,H=data["full_texture_size"];x,top=data["trim_top_left_in_full_canvas"];_,_,w,h=data["trim_texture_rect_y_up"]
    ax.set_facecolor("#e9ecea")
    full_path=DEST/f"source_full_textures/sprite_{sprite}_full_texture.png"
    if full_path.exists():ax.imshow(Image.open(full_path).convert("RGBA"),extent=[0,W,H,0],interpolation="nearest")
    else:ax.imshow(image(sprite),extent=[x,x+w,top+h,top],interpolation="nearest")
    ax.add_patch(plt.Rectangle((0,0),W,H,fill=False,color="#60686d",lw=1.5,label="Sprite完整画布"))
    ax.add_patch(plt.Rectangle((x,top),w,h,fill=False,color="#10a8af",lw=1.4,label="trim连续范围"))
    ax.scatter([W/2],[H/2],marker="x",s=75,c="#ef8b2d",lw=2,label="Sprite pivot")
    x0,y0,x1,y1=data["alpha_bbox_crop_pixels"];cw,ch=data["decoded_crop_png_size"]
    ax.add_patch(plt.Rectangle((x+x0/cw*w,top+y0/ch*h),(x1-x0)/cw*w,(y1-y0)/ch*h,fill=False,color="#b14db6",lw=1,label="非零Alpha包围框"))
    names={"79543":["shoulder","elbow","wrist"],"84807":["shoulder","elbow","wrist"],"87557":["hip","knee","ankle"],"85649":["cloth_root","near_shoulder_mount","head_neck"],"65806":["head_neck"],"87679":["hair_mount","hair_bone_1","hair_bone_5"]}[sprite]
    for a in ANCHORS[sprite]["anchors"]:
        if a["name"] not in names:continue
        u,v=a["source_uv"];px,py=x+u*w,top+v*h
        ax.scatter([px],[py],c="#e84242",s=20,zorder=5)
        ax.annotate(a["name"],(px,py),xytext=(3,3),textcoords="offset points",fontsize=6,color="#b22222")
    ax.set_xlim(-W*.05,W*1.05);ax.set_ylim(H*1.05,-H*.05);ax.set_aspect("equal")
    ax.set_title(f"{sprite}：画布{W}×{H} / 导出裁图{cw}×{ch}",fontsize=10)
    ax.tick_params(labelsize=6)
axes[0,0].legend(loc="lower left",fontsize=6)
fig.suptitle("同一素材的四种坐标：完整Sprite画布、trim范围、有效Alpha、骨关节",fontsize=14)
fig.tight_layout();fig.savefig(DEST/"01_四种坐标不可混用.png");plt.close(fig)

fig,axes=plt.subplots(1,3,figsize=(13,8),dpi=170)
for ax,sprite in zip(axes,["84807","79543","85649"]):
    im=image(sprite);m=source_meshes[sprite]
    ax.set_facecolor("#58656b");ax.imshow(im,interpolation="nearest")
    uv=np.asarray(m["uvConvention"]["uvCropLowerLeft"]);uv[:,1]=1-uv[:,1];uv*=im.size
    for tri in m["triangles"]:
        q=uv[tri+tri[:1]];ax.plot(q[:,0],q[:,1],c="white",lw=.45,alpha=.55)
    for i,b in enumerate(m["skinning"]["spriteBones"]):
        a=next((v for v in ANCHORS[sprite]["anchors"] if v.get("boneIndex")==i),None)
        if a is None:continue
        q=np.array(a["source_uv"])*im.size;ax.scatter(*q,s=25,label=b["name"])
        ax.annotate(b["name"],q,xytext=(3,3),textcoords="offset points",fontsize=7,color="#fff1b0")
    ax.set_title(f"{sprite} / {im.width}×{im.height} / 原网格+骨pivot",fontsize=10)
    ax.set_xticks(range(0,im.width+1,4));ax.tick_params(labelsize=6);ax.legend(fontsize=6,loc="lower left")
fig.suptitle("袖根不是图像边界的几何顶点；手／袖在同一Sprite内以权重连接",fontsize=13)
fig.tight_layout();fig.savefig(DEST/"02_原袖根_关节_衣身骨链.png");plt.close(fig)

WIDTH,HEIGHT,SCALE=640,640,4.0
ORIGIN=np.array([320.,530.])
def raster_part(index,clip,phase,cap_only=False,cap_fraction=.25):
    record=FRAMES["meshes"][index];im=np.array(image(str(record["spriteId"])))
    if cap_only:
        im=im.copy();im[round(im.shape[0]*cap_fraction):,:,3]=0
    verts=np.array(FRAMES["clips"][clip]["frames"][phase][index])*SCALE+ORIGIN
    uv=np.array(record["uv"])*[im.shape[1],im.shape[0]]
    canvas=np.zeros((HEIGHT,WIDTH,4),np.uint8)
    for tri in record["triangles"]:
        src=uv[tri].astype(np.float32);dst=verts[tri].astype(np.float32)
        transform=cv2.getAffineTransform(src,dst)
        warped=cv2.warpAffine(im,transform,(WIDTH,HEIGHT),flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT)
        mask=np.zeros((HEIGHT,WIDTH),np.uint8);cv2.fillConvexPoly(mask,np.round(dst).astype(np.int32),255)
        alpha=warped[:,:,3]*(mask/255)
        replace=alpha>canvas[:,:,3]
        canvas[replace,:3]=warped[replace,:3];canvas[:,:,3]=np.maximum(canvas[:,:,3],alpha).astype(np.uint8)
    return canvas

assembly_stats={}
for clip,phase,title in [("Idle",0,"静站 phase0"),("Run_New1",36,"前跑 phase0.25")]:
    parts=[raster_part(i,clip,phase) for i in range(len(FRAMES["meshes"]))]
    assembled=np.full((HEIGHT,WIDTH,3),[80,92,99],np.float32)
    colored=assembled.copy()
    for index,part in enumerate(parts):
        a=part[:,:,3:]/255.;assembled=assembled*(1-a)+part[:,:,:3]*a
        color=np.array(matplotlib.colors.to_rgb(COLORS[index]))*255
        colored=colored*(1-a*.8)+color*a*.8
    fig,axes=plt.subplots(1,3,figsize=(14,6),dpi=150)
    axes[0].imshow(assembled.astype(np.uint8));axes[0].set_title("源图+源网格：已定位源曲线重建")
    axes[1].imshow(colored.astype(np.uint8));axes[1].set_title("部件着色：保持同相位／同深度顺序")
    for index,record in enumerate(FRAMES["meshes"]):
        verts=np.array(FRAMES["clips"][clip]["frames"][phase][index]);axes[2].scatter(verts[:,0],verts[:,1],s=4,c=COLORS[index],label=f"{record['spriteId']} {record['category']}")
    axes[2].invert_yaxis();axes[2].set_aspect("equal");axes[2].legend(fontsize=6);axes[2].set_title("相同root坐标：源顶点覆盖域")
    for ax in axes[:2]:ax.set_xticks([]);ax.set_yticks([])
    fig.suptitle(title+" / 不含原Animator状态机和原DynamicBone求解",fontsize=12)
    fig.tight_layout();fig.savefig(DEST/("03_静站真实部件叠放.png" if clip=="Idle" else "04_跑动真实部件叠放.png"));plt.close(fig)
    ids=[str(m["spriteId"]) for m in FRAMES["meshes"]];body_index=ids.index("85649");face_index=ids.index("65806");hair_index=ids.index("87679")
    body=parts[body_index][:,:,3]>=179;face=parts[face_index][:,:,3]>=179;hair=parts[hair_index][:,:,3]>=179
    stat={"face_opaque_pixels":int(face.sum()),"face_hair_overlap_opaque_pixels":int((face&hair).sum()),"face_hair_overlap_fraction":float((face&hair).sum()/max(1,face.sum())),"sleeve_cap_body_overlap":{},"leg_root_body_overlap":{}}
    for id in ["79543","84807"]:
        index=ids.index(id);cap=raster_part(index,clip,phase,True)[:,:,3]>=179
        stat["sleeve_cap_body_overlap"][id]={"cap_opaque_pixels":int(cap.sum()),"body_overlap_pixels":int((cap&body).sum()),"fraction":float((cap&body).sum()/max(1,cap.sum()))}
    for index,record in enumerate(FRAMES["meshes"]):
        if record["category"]!="leg":continue
        cap=raster_part(index,clip,phase,True,.18)[:,:,3]>=179
        limb="youtui" if "/youtui/" in record["rendererPath"] else "zuotui"
        bone_index=next(i for i,path in enumerate(FRAMES["bonePaths"]) if f"/Foot/{limb}/" in path and path.endswith("/tui1"))
        hip=np.array(FRAMES["clips"][clip]["boneFrames"][phase][bone_index])*SCALE+ORIGIN
        hx,hy=np.round(hip).astype(int)
        stat["leg_root_body_overlap"][limb]={"root_opaque_pixels":int(cap.sum()),"body_overlap_pixels":int((cap&body).sum()),"fraction":float((cap&body).sum()/max(1,cap.sum())),"hip_pivot_inside_body_opaque_alpha":bool(body[hy,hx]),"root_region_definition":"source crop v<=0.18, alpha>=0.7"}
    assembly_stats[clip]=stat
    fig,axes=plt.subplots(1,3,figsize=(13,6),dpi=160)
    axes[0].imshow(assembled.astype(np.uint8));axes[0].set_title("原完整画面：腿根没有显露断口")
    isolated=np.full((HEIGHT,WIDTH,3),[80,92,99],np.float32)
    cap_view=isolated.copy()
    for index,record in enumerate(FRAMES["meshes"]):
        if record["category"]!="leg":continue
        part=parts[index];a=part[:,:,3:]/255.;isolated=isolated*(1-a)+part[:,:,:3]*a
        cap=raster_part(index,clip,phase,True,.18);a=cap[:,:,3:]/255.;cap_view=cap_view*(1-a)+np.array(matplotlib.colors.to_rgb(COLORS[index]))*255*a
    axes[1].imshow(isolated.astype(np.uint8));axes[1].imshow(parts[body_index],alpha=.25)
    axes[1].set_title("只显原双腿，衣身淡显：根部插在袍内")
    axes[2].imshow(cap_view.astype(np.uint8));axes[2].contour(body.astype(float),levels=[.5],colors=["#ffd46a"],linewidths=.7)
    axes[2].set_title("腿图上18%有效Alpha与衣身覆盖范围")
    for limb in ["zuotui","youtui"]:
        bone_index=next(i for i,path in enumerate(FRAMES["bonePaths"]) if f"/Foot/{limb}/" in path and path.endswith("/tui1"))
        hip=np.array(FRAMES["clips"][clip]["boneFrames"][phase][bone_index])*SCALE+ORIGIN
        for ax in axes[1:]:ax.plot(*hip,"rx",ms=9,mew=2)
    for ax in axes:
        ax.set_xlim(130,490);ax.set_ylim(600,210);ax.set_xticks([]);ax.set_yticks([])
    fig.suptitle(title+" / 红叉=真实髋pivot；分离示意只用于研究，不改变源素材",fontsize=12)
    fig.tight_layout();fig.savefig(DEST/("05_静站腿根插接.png" if clip=="Idle" else "06_跑动腿根插接.png"));plt.close(fig)

files=["rig.json","skins.json","animations.json","source_reference_vertex_frames.json"]
report={"source":SKINS["source"],"source_input_hashes":{name:hashlib.sha256((SOURCE/name).read_bytes()).hexdigest() for name in files},"measurements":all_measurements,"assembly_overlap_samples":{"method":"Original crop alpha warped through the original triangles at fixed source phases; alpha>=.7 on a 4x diagnostic raster. Pixel areas depend on rasterization and are not engine runtime measurements.","samples":assembly_stats},"limits":["No original Animator/state machine or transparency-sort camera was executed.","Run clips exclude one unresolved path; source secondaryFrames are not used here.","Trim top-left is derived from UV fields. Explicit TextureRectOffset/atlasRectOffset/settingsRaw/m_Offset are separately recorded in the original serialized-fields evidence; matching values are not generalized to other assets."]}
(DEST/"原素材坐标与覆盖_只读测量.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps({"destination":str(DEST),"overlap":assembly_stats},ensure_ascii=False,indent=2))
