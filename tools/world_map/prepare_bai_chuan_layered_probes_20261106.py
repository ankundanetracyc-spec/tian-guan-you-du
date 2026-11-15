"""Locate opaque art pixels for the real-renderer compositing checks."""
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage as ndi

root = Path(__file__).resolve().parents[2]
data = json.loads((root/'resources/world/bai_chuan/layered_landscape_20261106.json').read_text(encoding='utf-8'))
mask = np.asarray(Image.open(root/'textures/world/bai_chuan/layered_20261106/feature_data.png'))
mountain = next(row for row in data['mountains'] if row['id']=='mountain_0164')
x,y,w,h = mountain['source_rect']
local = mask[y:y+h,x:x+w]
labels = local[:,:,2].astype(np.uint16)+local[:,:,3].astype(np.uint16)*256
valid = ndi.binary_erosion((labels==mountain['label'])&(local[:,:,1]>=253), structure=np.ones((29,15)))
ys,xs = np.nonzero(valid)
index = np.argmin((xs-w*.5)**2+(ys-h*.5)**2)
scale = data['source_scale']
tree = min((r for r in data['trees'] if r['asset']=='2' and r['height']>73), key=lambda r: (r['position'][0]-3600)**2+(r['position'][1]-3600)**2)
probes = [
    {'id':mountain['id'],'point':[(x+int(xs[index]))*scale,(y+int(ys[index])+12)*scale], 'max_visible_fraction':.12},
    {'id':tree['id'],'point':[tree['position'][0],tree['position'][1]-tree['height']*.28], 'max_visible_fraction':.55},
]
out = root/'output/bai_chuan_layered_20261106/occlusion_probes.json'
out.write_text(json.dumps(probes,indent=2)+'\n',encoding='utf-8')
print(out)
