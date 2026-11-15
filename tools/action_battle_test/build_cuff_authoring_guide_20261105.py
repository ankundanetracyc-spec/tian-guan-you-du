"""Draw an engineering guide from explicit curves, not a production bitmap."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

SIZE=1254
ROOT=Path(__file__).parent/'art/ink_outfit_01_20261104/assets/template'
ROOT.mkdir(parents=True,exist_ok=True)

def bezier(a,b,c,d):
    out=[]
    for i in range(41):
        t=i/40
        out.append(tuple((1-t)**3*a[j]+3*(1-t)**2*t*b[j]+3*(1-t)*t*t*c[j]+t**3*d[j] for j in range(2)))
    return out

segments=[
 ((535,270),(535,190),(572,150),(620,150)),
 ((620,150),(675,150),(705,190),(705,270)),
 ((705,270),(690,390),(660,520),(650,600)),
 ((650,600),(650,616),(652,626),(654,644)),
 ((654,644),(661,712),(670,790),(676,853)),
 ((676,853),(610,878),(544,895),(478,907)),
 ((478,907),(495,840),(520,740),(545,634)),
 ((545,634),(549,622),(551,614),(550,600)),
 ((550,600),(548,500),(543,380),(535,270)),
]
outline=[]
for segment in segments: outline.extend(bezier(*segment))
im=Image.new('RGB',(SIZE,SIZE),(151,164,164))
d=ImageDraw.Draw(im)
d.polygon(outline,fill=(247,237,229))
d.line(bezier((478,907),(544,895),(610,878),(676,853)),fill=(127,153,155),width=5)
# A schematic hand location; the image generator must preserve the real hand reference.
hand=[(580,875),(638,864),(651,887),(655,917),(646,935),(642,992),(633,1002),(624,999),(615,1008),(605,1006),(597,996),(588,997),(582,986),(574,953),(574,908)]
d.polygon(hand,fill=(237,209,191))
im.save(ROOT/'shared_sleeve_rest_shape_v15.png')

annotated=im.copy()
ad=ImageDraw.Draw(annotated)
for name,p in [('S',(620,272)),('E',(600,610)),('W',(610,875))]:
    ad.ellipse((p[0]-5,p[1]-5,p[0]+5,p[1]+5),fill=(199,68,63))
    ad.text((p[0]+10,p[1]-8),name,fill=(91,54,42))
ad.line([(600,610),(610,875)],fill=(79,131,167),width=2)
ad.line([(478,907),(676,853)],fill=(79,131,167),width=2)
annotated.save(ROOT/'shared_sleeve_rest_anchors_v15.png')
record={'engineering_guide_only':True,'canvas':[SIZE,SIZE],'common_to_both_sides':True,
 'shoulder':[620,272],'elbow':[600,610],'wrist':[610,875],
 'cuff_lower_edge':[478,907],'cuff_upper_edge':[676,853],
 'cuff_width_px':205.2315765509403,'intended_upper_lower_clearance_ratio':[1,2],
 'upper_arm_taper':'170 at shoulder, down to100 at elbow',
 'forearm_flare':'100 at elbow, monotonically to205 at opening; no midarm bag',
 'no_painted_folds':True,
 'hand_scale_same_for_both_sides':True,
 'note':'Authored target, not measurement of an AI image. Actual output landmarks require reinspection; source motion rig coordinates remain separately referenced.'}
(ROOT/'shared_sleeve_authoring_v15.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(ROOT/'shared_sleeve_rest_shape_v15.png')
