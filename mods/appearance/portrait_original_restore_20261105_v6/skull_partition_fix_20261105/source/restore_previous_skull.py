"""Restore the original high, complete male skull without moving the accepted neck."""
from pathlib import Path
import hashlib,json
from PIL import Image,ImageDraw
import cv2,numpy as np
from scipy.ndimage import distance_transform_edt
R=Path(__file__).resolve().parent.parent
B=R.parent
A=B.parent/'ink_three_variants_20261004'
old_source=B.parent/'new_ink_hair_20261003_v1/male/face_shape.png'
current_source=R/'before_male/face_shape/01.png'
if not current_source.exists():
 current_source.parent.mkdir(parents=True,exist_ok=True)
 current_source.write_bytes((A/'male/face_shape/01.png').read_bytes())
reference_source=B/'source/reference_canvas_03.png'
def read(p):return np.array(Image.open(p).convert('RGBA'))
old=read(old_source);current=read(current_source);ref=read(reference_source)
assert old.shape==current.shape==(1536,1024,4)
H,W=old.shape[:2];Y,X=np.indices((H,W))
def poly(points,feather=1):
 m=np.zeros((H,W),np.float32);cv2.fillPoly(m,[np.array(points,np.int32)],1)
 return cv2.GaussianBlur(m,(0,0),feather) if feather else m
# Clean the isolated old-source temple marks and imported dark neck-side fragments.
# The skin itself, ears, jaw line and scalp outline retain the old drawing.
zones=np.maximum.reduce([
 poly([(419,366),(432,366),(432,397),(419,397)],1),
 poly([(413,438),(430,438),(442,485),(444,521),(431,521),(419,474)],1),
 poly([(594,437),(609,437),(608,489),(602,520),(591,520)],1)
])
rgb=old[:,:,:3].astype(float)
# A visible old painted rim is an anatomical line; only suspicious very dark imported ink is repaired.
bad=(rgb.mean(2)<150)&(old[:,:,3]>0)&(zones>.01)
bad=cv2.dilate(bad.astype('uint8'),np.ones((3,3),np.uint8)).astype(float)*zones
skin=(old[:,:,3]>230)&(zones<.001)&(rgb[:,:,0]-rgb[:,:,1]>12)&(rgb[:,:,0]-rgb[:,:,2]>28)&(rgb.mean(2)>175)
_,indices=distance_transform_edt(~skin,return_indices=True)
nearest=rgb[indices[0],indices[1]]
nearest=cv2.GaussianBlur(nearest.astype(np.float32),(0,0),1.5).astype(float)
clean=rgb*(1-bad[:,:,None])+nearest*bad[:,:,None]
# Remove small dark scalp stubble/dust using a hairless old head's own nearby skin.
scalp=poly([(405,196),(448,166),(516,159),(589,184),(627,221),(640,285),(635,310),(393,310),(389,259)],3)
scalp_dark=((rgb.mean(2)<120)&(old[:,:,3]>230)&(scalp>.5)).astype('uint8')
if scalp_dark.any():clean=cv2.inpaint(clean.clip(0,255).astype('uint8'),scalp_dark*255,4,cv2.INPAINT_TELEA).astype(float)
# Three clean skin areas are shared in registration, but no eyes/brows/nose/lips are sampled.
sample=np.zeros((H,W),bool)
for x,y,xx,yy in [(485,295,570,331),(461,409,485,439),(547,410,576,439)]:sample[y:yy,x:xx]=True
ref_rgb=ref[:,:,:3].astype(float)
sample&=(old[:,:,3]>230)&(ref_rgb[:,:,0]-ref_rgb[:,:,1]>10)&(ref_rgb[:,:,0]-ref_rgb[:,:,2]>30)
lab=cv2.cvtColor(clean.astype(np.float32)/255,cv2.COLOR_RGB2LAB)
lab_ref=cv2.cvtColor(ref[:,:,:3].astype(np.float32)/255,cv2.COLOR_RGB2LAB)
delta=np.clip(np.median(lab_ref[sample],axis=0)-np.median(lab[sample],axis=0),[-5,-2.5,-2.5],[5,2.5,2.5])
tone_weight=np.clip((clean.mean(2)-125)/55,0,1)*(old[:,:,3]>0)
lab+=tone_weight[:,:,None]*delta
toned=np.rint(cv2.cvtColor(lab,cv2.COLOR_LAB2RGB)*255).clip(0,255)
old_clean=old.copy();old_clean[:,:,:3]=toned
# Do not resize or translate any region. Transition only through the inner lower-face/neck strip.
weight=np.clip((Y-484)/36,0,1)[:,:,None]
oa=old_clean[:,:,3:4].astype(float)/255;ca=current[:,:,3:4].astype(float)/255
alpha=oa*(1-weight)+ca*weight
rgb_out=(old_clean[:,:,:3]*oa*(1-weight)+current[:,:,:3]*ca*weight)/np.maximum(alpha,1e-9)
out=np.rint(np.concatenate([rgb_out,alpha*255],2)).clip(0,255).astype('uint8')
out[520:]=current[520:]
out[out[:,:,3]==0,:3]=0
assert np.array_equal(out[520:],current[520:])
assert np.array_equal(out[:484,:,3],old[:484,:,3])
destination=R/'male/face_shape/01.png';destination.parent.mkdir(parents=True,exist_ok=True)
Image.fromarray(out).save(destination)
Q=R/'qa';Q.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def measures(a):
 m=a[:,:,3]>=230;ys,xs=np.where(m)
 rows={}
 for y in [180,210,230,250,280,330,370,400,430,450,470,490,510,520,530,550,580,600,650,700]:
  v=np.where(m[y])[0];rows[str(y)]=[int(v[0]),int(v[-1]),int(len(v))] if len(v) else None
 return {'bbox':[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())],'rows':rows}
report={'source':str(old_source),'source_sha256':sha(old_source),'equivalent_source':str(B.parent/'portrait_original_refinement_20261103_v1/parts/male/face_shape.png'),'source_equivalent_sha':sha(B.parent/'portrait_original_refinement_20261103_v1/parts/male/face_shape.png'),'before':str(current_source),'before_sha256':sha(current_source),'selected':str(destination),'selected_sha256':sha(destination),'canvas':[W,H],'no_resize_or_translation':True,'old_upper_head_alpha_exact_until_y483':True,'current_neck_rgba_exact_from_y520':True,'before_measurements':measures(current),'old_measurements':measures(old),'selected_measurements':measures(out),'skin_reference':str(reference_source),'lab_median_shift':delta.tolist(),'skin_sample_count':int(sample.sum()),'dark_foreign_ink_pixels_repaired':int((bad>.01).sum()),'scalp_dark_pixels_repaired':int(scalp_dark.sum()),'transition_rows':[484,519]}
(Q/'restored_skull_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
features=[('nose',1),('eyes',1),('eyebrows',1),('mouth',1)]
for bg,name in [('#e8e1d6','light'),('#273238','dark')]:
 panels=[]
 for data in [current,old,out]:
  pure=Image.new('RGBA',(W,H),bg);pure.alpha_composite(Image.fromarray(data));panels.append(pure.crop((340,135,690,745)).resize((420,732)))
 overview=Image.new('RGB',(1260,780),bg);d=ImageDraw.Draw(overview)
 for i,img in enumerate(panels):overview.paste(img,(i*420,40));d.text((i*420+10,10),['A CURRENT LOW SKULL','B ORIGINAL HIGH SKULL','C RESTORED / SAME NECK'][i],fill='white' if name=='dark' else 'black')
 overview.save(Q/f'skull_three_way_{name}.png')
 full=Image.new('RGBA',(W,H),bg);full.alpha_composite(Image.fromarray(out))
 for part,i in features:full.alpha_composite(Image.open(A/'male'/part/f'{i:02}.png').convert('RGBA'))
 full.crop((340,135,690,745)).resize((700,1220)).save(Q/f'restored_head_current_features_{name}.png')
print(json.dumps({'selected':str(destination),'old_top':report['old_measurements']['bbox'][1],'before_top':report['before_measurements']['bbox'][1],'selected_top':report['selected_measurements']['bbox'][1],'neck_rgba_unchanged_y520_plus':True,'lab_shift':delta.tolist()}))
