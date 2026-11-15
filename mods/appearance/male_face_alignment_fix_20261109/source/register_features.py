"""Whole-pair similarity registration from explicit current semantic controls.

Reads source/feature_registration.json, writes package candidates and QA only.
Generated pixels remain generated pixels: never paste original RGB or paint locally.
"""
from pathlib import Path
import argparse
import math
import json
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,write_json,freeze,image

def fit_similarity(source,target):
    source=np.asarray(source,dtype=float);target=np.asarray(target,dtype=float)
    assert source.shape==target.shape and source.shape[1]==2 and len(source)>=2
    rows=[];values=[]
    for (x,y),(u,v) in zip(source,target):
        rows.extend([[x,-y,1,0],[y,x,0,1]]);values.extend([u,v])
    a,b,tx,ty=np.linalg.lstsq(np.asarray(rows),np.asarray(values),rcond=None)[0]
    mapped=np.column_stack([a*source[:,0]-b*source[:,1]+tx,b*source[:,0]+a*source[:,1]+ty])
    return [float(a),float(b),float(tx),float(ty)],np.linalg.norm(mapped-target,axis=1).tolist()

def sample(src,transform):
    a,b,tx,ty=transform
    src=np.asarray(src).astype(float)
    alpha=src[:,:,3:4]/255
    rgba=np.concatenate([src[:,:,:3]*alpha,alpha*255],axis=2)
    yy,xx=np.indices((1536,1024),dtype=float)
    determinant=a*a+b*b
    sx=(a*(xx-tx)+b*(yy-ty))/determinant
    sy=(-b*(xx-tx)+a*(yy-ty))/determinant
    xi=np.floor(sx).astype(int);yi=np.floor(sy).astype(int);fx=sx-xi;fy=sy-yi
    out=np.zeros((1536,1024,4),dtype=float)
    for ox,oy,w in [(0,0,(1-fx)*(1-fy)),(1,0,fx*(1-fy)),(0,1,(1-fx)*fy),(1,1,fx*fy)]:
        px=xi+ox;py=yi+oy;ok=(px>=0)&(px<src.shape[1])&(py>=0)&(py<src.shape[0])
        out[ok]+=rgba[py[ok],px[ok]]*w[ok,None]
    aa=out[:,:,3];rgb=np.zeros_like(out[:,:,:3]);ok=aa>1e-8
    rgb[ok]=out[:,:,:3][ok]*255/aa[ok,None]
    result=np.concatenate([np.clip(np.rint(rgb),0,255),np.clip(np.rint(aa[:,:,None]),0,255)],axis=2).astype(np.uint8)
    result[result[:,:,3]==0,:3]=0
    return Image.fromarray(result,'RGBA')

def load_nose_mouth(part):
    path=PACK/f'male/{part}/01.png'
    return Image.open(path).convert('RGBA') if path.exists() else image(part)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--config',default='source/feature_registration.json');parser.add_argument('--only',nargs='*',default=[]);args=parser.parse_args()
    baseline=freeze();config=json.loads((PACK/args.config).read_text(encoding='utf-8-sig'))
    records=[];staged={}
    for rec in config['records']:
        if args.only and f"{rec['part']}:{rec['index']}" not in args.only:continue
        assert rec['part'] in ['eyes','eyebrows'] and rec['index'] in [1,4,5]
        if rec.get('placeholder'):raise ValueError('Controls remain placeholders')
        srcpath=Path(rec['native']);srcpath=srcpath if srcpath.is_absolute() else PACK/srcpath
        src=Image.open(srcpath).convert('RGBA')
        if 'native_points' in rec:transform,residuals=fit_similarity(rec['native_points'],rec['target_points'])
        else:transform=rec['similarity'];residuals=[]
        candidate=sample(src,transform)
        dest=PACK/f"male/{rec['part']}/{rec['index']:02}.png";dest.parent.mkdir(parents=True,exist_ok=True);candidate.save(dest)
        staged[(rec['part'],rec['index'])]=candidate
        a,b,tx,ty=transform
        records.append({**rec,'native_sha256':SHA(srcpath),'native_size':list(src.size),'candidate':dest.relative_to(PACK).as_posix(),
                        'sha256':SHA(dest),'native_to_frame':{'a':a,'b':b,'tx':tx,'ty':ty,'scale':math.hypot(a,b),'rotation_deg':math.degrees(math.atan2(b,a))},
                        'control_residuals_px':residuals,'maximum_control_residual_px':max(residuals) if residuals else None,
                        'one_pass_premultiplied_alpha_bilinear_sampling':True,'local_warps':False,'source_RGB_paste':False})
    # Diagnostic assembled crops. They intentionally expose forehead and ears.
    for n in [1,4,5]:
        for label,color in [('light',(231,225,214,255)),('dark',(38,43,43,255))]:
            out=Image.new('RGBA',(1024,1536),color)
            def feature(part):
                if (part,n) in staged:return staged[(part,n)]
                existing=PACK/f'male/{part}/{n:02}.png'
                return Image.open(existing).convert('RGBA') if existing.exists() else image(part,n)
            for img in [image('face_shape'),load_nose_mouth('nose'),feature('eyes'),feature('eyebrows'),load_nose_mouth('mouth')]:out.alpha_composite(img)
            out.crop((350,245,680,570)).convert('RGB').save(QA/f'candidate_face_{n:02}_{label}_1x.png')
            out.crop((350,245,680,570)).resize((660,650),Image.Resampling.NEAREST).convert('RGB').save(QA/f'candidate_face_{n:02}_{label}_2x.png')
    for r in baseline['files']:assert SHA(ROOT/r['file'])==r['sha256'],r['file']
    if args.only and (PACK/'source/feature_registration_report.json').exists():
        previous=json.loads((PACK/'source/feature_registration_report.json').read_text(encoding='utf-8-sig'))
        current={(r['part'],r['index']):r for r in previous['records']}
        current.update({(r['part'],r['index']):r for r in records})
        records=[current[(r['part'],r['index'])] for r in config['records']]
    report={'schema':'male-face-whole-pair-registration/v1','scope':'Only generated eye/brow layers; active PNG/configuration unchanged','records':records,'baseline_preserved':True}
    write_json(PACK/'source/feature_registration_report.json',report)
    print(json.dumps({'staged':len(records),'baseline_preserved':True}))

if __name__=='__main__':main()
