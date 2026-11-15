"""Remove the old head's painted black rim and isolated skin debris in place.

No resampling or movement. Only the boundary receives subpixel alpha antialiasing;
the image interior, ear anatomy and the fixed neck at y520+ are not blurred.
"""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,cv2,json,hashlib
from scipy.ndimage import distance_transform_edt
R=Path(__file__).resolve().parent.parent
source=R/'source/restored_head_before_edge_cleanup.png'
destination=R/'male/face_shape/01.png'
a=np.array(Image.open(source).convert('RGBA'));out=a.copy()
H,W=a.shape[:2];Y,X=np.indices((H,W))
rgb=a[:,:,:3].astype(float);alpha=a[:,:,3].astype(float)
inside=alpha>=128
distance=distance_transform_edt(inside)
outside_distance=distance_transform_edt(~inside)
# Borrow the existing warm skin several pixels inside the object, never a neighbouring
# background or dark outline pixel. This changes just the ink rim, not the head surface.
core=(distance>=6)&(alpha>=250)
_,near_indices=distance_transform_edt(~core,return_indices=True)
near_rgb=rgb[near_indices[0],near_indices[1]]
scalp=(Y<350)
ear=(Y>=340)&(Y<445)
lower=(Y>=440)&(Y<520)
edge=np.maximum.reduce([
 np.clip((3.6-distance)/2.2,0,1)*scalp,
 np.clip((2.6-distance)/1.5,0,1)*ear*.7,
 np.clip((3.2-distance)/2.0,0,1)*lower
])*(alpha>0)
out[:,:,:3]=np.rint(rgb*(1-edge[:,:,None])+near_rgb*edge[:,:,None]).clip(0,255)
# Inpaint only the few detected dark specks. No whole-head smoothing is performed.
work=out[:,:,:3].copy()
grey=cv2.cvtColor(work,cv2.COLOR_RGB2GRAY).astype(float)
median=cv2.medianBlur(work,5)
median_grey=cv2.cvtColor(median,cv2.COLOR_RGB2GRAY).astype(float)
skin_zone=(Y<340)&(distance>7)
temple=((X>=421)&(X<=430)&(Y>=362)&(Y<=407))|((X>=592)&(X<=601)&(Y>=353)&(Y<=410))
spots=((median_grey-grey>12)&skin_zone)|((median_grey-grey>5)&temple)
spots&=(alpha>240)
spots=cv2.dilate(spots.astype('uint8'),np.ones((3,3),np.uint8))
spots[(Y>=520)|~inside]=0
if spots.any():
 fixed=cv2.inpaint(work,spots*255,3,cv2.INPAINT_TELEA)
 work[spots>0]=fixed[spots>0]
out[:,:,:3]=work
# Smooth alpha coverage only at its existing edge, keeping the 50% anatomical contour.
smooth_alpha=cv2.GaussianBlur(alpha.astype(np.float32),(0,0),.6)
band=(distance<=2.2)|(outside_distance<=1.5)
coverage=np.where(band,smooth_alpha,alpha)
coverage[Y>=520]=alpha[Y>=520]
out[:,:,3]=np.rint(coverage).clip(0,255)
out[520:]=a[520:]
# Newly antialiased coverage uses adjacent skin RGB, not the transparent PNG's black RGB.
new_edge=(out[:,:,3]>0)&((a[:,:,3]==0)|(distance<=1))&(Y<520)
out[:,:,:3][new_edge]=near_rgb[new_edge]
out[out[:,:,3]==0,:3]=0
assert np.array_equal(out[520:],a[520:])
before_contour=inside
after_contour=out[:,:,3]>=128
before_boundary=before_contour&~cv2.erode(before_contour.astype('uint8'),np.ones((3,3),np.uint8)).astype(bool)
after_boundary=after_contour&~cv2.erode(after_contour.astype('uint8'),np.ones((3,3),np.uint8)).astype(bool)
bdist=distance_transform_edt(~before_boundary);adist=distance_transform_edt(~after_boundary)
max_shift=max(float(bdist[after_boundary].max()),float(adist[before_boundary].max()))
assert max_shift<=1.01,('Anatomical contour moved beyond one source pixel',max_shift)
destination.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(out).save(destination)
Q=R/'qa';Q.mkdir(exist_ok=True)
boxes={'crown':(375,157,655,275),'ear_left':(355,330,440,460),'ear_right':(594,330,675,460),'jaw':(400,430,625,565)}
for bg,name in [('#e8e1d6','light'),('#273238','dark')]:
 for label,box in boxes.items():
  panels=[]
  for data in [a,out]:
   image=Image.new('RGBA',(W,H),bg);image.alpha_composite(Image.fromarray(data));panels.append(image.crop(box).resize(((box[2]-box[0])*4,(box[3]-box[1])*4),Image.Resampling.NEAREST))
  w,h=panels[0].size;image=Image.new('RGB',(2*w,h+28),bg);draw=ImageDraw.Draw(image)
  for i,panel in enumerate(panels):image.paste(panel,(i*w,28));draw.text((i*w+6,7),'BEFORE' if i==0 else 'AFTER - SAME GEOMETRY',fill='white' if name=='dark' else 'black')
  image.save(Q/f'head_outline_{label}_{name}_4x.png')
 for label,data in [('before',a),('after',out)]:
  image=Image.new('RGBA',(W,H),bg);image.alpha_composite(Image.fromarray(data));image.crop((340,135,690,745)).resize((700,1220)).save(Q/f'head_outline_{label}_{name}.png')
def box(mask):
 ys,xs=np.where(mask);return [int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
report={'source':str(source),'source_sha256':sha(source),'output':str(destination),'output_sha256':sha(destination),'canvas':[W,H],'no_resize_or_translation':True,'neck_y520_plus_rgba_identical':True,'maximum_50pct_contour_shift_px':max_shift,'before_50pct_bbox':box(before_contour),'after_50pct_bbox':box(after_contour),'changed_50pct_shape_pixels':int((before_contour!=after_contour).sum()),'changed_alpha_pixels':int((a[:,:,3]!=out[:,:,3]).sum()),'changed_visible_rgb_pixels':int(((a[:,:,3]>0)&np.any(a[:,:,:3]!=out[:,:,:3],axis=2)).sum()),'ink_rim_pixels_treated':int((edge>.01).sum()),'isolated_dark_skin_pixels_repaired':int((spots>0).sum()),'notes':['Only the painted outer ink rim and detected tiny skin marks are repaired.','Ear inner anatomy and natural chin structure are not masked out.','All RGBA at y520 and below are copied byte-for-byte from the restored-head source.']}
(Q/'head_outline_cleanup_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False))
