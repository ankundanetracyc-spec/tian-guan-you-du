"""Match original face skin to user-supplied blank-face reference; no geometry edit."""
from pathlib import Path
import importlib.util,json,shutil
import numpy as np
from PIL import Image
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/portrait_rebuild_20261103/alignment_skin_revision'
O.mkdir(parents=True,exist_ok=True)
A=R/'mods/appearance/ink_three_variants_20261004/female'
REF=Path('C:/Users/34784/AppData/Local/Temp/codex-clipboard-7463b1c7-ad42-4d99-a32f-702519735226.png')
if not (O/'blank_reference.png').exists():shutil.copyfile(REF,O/'blank_reference.png')
if not (O/'face_before.png').exists():shutil.copyfile(A/'face_shape/04.png',O/'face_before.png')
face=np.array(Image.open(O/'face_before.png').convert('RGBA'))
ref=np.array(Image.open(O/'blank_reference.png').convert('RGB'))
samples=[('forehead',(280,235,345,280),(485,290,550,335)),('cheeks',(250,350,375,390),(455,405,580,445)),('chin',(300,445,330,470),(505,495,535,520)),('neck',(300,520,335,560),(505,570,540,610))]
records=[]
for name,(x,y,u,v),(a,b,c,d) in samples:
 target=np.median(ref[y:v,x:u].reshape(-1,3),axis=0)
 old=np.median(face[b:d,a:c,:3].reshape(-1,3),axis=0)
 records.append({'region':name,'source_median':target.tolist(),'before_median':old.tolist(),'delta':(target-old).tolist(),'y':(b+d)/2,'target_box':[a,b,c,d]})
ys=np.arange(1536)
offset=np.stack([np.interp(ys,[r['y'] for r in records],[r['delta'][c] for r in records]) for c in range(3)],axis=-1)
rgb=face[:,:,:3].astype(float)
# Leave ink contour depths intact; gradually apply color match in painted skin.
luma=rgb@np.array([.2126,.7152,.0722]);weight=np.clip((luma-100)/90,0,1);weight=weight*weight*(3-2*weight)
out=face.copy();out[:,:,:3]=np.rint(np.clip(rgb+offset[:,None,:]*weight[:,:,None],0,255));out[out[:,:,3]==0]=0
dest=O/'female/face_shape/04.png';dest.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(out).save(dest)
for r in records:
 a,b,c,d=r['target_box'];r['after_median']=np.median(out[b:d,a:c,:3].reshape(-1,3),axis=0).tolist()
spec=importlib.util.spec_from_file_location('feature_source',R/'tools/extract_three_style_features_20261103.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
module.OUT=O;module.FACE=dest;module.base=out[:,:,:3].astype(np.float32);module.main()
for i in range(1,4):
 canvas=Image.new('RGBA',(1024,1536),(235,229,217,255))
 for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
  if part=='face_shape':p=dest
  elif part in ['eyes','eyebrows','nose','mouth']:p=O/'female'/part/f'{i:02}.png'
  else:p=A/part/f'{i if part.startswith("hair") else 1:02}.png'
  canvas.alpha_composite(Image.open(p).convert('RGBA'))
 canvas.crop((280,35,765,720)).convert('RGB').save(O/f'skin_only_{i:02}.jpg',quality=97)
(O/'skin_match.json').write_text(json.dumps({'method':'Original face RGB adjusted by measured reference skin region medians, smoothly varying vertically; same alpha, no geometry change. Features re-extracted against new skin to avoid matte mismatch.','samples':records,'alpha_unchanged':bool(np.array_equal(face[:,:,3],out[:,:,3]))},ensure_ascii=False,indent=2),encoding='utf-8')
