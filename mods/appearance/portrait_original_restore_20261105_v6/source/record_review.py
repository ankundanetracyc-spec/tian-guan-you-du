from pathlib import Path
import json
from PIL import Image
import numpy as np
p=Path(__file__).resolve().parent.parent
d=json.loads((p/'qa/candidate_inventory.json').read_text())
checks=[]
for part,i,r in [('eyes',1,1),('eyes',2,3),('eyebrows',1,1),('eyebrows',2,2),('eyebrows',3,3),('nose',1,3),('mouth',1,3)]:
 a=np.array(Image.open(p/f'male/{part}/{i:02}.png'))
 b=np.array(Image.open(p/f'source/reference_canvas_{r:02}.png'))
 m=a[:,:,3]>0
 ok=bool(np.array_equal(a[:,:,:3][m],b[:,:,:3][m]))
 assert ok
 checks.append({'part':part,'index':i,'original_rgb_equal':ok,'visible_pixels':int(m.sum())})
d.update(root_visual_review_complete=True,user_visual_approval=False,original_feature_checks=checks,reviewed=['three full composites','nine front/back combinations on dark and light','seven neck/collar pairs on dark and light'],limitations=['Hidden face/hair and unseen ends are reconstructed, not original pixels.','Shared face and shared nose/mouth mean no claim of pixel-identical whole portraits.'])
(p/'qa/final_review.json').write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
print('Seven feature assets preserve all visible source RGB at identical coordinates')
