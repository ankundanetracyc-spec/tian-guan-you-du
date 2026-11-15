"""Read-only old beard alignment relative to newly centered nose/mouth."""
import json
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import PACK,QA,ROOT,ACTIVE,SHA,freeze,image,write_json
from center_nose_mouth import current_comp

def quantile(v,w,q):
    order=np.argsort(v);v=v[order];w=w[order]
    return float(v[np.searchsorted(np.cumsum(w),q*w.sum())])

def measure(a,roi):
    l,t,r,b=roi;v=a[t:b,l:r].astype(float);yy,xx=np.indices(v.shape[:2]);xx+=l;yy+=t
    weights=np.maximum(170-v[:,:,:3].mean(2),0)*v[:,:,3]/255
    on=(weights>0)&(v[:,:,3]>=16)
    weights*=on
    if weights.sum()<1:return None
    x=xx[on];y=yy[on];w=weights[on]
    xlo,xhi=quantile(x,w,.01),quantile(x,w,.99)
    ylo,yhi=quantile(y,w,.01),quantile(y,w,.99)
    def center(mask):
        ww=weights*mask;return [float((xx*ww).sum()/ww.sum()),float((yy*ww).sum()/ww.sum())]
    endpoints=[center(abs(xx-xlo)<=1),center(abs(xx-xhi)<=1)]
    low=on&(yy>=quantile(y,w,.85))
    return {'ink_centroid':center(on),'ink_endpoint_left_right':endpoints,'endpoint_midpoint_x':float((endpoints[0][0]+endpoints[1][0])/2),
            'lower15pct_center':center(low),'ink_quantiles_x_1_99':[xlo,xhi],'ink_quantiles_y_1_99':[ylo,yhi],
            'roi_half_open':roi,'pigment_weight_sum':float(weights.sum())}

def main():
    baseline=freeze();records=[]
    fixed_alpha=np.asarray(image('face_shape'))[:,:,3]
    yy,xx=np.indices(fixed_alpha.shape)
    nose=Image.open(PACK/'male/nose/01.png').convert('RGBA');mouth=Image.open(PACK/'male/mouth/01.png').convert('RGBA')
    table=Image.new('RGB',(5*430,330),'white')
    names={2:'short moustache+jaw',3:'fine moustache',4:'short chin only',5:'separate moustache+point',6:'light connected'}
    for col,n in enumerate(range(2,7)):
        beard=image('face_accessory',n);a=np.asarray(beard)
        # Connected-beard cheek strokes must not be mistaken for moustache tails.
        upper=measure(a,[462,449,576,480] if n==6 else [440,449,590,490]);lower=measure(a,[425,490,610,585])
        record={'physical_index':n,'name':names[n],'sha256':SHA(ACTIVE/f'male/face_accessory/{n:02}.png'),'upper_moustache':upper,'lower_chin_jaw':lower}
        if upper and lower:
            record['upper_lower_endpoint_midpoint_difference_x']=upper['endpoint_midpoint_x']-lower['endpoint_midpoint_x']
            record['suggested_global_translation_if_upper_centered']=513-upper['endpoint_midpoint_x']
            record['lower_bottom_after_same_translation']=lower['lower15pct_center'][0]+513-upper['endpoint_midpoint_x']
        elif upper:record['suggested_global_translation_if_upper_centered']=513-upper['endpoint_midpoint_x']
        elif lower:record['suggested_global_translation_if_lower_centered']=513-lower['lower15pct_center'][0]
        dx=record.get('suggested_global_translation_if_upper_centered',record.get('suggested_global_translation_if_lower_centered'))
        checks=[]
        for shift in [0,int(round(dx))]:
            outside=(a[:,:,3]>=64)&(np.roll(fixed_alpha,-shift,axis=1)<128)&(yy>=435)&(yy<=545)
            checks.append({'dx_integer_diagnostic':shift,'total':int(outside.sum()),'left':int(np.count_nonzero(outside&(xx<513))),
                           'right':int(np.count_nonzero(outside&(xx>=513)))})
        record['side_rim_outside_fixedface_alpha128_y435_545']=checks
        out=current_comp(1,nose,mouth);out.alpha_composite(beard)
        view=out.crop((405,435,620,585)).resize((430,300),Image.Resampling.NEAREST).convert('RGB');d=ImageDraw.Draw(view)
        d.line(((513-405)*2,0,(513-405)*2,299),fill=(0,170,130),width=1)
        for metric,color in [(upper,(225,40,40)),(lower,(40,80,230))]:
            if metric:
                for p in [metric['ink_centroid'],metric['lower15pct_center']]:
                    x,y=(p[0]-405)*2,(p[1]-435)*2;d.ellipse((x-3,y-3,x+3,y+3),outline=color,width=1)
        table.paste(view,(col*430,28));ImageDraw.Draw(table).text((col*430+4,6),f'{n:02} '+names[n],fill='black')
        view.save(QA/f'beard{n:02}_with_centered_nose_mouth_2x.png')
        records.append(record)
    table.save(QA/'beards_current_after_nose_mouth_centering.png')
    for r in baseline['files']:assert SHA(ROOT/r['file'])==r['sha256']
    report={'schema':'male-existing-beard-semantic-audit/v1','scope':'Read-only current5 nonempty beard PNGs; no edits or proposed per-pixel reconstruction.',
            'axis_x':513,'metric':'Pigment darkness below170 weighted by alpha, opacity>=16; local anatomical moustache/chin ROIs, 1/99% ink quantile endpoints and lower15% ink centroid. No whole-alpha-bbox centers.',
            'uncertainty_px':1.5,'records':records,'all67_png_two_configs_preserved':True,
            'assessment':'02/03/04/05 upper/lower pigment centers consistently lie near517, not513; whole-layer left translation3.5..4.3px is geometrically plausible and does not introduce measured side rim spill.06 upper517.476/lower-bottom518.866 both need centering, but its cheek/sideburn outline already has119px outside fixedface and integer-4 diagnostic increases this to270px. Do not blindly translate06; root should review re-extraction or a whole-layer similarity with explicit cheek fit. No beard candidate written.'}
    write_json(QA/'beard_geometry_diagnosis.json',report)
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
