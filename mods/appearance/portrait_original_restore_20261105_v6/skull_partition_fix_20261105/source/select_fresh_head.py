"""Newly generated head RGB, exported against the fixed neck anchor contour."""
from pathlib import Path
from PIL import Image
import cv2,numpy as np,json,hashlib
from scipy.interpolate import PchipInterpolator
R=Path(__file__).resolve().parent.parent
source=R/'source/head_fresh_raw.png';oldpath=R/'before_male/face_shape/01.png'
native=np.array(Image.open(source).convert('RGBA'));old=np.array(Image.open(oldpath).convert('RGBA'))
assert native.shape==old.shape==(1536,1024,4)
out=native.copy();Y=np.arange(1536)[:,None];X=np.arange(1024)[None,:]
# Smooth neck geometry with fixed collar anchors. Do not reintroduce the former neck's jagged Alpha.
def native_half_edges(y):
 line=native[y,:,3].astype(float)/255;ids=np.where(line>=.5)[0];l=int(ids[0]);r=int(ids[-1])
 return l-1+(.5-line[l-1])/max(line[l]-line[l-1],1e-8),r+(line[r]-.5)/max(line[r]-line[r+1],1e-8)
left0,right0=native_half_edges(488)
control_y=[488,500,510,520,530,550,580,590,600,620,640,650,680,700,720,729,731]
left=[left0,429.2,429.45,429.45,429.45,429.45,429.45,432.45,440.45,455.45,471.45,479.45,499.45,512.45,525.45,531.45,531.45]
right=[right0,600.1,601.45,603.45,605.45,605.45,605.45,603.45,598.45,589.45,580.45,575.45,559.45,548.45,537.45,532.45,532.45]
ys=np.arange(488,732)
le=PchipInterpolator(control_y,left)(ys)
ri=PchipInterpolator(control_y,right)(ys)
coverage=np.clip((X-le[:,None])/.9+.5,0,1)*np.clip((ri[:,None]-X)/.9+.5,0,1)
coverage[-2:]*=np.array([.65,.15])[:,None]
out[488:732,:,3]=np.rint(coverage*255);out[732:,:,3]=0
# The generated neck is at most a few pixels narrower on one side. Extend its own interior skin at those anchors.
for y in range(488,732):
 valid=np.where(native[y,:,3]>230)[0]
 if not len(valid):valid=np.where(native[max(510,y-3),:,3]>230)[0]
 if not len(valid):continue
 for x in np.where((out[y,:,3]>0)&(native[y,:,3]<=230))[0]:
  nearest=valid[np.argmin(abs(valid-x))]
  out[y,x,:3]=native[y,nearest,:3]
out[out[:,:,3]<=2,3]=0;out[out[:,:,3]==0,:3]=0
p=R/'male/face_shape/01.png';Image.fromarray(out).save(p)
for y in [530,550,580]:
 xs=np.where(out[y,:,3]>=230)[0];assert (int(xs[0]),int(xs[-1]))==(430,605)
rows={}
for y in [172,175,210,280,330,370,520,530,550,580,600,620,650,680,700,720,729]:
 xs=np.where(out[y,:,3]>=230)[0];rows[y]=[int(xs[0]),int(xs[-1])] if len(xs) else None
ys,xs=np.where(out[:,:,3]>=230)
report={'mode':'fresh built-in image_gen render','generated_rgb_only':True,'old_portrait_rgb_copied':False,'canvas':[1024,1536],'resized_or_shifted':False,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'selected_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'opaque_bbox':[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())],'neck_anchor_rows_530_550_580_exact':True,'neck_contour':'smooth PCHIP anchored collar support; former y510-520 notch removed','neck_control_rows':rows,'native_rgb_changed_only_for_generated_skin_edge_extension_below_y488':True}
(R/'qa/fresh_head_selection.json').write_text(json.dumps(report,indent=2),encoding='utf8')
for bg,label in [('#e8e1d6','light'),('#273238','dark')]:
 a=Image.fromarray(out);b=Image.new('RGBA',a.size,bg);b.alpha_composite(a)
 b.crop((340,150,690,750)).resize((700,1200)).save(R/'qa'/f'fresh_head_selected_{label}.png')
print('Fresh generated head selected; smooth neck with fixed collar anchors; no old portrait RGB copied')
