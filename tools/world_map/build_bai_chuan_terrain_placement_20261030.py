"""Terrain-informed display placement; never edits authoritative game geography."""
from pathlib import Path
import hashlib,json,math
import numpy as np
from scipy import ndimage
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'textures/world/bai_chuan/acceptance_20261030'
DATA=ROOT/'docs/prototypes/百川宗门城镇资源规划数据_20261027.json'
W=3762

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
def integral(m):return np.pad(m.astype(np.int32).cumsum(0,dtype=np.int32).cumsum(1,dtype=np.int32),((1,0),(1,0)))
def box(ii,x0,y0,x1,y1):
    x0=np.clip(x0,0,W).astype(int);x1=np.clip(x1,0,W).astype(int)
    y0=np.clip(y0,0,W).astype(int);y1=np.clip(y1,0,W).astype(int)
    return (ii[y1,x1]-ii[y0,x1]-ii[y1,x0]+ii[y0,x0])/np.maximum(1,(x1-x0)*(y1-y0))
def classify(r):
    n=r['name']
    if r['type']=='dock':return '岸边渡口'
    if any(c in n for c in '谷涧峡沟'):return '山间谷口'
    if r['type']=='sect':return '临水宗门' if any(c in n for c in '水河') else '山前宗门'
    if r.get('asset'):return '平缓聚落'
    if any(c in n for c in '泽潭泉溪渊湾滩浦'):return '水岸野外'
    if any(c in n for c in '林'):return '林缘野外'
    if any(c in n for c in '山岭峰崖岩窟洞'):return '山麓野外'
    return '平地野外'

def main():
    before=hashlib.sha256(DATA.read_bytes()).hexdigest()
    rows=read(BASE/'presentation.json')['nodes'];layout=read(BASE/'display_layout_v2.json')
    image=Image.open(BASE/'master_3762.png').convert('RGB');rgb=np.array(image).astype(np.float32)
    water=(rgb[:,:,2]>rgb[:,:,0]*1.15)&(rgb[:,:,2]>rgb[:,:,1]*1.04)
    mountain=(rgb.max(2)-rgb.min(2)<36)&(rgb.mean(2)<151)&~water
    forest=(rgb.mean(2)<140)&(rgb[:,:,1]-rgb[:,:,2]>22)&(rgb[:,:,0]-rgb[:,:,2]>45)&~water
    wi=integral(ndimage.binary_dilation(water,iterations=4));mi=integral(mountain);fi=integral(forest)
    wd=ndimage.distance_transform_edt(~water);fd=ndimage.distance_transform_edt(~forest)
    positions=np.array([layout['locations'][r['id']]['drawPos'] for r in rows],float);original=positions.copy()
    built=np.array([r.get('asset') is not None for r in rows]);poles=positions[:3].copy()
    zones=((positions[:,None]-poles[None])**2).sum(2).argmin(1)
    out={};audit=[]
    # Valleys and shoreline are most semantically constrained, settle those before towns.
    order=sorted(range(len(rows)),key=lambda i:(0 if classify(rows[i]) in ['山间谷口','岸边渡口'] else 1,i))
    for i in order:
        row=rows[i];kind=classify(row);start=original[i];radius=220 if not built[i] else 160
        if kind=='岸边渡口':radius=300
        gx,gy=np.meshgrid(np.arange(-radius,radius+1,12),np.arange(-radius,radius+1,12))
        options=np.vstack((start,start+np.column_stack((gx.ravel(),gy.ravel()))))
        good=(np.linalg.norm(options-start,axis=1)<=radius)&(options[:,0]>=35)&(options[:,0]<W-35)&(options[:,1]>=70)&(options[:,1]<W-35)
        good&=(((options[:,None]-poles[None])**2).sum(2).argmin(1)==zones[i])
        options=options[good];x,y=options.T
        width=row.get('width',0) or 14;height=row.get('height',0) or 14
        dry=box(wi,x-width*.5-4,y-height-3,x+width*.5+4,y+5)==0
        options=options[dry]
        if not len(options):options=start[None]
        x,y=options.T
        body=box(mi,x-width*.5,y-height,x+width*.5,y)
        woods=box(fi,x-width*.5,y-height,x+width*.5,y)
        around=box(mi,x-140,y-150,x+140,y+100)
        upper=box(mi,x-100,y-170,x+100,y-35)
        left=box(mi,x-180,y-110,x-28,y+70);right=box(mi,x+28,y-110,x+180,y+70)
        top=box(mi,x-110,y-180,x+110,y-28);bottom=box(mi,x-110,y+28,x+110,y+150)
        flanks=np.maximum(np.minimum(left,right),np.minimum(top,bottom))
        water_dist=wd[y.astype(int),x.astype(int)];forest_dist=fd[y.astype(int),x.astype(int)]
        if kind=='山间谷口':terrain=body*75+np.maximum(0,.10-flanks)*220+np.maximum(0,.09-around)*80
        elif kind=='山前宗门':terrain=body*70+woods*30+np.maximum(0,.15-upper)*150
        elif kind in ['岸边渡口','临水宗门']:terrain=np.abs(water_dist-14)*.8+body*100+woods*30
        elif kind=='水岸野外':terrain=np.abs(water_dist-20)*.5+body*30
        elif kind=='林缘野外':terrain=np.abs(forest_dist-8)*.4+body*45
        elif kind=='山麓野外':terrain=body*70+np.maximum(0,.14-around)*160
        else:terrain=body*180+woods*40+np.maximum(0,24-water_dist)*2
        distances=np.sqrt(((options[:,None]-positions[None])**2).sum(2));distances[:,i]=99999
        desired=np.where(built&built[i],270.,215.)
        spacing=np.maximum(0,desired[None]-distances)
        penalty=(spacing**2).sum(1)*.08
        movement=np.linalg.norm(options-start,axis=1)*.032
        score=terrain+penalty+movement
        # Three primary anchors remain stable for the route prototype and recognisable layout.
        selected=0 if row.get('level')==1 else int(np.argmin(score))
        if np.linalg.norm(options[selected]-start)<24:selected=0
        positions[i]=options[selected]
        old_metric=float(terrain[0]);new_metric=float(terrain[selected])
        out[row['id']]={'name':row['name'],'drawPos':np.round(positions[i]).astype(int).tolist(),
            'previousDrawPos':start.astype(int).tolist(),'terrainClass':kind,
            'reason':{'山间谷口':'向相邻山群夹出的低处或谷口靠拢，避开峰面与水面','山前宗门':'门楼放在山前低肩，背景有山而不压山峰','临水宗门':'靠近水岸的干地，门楼保持完整','岸边渡口':'移至靠岸干地，贴近可辨河岸','水岸野外':'以河湖岸边为展示锚点，不把圆点放进水里','林缘野外':'靠树林边缘留可见圆点','山麓野外':'靠山脚或岩体边缘，不压主峰','平缓聚落':'选开阔平缓干地，减少建筑压山或树林','平地野外':'保持开阔可落脚地形'}[kind],
            'shiftPixels':round(float(np.linalg.norm(positions[i]-start)),1),'terrainScoreBefore':round(old_metric,3),'terrainScoreAfter':round(new_metric,3),
            'review':'image features assisted, key valley crops visually reviewed; no elevation or travel-rule proof'}
    # Manual correction after reading the actual wide map crops. These are semantic
    # drawing judgements, deliberately allowed to override a misleading colour score.
    manual={
        'bc_place_049':([1380,210],'从山体正面移到北侧山群与南侧低岭间的空隙，留出可读的谷口'),
        'bc_place_076':([2880,3640],'沿海山群内侧低口，避开正面主峰和海岸凸角'),
        'bc_place_121':([3280,2610],'由海岸凸角向内陆移，落到后方高山与前方低岭之间的开口，不压西侧小山脊'),
        'bc_place_080':([3130,2792],'撤离外海孤岛，改到入海河段南岸主陆干地，避免路线被迫横跨外海'),
    }
    for id,(point,reason) in manual.items():
        i=next(i for i,r in enumerate(rows) if r['id']==id);positions[i]=point
        out[id]['drawPos']=point;out[id]['reason']=reason
        out[id]['shiftPixels']=round(float(np.linalg.norm(positions[i]-original[i])),1)
        out[id]['manualVisualReview']=True
        out[id]['terrainScoreAfter']=None
        out[id]['review']='人工按带坐标大幅局部核对，不以色彩分数作为地貌正确证明'
    write(BASE/'terrain_location_adjustments.json',{'version':'terrain_locations_20261030','displayOnly':True,'sourcePlanSha256':before,'locations':out,
        'limits':'Painted-map semantic placement only. Not a terrain heightmap and not a change in travel permissions or resource ownership.'})
    d=np.sqrt(((positions[:,None]-positions[None])**2).sum(2));np.fill_diagonal(d,np.inf)
    report={'reviewed':123,'changed':sum(v['shiftPixels']>0 for v in out.values()),'maxShift':max(v['shiftPixels'] for v in out.values()),'minimumNamedSpacing':round(float(d.min()),2),
        'logicalPlanUnchanged':hashlib.sha256(DATA.read_bytes()).hexdigest()==before,'byTerrainClass':{k:sum(v['terrainClass']==k for v in out.values()) for k in sorted(set(v['terrainClass'] for v in out.values()))}}
    labels,_=ndimage.label(~water)
    frame_land=set(np.r_[labels[0],labels[-1],labels[:,0],labels[:,-1]].tolist())-{0}
    report.update({'pointWaterOverlapCount':sum(bool(water[int(p[1]),int(p[0])]) for p in positions),
        'isolatedLandComponentPointIds':[r['id'] for r,p in zip(rows,positions) if labels[int(p[1]),int(p[0])] not in frame_land],
        'landConnectivityLimit':'Frame-connected land is a colour-mask aid; river cuts also split components, so it is not an elevation or traversability proof.',
        'manuallyReviewedKeyCases':['bc_place_049','bc_place_121','bc_place_080','bc_place_046'],
        'finalFileSha256':hashlib.sha256((BASE/'terrain_location_adjustments.json').read_bytes()).hexdigest()})
    write(BASE/'terrain_location_adjustments_checks.json',report)
    font=ImageFont.truetype(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),20)
    # Local sheets put a red circle on the proposed display point, unscaled map crop beside name.
    for suffix,selected_rows in [('valleys',[r for r in rows if classify(r)=='山间谷口']),('settlements',[r for r in rows if r.get('asset')]),('water',[r for r in rows if classify(r) in ['岸边渡口','水岸野外','临水宗门']])]:
        sheet=Image.new('RGB',(1600,math.ceil(len(selected_rows)/4)*310),(245,232,201));draw=ImageDraw.Draw(sheet)
        for j,row in enumerate(selected_rows):
            e=out[row['id']];x,y=e['drawPos'];a=j%4*400;b=j//4*310
            crop=image.crop((x-250,y-170,x+250,y+170)).resize((400,272));sheet.paste(crop,(a,b+30))
            draw.text((a+4,b+3),row['id'][-3:]+' '+row['name']+' '+str([x,y]),font=font,fill='black')
            draw.ellipse((a+195,b+161,a+205,b+171),outline='red',width=2)
        sheet.save(BASE/f'terrain_after_{suffix}.jpg',quality=91)
    key_ids=['bc_place_049','bc_place_121','bc_place_080','bc_place_046']
    sheet=Image.new('RGB',(1200,1640),(245,232,201));draw=ImageDraw.Draw(sheet)
    for j,id in enumerate(key_ids):
        entry=out[id];old=np.array(entry['previousDrawPos']);new=np.array(entry['drawPos']);center=(old+new)*.5
        left=max(0,min(W-800,int(center[0])-400));top=max(0,min(W-500,int(center[1])-250))
        crop=image.crop((left,top,left+800,top+500)).resize((600,375))
        for k,p in enumerate([old,new]):
            a=k*600;y=j*410;sheet.paste(crop,(a,y+30))
            draw.text((a+4,y+2),entry['name']+(' 调整前' if k==0 else ' 调整后')+' '+str(p.tolist()),font=font,fill='black')
            x=(p[0]-left)*.75+a;yy=(p[1]-top)*.75+y+30
            draw.ellipse((x-6,yy-6,x+6,yy+6),outline=('red' if k==0 else (0,90,20)),width=3)
    sheet.save(BASE/'terrain_key_before_after.jpg',quality=93)
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
