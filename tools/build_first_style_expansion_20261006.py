"""Register three separately painted male eye/brow variants to the first style.

Only continuous-alpha cleanup, global pigment calibration, and one uniform
transform per complete pair are used. No procedural drawing or local warping.
"""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
from pathlib import Path
import hashlib,json,shutil,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from build_ink_three_variants import read,save,place,paired_transform
from build_soft_masculine_ink_revision import isolated
from build_sclera_refinement_20261005 import prepare
from build_natural_eye_scale_20261005 import eye_indicators

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/ink_three_variants_20261004'
SRC=PACK/'source/style_expansion_20261006'
OUT=SRC/'candidate_first'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']

def brows(key):
    a=isolated(read(SRC/f'{key}_first_eyebrows.png'))
    h,w=a.shape[:2];yy,xx=np.indices((h,w));weight=(a[:,:,3]/255)**3
    weight[a[:,:,:3].mean(2)>160]=0
    centres=[]
    for lo,hi in [(0,w//2),(w//2,w)]:
        z=weight[:,lo:hi]
        centres.append(((xx[:,lo:hi]*z).sum()/z.sum(),(yy[:,lo:hi]*z).sum()/z.sum()))
    ref=read(PACK/'male/eyebrows/01.png')
    rsel=(ref[:,:,3]>180)&(ref[:,:,:3].mean(2)<150)
    sel=(a[:,:,3]>180)&(a[:,:,:3].mean(2)<150)
    source_q=np.percentile(a[:,:,:3][sel],[10,50,90],axis=0)
    target_q=np.percentile(ref[:,:,:3][rsel],[10,50,90],axis=0)
    # Transfer the painted fibre contrast into alpha when unmixing the raw
    # opaque grey underpaint. Merely darkening RGB would flatten all the hairs.
    pigment=target_q[1]
    paper=np.array([238,218,202],np.float32)
    coverage=np.clip(np.mean((paper-a[:,:,:3])/(paper-pigment),axis=2),0,1)
    a[:,:,3]*=coverage
    alpha_scale=float(ref[:,:,3].max()/a[:,:,3].max())
    a[:,:,3]*=alpha_scale
    a[:,:,:3]=pigment
    transform=paired_transform(*centres,(456,346),(579,345))
    return place(a,transform),{'source_centres':centres,'target_centres':[[456,346],[579,345]],'transform':list(transform),'global_alpha_scale':alpha_scale,'source_pigment_quantiles':source_q.tolist(),'first_style_pigment_quantiles':target_q.tolist(),'painted_fibre_contrast_preserved_in_alpha':True}

def layer(part,index):
    file=OUT/part/f'{index:02}.png'
    if not file.exists():file=PACK/'male'/part/f'{index:02}.png'
    return Image.open(file).convert('RGBA')

def compose(index,face=1,bg='#e8e3dc',hide_front=False,brow=None,eye=None):
    result=Image.new('RGBA',(1024,1536))
    for part in ORDER:
        if hide_front and part=='hair_front':continue
        n=(brow or index) if part=='eyebrows' else (eye or index) if part=='eyes' else face if part=='face_shape' else 1
        result.alpha_composite(layer(part,n))
    base=Image.new('RGBA',result.size,bg);base.alpha_composite(result)
    return base.convert('RGB')

def previews(specs):
    p=PACK/'preview';font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',23)
    comparison=Image.new('RGB',(1600,520),'#e8e3dc');d=ImageDraw.Draw(comparison)
    for col,(index,title) in enumerate([(1,'第一款 · 画风母版')]+[(s['index'],s['name']) for s in specs]):
        comparison.paste(compose(index).crop((365,290,668,605)).resize((380,395)),(col*400+10,58))
        d.text((col*400+12,12),title,font=font,fill='#352f2b')
    comparison.save(p/'male_first_style_comparison_20261006.jpg',quality=95)
    parts=Image.new('RGB',(1600,560),'#e8e3dc');pd=ImageDraw.Draw(parts)
    for col,(index,title) in enumerate([(1,'第一款')]+[(s['index'],s['name']) for s in specs]):
        for row,bg in enumerate(['#e8e3dc','#28302d']):
            pd.text((col*400+10,row*280+8),title+' · 眉 / 眼',font=font,fill='#352f2b' if row==0 else '#e8e3dc')
            for n,part in enumerate(['eyebrows','eyes']):
                cut=layer(part,index).crop((400,325 if part=='eyebrows' else 355,637,365 if part=='eyebrows' else 401))
                base=Image.new('RGBA',cut.size,bg);base.alpha_composite(cut)
                parts.paste(base.resize((380,round(cut.height*380/cut.width))).convert('RGB'),(col*400+10,row*280+45+n*100))
    parts.save(p/'male_first_style_split_20261006.jpg',quality=95)
    qa=Image.new('RGB',(1350,1380),'#e8e3dc');qd=ImageDraw.Draw(qa)
    for col,s in enumerate(specs):
        for face in range(1,4):
            for row,bg in enumerate(['#e8e3dc','#28302d']):
                slot=(face-1)*2+row
                qa.paste(compose(s['index'],face,bg,True).crop((345,295,690,560)).resize((290,223)),(col*450+5,slot*230+3))
                qd.text((col*450+302,slot*230+20),s['name']+'\n脸型 '+str(face)+'\n无前发',font=font,fill='#352f2b')
        compose(s['index']).save(p/f"male_first_style_{s['key']}_20261006.png")
    qa.save(p/'male_first_style_qa_20261006.jpg',quality=95)

def export():
    specs=json.loads((SRC/'first_style_registration_inputs.json').read_text(encoding='utf-8'))
    records=[]
    for s in specs:
        key=s['key'];index=s['index']
        b,bt=brows(key);save(OUT/'eyebrows'/f'{index:02}.png',b)
        e,et=prepare(key+'_first',s['pupils'][0],s['pupils'][1],s['eye_colour_core'],source_dir=SRC)
        save(OUT/'eyes'/f'{index:02}.png',e)
        records.append({**s,'eye_transform':list(et),'target_pupils':[[464,375],[569,375]],'brow_registration':bt,'eye_indicators':eye_indicators(OUT/'eyes'/f'{index:02}.png'),'separate_part_sources':True,'no_per_eye_warp':True,'lashes_not_alpha_clipped_by_colour_core':True,'style_master':'male/eyebrows/01.png and male/eyes/01.png','visual_approved_by_user':False})
    (SRC/'first_style_registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    previews(specs)
    for record in records:print(record['name'],record['eye_indicators'])
    return records

def publish(records):
    baseline=json.loads((SRC/'before_hashes.json').read_text(encoding='utf-8'))
    for file,digest in baseline.items():assert hashlib.sha256((PACK/file).read_bytes()).hexdigest()==digest,file+' changed since source baseline'
    for r in records:
        m=r['eye_indicators']
        assert m['left_opaque_eye_width']<=71 and m['left_opaque_eye_height']<=19,r['name']+' exceeds first-eye size range'
        for part in ['eyes','eyebrows']:
            dst=PACK/'male'/part/f"{r['index']:02}.png"
            assert not dst.exists(),'New index must not overwrite an existing part'
    for r in records:
        for part in ['eyes','eyebrows']:shutil.copy2(OUT/part/f"{r['index']:02}.png",PACK/'male'/part/f"{r['index']:02}.png")
    configfile=PACK/'appearance_parts.json';config=json.loads(configfile.read_text(encoding='utf-8'))
    names={'male_eyebrows':['温玉柔眉','长风平眉','苍山厚眉'],'male_eyes':['温玉和目','长风清目','苍山静目']}
    for c in config['components']:
        if c['id'] in names:
            c['variant_names']+=names[c['id']];c['variant_count']+=3
            if c['id']=='male_eyes':c['variant_indices'] += [7,8,9]
    config['rig_id']='ink_three_variants_20261004_active76'
    configfile.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    f=PACK/'portrait_manifest.json';m=json.loads(f.read_text(encoding='utf-8'))
    m['style_expansion']={'date':'2026-10-06','style_master':['male/eyebrows/01.png','male/eyes/01.png'],'registration':'source/style_expansion_20261006/first_style_registration.json','prompts':'source/style_expansion_20261006/first_style_jobs.json','new_files':[f"male/{part}/{r['index']:02}.png" for r in records for part in ['eyes','eyebrows']],'visual_approved_by_user':False}
    m['active_variant_count']=sum(c['variant_count'] for c in config['components']);m['visual_approved_by_user']=False
    f.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Published six new parts, preserving all original assets and saved ordinals.')

if __name__=='__main__':
    if (PACK/'source/eye_diversity_revision_20261006/asset_verification.json').exists():
        raise SystemExit('Eyes 08 and 09 have a newer diversity revision. This initial expansion builder is historical; use build_eye_diversity_revision_20261006.py.')
    records=export()
    if '--publish' in sys.argv:publish(records)
