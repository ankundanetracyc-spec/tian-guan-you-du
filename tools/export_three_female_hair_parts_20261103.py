from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,cv2,json,hashlib
from scipy import ndimage
R=Path(__file__).resolve().parents[1];P=R/'mods/appearance/portrait_rebuild_20261103';O=P/'three_style_parts';Q=O/'qa';Q.mkdir(parents=True,exist_ok=True)
face=Image.open(R/'mods/appearance/ink_three_variants_20261004/female/face_shape/04.png').convert('RGBA')
records=[]
for style in range(1,4):
 for part in ['hair_front','hair_back']:
  source=P/'single_style_sample/parts'/f'{part}.png' if style==1 else P/'final/female'/part/f'{style:02}.png'
  im=np.array(Image.open(source).convert('RGBA'));out=im.copy();method='Byte-identical reuse of reviewed single-style sample' if style==1 else 'Reuse already registered same-canvas candidate, no whole-layer transform'
  fixed=None
  if part=='hair_front' and style>1:
   y,x=np.indices(im.shape[:2],dtype=np.float32)
   def smooth(a,b,v):
    t=np.clip((v-a)/(b-a),0,1);return t*t*(3-2*t)
   vertical=smooth(175,245,y)*(1-smooth(285,355,y));horizontal=1-smooth(80,155,np.abs(x-518));displacement=22*vertical*horizontal
   alpha=im[:,:,3].astype(np.float32)/255;premul=im[:,:,:3].astype(np.float32)*alpha[:,:,None]
   newa=np.clip(cv2.remap(alpha,x,y-displacement,cv2.INTER_CUBIC,borderMode=cv2.BORDER_CONSTANT),0,1);newrgb=cv2.remap(premul,x,y-displacement,cv2.INTER_CUBIC,borderMode=cv2.BORDER_CONSTANT);changed=displacement>.001
   out[changed,:3]=np.rint(np.clip(newrgb/np.maximum(newa[:,:,None],1/255),0,255))[changed];out[changed,3]=np.rint(newa*255)[changed];out[out[:,:,3]==0,:3]=0
   visible=(im[:,:,3]>0)|(out[:,:,3]>0);diff=(im!=out).any(2)&visible;fixed={'above175_visible_changes':int(diff[:175].sum()),'below355_visible_changes':int(diff[355:].sum())}
   method+='; same localized inner-forehead pixel resampling as approved sample, maximum22px downward. Crown/outside/ear roots/chest fixed.'
  invisible_cleanup=int(((out[:,:,3]==0)&(out[:,:,:3]!=0).any(2)).sum())
  out[out[:,:,3]==0,:3]=0
  if invisible_cleanup:method=method.replace('Byte-identical reuse','Visible-pixel-identical reuse')+'; cleared '+str(invisible_cleanup)+' invisible RGB residues at alpha0, no visible pixel change'
  dest=O/'female'/part/f'{style:02}.png';dest.parent.mkdir(parents=True,exist_ok=True)
  if style==1 and not invisible_cleanup:dest.write_bytes(source.read_bytes())
  else:Image.fromarray(out).save(dest)
  def edge(a):
   ys=np.where(a[180:340,516:521,3].mean(1)>128)[0];return int(ys.max()+180) if len(ys) else None
  holes=ndimage.binary_fill_holes(out[:,:,3]>=128)&(out[:,:,3]<128);lab,n=ndimage.label(holes);sz=np.bincount(lab.ravel())[1:]
  records.append({'style':style,'part':part,'source':str(source.relative_to(R)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'output':str(dest.relative_to(R)),'output_sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'canvas':[1024,1536],'mode':'RGBA','alpha0_nonzero_rgb_count':int(((out[:,:,3]==0)&(out[:,:,:3]!=0).any(2)).sum()),'whole_layer_scale':1,'whole_layer_offset':[0,0],'method':method,'fixed_zone_checks':fixed,'center_edge_before':edge(im) if part=='hair_front' else None,'center_edge_after':edge(out) if part=='hair_front' else None,'back_center_alphas':[int(out[y,519,3]) for y in [300,379,450]] if part=='hair_back' else None,'largest_alpha128_enclosed_hole_pixels':int(sz.max()) if n else 0})
# Uniform face and actual samples to expose scale differences; all composites at native origin.
board=Image.new('RGB',(1536,768),(235,229,217))
heads=Image.new('RGB',(1260,600),(235,229,217))
for style in range(1,4):
 canvas=Image.new('RGBA',(1024,1536),(235,229,217,255))
 for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
  if part in ['hair_front','hair_back']:f=O/'female'/part/f'{style:02}.png'
  elif part=='face_shape':f=R/'mods/appearance/ink_three_variants_20261004/female/face_shape/04.png'
  elif part=='clothing':f=R/'mods/appearance/ink_three_variants_20261004/female/clothing/01.png'
  else:f=P/'single_style_sample/parts'/f'{part}.png'
  canvas.alpha_composite(Image.open(f).convert('RGBA'))
 canvas.save(Q/f'hair_family_{style:02}_same_face.png');board.paste(canvas.resize((512,768)).convert('RGB'),((style-1)*512,0));heads.paste(canvas.crop((310,40,730,640)).convert('RGB'),((style-1)*420,0))
board.save(Q/'hair_families_same_face.jpg');heads.save(Q/'hair_families_same_face_heads.jpg')
report={'status':'three_female_hair_pairs_for_combination_review_not_installed','fixed_face_source':'mods/appearance/ink_three_variants_20261004/female/face_shape/04.png','fixed_face_sha256':hashlib.sha256((R/'mods/appearance/ink_three_variants_20261004/female/face_shape/04.png').read_bytes()).hexdigest(),'style_reference_sources':[f'mods/appearance/portrait_rebuild_20261103/source/female_hair_final/reference_{i:02}.png' for i in range(1,4)],'style_names':['半束发簪披发','低盘髻细垂鬓','编束半披发'],'records':records,'limitations':['Reuses generated transparent hair candidate paintings that were already registered; not direct whole reference hair pixel extraction.','Style references inform design only. All whole-layer scale factors remain1 and all canvas offsets0.','Local inner-hairline pixel resampling changes only inner forehead area for02/03; hidden-source/scaffold three-point certification is not available.','Native previews use identical approved face04 and single-sample features to isolate hair geometry; root will provide style-specific feature combinations.','Original clothing01 still contains embedded thin chest lines; these previews inherit that separate known issue.','No runtime assets or active configuration changed; semantic absence of every isolated skin pixel is not mathematically provable by color thresholds.']}
(O/'female_hair_sources_and_limits.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(records,ensure_ascii=False,indent=2))
