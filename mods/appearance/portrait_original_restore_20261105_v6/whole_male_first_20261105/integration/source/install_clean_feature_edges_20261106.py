from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil
R=Path(__file__).resolve().parent.parent;P=R.parents[4];A=P/'mods/appearance/ink_three_variants_20261004';E=R/'edge_cleanup_20261106';I=R/'installation'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
reportpath=I/'install_report.json';report=json.loads(reportpath.read_text(encoding='utf8'))
for r in report['records']:assert sha(P/r['resource_path'][6:])==r['sha256'],r['resource_path']
head_sha=sha(A/'male/face_shape/01.png');changed=[]
for part in ['eyebrows','eyes']:
 active=A/'male'/part/'01.png';candidate=E/'male'/part/'01.png';old=np.array(Image.open(active));new=np.array(Image.open(candidate));assert old.shape==new.shape==(1536,1024,4)
 backup=E/'installation_before'/part/'01.png';backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(active,backup)
 # Anchor preservation is tested on the dark pupil/brow ink core; edge pixels may be matted.
 lum=old[:,:,:3]@np.array([.2126,.7152,.0722]);interior=np.ones(old.shape[:2],bool)
 for dy in [-1,0,1]:
  for dx in [-1,0,1]:interior&=np.roll(np.roll(old[:,:,3],dy,axis=0),dx,axis=1)>=200
 # Brown antialiased pixels near the old matte can be quite dark; protect the
 # deepest ink, while allowing those edge mixtures to become transparent.
 core=(old[:,:,3]>=250)&(lum<75)&interior
 assert np.all(new[:,:,3][core]>=230),f'Lost dark feature core: {part}'
 assert np.array_equal(new[:,:,:3][core],old[:,:,:3][core]),f'Core RGB changed: {part}'
 assert np.count_nonzero(core)>60
 shutil.copy2(candidate,R/'male'/part/'01.png');shutil.copy2(candidate,active)
 rec=next(r for r in report['records'] if r['part']==part);rec['sha256']=sha(active)
 changed.append({'part':part,'old_sha256':sha(backup),'sha256':sha(active),'changed_pixels':int(np.any(old!=new,axis=2).sum()),'dark_core_pixels_unchanged':int(core.sum()),'canvas':[1024,1536],'coordinate_shift':[0,0]})
assert sha(A/'male/face_shape/01.png')==head_sha
manifestpath=A/'portrait_manifest.json';manifest=json.loads(manifestpath.read_text(encoding='utf8'));manifest['latest_male_rebuild']['approved_whole_01']['brow_eye_edge_cleanup']={'date':'2026-11-06','source':'res://'+E.relative_to(P).as_posix(),'parts':['eyebrows','eyes'],'scope':'Remove residual skin patches and hard rectangular seams; dark ink core and placement preserved','head_neck_unchanged':True}
manifestpath.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8');report['manifest_after_sha256']=sha(manifestpath);report['brow_eye_edge_cleanup']=changed
reportpath.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8');(E/'installation_report.json').write_text(json.dumps({'parts':changed,'head_neck_sha256':head_sha,'head_neck_changed':False},ensure_ascii=False,indent=2),encoding='utf8')
print('Installed clean brow/eye edges; exact dark ink cores, head/neck and other parts retained')
