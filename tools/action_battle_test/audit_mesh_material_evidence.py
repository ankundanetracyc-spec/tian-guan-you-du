"""Read-only evidence plots for source sprites and the actual current meshes."""
from pathlib import Path
import json
import math
import numpy as np
from PIL import Image
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

P = Path(__file__).resolve().parents[2]
T = Path(__file__).resolve().parent
D = P / "docs/美术规范/鬼谷原作网格与素材重构_20261104/证据图"
D.mkdir(parents=True, exist_ok=True)
CURRENT = json.loads((D / "玉虚实际网格与权重.json").read_text(encoding="utf-8"))
SOURCE = json.loads((T / "guigu_rig/skins.json").read_text(encoding="utf-8"))["meshes"]
FONT = FontProperties(fname="C:/Windows/Fonts/msyh.ttc")
matplotlib.rcParams["font.family"] = FONT.get_name()
matplotlib.rcParams["axes.unicode_minus"] = False
COLORS = ["#ffcd55", "#ec6971", "#63dfc0", "#61aaf5", "#ce83ee", "#f49a53", "#a3dc5e", "#e89fc7"]

def style(ax, title):
    ax.set_facecolor("#4d5960")
    ax.set_title(title, fontproperties=FONT, fontsize=11)
    ax.set_xticks([]); ax.set_yticks([])
    for spine in ax.spines.values(): spine.set_visible(False)

def raw_image(name): return Image.open(T / "art/yuxu_fullbody_v15" / name).convert("RGBA")
def source_image(mesh): return Image.open((T / "guigu_rig" / mesh["sourceImage"]).resolve()).convert("RGBA")
def mesh_by_name(name): return next(m for m in CURRENT["bind"]["meshes"] if m["name"] == name)
def source_by_id(number): return next(m for m in SOURCE if m["sourceSpriteId"] == number)

fig, axs = plt.subplots(2, 4, figsize=(12, 9), dpi=160)
items = [(raw_image("body.png").crop((275,310,665,1340)), "当前衣身：闭合钟形长袍"),
         (raw_image("arm_relaxed_v19.png").crop((360,345,715,630)), "当前近/远臂：共用一张弯袖"),
         (raw_image("leg.png").crop((325,660,520,1490)), "当前近/远腿：共用一张直腿"),
         (raw_image("cutout.png").crop((335,75,580,385)), "当前头颈：独立头部"),
         (source_image(source_by_id(79450)), "原作79450：单一连续衣身"),
         (source_image(source_by_id(89721)), "原作89721：近侧袖/手"),
         (source_image(source_by_id(78517)), "原作78517：远侧袖/手"),
         (source_image(source_by_id(61872)), "原作61872：裤腿/脚")]
for ax, (im, title) in zip(axs.flat, items):
    style(ax, title); ax.imshow(im, interpolation="nearest"); ax.set_aspect("equal")
fig.suptitle("实际素材对照：各图按画布铺开；未对齐真实人体比例", fontproperties=FONT, fontsize=14)
fig.tight_layout(); fig.savefig(D / "01_实际素材形状对照.png"); plt.close(fig)

def draw_current(ax, mesh_name, texture_name, title, crop):
    m = mesh_by_name(mesh_name)
    im = raw_image(texture_name)
    ax.imshow(im, interpolation="nearest")
    uv = np.array(m["uv"])
    triangles = m["triangles"] + [tri for c in m["children"] if c["name"] == "OverlappingRearSkirt" for tri in c["triangles"]]
    for triangle in triangles:
        p = uv[triangle + triangle[:1]]
        ax.plot(p[:,0],p[:,1],color="#ffffff",lw=.18,alpha=.25)
    w = np.array(m["weights"])
    dominant = w.argmax(axis=0)
    ax.scatter(uv[:,0],uv[:,1],s=5,c=[COLORS[i] for i in dominant],zorder=4)
    x0,y0,x1,y1 = crop
    ax.set_xlim(x0,x1); ax.set_ylim(y1,y0)
    style(ax,title)
    for i,key in enumerate(m["keys"]): ax.scatter([],[],c=COLORS[i],s=15,label=key)
    ax.legend(loc="lower left",fontsize=5,framealpha=.7)

def source_uv(m):
    im = source_image(m)
    u = np.array(m["uvConvention"]["uvCropLowerLeft"])
    return np.column_stack([u[:,0]*im.width,(1-u[:,1])*im.height])

def draw_source(ax, number, title):
    m = source_by_id(number); im = source_image(m)
    ax.imshow(im,interpolation="nearest")
    uv = source_uv(m)
    for tri in m["triangles"]:
        a = uv[tri + tri[:1]]; ax.plot(a[:,0],a[:,1],c="#ffffff",lw=.7,alpha=.8)
    dominant = [max(w,key=lambda x:x["weight"])["boneIndex"] for w in m["skinning"]["boneWeights"]]
    ax.scatter(uv[:,0],uv[:,1],s=22,c=[COLORS[i] for i in dominant],zorder=4)
    style(ax,title)
    for b in m["skinning"]["spriteBones"]: ax.scatter([],[],c=COLORS[b["index"]],s=20,label=b["name"])
    ax.legend(loc="lower left",fontsize=7,framealpha=.7)

fig,axs=plt.subplots(1,3,figsize=(12,9),dpi=180)
draw_current(axs[0],"robe_back","body.png","当前后片：规则网格＋独立后骨链",(270,305,670,1345))
draw_current(axs[1],"robe_front","body.png","当前前片：沿蓝襟切开＋独立前骨链",(270,305,670,1345))
draw_source(axs[2],79450,"原作衣身：21顶点，4骨连续变形")
fig.suptitle("真实UV、拓扑和主权重：颜色是最大权重骨；原作前后缘共享同一衣摆链",fontproperties=FONT,fontsize=13)
fig.tight_layout();fig.savefig(D/"02_衣身真实UV网格与主权重.png");plt.close(fig)

fig,axs=plt.subplots(2,2,figsize=(11,8),dpi=180)
draw_current(axs[0,0],"near_arm","arm_relaxed_v19.png","当前近臂：原上臂133px，前臂168px",(350,330,725,640))
draw_current(axs[1,0],"far_arm","arm_relaxed_v19.png","当前远臂：复制同一弯袖和权重",(350,330,725,640))
for ax in axs[:,0]:
    points=np.array([[425,435],[466,562],[609,473]])
    ax.plot(points[:,0],points[:,1],c="#ff4040",lw=2,marker="o",zorder=9)
    for tag,p in zip(["肩","肘","腕"],points):ax.text(*p,tag,fontproperties=FONT,color="#ffeccc",fontsize=9,zorder=10)
draw_source(axs[0,1],89721,"原作近臂：独立窄袖，13顶点")
draw_source(axs[1,1],78517,"原作远臂：不同袖形，14顶点")
fig.suptitle("实际袖子、关节和权重：总臂长相同不能保证肘位、袖容积和前臂比例相同",fontproperties=FONT,fontsize=13)
fig.tight_layout();fig.savefig(D/"03_袖子关节点与权重.png");plt.close(fig)

def length(a,b):return float(np.linalg.norm(np.array(a)-np.array(b)))
frames=json.loads((P/"output/guigu_cloth_readback_20261104/Run_New1_full_gait_samples.json").read_text())
ratios={}
for side in ["left","right"]:
    upper=[length(f[side]["shoulder"],f[side]["elbow"]) for f in frames]
    lower=[length(f[side]["elbow"],f[side]["wrist"]) for f in frames]
    ratios[side]={"mean_upper":float(np.mean(upper)),"mean_lower":float(np.mean(lower)),"lower_over_upper":float(np.mean(lower)/np.mean(upper))}
summary={"current_arm":{"upper_pixels":length([425,435],[466,562]),"lower_pixels":length([466,562],[609,473]),"lower_over_upper":length([466,562],[609,473])/length([425,435],[466,562]),"same_texture_and_rest_chain_both_sides":True},"source_arm_run_samples":ratios,"current_meshes":[{"name":m["name"],"vertices":len(m["vertices"]),"bone_count":len(m["keys"]),"keys":m["keys"]} for m in CURRENT["bind"]["meshes"]],"source_clothing":{"vertices":len(source_by_id(79450)["vertices"]),"triangles":len(source_by_id(79450)["triangles"]),"bones":[b["name"] for b in source_by_id(79450)["skinning"]["spriteBones"]]},"source_material_is_distinct_from_current":True}
(D/"素材与结构量化记录.json").write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(summary,ensure_ascii=False,indent=2))
