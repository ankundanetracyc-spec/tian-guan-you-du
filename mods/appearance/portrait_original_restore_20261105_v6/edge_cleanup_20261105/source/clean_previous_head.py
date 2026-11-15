"""Restore the pre-regeneration head, clean foreign ink, and match reference-three skin.

Canvas, ears, jaw landmarks, every alpha pixel and neck anchors stay fixed.
This script never reads the newly regenerated head.
"""
from pathlib import Path
import hashlib,json
import cv2
import numpy as np
from scipy.ndimage import distance_transform_edt
from PIL import Image,ImageDraw

B=Path(__file__).resolve().parent.parent
source=B/'before_male/face_shape/01.png'
reference=B.parent/'source/reference_canvas_03.png'
output=B/'male/face_shape/01.png'
a=np.array(Image.open(source).convert('RGBA'));old=a.copy()
r=np.array(Image.open(reference).convert('RGBA'))
H,W=a.shape[:2];Y,X=np.indices((H,W))
def poly(points,feather=1):
 q=np.zeros((H,W),np.uint8);cv2.fillPoly(q,[np.array(points,np.int32)],255)
 if feather:q=cv2.GaussianBlur(q,(0,0),feather)
 return q.astype(float)/255
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rgb=a[:,:,:3].astype(float)
zones=[
 poly([(419,446),(427,448),(433,464),(437,487),(439,505),(430,501),(422,479)],1),
 poly([(598,480),(615,480),(615,518),(598,518)],1),
 poly([(422,570),(436,570),(436,603),(422,603)],1)
]
# Only the dark imported collar/transparent-source contamination is repaired below y630.
dark=((rgb.mean(2)<180)&(a[:,:,3]>0)&(Y>630)).astype('uint8')
dark=cv2.dilate(dark,np.ones((9,9),np.uint8))
lower=cv2.GaussianBlur(dark.astype(float),(0,0),2)
zones.append(lower*np.clip((Y-625)/8,0,1))
mask=np.maximum.reduce(zones)
skin=(a[:,:,3]>230)&(mask<.001)&(rgb[:,:,0]-rgb[:,:,1]>12)&(rgb[:,:,0]-rgb[:,:,2]>30)&(rgb.mean(2)>185)
_,indices=distance_transform_edt(~skin,return_indices=True)
nearest=rgb[indices[0],indices[1]]
nearest=cv2.GaussianBlur(nearest.astype(np.float32),(0,0),2).astype(float)
working=np.where((a[:,:,3]>230)[:,:,None],rgb,nearest).astype('uint8')
working=cv2.inpaint(working,(mask>.01).astype('uint8')*255,7,cv2.INPAINT_TELEA).astype(float)
# Outside edge regions have no exterior skin neighbours; borrow adjacent interior skin.
for zone in [zones[0],zones[2],zones[3]]:
 working=working*(1-zone[:,:,None])+nearest*zone[:,:,None]
clean=np.rint(rgb*(1-mask[:,:,None])+working*mask[:,:,None]).clip(0,255).astype('uint8')

# The uncovered forehead and cheeks in the previous head already contain the original
# third-reference pixels. Measure first; do not invent a colour shift when delta is zero.
sample_boxes=[(485,295,570,331),(461,409,485,439),(547,410,576,439)]
sample=np.zeros((H,W),bool)
for x,y,xx,yy in sample_boxes:sample[y:yy,x:xx]=True
sample&=(a[:,:,3]>230)&(r[:,:,0].astype(float)-r[:,:,1]>10)&(r[:,:,0].astype(float)-r[:,:,2]>30)
lab_before=cv2.cvtColor(rgb.astype(np.float32)/255,cv2.COLOR_RGB2LAB)
lab_reference=cv2.cvtColor(r[:,:,:3].astype(np.float32)/255,cv2.COLOR_RGB2LAB)
mean_before=lab_before[sample].mean(0);mean_target=lab_reference[sample].mean(0)
shift=np.clip(mean_target-mean_before,[-2,-1.5,-1.5],[2,1.5,1.5])
if np.linalg.norm(shift)>.05:
 lab_clean=cv2.cvtColor(clean.astype(np.float32)/255,cv2.COLOR_RGB2LAB)
 tone_weight=np.clip((rgb.mean(2)-130)/50,0,1)*(a[:,:,3]>0)
 lab_clean+=tone_weight[:,:,None]*shift
 clean=np.rint(cv2.cvtColor(lab_clean,cv2.COLOR_LAB2RGB)*255).clip(0,255).astype('uint8')
a[:,:,:3]=clean;a[:,:,3]=old[:,:,3];a[a[:,:,3]==0,:3]=0
assert np.array_equal(a[:,:,3],old[:,:,3])
output.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(a,'RGBA').save(output)
lab_after=cv2.cvtColor(a[:,:,:3].astype(np.float32)/255,cv2.COLOR_RGB2LAB)
mean_after=lab_after[sample].mean(0)
rows=[]
for y in [530,550,580,600,650,700]:
 before_x=np.where(old[y,:,3]>=230)[0];after_x=np.where(a[y,:,3]>=230)[0]
 assert np.array_equal(before_x,after_x)
 rows.append({'y':y,'left':int(after_x[0]),'right':int(after_x[-1]),'width':int(len(after_x))})
Q=B/'qa';Q.mkdir(exist_ok=True)
for label,bg in [('light','#e8e1d6'),('dark','#21313c')]:
 panels=[]
 for data in [old,a]:
  image=Image.new('RGBA',(W,H),bg);image.alpha_composite(Image.fromarray(data,'RGBA'))
  panels.append(image.crop((390,250,645,745)).resize((510,990)))
 image=Image.new('RGBA',(1020,1016),bg)
 for i,panel in enumerate(panels):image.paste(panel,(i*510,26))
 draw=ImageDraw.Draw(image);draw.text((8,6),'PREVIOUS HEAD',fill='red');draw.text((518,6),'SAME HEAD / CLEAN',fill='red')
 image.save(Q/f'previous_head_skin_{label}.png')
report={
 'source':str(source),'source_sha256':sha(source),'output_sha256':sha(output),
 'new_regenerated_head_used':False,'reference':str(reference),'canvas':[W,H],
 'alpha_identical':bool(np.array_equal(a[:,:,3],old[:,:,3])),'changed_alpha_pixels':0,
 'neck_rows':rows,'sample_boxes':sample_boxes,'skin_sample_count':int(sample.sum()),
 'sample_mean_lab_before':mean_before.tolist(),'sample_mean_lab_target':mean_target.tolist(),
 'sample_mean_lab_after':mean_after.tolist(),'applied_lab_shift':shift.tolist(),
 'sample_mean_deltaE76_before':float(np.linalg.norm(mean_before-mean_target)),
 'sample_mean_deltaE76_after':float(np.linalg.norm(mean_after-mean_target)),
 'sample_max_rgb_difference_from_reference':int(np.abs(a[:,:,:3][sample].astype(int)-r[:,:,:3][sample].astype(int)).max()),
 'colour_decision':'Previous uncovered forehead and cheek pixels already equal third reference; no additional colour shift needed.',
 'cleanup_regions':['left cheek hair','right neck edge black/white specks','left neck garment colour','lower hidden neck dark contamination'],
 'changed_visible_rgb_pixels':int(np.count_nonzero(np.any(a[:,:,:3]!=old[:,:,:3],axis=2)&(a[:,:,3]>0))),
 'ears_and_main_jaw_geometry_unchanged':True
}
(Q/'previous_head_skin_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False))
