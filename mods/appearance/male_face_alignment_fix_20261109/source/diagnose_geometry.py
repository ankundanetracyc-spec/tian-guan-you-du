"""Read-only current-male geometry audit. Writes only this package's baseline and QA."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / 'mods/appearance/ink_three_variants_20261004'
QA = PACK / 'qa'
SHA = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

def write_json(p, obj):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def freeze():
    dst = PACK / 'source/baseline.json'
    if dst.exists():
        result = json.loads(dst.read_text(encoding='utf-8-sig'))
        for r in result['files']:
            assert SHA(ROOT/r['file']) == r['sha256'], r['file']
        return result
    paths = sorted([*(ACTIVE/'male').rglob('*.png'), *(ACTIVE/'female').rglob('*.png')])
    assert len(paths) == 67, len(paths)
    paths += [ACTIVE/'appearance_parts.json', ACTIVE/'portrait_manifest.json']
    records=[]
    for p in paths:
        rel=p.relative_to(ROOT)
        backup=PACK/'source/frozen'/p.relative_to(ACTIVE)
        if p.suffix=='.json': backup=backup.with_suffix('.snapshot.json')
        backup.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(p,backup)
        records.append({'file':rel.as_posix(),'sha256':SHA(p),'backup':backup.relative_to(PACK).as_posix()})
    result={'schema':'male-face-alignment-baseline/v1','scope':'67 installed male/female PNGs plus two active configurations; configs snapshot-suffixed to prevent mod discovery',
            'active_png_count':67,'configuration_count':2,'files':records}
    write_json(dst,result)
    return result

def image(part,n=1):
    return Image.open(ACTIVE/f'male/{part}/{n:02}.png').convert('RGBA')

def compose(n):
    out=Image.new('RGBA',(1024,1536),(231,225,214,255))
    for p,k in [('face_shape',1),('nose',1),('eyes',n),('eyebrows',n),('mouth',1)]:
        out.alpha_composite(image(p,k))
    return out

def zoom_grid():
    QA.mkdir(parents=True,exist_ok=True)
    crop=(375,295,655,560)
    grid=Image.new('RGB',(1120,3*560),'white')
    for row,n in enumerate([1,4,5]):
        base=compose(n)
        view=base.crop(crop).resize((560,530),Image.Resampling.NEAREST).convert('RGB')
        draw=ImageDraw.Draw(view)
        for x in range(380,656,10):
            xx=(x-crop[0])*2
            draw.line((xx,0,xx,529),fill=(80,160,160) if x%50==0 else (170,190,180),width=1)
            if x%20==0:draw.text((xx+1,1),str(x),fill='black')
        for y in range(300,561,10):
            yy=(y-crop[1])*2
            draw.line((0,yy,559,yy),fill=(80,160,160) if y%50==0 else (170,190,180),width=1)
            if y%20==0:draw.text((1,yy+1),str(y),fill='black')
        grid.paste(base.crop(crop).resize((560,530),Image.Resampling.NEAREST).convert('RGB'),(0,row*560+25))
        grid.paste(view,(560,row*560+25))
        ImageDraw.Draw(grid).text((5,row*560+5),f'Current eyes/brows {n:02}; left clean / right 10px grid; 2x nearest',fill='black')
    grid.save(QA/'current_face_geometry_grid.png')
    head=image('face_shape',1)
    bg=Image.new('RGBA',head.size,(231,225,214,255));bg.alpha_composite(head)
    bg.crop((350,230,680,640)).resize((660,820)).convert('RGB').save(QA/'face_shape01_without_features.png')

def centroid(a, roi, threshold):
    l,t,r,b=roi
    sub=a[t:b,l:r].astype(float)
    weight=np.maximum(threshold-sub[:,:,:3].mean(2),0)*sub[:,:,3]/255
    yy,xx=np.indices(weight.shape)
    return [float((weight*(xx+l)).sum()/weight.sum()),float((weight*(yy+t)).sum()/weight.sum())]

def weighted_quantile(values,weights,q):
    order=np.argsort(values);values=values[order];weights=weights[order]
    return float(values[np.searchsorted(np.cumsum(weights),q*weights.sum())])

def ink_geometry(a,roi,side,kind):
    l,t,r,b=roi;sub=a[t:b,l:r].astype(float);yy,xx=np.indices(sub.shape[:2]);xx+=l;yy+=t
    weights=np.maximum((170 if kind=='brow' else 145)-sub[:,:,:3].mean(2),0)*sub[:,:,3]/255
    weights[sub[:,:,3]<128]=0
    on=weights>0
    vx,vy,vw=xx[on],yy[on],weights[on]
    xlo=weighted_quantile(vx,vw,.01);xhi=weighted_quantile(vx,vw,.99)
    def endpoint(x):
        near=on&(abs(xx-x)<=1)
        w=weights*near
        return [float((xx*w).sum()/w.sum()),float((yy*w).sum()/w.sum())]
    outer=endpoint(xlo if side=='left' else xhi)
    inner=endpoint(xhi if side=='left' else xlo)
    result={'inner':inner,'outer':outer,'ink_centroid':centroid(a,roi,170 if kind=='brow' else 145),
            'dark_ink_horizontal_span_px':xhi-xlo,'roi':roi}
    if kind=='brow':
        # Highest dark ink crest is read from the body, not low-opacity support.
        # For a straight angular brow this can be near the lateral end.
        crest=[]
        for x in range(int(xlo)+2,int(xhi)-1):
            near=on&(abs(xx-x)<=2)
            if not near.any():continue
            crest.append((weighted_quantile(yy[near],weights[near],.10),x))
        ytop=min(z[0] for z in crest)
        xs=[x for y,x in crest if y<=ytop+.5]
        xp=float(np.mean(xs))
        result['highest_body_crest']=[xp,float(ytop)]
    return result

def geometry_audit():
    face=np.asarray(image('face_shape'))
    gray=face[:,:,:3].mean(2)
    forehead=[]
    for y in [260,280,300,320,330,340]:
        on=np.where(face[y,:,3]>=230)[0]
        forehead.append({'y':y,'left':int(on[0]),'right':int(on[-1]),'center_x':float((on[0]+on[-1])/2)})
    # Narrow hand-read windows follow the actual painted face/neck ridge. Ears,
    # transparent black, and the outer neck silhouette are explicitly excluded.
    ridge_rois={360:[395,407,618,630],390:[398,410,613,624],410:[405,416,608,620],
                430:[412,424,603,615],450:[420,432,595,605],470:[428,441,588,600],
                490:[438,450,577,588],510:[455,469,560,573],530:[475,487,540,553]}
    ridges=[]
    for y,(l,r,rl,rr) in ridge_rois.items():
        def ridge(left,right):
            v=gray[y,left:right].copy();v[face[y,left:right,3]<230]=np.inf
            return left+int(np.argmin(v))
        x1,x2=ridge(l,r),ridge(rl,rr)
        ridges.append({'y':y,'left':x1,'right':x2,'center_x':(x1+x2)/2,'search_x_half_open':[l,r,rl,rr]})
    axis_at_eyes=float(np.median([r['center_x'] for r in ridges if r['y']<=410]))
    axis_at_lower=float(np.median([r['center_x'] for r in ridges if r['y']>=430]))
    pupil_rois=[[453,358,475,374],[557,358,581,374]]
    eyes={};brows={}
    for n in [1,4,5]:
        a=np.asarray(image('eyes',n))
        pupils=[centroid(a,roi,100) for roi in pupil_rois]
        ek={side:ink_geometry(a,roi,side,'eye') for side,roi in zip(['left','right'],[[416,350,498,382],[533,349,615,383]])}
        mid=np.mean(pupils,axis=0)
        ek['pupils']=pupils;ek['midpoint']=mid.tolist();ek['pupil_dy_right_minus_left']=pupils[1][1]-pupils[0][1]
        ek['pupil_distance_px']=float(np.linalg.norm(np.array(pupils[1])-pupils[0]))
        ek['midpoint_dx_from_face_eye_rows']=float(mid[0]-axis_at_eyes)
        ek['pupil_kernel_threshold_sensitivity']={str(thr):[centroid(a,roi,thr) for roi in pupil_rois] for thr in [70,90,110]}
        # Eyelid contacts are manually read from actual current-PNG pixels at
        # iris center after inspecting 6x QA. These are NOT alpha support edges.
        contacts={1:[[463,362],[463,376],[569,361],[569,376]],
                  4:[[463,360],[463,376],[568,359],[568,375]],
                  5:[[463,361],[463,375],[568,362],[568,376]]}[n]
        ek['lid_contact_points_upper_lower_left_then_right']=contacts
        ek['fissure_at_iris_height_px']=[contacts[1][1]-contacts[0][1],contacts[3][1]-contacts[2][1]]
        eyes[f'{n:02}']=ek
        a=np.asarray(image('eyebrows',n))
        bk={side:ink_geometry(a,roi,side,'brow') for side,roi in zip(['left','right'],[[410,326,503,359],[527,325,617,360]])}
        bk['inner_head_midpoint']=np.mean([bk['left']['inner'],bk['right']['inner']],axis=0).tolist()
        bk['outer_tail_midpoint']=np.mean([bk['left']['outer'],bk['right']['outer']],axis=0).tolist()
        bk['stroke_centroid_midpoint']=np.mean([bk['left']['ink_centroid'],bk['right']['ink_centroid']],axis=0).tolist()
        bk['inner_head_dy_right_minus_left']=bk['right']['inner'][1]-bk['left']['inner'][1]
        bk['outer_tail_dy_right_minus_left']=bk['right']['outer'][1]-bk['left']['outer'][1]
        bk['crest_dy_right_minus_left']=bk['right']['highest_body_crest'][1]-bk['left']['highest_body_crest'][1]
        brows[f'{n:02}']=bk
    nose=np.asarray(image('nose'))
    nostrils=[centroid(nose,roi,150) for roi in [[497,434,514,451],[522,434,539,451]]]
    mouth=np.asarray(image('mouth'))
    lip_center=centroid(mouth,[477,466,562,483],150)
    mouth_ink=ink_geometry(mouth,[474,468,564,482],'left','eye')
    mouth_corner_mid=np.mean([mouth_ink['outer'],mouth_ink['inner']],axis=0).tolist()
    # Record historical transforms without using old requested controls as new measurements.
    provenance={}
    specs=[('01','male_style_alignment_20261106/integration'),('04','male_loose_style_install_20261106'),('05','male_high_tie_install_20261107')]
    for key,folder in specs:
        p=ROOT/'mods/appearance'/folder/'source/feature_import.json'
        src=json.loads(p.read_text(encoding='utf-8-sig'))
        shared=src.get('shared_source_to_frame')
        records=[]
        for rec in src['records']:
            if rec['part'] not in ['eyebrows','eyes']:continue
            xf=rec.get('combined_native_to_frame',rec.get('transform'))
            current_sha=SHA(ACTIVE/rec['file'])
            records.append({'part':rec['part'],'declared_sha256':rec['sha256'],'active_sha256':current_sha,'declared_hash_matches_active':current_sha==rec['sha256'],
                            'transform':xf,'rotation_deg':math.degrees(math.atan2(xf['b'],xf['a'])),
                            'declared_max_native_fit_error_px':rec.get('maximum_native_to_source_residual_px',rec.get('maximum_residual_px'))})
        provenance[key]={'source_file':p.relative_to(ROOT).as_posix(),'sha256':SHA(p),'shared_source_to_frame':shared,
                         'shared_rotation_deg':math.degrees(math.atan2(shared[1][0],shared[0][0])) if shared else None,'records':records}
    provenance['04']['later_current_override']={
        'authority':'docs/美术规范/男性原图完整还原_20261105/README.md (2026-11-07 eye balance and left outer lash seam repair sections)',
        'current_eye_processing':'male_eye_balance_fix_20261107 then male_layer_seam_fix_20261107',
        'warning':'The initial whole-eye04 transform is historical, not an exact transform of the final repaired current04. All pupil and ink measurements in this report read current81723d... pixels.'}
    result={'schema':'male-current-face-semantic-audit/v1','coordinate_system':'Current installed PNG canvas1024x1536, x right/y down; no rescale or mutation',
            'method':{'face':'Before ears y260..340 alpha230 row endpoints; y360..530 actual painted face ridge in restricted row windows, explicitly excludes ears/neck outline. Not full-image alpha bounding-box centers.',
                      'pupils':'Fixed small kernel ROIs, meanRGB darkness below100 weighted by alpha, threshold sensitivity70/90/110 retained; not whole-eye alpha bbox.',
                      'corners':'1% and99% cumulative dark ink x quantiles within each anatomical eye ROI, y weighted in ±1px columns. Visible ink-tip proxy, not falsely precise scleral join; about1–2px reading uncertainty.',
                      'brows':'Dark ink below170, alpha>=128; 1/99% ink quantiles for inner head/outer tail, upper10% local body crest for peak, pigment-weighted centroid. No transparent alpha margins.',
                      'manual_contacts':'Actual PNG 6x inspection; upper/lower iris contacts approximate ±1px. Natural painted asymmetry cannot be reduced to bounding-box size.',
                      'bridge_root':'No crisp anatomic root mark in extracted nose. Manual central highlight ridge atx517±2 aroundy397; uncertainty explicit.'},
            'face':{'forehead_edges':forehead,'face_ridges':ridges,'eye_region_axis_x':axis_at_eyes,'lower_face_axis_x':axis_at_lower,'chosen_shared_axis_x':513,
                    'chin_dark_ridge_reference':[513.5,543]},
            'eyes':eyes,'brows':brows,
            'nose_mouth':{'nose_bridge_root_approx':[517,397],'nose_bridge_root_uncertainty_px':2,'nostrils':nostrils,
                          'nostril_midpoint':np.mean(nostrils,axis=0).tolist(),'nose_tip_lower_center_approx':[517,450],
                          'lip_dark_seam_centroid':lip_center,'lip_corner_ink_midpoint':mouth_corner_mid,
                          'nose_mid_dx_from_lower_face':float(np.mean(nostrils,axis=0)[0]-axis_at_lower),
                          'lip_centroid_dx_from_lower_face':lip_center[0]-axis_at_lower},
            'historical_registration':provenance,'active_pngs_written':False,'candidate_pngs_written':False}
    write_json(QA/'geometry_diagnosis.json',result)
    annotated=Image.new('RGB',(3*660,820),'white')
    for col,n in enumerate([1,4,5]):
        view=compose(n).crop((350,230,680,640)).resize((660,820),Image.Resampling.NEAREST).convert('RGB');d=ImageDraw.Draw(view)
        xy=lambda p:((p[0]-350)*2,(p[1]-230)*2)
        for row in forehead+ridges:
            p=xy([row['center_x'],row['y']]);d.ellipse((p[0]-2,p[1]-2,p[0]+2,p[1]+2),fill=(0,190,130))
        d.line([xy([513,230]),xy([513,640])],fill=(0,170,130),width=1)
        for p in eyes[f'{n:02}']['pupils']:
            x,y=xy(p);d.line((x-8,y,x+8,y),fill=(230,30,30));d.line((x,y-8,x,y+8),fill=(230,30,30))
        for side in ['left','right']:
            for tag in ['inner','outer','highest_body_crest','ink_centroid']:
                x,y=xy(brows[f'{n:02}'][side][tag]);d.ellipse((x-2,y-2,x+2,y+2),fill=(45,80,230))
        for p in [np.mean(nostrils,axis=0),lip_center]:
            x,y=xy(p);d.ellipse((x-4,y-4,x+4,y+4),outline=(240,110,0),width=2)
        d.text((8,8),f'{n:02} green: face axis513 / red: pupils / blue: brow / orange: nose+lip',fill='black')
        annotated.paste(view,(col*660,0))
    annotated.save(QA/'semantic_landmarks_current_01_04_05.png')
    return result

if __name__=='__main__':
    base=freeze()
    zoom_grid()
    diag=geometry_audit()
    for r in base['files']:assert SHA(ROOT/r['file'])==r['sha256'],r['file']
    print(json.dumps({'frozen_pngs':base['active_png_count'],'configs':2,'face':diag['face'],'eye_midpoints':{k:v['midpoint'] for k,v in diag['eyes'].items()},'nose_mouth':diag['nose_mouth'],'preserved':True}))
