"""One-pass premultiplied-alpha whole-layer translation; no painting or active writes."""
from pathlib import Path
import json
import hashlib
import numpy as np
from PIL import Image, ImageDraw
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,centroid,write_json,freeze,image

AXIS=513.0

def shift_horizontal(src,dx):
    arr=np.asarray(src).astype(np.float64)
    alpha=arr[:,:,3:4]/255.0
    premul=np.concatenate([arr[:,:,:3]*alpha,alpha*255],axis=2)
    sx=np.arange(arr.shape[1],dtype=float)-dx
    low=np.floor(sx).astype(int);frac=sx-low
    out=np.zeros_like(premul)
    for ii,weight in [(low,1-frac),(low+1,frac)]:
        ok=(ii>=0)&(ii<arr.shape[1])
        out[:,ok]+=premul[:,ii[ok]]*weight[None,ok,None]
    a=out[:,:,3]
    rgb=np.zeros_like(out[:,:,:3]);on=a>1e-8
    rgb[on]=out[:,:,:3][on]*255/a[on,None]
    result=np.concatenate([np.clip(np.rint(rgb),0,255),np.clip(np.rint(a[:,:,None]),0,255)],axis=2).astype(np.uint8)
    result[result[:,:,3]==0,:3]=0
    return Image.fromarray(result,'RGBA')

def current_comp(eyes=1,nose=None,mouth=None):
    out=Image.new('RGBA',(1024,1536),(231,225,214,255))
    for img in [image('face_shape'),nose if nose is not None else image('nose'),image('eyes',eyes),image('eyebrows',eyes),mouth if mouth is not None else image('mouth')]:out.alpha_composite(img)
    return out

def main():
    baseline=freeze()
    diagnosis=json.loads((QA/'geometry_diagnosis.json').read_text(encoding='utf-8'))
    positions={'nose':diagnosis['nose_mouth']['nostril_midpoint'][0], 'mouth':diagnosis['nose_mouth']['lip_dark_seam_centroid'][0]}
    final={};records=[]
    for part in ['nose','mouth']:
        dx=AXIS-positions[part]
        srcpath=PACK/'source/frozen/male'/part/'01.png'
        assert SHA(srcpath)==SHA(ACTIVE/f'male/{part}/01.png')
        src=Image.open(srcpath).convert('RGBA')
        candidate=shift_horizontal(src,dx)
        dest=PACK/f'male/{part}/01.png';dest.parent.mkdir(parents=True,exist_ok=True);candidate.save(dest)
        final[part]=candidate
        aa=np.asarray(src)[:,:,3];bb=np.asarray(candidate)[:,:,3]
        if part=='nose':
            # Same anatomical windows translated by nearest integer displacement.
            measured=[centroid(np.asarray(candidate),roi,150) for roi in [[493,434,510,451],[518,434,535,451]]]
            after_x=float(np.mean(measured,axis=0)[0])
        else:
            measured=centroid(np.asarray(candidate),[473,466,558,483],150);after_x=measured[0]
        records.append({'part':part,'source':srcpath.relative_to(PACK).as_posix(),'source_sha256':SHA(srcpath),
                        'target':dest.relative_to(PACK).as_posix(),'candidate_sha256':SHA(dest),'source_semantic_center_x':positions[part],
                        'target_axis_x':AXIS,'translation':[dx,0],'matrix':[[1,0,dx],[0,1,0]],
                        'one_pass_premultiplied_alpha_bilinear_sampling':True,'rotation_deg':0,'scale':1,
                        'alpha_mass_source':int(aa.sum()),'alpha_mass_candidate':int(bb.sum()),
                        'semantic_measure_after_x':after_x,'semantic_after_minus_axis_px':after_x-AXIS,
                        'after_measurement':measured,'RGB_repainting':False,'local_warp':False,'source_rgb_paste':False})
    crop=(375,310,655,558)
    panel=Image.new('RGB',(1120,3*526),'white')
    for row,n in enumerate([1,4,5]):
        for col,cmp in enumerate([current_comp(n),current_comp(n,final['nose'],final['mouth'])]):
            v=cmp.crop(crop).resize((560,496),Image.Resampling.NEAREST).convert('RGB');d=ImageDraw.Draw(v)
            d.line(((AXIS-375)*2,0,(AXIS-375)*2,495),fill=(0,170,130),width=1)
            panel.paste(v,(col*560,row*526+28))
            ImageDraw.Draw(panel).text((col*560+5,row*526+6),f'eyes/brows{n:02} '+('BEFORE' if col==0 else 'NOSE/MOUTH CENTERED, same old eyes/brows'),fill='black')
    panel.save(QA/'nose_mouth_centering_before_after_2x.png')
    normal=Image.new('RGB',(560,278),'white')
    normal.paste(current_comp().crop(crop).convert('RGB'),(0,26));normal.paste(current_comp(1,final['nose'],final['mouth']).crop(crop).convert('RGB'),(280,26))
    ImageDraw.Draw(normal).text((5,5),'BEFORE / centered nose+mouth (1x)',fill='black')
    normal.save(QA/'nose_mouth_centering_before_after_1x.png')
    for r in baseline['files']:assert SHA(ROOT/r['file'])==r['sha256'],r['file']
    report={'schema':'male-nose-mouth-whole-layer-centering/v1','axis_x':AXIS,'measurement_source':'qa/geometry_diagnosis.json',
            'scope':'Candidate nose01/mouth01 only; fixed head/neck, eyes,brows,hair,clothes unchanged. Candidate QA deliberately keeps original asymmetric eyes/brows pending generated replacements.',
            'method':'One full-layer horizontal fractional translation from frozen PNG using one pass premultiplied-alpha bilinear sampling. No local warp or RGB changes beyond resampling, no crop/rectangular fill.',
            'records':records,'active_67_png_and_two_configurations_preserved':True}
    write_json(PACK/'source/nose_mouth_centering.json',report)
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
