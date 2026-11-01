"""Read-only map-art routing review. Colour masks are not elevation/traversability."""
from __future__ import annotations
import argparse,json,math
from pathlib import Path
import numpy as np
from scipy import ndimage
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'textures/world/bai_chuan/acceptance_20261030'
DATA=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def resample(points,step=3.):
    p=np.array(points,float);length=np.r_[0,np.linalg.norm(np.diff(p,axis=0),axis=1).cumsum()]
    if length[-1]<.001:return p[:1],0.
    samples=np.linspace(0,length[-1],max(2,int(math.ceil(length[-1]/step))+1))
    return np.column_stack([np.interp(samples,length,p[:,a]) for a in range(2)]),float(length[-1])
def longest(mask,spacing):
    starts=np.flatnonzero(np.diff(np.r_[False,mask,False].astype(int))==1)
    ends=np.flatnonzero(np.diff(np.r_[False,mask,False].astype(int))==-1)
    if not len(starts):return 0.,0,0
    j=(ends-starts).argmax();return float((ends[j]-starts[j])*spacing),int(starts[j]),int(ends[j])

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--layout',default='display_layout_v2.json');args=parser.parse_args()
    path=Path(args.layout);path=path if path.is_absolute() else BASE/path
    layout=read(path);data=read(DATA);labels={n['id']:n['name'] for n in data['nodes']}
    graph=layout.get('displayGraph',{}).get('edges',data['roadCurveGeometry']['edges'])
    image=Image.open(BASE/'master_3762.png').convert('RGB');rgb=np.array(image).astype(float);h,w=rgb.shape[:2]
    water=(rgb[:,:,2]>rgb[:,:,0]*1.15)&(rgb[:,:,2]>rgb[:,:,1]*1.04)
    rock=(rgb.max(2)-rgb.min(2)<44)&(rgb.mean(2)<160)&~water
    lab,n=ndimage.label(~water);counts=np.bincount(lab.ravel());large=counts>80000;large[0]=False
    mainland=large[lab];shore=np.array([np.flatnonzero(row)[-1] if row.any() else int(w*.87) for row in mainland]);shore=ndimage.median_filter(shore,size=35)
    sea=water&(np.arange(w)[None,:]>shore[:,None])
    out=BASE/('route_terrain_review_'+path.stem.replace('display_layout_',''));out.mkdir(exist_ok=True)
    rows=[];cached={};total_length=0.;totals=np.zeros(3)
    for key,points in layout['displayEdges'].items():
        samples,length=resample(points);xy=np.clip(np.round(samples).astype(int),[0,0],[w-1,h-1]);spacing=length/max(1,len(samples)-1)
        masks=[rock[xy[:,1],xy[:,0]],water[xy[:,1],xy[:,0]],sea[xy[:,1],xy[:,0]]]
        totals+=np.array([m.mean()*length for m in masks]);total_length+=length
        row={'edge':key,'from':graph[key]['from'],'to':graph[key]['to'],'lengthPixels':round(length,2)}
        for name,m in zip(['mountain','water','sea'],masks):
            run,a,b=longest(m,spacing);row[name+'Fraction']=round(float(m.mean()),4);row[name+'LongestRunPixels']=round(run,2)
            row[name+'WorstCenter']=np.round(samples[a:b].mean(0) if b>a else samples[len(samples)//2],2).tolist()
        rows.append(row);cached[key]=samples
    report={'layout':path.name,'edges':len(rows),'totalDisplayLengthPixels':round(total_length,2),
        'lengthWeightedFractions':dict(zip(['mountain','water','sea'],np.round(totals/total_length,4).tolist())),
        'longMountainEdgesOver80Pixels':sum(r['mountainLongestRunPixels']>80 for r in rows),'longWaterEdgesOver80Pixels':sum(r['waterLongestRunPixels']>80 for r in rows),
        'limits':'Colour-mask review of painted mountains/water only; not height, bridge permission, collision proof or user visual acceptance.',
        'topMountain':sorted(rows,key=lambda r:r['mountainLongestRunPixels'],reverse=True)[:10],
        'topWater':sorted(rows,key=lambda r:r['waterLongestRunPixels'],reverse=True)[:10],
        'allEdges':rows}
    (out/'terrain_metrics.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    font=ImageFont.truetype(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),17)
    for category,ranking in [('mountain',report['topMountain']),('water',report['topWater'])]:
        sheet=Image.new('RGB',(1600,5*340),(244,231,202));d=ImageDraw.Draw(sheet)
        for i,row in enumerate(ranking):
            key=row['edge'];cx,cy=row[category+'WorstCenter'];left=max(0,min(w-900,int(cx)-450));top=max(0,min(h-600,int(cy)-300))
            crop=image.crop((left,top,left+900,top+600));draw=ImageDraw.Draw(crop)
            pts=[(float(x-left),float(y-top)) for x,y in layout['displayEdges'][key]]
            draw.line(pts,fill=(244,232,192),width=6);draw.line(pts,fill=(23,21,18),width=3)
            draw.ellipse((cx-left-6,cy-top-6,cx-left+6,cy-top+6),outline=(200,25,20),width=3)
            for endpoint,position in [(row['from'],pts[0]),(row['to'],pts[-1])]:
                x,y=position
                draw.ellipse((x-5,y-5,x+5,y+5),fill=(244,228,181),outline=(20,18,14),width=2)
                name=labels.get(endpoint,endpoint.replace('bc_transit_','T'))
                draw.text((x+7,y-8),name,font=font,fill=(12,10,5),stroke_width=1,stroke_fill=(240,223,176))
            crop.save(out/f'{category}_{i+1:02}.jpg',quality=94)
            a=i%2*800;b=i//2*340
            sheet.paste(crop.resize((800,300)),(a,b+35))
            title=f"{i+1}. {key.replace('bc_place_','P').replace('bc_transit_','T')} / {row[category+'LongestRunPixels']:.0f}px"
            d.text((a+4,b+6),title,font=font,fill=(10,10,5))
        sheet.save(out/f'top10_{category}_contact.jpg',quality=92)
    print(json.dumps({k:v for k,v in report.items() if k not in ['topMountain','topWater','allEdges']},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
