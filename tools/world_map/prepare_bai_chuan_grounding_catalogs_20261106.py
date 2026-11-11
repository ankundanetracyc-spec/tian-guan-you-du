"""Measure complete native components and foot anchors; never repaint RGBA."""
from pathlib import Path
import hashlib
import json
import numpy as np
from scipy import ndimage as ndi
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/'textures/world/bai_chuan/forest_grounding_20261106'
OUT = ROOT/'output/bai_chuan_forest_grounding_20261106'
TREE_IDS = ['natural_oak_mature','natural_oak_young','natural_pine_wide',
            'natural_pine_lean','natural_elm_wide','natural_sapling']
GROUND_IDS = ['forest_floor','forest_edge','root_shade','mountain_scree',
              'mountain_soil','mountain_meadow']


def measured_assets(path, ids, columns, rows, tree=False):
    image = Image.open(path).convert('RGBA')
    alpha = np.asarray(image)[:,:,3]
    labels, count = ndi.label(alpha>=128)
    sizes = np.bincount(labels.ravel()); sizes[0]=0
    components = np.argsort(sizes)[-len(ids):]
    boxes = ndi.find_objects(labels)
    # Associate reading-order identities with full connected silhouettes, not
    # equal grid cuts: the first, third and fifth tree crowns cross cell lines.
    ordered = sorted(components, key=lambda k: ((boxes[k-1][0].start+boxes[k-1][0].stop)*.5))
    ordered = [k for row in range(rows) for k in sorted(ordered[row*columns:(row+1)*columns],
               key=lambda k: (boxes[k-1][1].start+boxes[k-1][1].stop)*.5)]
    primary = np.zeros_like(alpha,dtype=np.uint8)
    for i,k in enumerate(ordered): primary[labels==k]=i+1
    nearest = ndi.distance_transform_edt(primary==0,return_distances=False,return_indices=True)
    ownership = primary[tuple(nearest)]
    assets={}
    for i,ident in enumerate(ids):
        own = (ownership==i+1)&(alpha>=8)
        yy,xx=np.nonzero(own)
        x0=max(0,int(xx.min())-12);x1=min(image.width,int(xx.max())+13)
        y0=max(0,int(yy.min())-12);y1=min(image.height,int(yy.max())+13)
        region_alpha=alpha[y0:y1,x0:x1]
        foreign = (ownership[y0:y1,x0:x1]!=i+1)&(region_alpha>=32)
        assert not foreign.any(),f'Neighbour in crop {ident}'
        opaque=(ownership==i+1)&(alpha>=128)
        oy,ox=np.nonzero(opaque)
        if tree:
            foot_y=int(oy.max())+1
            foot_strip=opaque[max(y0,foot_y-5):foot_y]
            fy,fx=np.nonzero(foot_strip)
            foot_x=float(np.mean(fx))+.5
            pivot=[(foot_x-x0)/(x1-x0),(foot_y-y0)/(y1-y0)]
        else:
            pivot=[.5,.5];foot_x=(x0+x1)*.5;foot_y=(y0+y1)*.5
        assets[ident]={'region':[x0,y0,x1-x0,y1-y0],
            'native_size':[x1-x0,y1-y0],'pivot':pivot,
            'foot_px':[foot_x,foot_y],'complete_opaque_pixels':int(opaque.sum()),
            'region_edge_alpha_max':int(max(region_alpha[0].max(),region_alpha[-1].max(),region_alpha[:,0].max(),region_alpha[:,-1].max()))}
        if tree:
            assets[ident]['height_range']={'natural_oak_mature':[115,138],
                'natural_oak_young':[99,124],'natural_pine_wide':[110,136],
                'natural_pine_lean':[105,132],'natural_elm_wide':[112,136],
                'natural_sapling':[76,98]}[ident]
            assets[ident]['family']='pine' if 'pine' in ident else 'broadleaf'
        else:
            assets[ident]['role']=ident
            assets[ident]['width_range']=[30,50] if ident=='root_shade' else [150,300]
            assets[ident]['opacity_range']={'forest_floor':[.25,.45],
                'forest_edge':[.35,.60],'root_shade':[.22,.42]}.get(ident,[.45,.75])
            if ident=='root_shade':assets[ident]['tint']=[.64,.67,.50]
    return {'version':1,'mode':'built_in_imagegen',
        'atlas':'res://'+path.relative_to(ROOT).as_posix(),
        'runtime_atlas':'res://'+path.with_suffix('.res').relative_to(ROOT).as_posix(),
        'atlas_size':[image.width,image.height],
        'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
        'source_unchanged':True,'assets':assets}


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for source,dest,ids,columns,rows,tree in [
        ('trees_natural_native.png','trees_catalog.json',TREE_IDS,2,3,True),
        ('transitions_native.png','ground_catalog.json',GROUND_IDS,3,2,False)]:
        catalog=measured_assets(ART/source,ids,columns,rows,tree)
        (ART/dest).write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        print(json.dumps({'catalog':dest,'native_atlas':catalog['atlas_size'],
            'regions':{key:asset['region'] for key,asset in catalog['assets'].items()},
            'max_crop_edge_alpha':max(a['region_edge_alpha_max'] for a in catalog['assets'].values())}))


if __name__=='__main__':main()
