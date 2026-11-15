"""Measure native silhouettes and solid bridge deck; preserve every RGBA byte."""
from pathlib import Path
import importlib.util
import hashlib
import json
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'textures/world/bai_chuan/compact_peaks_20261109'
PEAK_IDS=['compact_steep','compact_crag','compact_round','compact_weathered','compact_foothill','compact_lowhill']


def largest_rectangle(mask):
    heights=np.zeros(mask.shape[1],dtype=np.int32)
    best=(0,0,0,0,0)
    for y,row in enumerate(mask):
        heights=np.where(row,heights+1,0)
        stack=[]
        for x in range(mask.shape[1]+1):
            current=int(heights[x]) if x<mask.shape[1] else 0
            start=x
            while stack and stack[-1][1]>current:
                left,height=stack.pop()
                area=(x-left)*height
                if area>best[0]:best=(area,left,y-height+1,x,y+1)
                start=left
            if not stack or stack[-1][1]<current:stack.append((start,current))
    return list(best[1:])


def main():
    source_spec=importlib.util.spec_from_file_location('grounding_measure',ROOT/'tools/world_map/prepare_bai_chuan_grounding_catalogs_20261106.py')
    source_module=importlib.util.module_from_spec(source_spec);source_spec.loader.exec_module(source_module)
    peak_path=ART/'peaks_compact_native.png'
    measured=source_module.measured_assets(peak_path,PEAK_IDS,2,3,False)
    image=np.asarray(Image.open(peak_path).convert('RGBA'))
    ranges=[[160,360],[150,330],[140,290],[140,260],[90,180],[70,150]]
    assets={}
    for i,key in enumerate(PEAK_IDS):
        entry=measured['assets'][key]
        x,y,w,h=entry['region'];alpha=image[y:y+h,x:x+w,3]
        yy,xx=np.nonzero(alpha>=128)
        entry['pivot']=[.5,float(yy.max()+1)/h]
        entry['foot_px']=[x+w*.5,y+int(yy.max()+1)]
        for unused in ['width_range','opacity_range','role']:
            entry.pop(unused,None)
        entry.update(bank='compact',kind='peak' if i<4 else 'foothill',height_range=ranges[i])
        assets[key]=entry
    peaks={'version':1,'mode':'built_in_imagegen','atlas':measured['atlas'],
        'atlas_size':measured['atlas_size'],'source_unchanged':True,
        'mountain_sources':{'compact':{'path':measured['atlas'],'runtime_path':measured['runtime_atlas'],
            'size':measured['atlas_size'],'sha256':measured['source_sha256']}},'mountain_catalog':assets}
    (ART/'peaks_catalog.json').write_text(json.dumps(peaks,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

    bridge_path=ART/'bridge_topdown_native.png'
    bridge=Image.open(bridge_path).convert('RGBA');alpha=np.asarray(bridge)[:,:,3]
    yy,xx=np.nonzero(alpha>=8)
    x0=max(0,int(xx.min())-12);y0=max(0,int(yy.min())-12)
    x1=min(bridge.width,int(xx.max())+13);y1=min(bridge.height,int(yy.max())+13)
    deck=largest_rectangle(alpha>=248)
    dx0,dy0,dx1,dy1=deck
    assert int(alpha[dy0:dy1,dx0:dx1].min())>=248
    pivot=[(dx0+dx1)*.5,(dy0+dy1)*.5]
    bridge_catalog={'version':1,'mode':'built_in_imagegen',
        'atlas':'res://'+bridge_path.relative_to(ROOT).as_posix(),
        'runtime_atlas':'res://'+bridge_path.with_suffix('.res').relative_to(ROOT).as_posix(),
        'atlas_size':list(bridge.size),'source_sha256':hashlib.sha256(bridge_path.read_bytes()).hexdigest(),
        'source_unchanged':True,'perspective':'strict_top_down','bridge_catalog':{
            'wood_plan':{'atlas':'res://'+bridge_path.relative_to(ROOT).as_posix(),
                'region':[x0,y0,x1-x0,y1-y0],'native_size':[x1-x0,y1-y0],
                'pivot':[(pivot[0]-x0)/(x1-x0),(pivot[1]-y0)/(y1-y0)],
                'native_pivot_px':pivot,'deck_rect':[dx0-x0,dy0-y0,dx1-dx0,dy1-dy0],
                'deck_rect_atlas':deck,'deck_alpha_min':int(alpha[dy0:dy1,dx0:dx1].min()),
                'perspective':'strict_top_down'}}}
    (ART/'bridge_catalog.json').write_text(json.dumps(bridge_catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'peaks_native':peaks['atlas_size'],'peak_assets':len(assets),
        'bridge_native':list(bridge.size),'bridge_region':[x0,y0,x1-x0,y1-y0],
        'solid_deck':deck,'deck_alpha_min':bridge_catalog['bridge_catalog']['wood_plan']['deck_alpha_min']}))


if __name__=='__main__':main()
