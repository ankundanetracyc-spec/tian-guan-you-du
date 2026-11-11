"""Measure final candidate pixels; never substitutes registration target points.

The measurements support review and carry explicit reading uncertainty. The
independent visual acceptance file is owned by audit/root, never this script.
"""
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import PACK,ACTIVE,QA,SHA,write_json,centroid,ink_geometry,weighted_quantile
from diagnose_beards import measure as beard_measure

def pixel(part,n):
    path=PACK/f'male/{part}/{n:02}.png'
    return path,np.asarray(Image.open(path).convert('RGBA'))

def run_around(mask,center):
    center=max(0,min(len(mask)-1,int(round(center))))
    if not mask[center]:
        options=np.where(mask)[0]
        if not len(options):return None
        center=int(options[np.argmin(abs(options-center))])
    left=right=center
    while left>0 and mask[left-1]:left-=1
    while right+1<len(mask) and mask[right+1]:right+=1
    return left,right

def corner(a,roi,extreme,outer_tip=False):
    l,t,r,b=roi;v=a[t:b,l:r].astype(float);gray=v[:,:,:3].mean(2)
    # Real outer lashes end in a single antialiased pigment pixel. Requiring
    # two opaque rows would measure the lash body instead of its visible tip.
    on=(gray<(200 if outer_tip else 190))&(v[:,:,3]>=(64 if outer_tip else 128))
    columns=np.where(on.sum(0)>=(1 if outer_tip else 2))[0]
    if not len(columns):raise ValueError('Missing semantic corner in '+str(roi))
    x=int(columns[0] if extreme=='left' else columns[-1]);ys=np.where(on[:,x])[0]
    w=(220-gray[ys,x])*v[ys,x,3]/255
    return [float(l+x),float(t+(ys*w).sum()/w.sum())]

def eye_semantics(a):
    pupils=[centroid(a,r,100) for r in [[450,355,473,376],[554,355,577,376]]]
    # Restricted actual corner windows, not whole-eye support bounding boxes.
    inner=[corner(a,[478,366,497,382],'right'),corner(a,[529,366,549,382],'left')]
    outer=[corner(a,[414,350,441,374],'left',True),corner(a,[585,350,616,374],'right',True)]
    gray=a[:,:,:3].mean(2);alpha=a[:,:,3]
    upp=[];low=[];iriswidth=[];irisheight=[];occlusion=[];iriscenters=[];width_rows=[];core_bounds=[]
    for side in range(2):
        px,py=pupils[side];upper=[];lower=[]
        for fraction in [.25,.5,.75]:
            x=inner[side][0]+fraction*(outer[side][0]-inner[side][0]);xx=int(round(x))
            profile=gray[:,xx-1:xx+2].mean(1);aa=alpha[:,xx-1:xx+2].mean(1)
            ys=np.arange(max(350,int(round(py))-9),min(373,int(round(py))))
            valid=ys[aa[ys]>=128]
            top=int(valid[np.argmin(profile[valid])])
            # Read the lowest actual dark lower-lid ridge. A local minimum is
            # separated from iris texture and transparent padding by support.
            lower_candidates=[y for y in range(max(top+5,363),383) if aa[y]>=128 and profile[y]<205 and profile[y]<=profile[y-1] and profile[y]<=profile[y+1]]
            if lower_candidates:bottom=max(lower_candidates)
            else:
                ys=np.arange(max(top+7,int(round(py))+5),382);valid=ys[aa[ys]>=128];bottom=int(valid[np.argmin(profile[valid])])
            upper.append([float(x),float(top)]);lower.append([float(x),float(bottom)])
        upp.append(upper);low.append(lower)
        # Central dark iris run below pupil glint, bounded by the observed
        # scleral light gaps on both sides; disjoint corner ink is excluded.
        runs=[]
        for y in [int(round(py))+4,int(round(py))+5,int(round(py))+6]:
            l=max(0,int(round(px))-19);r=min(1024,int(round(px))+20)
            mask=(gray[y,l:r]<155)&(alpha[y,l:r]>=128)
            run=run_around(mask,px-l)
            if run and run[0]>0 and run[1]<r-l-1:runs.append([run[0]+l,run[1]+l,y])
        if not runs:raise ValueError('Cannot isolate iris in current pixels')
        width_rows.append([{'left':l,'right':r,'y':y,'width':r-l+1} for l,r,y in runs])
        widths=[r-l+1 for l,r,y in runs];width=float(np.median(widths));iriswidth.append(width)
        cx=float(np.median([(r+l)/2 for l,r,y in runs]));xx=int(round(cx))
        top=max(350,int(round(py))-6);bottom=min(382,int(round(py))+15)
        # Solid iris core excludes the separate translucent lower-lash/matte
        # fringe, whose brown RGB must not extend the visible iris downward.
        vertical=(gray[top:bottom,xx]<150)&(alpha[top:bottom,xx]>=230)
        # A bright pupil glint is an internal hole, not an iris boundary. Fill
        # only measured1–2px internal holes in this diagnostic mask, and select
        # the brown iris run below the glint rather than the upper lash run.
        on=np.where(vertical)[0]
        for k in range(len(on)-1):
            if 1 < on[k+1]-on[k] <= 3:vertical[on[k]:on[k+1]+1]=True
        run=run_around(vertical,py+4-top)
        iy0,iy1=run[0]+top,run[1]+top
        core_bounds.append([int(iy0),int(iy1)])
        height=float(iy1-iy0+1);irisheight.append(height);occlusion.append(max(0.,width-height))
        # Read actual bounded iris paint, never infer its center from an upper
        # crease or the full alpha envelope. This weighted brown-body center is
        # an observable pigment proxy and remains subject to anatomical review.
        xr0=max(0,int(round(cx-width/2))+1);xr1=min(1024,int(round(cx+width/2)))
        iriscenters.append(centroid(a,[xr0,iy0,xr1,iy1+1],155))
    return {'pupils':pupils,'inner_corners':inner,'outer_corners':outer,'upper_lid_samples':upp,'lower_lid_samples':low,'iris_centers':iriscenters}, {
        'iris_widths':iriswidth,'iris_heights':irisheight,'upper_lid_occlusion_px':occlusion,
        'iris_method':'Observed central brown-body proxy, not a full anatomic iris segmentation. Horizontal contiguous central runs threshold155, median of up to3 valid rows with scleral gaps; vertical brown core threshold150/Alpha230 excludes faint upper crease and soft lower-lash fringe;1–2px internal glint gaps bridged for measurement only. Center is actual bounded brown-ink centroid, not extrapolated from alpha. About1px reading uncertainty.',
        'iris_width_sample_rows':width_rows,'iris_brown_core_vertical_bounds_y_inclusive':core_bounds,
        'iris_height_measurement_kind':'central_visible_brown_core_proxy_px',
        'upper_lid_occlusion_measurement_kind':'width_minus_visible_brown_core_height_estimate_px; circular lower-tangent assumption, independent visual iris/occlusion review mandatory'}

def brow_semantics(a):
    geometry=[ink_geometry(a,r,s,'brow') for r,s in zip([[410,325,503,360],[527,325,617,360]],['left','right'])]
    points={'inner_heads':[g['inner'] for g in geometry],'outer_tails':[g['outer'] for g in geometry],'crests':[g['highest_body_crest'] for g in geometry],
            'ink_weighted_centroids':[g['ink_centroid'] for g in geometry]}
    thickness=[];darkness=[]
    for s,g in enumerate(geometry):
        thick=[];deep=[]
        for q in [.25,.5,.75]:
            x=int(round(g['inner'][0]+q*(g['outer'][0]-g['inner'][0])))
            v=a[325:361,x-1:x+2].astype(float);gray=v[:,:,:3].mean(2);alpha=v[:,:,3]
            w=np.maximum(170-gray,0)*alpha/255;w[alpha<24]=0
            profile=w.sum(1);yy=np.arange(325,361)
            thick.append(weighted_quantile(yy,profile,.95)-weighted_quantile(yy,profile,.05)+1)
            core=(gray<170)&(alpha>=128);deep.append(float((255-gray[core]).mean()))
        thickness.append(thick);darkness.append(deep)
    return points,{'widths':[abs(g['inner'][0]-g['outer'][0]) for g in geometry],'thickness_at_quartiles':thickness,'mean_darkness_at_quartiles':darkness}

def main():
    records=[]
    def add(part,n,points,measurements):
        path=PACK/f'male/{part}/{n:02}.png'
        records.append({'file':path.relative_to(PACK).as_posix(),'part':part,'index':n,'sha256':SHA(path),'points':points,'measurements':measurements,'reading_uncertainty_px':1.0})
    plots=[]
    for n in [1,4,5]:
        path,a=pixel('eyes',n);points,metrics=eye_semantics(a)
        # Independent reader disambiguates a faint outer lid boundary from the
        # darker interior transition. Override is bound to exact reviewed PNG.
        if SHA(path)=='a69e053c0f0af4be64b10a157c0f076aede96ab44ccc0915665f74c50a2560da':
            metrics['auxiliary_dark_ridge_lower_lid_samples']=[[p[:] for p in side] for side in points['lower_lid_samples']]
            points['lower_lid_samples'][0][2]=[438.,372.]
            points['lower_lid_samples'][1][2]=[586.5,374.]
            metrics['outer75_semantic_boundary_review']={
                'reviewer':'portrait_audit','exact_png_sha256':SHA(path),
                'observation':'At outer75%, left dark-ridgey370/right373 are interior transitions. Visible lower-lid/skin boundary isleft372/right374, approximately1px reading uncertainty. Both dark-ridge originals remain above as separate auxiliary measurements.'}
        add('eyes',n,points,metrics)
        base=Image.new('RGBA',(1024,1536),(231,225,214,255));base.alpha_composite(Image.fromarray(a));v=base.crop((410,345,620,388)).resize((1260,258),Image.Resampling.NEAREST).convert('RGB');d=ImageDraw.Draw(v)
        for key,color in [('upper_lid_samples',(0,190,120)),('lower_lid_samples',(50,100,250))]:
            for side in points[key]:
                d.line([((p[0]-410)*6,(p[1]-345)*6) for p in side],fill=color,width=2)
                for p in side:
                    x,y=(p[0]-410)*6,(p[1]-345)*6;d.ellipse((x-3,y-3,x+3,y+3),fill=color)
        for p in points['inner_corners']+points['outer_corners']:
            x,y=(p[0]-410)*6,(p[1]-345)*6;d.ellipse((x-3,y-3,x+3,y+3),fill=(230,30,30))
        plots.append((n,v))
        path,a=pixel('eyebrows',n);points,metrics=brow_semantics(a);add('eyebrows',n,points,metrics)
    path,a=pixel('nose',1);nostrils=[centroid(a,r,150) for r in [[493,434,510,451],[518,434,535,451]]]
    add('nose',1,{'nostrils':nostrils,'center':[float(np.mean(nostrils,axis=0)[0]),450.]},{'lower_center_method':'Actual shifted columella bottomy450 confirmed in6x view, x measured from current paired nostril kernels.'})
    path,a=pixel('mouth',1);seam=centroid(a,[473,466,558,483],150);g=ink_geometry(a,[470,469,559,484],'left','eye')
    add('mouth',1,{'seam_center':seam,'corners':[g['outer'],g['inner']]},{'corner_method':'Actual dark seam terminal ink centers, not transparent lip support bounds.'})
    for n in [2,3,4,5,6]:
        path,a=pixel('face_accessory',n);upper=beard_measure(a,[458,449,572,490] if n==6 else [436,449,586,490]);lower=beard_measure(a,[421,490,606,585])
        points={};present=[]
        for name,g in [('upper',upper),('lower',lower)]:
            points[name+'_center']=None;points[name+'_endpoints']=None
            if g:
                present.append(name);points[name+'_center']=[g['endpoint_midpoint_x'],g['ink_centroid'][1]] if name=='upper' else g['lower15pct_center']
                points[name+'_endpoints']=g['ink_endpoint_left_right']
        add('face_accessory',n,points,{'present_regions':present,'center_method':'Upper paired pigment endpoint midpoint; lower bottom15% actual pigment centroid. Central philtrum gap may intentionally contain no hair.'})
    result={'schema':'male-face-final-semantics/v1','axis_x':513,'fixed_head_sha256':SHA(ACTIVE/'male/face_shape/01.png'),
            'basis':'Measurements read final candidate PNGs, independent of registration template/matrix. Current-SHA independent visual review remains required. Final04 has explicit generated-left whole-eye mechanical reflection provenance in source/eye04_paired_geometry.json.',
            'records':records}
    write_json(QA/'final_semantic_measurements.json',result)
    panel=Image.new('RGB',(1260,3*284),'white')
    for row,(n,v) in enumerate(plots):panel.paste(v,(0,row*284+25));ImageDraw.Draw(panel).text((5,row*284+5),f'candidate eyes{n:02} actual semantic corners/lid samples: upper green, lower blue, corners red',fill='black')
    panel.save(QA/'candidate_eye_semantic_samples_6x.png')
    print({'records':len(records),'file':'qa/final_semantic_measurements.json'})

if __name__=='__main__':main()
