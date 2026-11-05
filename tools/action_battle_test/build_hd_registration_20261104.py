"""Register unchanged HD images to source pivots; manual targets need render review."""
from pathlib import Path
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
ORIGINAL = TOOLS / "output/guigu_source_reference_v2/male101/textures"
HD = TOOLS / "art/guigu_male101_ink_hd"
SOURCE = json.loads((ROOT / "docs/美术规范/鬼谷原作网格与素材重构_20261104/源模型局部锚点.json").read_text(encoding="utf-8"))["sprites"]
DEST = ROOT / "resources/appearance/guigu_male101/hd_registration_20261104.json"
THRESHOLD = .7

def rgba(sprite, hd=False): return Image.open((HD if hd else ORIGINAL) / f"sprite_{sprite}.png").convert("RGBA")
def bounds(sprite, hd=False):
    im=rgba(sprite,hd); a=np.asarray(im)[:,:,3]>=round(THRESHOLD*255)
    ys,xs=np.nonzero(a)
    return [int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)]
def row_span(sprite, y, hd=False):
    im=rgba(sprite,hd); a=np.asarray(im)[:,:,3]>=round(THRESHOLD*255)
    y=int(round(y)); chosen=min(range(im.height),key=lambda r:abs(r-y) if a[r].any() else 100000)
    xs=np.flatnonzero(a[chosen]);return [int(xs.min()),int(xs.max()+1),chosen]
def source_anchor(sprite,name): return next(a["source_uv"] for a in SOURCE[sprite]["anchors"] if a["name"]==name)
def anchor(name,uv,px): return {"name":name,"source_uv":[float(x) for x in uv],"target_px":[float(x) for x in px],"target_method":"initial manual anatomical registration; needs native-render review"}
def bbox_controls(sprite):
    old=bounds(sprite); new=bounds(sprite,True); w,h=rgba(sprite).size
    return [anchor(name,[sx/w,sy/h],[tx,ty]) for name,sx,sy,tx,ty in [
        ("bbox_tl",old[0],old[1],new[0],new[1]),("bbox_tr",old[2],old[1],new[2],new[1]),
        ("bbox_bl",old[0],old[3],new[0],new[3]),("bbox_br",old[2],old[3],new[2],new[3])]]

sprites={}
for sprite,targets,tip in [
    ("79543",[("shoulder",[350,340]),("elbow",[350,900]),("wrist",[285,1450])],([.55,.955],[277,1805])),
    ("84807",[("shoulder",[225,260]),("elbow",[390,900]),("wrist",[550,1370])],([.77,.9535],[594,1678]))]:
    size=rgba(sprite).size;controls=[]
    for name,px in targets:
        uv=source_anchor(sprite,name); point=anchor(name,uv,px)
        old=row_span(sprite,uv[1]*size[1]); new=row_span(sprite,px[1],True)
        point.update(source_width=old[1]-old[0],target_width=new[1]-new[0],source_width_sample_row=old[2],target_width_sample_row=new[2])
        controls.append(point)
    point=anchor("finger_tip",*tip)
    old=row_span(sprite,tip[0][1]*size[1]);new=row_span(sprite,tip[1][1],True)
    point.update(source_width=old[1]-old[0],target_width=new[1]-new[0],source_width_sample_row=old[2],target_width_sample_row=new[2])
    controls.append(point)
    sprites[sprite]={"mode":"axis","source_size":list(size),"target_size":list(rgba(sprite,True).size),"joint_blend":.16,"anchors":controls}

sprite="87557";size=rgba(sprite).size;controls=[]
for name,px in [("hip",[275,180]),("knee",[285,950]),("ankle",[275,1860])]:
    uv=source_anchor(sprite,name);controls.append(anchor(name,uv,px))
    old=row_span(sprite,uv[1]*size[1]);new=row_span(sprite,px[1],True)
    controls.extend([anchor(name+"_left",[old[0]/size[0],uv[1]],[new[0],px[1]]),anchor(name+"_right",[old[1]/size[0],uv[1]],[new[1],px[1]])])
controls += [anchor("sole_center",source_anchor(sprite,"sole_center"),[420,2050]),anchor("sole_left",[.208333,.970588],[170,2050]),anchor("sole_right",[.833333,.970588],[650,2050])]
# The original row through its ankle already includes its projecting boot.
# Treating that row's rightmost toe pixel as the new ankle's right edge flips
# the foot's mapping. Keep the actual pivot and the independent sole controls.
controls=[a for a in controls if a["name"]!="ankle_right"]
sole_span=row_span(sprite,2050,True)
for a in controls:
    if a["name"]=="sole_right":a["target_px"]=[sole_span[1]-1,2050]
sprites[sprite]={"mode":"triangles","source_size":list(size),"target_size":list(rgba(sprite,True).size),"anchors":controls}

sprite="65806";old=bounds(sprite);new=bounds(sprite,True);sw,sh=rgba(sprite).size
crown=source_anchor(sprite,"head_crown")
controls=[anchor("head_crown",crown,[569,126]),anchor("head_neck",source_anchor(sprite,"head_neck"),[508,1030]),anchor("face_horizontal_span",[crown[0]+(old[2]-old[0])/sw,crown[1]],[569+(new[2]-new[0]),126])]
sprites[sprite]={"mode":"affine","source_size":[sw,sh],"target_size":list(rgba(sprite,True).size),"anchors":controls,"source_alpha_bbox":old,"target_alpha_bbox":new,"note":"Crown and neck establish the height/shear. A measured alpha-width span establishes the transverse scale; no local face squashing."}

sprite="87679";width,height=rgba(sprite,True).size
controls=[]
for name,p in [("hair_crown",[.58,.027]),("hair_front_edge",[.86,.20]),("hair_bone_1",[.56,.16]),("hair_bone_2",[.35,.17]),("hair_mount",[.54,.30]),("hair_bone_3",[.43,.45]),("hair_bone_4",[.40,.63]),("hair_bone_5",[.40,.79]),("hair_tip",[.45,.98])]:
    controls.append(anchor(name,source_anchor(sprite,name),[p[0]*width,p[1]*height]))
hair_alpha=np.asarray(rgba(sprite,True))[:,:,3]
hair_mask=hair_alpha>=round(THRESHOLD*255)
for a in controls:
    if a["name"]=="hair_crown":
        row=int(np.nonzero(hair_mask)[0].min());xs=np.flatnonzero(hair_mask[row]);a["target_px"]=[float(np.average(xs,weights=hair_alpha[row,xs])),row]
    elif a["name"]=="hair_front_edge":
        top=hair_mask[:round(height*.3)];column=int(np.nonzero(top)[1].max());ys=np.flatnonzero(top[:,column]);a["target_px"]=[column,float(np.average(ys,weights=hair_alpha[ys,column]))]
    elif a["name"]=="hair_tip":
        row=int(np.nonzero(hair_mask)[0].max());xs=np.flatnonzero(hair_mask[row]);a["target_px"]=[float(np.average(xs,weights=hair_alpha[row,xs])),row]
for name in ["hair_mount","hair_bone_3","hair_bone_4","hair_bone_5","hair_tip"]:
    center=next(a for a in controls if a["name"]==name)
    y=center["target_px"][1];span=row_span(sprite,y,True);v=center["source_uv"][1]
    controls.extend([anchor(name+"_row_left",[0,v],[span[0],y]),anchor(name+"_row_right",[1,v],[span[1]-1,y])])
controls=[a for a in controls if a["name"] in ["hair_crown","hair_front_edge","hair_tip"]]
sprites[sprite]={"mode":"affine","source_size":list(rgba(sprite).size),"target_size":[width,height],"anchors":controls,"note":"Three visible contour anchors preserve strand direction; the earlier nine-point target overfit sheared the hair into transverse bands."}

sprite="85649"; controls=bbox_controls(sprite); old=bounds(sprite);new=bounds(sprite,True);w,h=rgba(sprite).size;tw,th=rgba(sprite,True).size
controls += [anchor("waist_left",[old[0]/w,.401],[new[0],th*.345]),anchor("waist_right",[old[2]/w,.401],[new[2],th*.345])]
sprites[sprite]={"mode":"triangles","source_size":[w,h],"target_size":[tw,th],"anchors":controls}

# Cover the whole original UV domain. Choosing unrelated extrapolation
# triangles outside a pivot-only convex hull produces visible discontinuities.
for sprite,entry in sprites.items():
    if entry["mode"]!="triangles":continue
    tw,th=entry["target_size"]
    entry["anchors"] += [anchor("canvas_tl",[0,0],[0,0]),anchor("canvas_tr",[1,0],[tw,0]),anchor("canvas_bl",[0,1],[0,th]),anchor("canvas_br",[1,1],[tw,th])]

output={"schemaVersion":1,"description":"Source skeleton/vertices stay unchanged; HD pixel registration is manually initialized from visual anatomy and alpha spans.","alpha_threshold":THRESHOLD,"review_status":"initial manual registration; native render review required","sprites":sprites}
DEST.parent.mkdir(parents=True,exist_ok=True)
DEST.write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps({"destination":str(DEST),"sprites":{k:{"mode":v["mode"],"target_size":v["target_size"],"anchors":len(v["anchors"])} for k,v in sprites.items()}},ensure_ascii=False))
