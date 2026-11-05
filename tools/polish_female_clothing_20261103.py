"""Register generated polish at identity; constrain to original garment ownership."""
from pathlib import Path
import json, shutil, hashlib
import numpy as np
import cv2
from PIL import Image
R=Path(__file__).resolve().parents[1]
O=R/'mods/appearance/portrait_rebuild_20261103/three_style_parts'
REV=O/'revisions/clothing_polish';REV.mkdir(parents=True,exist_ok=True)
original=R/'mods/appearance/ink_three_variants_20261004/female/clothing/01.png'
generated=Path('E:/tmp/CodexData/generated_images/01a1a0b3-f096-7a82-a756-a8d54a0e071a/exec-d1cc662d-7cc4-4db0-97a3-5b409c02356d.png')
shutil.copyfile(original,REV/'original.png');shutil.copyfile(generated,REV/'generated.png')
old=np.array(Image.open(original).convert('RGBA'));new=np.array(Image.open(generated).convert('RGBA'))
assert old.shape==new.shape==(1536,1024,4)
mask=(old[:,:,3]>=128).astype(np.uint8)*255
# Neck belongs to face layer, not clothing. Remove old cutout skin remnants.
cv2.fillPoly(mask,[np.array([(0,0),(1023,0),(1023,540),(584,540),(584,552),(520,690),(450,552),(450,540),(0,540)],np.int32)],0)
mask=cv2.morphologyEx(mask,cv2.MORPH_OPEN,np.ones((3,3),np.uint8))
n,labels,stats,_=cv2.connectedComponentsWithStats(mask)
mask=np.where(labels==1+np.argmax(stats[1:,cv2.CC_STAT_AREA]),mask,0).astype(np.uint8)
mask=cv2.GaussianBlur(mask,(3,3),.55)
new[:,:,3]=np.minimum(new[:,:,3],mask)
new[new[:,:,3]==0]=0
out=O/'female/clothing/01.png';out.parent.mkdir(exist_ok=True)
Image.fromarray(new).save(out)
for label,arr in [('before',old),('after',new)]:
    bg=Image.new('RGBA',(1024,1536),(235,229,217,255));bg.alpha_composite(Image.fromarray(arr))
    bg.crop((80,520,950,1070)).convert('RGB').save(REV/f'{label}_collar_shoulders.jpg',quality=96)
record={'canvas':[1024,1536],'scale':1,'translation':[0,0],'method':'imagegen local garment polish followed by original silhouette alpha constraint and removal of skin ownership inside neckline','source_sha256':hashlib.sha256(original.read_bytes()).hexdigest(),'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'limitations':'Generated brushwork and small motif details differ from original. No geometric warp or canvas resize; original neckline/shoulder ownership constrains output.','output':str(out)}
(REV/'registration.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(record,ensure_ascii=False))
