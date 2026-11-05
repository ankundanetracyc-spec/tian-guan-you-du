"""Bake first-pass speed classes from the selected original map; no nav writes."""
from pathlib import Path
import hashlib
import json
import re
from collections import Counter
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'resources/world/bai_chuan/terrain_travel_20261105.json'
REVIEW=ROOT/'output/bai_chuan_terrain_speed_20261105'
SCALE=ROOT/'gdscripts/world/BaiChuanWorldScale.gd'


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    config=SCALE.read_text(encoding='utf-8')
    path=lambda key: ROOT/re.search(rf'const {key} := "res://([^"]+)"',config).group(1)
    art_base=path('ART_BASE')
    master=art_base/re.search(r'const MASTER_FILE := "([^"]+)"',config).group(1)
    nav_path=path('NAVIGATION_PATH')
    layout_path=path('LAYOUT_PATH')
    inputs={str(p.relative_to(ROOT)):sha(p) for p in (master,nav_path,layout_path)}
    nav=json.loads(nav_path.read_text(encoding='utf-8'))
    columns=int(nav['columns'])
    with Image.open(master) as source:
        if source.size!=(3762,3762): raise ValueError('This prototype is authored for the selected 3762 original map.')
        pixels=np.asarray(source.convert('RGB')).astype(np.int16)
    red,green,blue=pixels.transpose(2,0,1)
    value=pixels.mean(2)
    water=(blue>red*1.15)&(blue>green*1.04)
    # The original trees are warm olive, often R>=G: use the full colour family
    # and local canopy texture instead of assuming the later grey-green art.
    rock_seed=(blue>=green-12)&(blue>=red-12)&(pixels.max(2)-pixels.min(2)<60)&(value<180)&~water
    local_value=ndimage.gaussian_filter(value,3)
    local_variance=np.maximum(0,ndimage.gaussian_filter(value*value,3)-local_value*local_value)
    forest_colour=(green>=red*.97)&(green-blue>=14)&(red-blue>=16)&(value<145)&~water&~rock_seed
    canopy_seed=forest_colour&(local_variance>49)
    forest=forest_colour&ndimage.binary_dilation(canopy_seed,iterations=4)
    forest=ndimage.binary_fill_holes(ndimage.binary_closing(forest,iterations=2))
    hill=rock_seed&~forest
    hill=ndimage.binary_fill_holes(ndimage.binary_closing(hill,iterations=2))
    step=8
    def fraction(mask):
        padded=np.pad(mask,((0,columns*step-mask.shape[0]),(0,columns*step-mask.shape[1])),constant_values=False)
        return padded.reshape(columns,step,columns,step).mean(axis=(1,3))
    water_fraction,forest_fraction,hill_fraction=map(fraction,(water,forest,hill))
    walkable=np.asarray([[code=='0' for code in row] for row in nav['rows']])
    woods=(forest_fraction>=.28)&walkable
    labels,count=ndimage.label(woods)
    sizes=np.bincount(labels.ravel())
    woods &= sizes[labels]>=4
    hills=(hill_fraction>=.25)&walkable&~woods
    crossing=(water_fraction>=.30)&walkable
    types={
      'P':{'id':'plain','name':'平地','multiplier':1.0},
      'F':{'id':'forest','name':'林地','multiplier':0.8},
      'H':{'id':'hill','name':'山地','multiplier':0.6},
      'C':{'id':'crossing','name':'桥渡','multiplier':1.0},
      '_':{'id':'blocked','name':'不可通行','multiplier':1.0}}
    codes=np.full((columns,columns),'P',dtype='<U1')
    codes[woods]='F'; codes[hills]='H'; codes[crossing]='C'; codes[~walkable]='_'
    rows=[''.join(row) for row in codes]
    result={'version':1,'profile_id':'terrain-speed-v1','experimental':True,
            'world_size':nav['world_size'],'world_km':nav['world_km'],'columns':columns,
            'cell_size':nav['cell_size'],'types':types,'rows':rows,
            'source_files':inputs,'scope':'Travel speed/time only. Original navigation, artwork and locations are not modified.',
            'classification':'Original-art colour groups at authored 8px source cells: forest>=28%, cool rock>=25%, water crossing>=30%; small forest specks under 4 cells ignored.'}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    REVIEW.mkdir(parents=True,exist_ok=True)
    colours={'P':(225,219,194),'F':(81,131,92),'H':(153,120,90),'C':(102,163,191),'_':(68,70,73)}
    display=np.asarray([[colours[code] for code in row] for row in codes],dtype=np.uint8)
    preview=Image.fromarray(display).resize((1413,1413),Image.Resampling.NEAREST)
    preview.save(REVIEW/'terrain_speed_grid.png')
    audit={'profile_id':result['profile_id'],'prototype_parameters':types,
           'walkable_class_counts':dict(Counter(codes[walkable].ravel())),
           'blocked_cells_preserved':int((~walkable).sum()),'rows':columns,'world_size':nav['world_size'],
           'source_files_unchanged':all(sha(ROOT/p)==digest for p,digest in inputs.items()),
           'source_files':inputs,'profile_sha256':sha(OUT)}
    (REVIEW/'terrain_speed_bake_audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'profile_id':result['profile_id'],'class_counts':audit['walkable_class_counts'],
                      'blocked_cells_preserved':audit['blocked_cells_preserved'],
                      'source_files_unchanged':audit['source_files_unchanged'],
                      'profile_sha256':audit['profile_sha256']}))


if __name__=='__main__': main()
