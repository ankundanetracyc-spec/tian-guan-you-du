"""Plot source-exact pivots and manually initialized HD targets without changing art."""
from pathlib import Path
import json
from PIL import Image
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties

ROOT=Path(__file__).resolve().parents[2]
TOOLS=Path(__file__).resolve().parent
DATA=json.loads((ROOT/"resources/appearance/guigu_male101/hd_registration_20261104.json").read_text(encoding="utf-8"))
DEST=ROOT/"docs/美术规范/鬼谷原作网格与素材重构_20261104/注册证据图"
DEST.mkdir(parents=True,exist_ok=True)
FONT=FontProperties(fname="C:/Windows/Fonts/msyh.ttc")
matplotlib.rcParams["font.family"]=FONT.get_name()
for sprite,entry in DATA["sprites"].items():
    old=Image.open(TOOLS/f"output/guigu_source_reference_v2/male101/textures/sprite_{sprite}.png").convert("RGBA")
    new=Image.open(TOOLS/f"art/guigu_male101_ink_hd/sprite_{sprite}.png").convert("RGBA")
    fig,axes=plt.subplots(1,2,figsize=(10,8),dpi=160)
    for ax,im,title in [(axes[0],old,"源部件：真实骨pivot／轮廓UV"),(axes[1],new,"高清部件：人工标定目标像素")]:
        ax.set_facecolor("#68737a");ax.imshow(im,interpolation="nearest");ax.set_title(title,fontproperties=FONT,fontsize=11)
        ax.set_xlim(-im.width*.05,im.width*1.05);ax.set_ylim(im.height*1.03,-im.height*.03)
        ax.tick_params(labelsize=7)
    labels=[]
    for index,a in enumerate(entry["anchors"]):
        if a["name"].startswith("canvas_"):continue
        x,y=a["source_uv"];oldp=(x*old.width,y*old.height);newp=a["target_px"]
        color=plt.cm.tab10(index%10)
        for ax,p in [(axes[0],oldp),(axes[1],newp)]:
            ax.plot(*p,marker="o",color=color,ms=4)
            ax.annotate(str(index),p,xytext=(4,4),textcoords="offset points",color="white",fontsize=8,weight="bold")
        if "target_width" in a:
            half=a["target_width"]/2;axes[1].plot([newp[0]-half,newp[0]+half],[newp[1],newp[1]],c=color,lw=.7)
        labels.append(f"{index}: {a['name']} -> ({newp[0]:.1f}, {newp[1]:.1f})")
    fig.suptitle(f"{sprite} / {entry['mode']} / 高清目标是人工标定，仍以实际组装审图",fontproperties=FONT,fontsize=12)
    fig.text(.02,.025,"\n".join(labels),fontproperties=FONT,fontsize=7,va="bottom",bbox={"facecolor":"white","alpha":.8,"edgecolor":"none"})
    fig.subplots_adjust(bottom=.24,top=.90,wspace=.35)
    fig.savefig(DEST/f"sprite_{sprite}_源UV与高清像素注册.png")
    plt.close(fig)
print(str(DEST))
