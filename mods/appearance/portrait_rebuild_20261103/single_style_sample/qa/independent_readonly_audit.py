from PIL import Image,ImageDraw
from pathlib import Path
import numpy as np,json,hashlib
from scipy import ndimage
base=Path('mods/appearance/portrait_rebuild_20261103/single_style_sample'); qa=base/'qa';qa.mkdir(exist_ok=True)
p={f.stem:Image.open(f).convert('RGBA') for f in (base/'parts').glob('*.png')}
report={'status':'read_only_audit_in_progress','parts':{}}
for k,im in p.items():
 a=np.array(im); mask=a[:,:,3]>0; labels,n=ndimage.label(mask); sizes=np.bincount(labels.ravel()); ids=np.argsort(sizes[1:])[::-1][:8]+1
 report['parts'][k]={'size':list(im.size),'mode':Image.open(base/'parts'/f'{k}.png').mode,'sha256':hashlib.sha256((base/'parts'/f'{k}.png').read_bytes()).hexdigest(),'bbox':list(im.getbbox()),'component_sizes':[int(sizes[i]) for i in ids]}
source=Path('mods/appearance/ink_three_variants_20261004/female/face_shape/04.png')
report['face_source_sha256']=hashlib.sha256(source.read_bytes()).hexdigest();report['face_source_equal_bytes']=(source.read_bytes()==(base/'parts/face_shape.png').read_bytes())
for bg,name in [((236,230,218,255),'light'),((48,51,55,255),'dark')]:
 im=Image.new('RGBA',(1024,1536),bg)
 for k in ['hair_back','face_shape']:im.alpha_composite(p[k])
 im.alpha_composite(Image.open('mods/appearance/ink_three_variants_20261004/female/clothing/01.png').convert('RGBA'))
 for k in ['eyes','eyebrows','nose','mouth','hair_front']:im.alpha_composite(p[k])
 im.save(qa/f'actual_composite_{name}.png');im.crop((310,100,730,600)).resize((840,1000)).save(qa/f'actual_head_{name}.png')
# Side by side individual layers, using identical native canvas origin on checker background.
canvas=Image.new('RGB',(1024*3,1536),(236,230,218))
for i,k in enumerate(['hair_front','hair_back','face_shape']):
 im=Image.new('RGBA',(1024,1536),(236,230,218,255));im.alpha_composite(p[k]);canvas.paste(im.convert('RGB'),(i*1024,0))
canvas.resize((1536,768)).save(qa/'hair_face_layers.jpg')
# Native 2x paired features for pigment landmark measurement.
features=Image.new('RGBA',(1024,1536),(236,230,218,255))
for k in ['eyes','eyebrows','nose','mouth']:features.alpha_composite(p[k])
features.crop((400,300,640,510)).resize((960,840)).save(qa/'feature_pigment_4x.png')
# Alpha topology: count enclosed fully transparent islands inside the main rear hair silhouette.
a=np.array(p['hair_back'])[:,:,3]; solid=a>=128;fill=ndimage.binary_fill_holes(solid);holes=fill&~solid; lab,n=ndimage.label(holes);sz=np.bincount(lab.ravel())[1:]
report['rear_hair_enclosed_holes_alpha128']={'count':int(n),'largest':int(sz.max()) if n else 0,'total_pixels':int(holes.sum()),'center_face_alpha':[int(a[y,x]) for x,y in [(519,300),(519,379),(519,450),(519,520),(519,600)]]}
# warm non-hair pixel candidates in front layer; this is heuristic, not proof of skin absence.
a=np.array(p['hair_front']); rgb=a[:,:,:3].astype(float);warm=(rgb[:,:,0]>rgb[:,:,2]+14)&(rgb[:,:,0]>120)&(a[:,:,3]>=32)
report['front_hair_warm_pixel_candidates']={'count':int(warm.sum()),'bbox':list(Image.fromarray(warm).getbbox()) if warm.any() else None,'method':'R>B+14 and R>120 and alpha>=32; only a color heuristic'}
(qa/'independent_readonly_audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False,indent=2))

