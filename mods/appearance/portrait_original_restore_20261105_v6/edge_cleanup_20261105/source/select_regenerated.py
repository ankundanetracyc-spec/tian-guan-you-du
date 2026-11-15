"""Select the newly generated clean layers on the unchanged common portrait canvas."""
from pathlib import Path
from PIL import Image
import json,cv2,numpy as np
R=Path(__file__).resolve().parent.parent
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):
 p.parent.mkdir(parents=True,exist_ok=True);a=np.rint(a).clip(0,255).astype('uint8');a[a[:,:,3]==0,:3]=0;Image.fromarray(a).save(p)
records=[]
for part,stem in [('hair_front','front'),('hair_back','back')]:
 for i in range(1,4):
  source=R/'source/regen'/f'{stem}0{i}_revision.png'
  if not source.exists():source=R/'source/regen'/f'{stem}0{i}_raw.png'
  a=read(source)
  assert a.shape==(1536,1024,4),'Generated layer must retain the exact common canvas'
  # Remove only invisible alpha dust; generated lock interiors and outlines remain native.
  a[a[:,:,3]<=2,3]=0
  save(R/'male'/part/f'{i:02}.png',a)
  n,labels,stats,_=cv2.connectedComponentsWithStats((a[:,:,3]>=20).astype('uint8'),8)
  records.append({'part':part,'index':i,'native_source':source.relative_to(R).as_posix(),'canvas':[1024,1536],'resized':False,'runtime_offset':[0,0],'components_above_alpha20':sorted(stats[1:,4].tolist(),reverse=True)})
before=read(R/'before_male/face_shape/01.png')
face=read(R/'male/face_shape/01.png')
assert np.array_equal(face[:,:,3],before[:,:,3])
records.append({'part':'face_shape','index':1,'source':'before_male/face_shape/01.png; cleaned by clean_previous_head.py','generated_face_rejected_per_user':True,'canvas':[1024,1536],'resized':False,'face_neck_alpha_exactly_preserved':True})
(R/'qa/regenerated_selection.json').write_text(json.dumps(records,indent=2),encoding='utf8')
print('Selected six regenerated hair layers; previous head and its exact face/neck alpha retained')
