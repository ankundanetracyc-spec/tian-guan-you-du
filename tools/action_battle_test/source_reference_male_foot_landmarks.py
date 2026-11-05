"""Document original male101 boot regions; no HD art or runtime is changed."""
from __future__ import annotations
import json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
MODEL=ROOT/"tools/action_battle_test/output/guigu_source_reference_v2/male101"
DOC=ROOT/"docs/美术规范/鬼谷局内素材拼接研究_20261104"

def main():
    image=Image.open(MODEL/"textures/sprite_87557.png").convert("RGBA")
    rgba=np.array(image);alpha=rgba[:,:,3];solid=alpha>=128
    previous=json.loads((ROOT/"docs/美术规范/鬼谷原作网格与素材重构_20261104/源模型局部锚点.json").read_text(encoding="utf-8"))
    pivots=[a for a in previous["sprites"]["87557"]["anchors"] if a["name"] in ["hip","knee","ankle"]]
    anchors=[{"name":a["name"],"kind":"source_rig_bindpose_pivot","source_pixels":a["source_pixels"],"source_uv":a["source_uv"],"definition":"inverse source bindpose pivot; actual source rig joint"} for a in pivots]
    def add(name,xy,definition):
        anchors.append({"name":name,"kind":"texture_observation_not_rig_joint","source_pixels":list(xy),"source_uv":[xy[0]/24,xy[1]/68],"definition":definition})
    row44=np.flatnonzero(solid[44]);cuff_x=float(np.average(row44+.5,weights=alpha[44,row44]))
    add("boot_cuff_top",(cuff_x,42.),"Top of observed gray/light binding band; band occupies original rows42..46")
    add("boot_cuff_bottom",(cuff_x,47.),"Bottom edge after gray binding row46; gold shaft starts at row47")
    add("heel_back",(5.,64.),"Core-alpha left edge in the heel/sole observation row64; not a separate heel bone")
    add("heel_sole",(5.,65.),"Core-alpha lower-left outer pixel corner of row64")
    add("instep",(12.5,60.),"Upper core-alpha boundary at original pixel column12; visible instep observation")
    add("ball_upper",(14.5,62.),"Upper core-alpha boundary at forefoot observation column14; not an anatomical metatarsal pivot")
    add("ball_sole",(14.5,65.),"Lower core-alpha boundary at observation column14")
    toe_y=np.flatnonzero(solid[:,20]);toe_center=float(np.average(toe_y+.5,weights=alpha[toe_y,20]))
    add("toe_tip",(21.,toe_center),"Right outer edge of rightmost core-alpha column20; alpha-weighted pixel-center Y")
    add("toe_sole",(21.,65.),"Core-alpha lower-right outer pixel corner of row64")
    add("sole_center",(13.,65.),"Midpoint of the visible core-alpha sole interval x5..21 at lower edge65")
    add("soft_sole_edge",(13.5,66.),"Faint nonzero-alpha fringe extends one more row to edge66; do not confuse it with core sole edge65")
    # Horizontal silhouette extent and thickness at explicitly defined columns,
    # not a claim about real anatomical dimensions or a new game parameter.
    foot=solid[60:65];ys,xs=np.nonzero(foot)
    length=int(xs.max()+1-xs.min())
    sections={}
    for column in [12,14,20]:
        rows=np.flatnonzero(solid[58:,column])+58
        sections[str(column)]={"upper":int(rows.min()),"lowerEdge":int(rows.max()+1),"thicknessPixels":int(rows.max()+1-rows.min()),"thicknessOverObservedFootExtent":(rows.max()+1-rows.min())/length}
    report={"subject":"Original male101 Sprite87557 trouser + binding + boot + foot, shared by both leg instances",
        "croppedPngSize":[24,68],"spriteDesignCanvas":[50,80],"spritePivot":[.5,.5],
        "coordinateConvention":"Cropped PNG top-left, X right/Y down. UV=original cropped pixels/[24,68]. These are not HD target anchors or Sprite design-canvas coordinates.",
        "alphaCoreThreshold":128,"regions":{"trouser":"opaque dark trouser above gray band; through row41","grayBindingRowsInclusive":[42,46],"goldBootBeginsRow":47,"coreSoleLowerEdge":65,"nonzeroSoftAlphaLowerEdge":66},
        "anchors":anchors,"archUnderside":{"separateRigBone":False,"distinctVisibleConcavityConfirmed":False,"note":"The tiny source underside is effectively a straight core-alpha sole edge; no precise concave arch can be inferred. Do not invent an arch pivot from the word arch."},
        "measuredProportions":{"definition":"Core alpha>=128, observed foot ROI rows60..64; these are source raster proportions, not gameplay dimensions","observedFootHorizontalExtent":[int(xs.min()),int(xs.max()+1)],"observedFootExtentPixels":length,"grayBindingToSoleHeightPixels":65-42,"goldBootToSoleHeightPixels":65-47,"sectionsByOriginalColumn":sections,"darkSoleBorderApproxPixels":1,"darkSoleBorderOverObservedFootExtent":1/length},
        "sourceMeshPadding":{"note":"Several original foot mesh vertices are outside visible alpha; source mesh envelope must not be painted fully opaque","examplesCropPixels":[[23.072,65.038],[20.704,60.118],[15.311,59.300]]},
        "limitations":"Only hip/knee/ankle entries are source rig pivots. Other names label observed texture regions and raster edges; the original24x68 raster does not establish exact anatomical heel/ball/arch joints. No runtime, source PNG, HD PNG or registration was modified."}
    DOC.mkdir(parents=True,exist_ok=True)
    (DOC/"男性101原靴分区与源锚点.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    sheet=Image.new("RGB",(1160,735),"#f3efe4");draw=ImageDraw.Draw(sheet)
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",19);small=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",15)
    draw.text((20,15),"男性101原裤腿与靴子：原PNG24×68，只有髋／膝／踝三骨；其余是纹理分区",font=font,fill="#263339")
    enlarged=image.resize((192,544),Image.Resampling.NEAREST);sheet.paste(enlarged,(40,78),enlarged)
    foot=image.crop((0,40,24,68)).resize((384,448),Image.Resampling.NEAREST);sheet.paste(foot,(340,78),foot)
    for y in range(29):draw.line((340,78+y*16,724,78+y*16),fill="#c3c7c0",width=1)
    for x in range(25):draw.line((340+x*16,78,340+x*16,526),fill="#c3c7c0",width=1)
    colors={"hip":"#df5450","knee":"#3a9d79","ankle":"#287ebc"}
    for anchor in anchors:
        name=anchor["name"];x,y=anchor["source_pixels"]
        if name in colors:
            px,py=40+x*8,78+y*8;draw.ellipse((px-4,py-4,px+4,py+4),fill=colors[name]);draw.text((240,py-10),name,font=small,fill=colors[name])
        if name in ["boot_cuff_top","boot_cuff_bottom","ankle","heel_sole","instep","ball_upper","ball_sole","toe_tip"]:
            px,py=340+x*16,78+(y-40)*16;draw.ellipse((px-3,py-3,px+3,py+3),fill=colors.get(name,"#dc8e39"))
    draw.text((770,80),"原素材分区",font=font,fill="#263339")
    for i,text in enumerate(["深色裤管：髋→膝，袍下真实可露", "灰亮束口：原42–46行", "金色靴筒：原47行起", "踝：真实tui3绑定pivot", "脚背：第12列上界60", "前掌：第14列62–65", "鞋尖：第20列63–65", "核心鞋底：下界65", "软Alpha边：还延至66", "没有可确认的独立凹足弓"]):
        draw.text((770,118+i*30),text,font=small,fill="#263339")
    draw.text((340,553),"足部水平可见范围：x5–21，共16源像素",font=font,fill="#263339")
    draw.text((340,585),"脚背截面厚5px；前掌厚3px；鞋尖厚2px",font=small,fill="#263339")
    draw.text((20,655),"蓝／红／绿点是原rig骨pivot；橙点仅描述纹理区，不是新增骨或精密解剖点。",font=font,fill="#263339")
    draw.text((20,689),"整数放大与像素网格仅用于研究；原PNG未改，不是新高清成品。源网格还包含透明形变余量。",font=small,fill="#263339")
    sheet.save(DOC/"男性101原裤腿靴子分区与足部细图.png")
    print(json.dumps({"output":str(DOC),"anchors":len(anchors),"sourceFootExtent":length,"runtimeModified":False},ensure_ascii=False))

if __name__=="__main__":main()
