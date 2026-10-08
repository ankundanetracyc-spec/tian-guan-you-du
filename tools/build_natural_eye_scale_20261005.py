"""Export normal-sized, less-exposed sclera eye edits with unchanged rig anchors."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
from pathlib import Path
import hashlib,json,shutil,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from build_ink_three_variants import save
from build_sclera_refinement_20261005 import prepare

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/ink_three_variants_20261004'
SRC=PACK/'source/natural_eye_scale_20261005'
OUT=SRC/'candidate'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
SPECS=[
 ('ningyue','凝岳',4,(431,506),(1104,506),[(260,489),(316,478),(376,478),(431,483),(501,501),(566,527),(644,567),(567,566),(487,568),(410,557),(339,538),(285,510)]),
 ('panshi','磐石',5,(441,502),(1090,502),[(284,489),(328,473),(382,474),(440,477),(504,493),(565,517),(631,563),(568,556),(488,561),(413,553),(348,535),(296,510)]),
 ('lingfeng','凌锋',6,(449,500),(1086,500),[(284,492),(338,472),(401,479),(449,486),(506,505),(564,532),(621,558),(560,558),(479,561),(413,554),(353,538),(306,514)]),
]

def compose(index,version='candidate',face=1,bg='#e8e3dc',hide_front=False):
    result=Image.new('RGBA',(1024,1536))
    for part in ORDER:
        if hide_front and part=='hair_front':continue
        n=index if part in ['eyes','eyebrows'] else face if part=='face_shape' else 1
        file=PACK/'male'/part/f'{n:02}.png'
        if part=='eyes':
            directory=OUT if version=='candidate' else SRC/version/'male/eyes'
            file=directory/f'{index:02}.png'
        result.alpha_composite(Image.open(file).convert('RGBA'))
    base=Image.new('RGBA',result.size,bg);base.alpha_composite(result)
    return base.convert('RGB')

def eye_indicators(file):
    a=np.array(Image.open(file).convert('RGBA'));rgb=a[:,:,:3].astype(int)
    opaque=(a[:,:,3]>200)&(rgb.mean(2)>80)
    opaque[:360]=False;opaque[399:]=False;opaque[:,512:]=False
    y,x=np.where(opaque)
    white=(a[:,:,3]>180)&(rgb.min(2)>165)&((rgb.max(2)-rgb.min(2))<58)
    return {'left_opaque_eye_width':int(x.max()-x.min()+1),'left_opaque_eye_height':int(y.max()-y.min()+1),
        'light_sclera_pixels':int(white[360:395,410:625].sum())}

def previews():
    preview=PACK/'preview';font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    board=Image.new('RGB',(1500,800),'#e8e3dc');d=ImageDraw.Draw(board)
    qa=Image.new('RGB',(1350,1380),'#e8e3dc');qd=ImageDraw.Draw(qa)
    parts=Image.new('RGB',(1500,370),'#e8e3dc');pd=ImageDraw.Draw(parts)
    guide=Image.new('RGB',(1500,800),'#e8e3dc');gd=ImageDraw.Draw(guide)
    for col,(key,name,index,*_) in enumerate(SPECS):
        for row,version in enumerate(['previous','candidate']):
            cut=compose(index,version).crop((375,310,660,525)).resize((450,340))
            board.paste(cut,(col*500+20,row*400+52));d.text((col*500+20,row*400+12),name+' · '+('上一版' if row==0 else '控制眼睛大小后'),font=font,fill='#352f2b')
        for row,version in enumerate(['reference','candidate']):
            guide.paste(compose(index,version).crop((375,310,660,525)).resize((450,340)),(col*500+20,row*400+52))
            gd.text((col*500+20,row*400+12),name+' · '+('原有大小基准' if row==0 else '本次修订'),font=font,fill='#352f2b')
        for face in range(1,4):
            for row,bg in enumerate(['#e8e3dc','#28302d']):
                slot=(face-1)*2+row
                qa.paste(compose(index,face=face,bg=bg,hide_front=True).crop((345,295,690,560)).resize((290,223)),(col*450+5,slot*230+3))
                qd.text((col*450+302,slot*230+20),name+'\n脸型 '+str(face)+'\n无前发',font=font,fill='#352f2b')
        for row,bg in enumerate(['#e8e3dc','#28302d']):
            cut=Image.open(OUT/f'{index:02}.png').convert('RGBA').crop((405,355,630,405))
            base=Image.new('RGBA',cut.size,bg);base.alpha_composite(cut)
            parts.paste(base.resize((480,106)).convert('RGB'),(col*500+10,row*175+53))
            pd.text((col*500+15,row*175+12),name+' · 独立眼睛层',font=font,fill='#352f2b')
        compose(index).save(preview/f'male_natural_eye_{key}_20261005.png')
    board.save(preview/'male_natural_eye_before_after_20261005.jpg',quality=95)
    guide.save(preview/'male_natural_eye_reference_20261005.jpg',quality=95)
    qa.save(preview/'male_natural_eye_qa_20261005.jpg',quality=95)
    parts.save(preview/'male_natural_eye_split_20261005.jpg',quality=95)

def export():
    records=[]
    for key,name,index,left,right,outline in SPECS:
        a,t=prepare(key,left,right,outline,source_dir=SRC);save(OUT/f'{index:02}.png',a)
        metrics={version:eye_indicators((OUT if version=='candidate' else SRC/version/'male/eyes')/f'{index:02}.png') for version in ['reference','previous','candidate']}
        records.append({'name':name,'index':index,'source':f'{key}_eyes.png','source_sha256':hashlib.sha256((SRC/f'{key}_eyes.png').read_bytes()).hexdigest(),
            'source_pupils':[left,right],'target_pupils':[[464,375],[569,375]],'whole_pair_uniform_transform':list(t),
            'organ_colour_mask_only':outline,'lashes_not_clipped':True,'metrics':metrics,'visual_approved_by_user':False})
    (SRC/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    previews()
    for record in records:print(record['name'],record['metrics'])

def publish():
    assert len(SPECS)==3,'All three sources must be registered before publishing.'
    normal_width_limit=max(eye_indicators(SRC/'reference/male/eyes'/f'{index:02}.png')['left_opaque_eye_width'] for _,_,index,*_ in SPECS)
    for _,name,index,*_ in SPECS:
        reference=eye_indicators(SRC/'reference/male/eyes'/f'{index:02}.png')
        previous=eye_indicators(SRC/'previous/male/eyes'/f'{index:02}.png')
        candidate=eye_indicators(OUT/f'{index:02}.png')
        # Opaque colour bounds include eyelid strokes, so allow three pixels of
        # per-style variation while also enforcing the original family's limit.
        # These are comparison indicators, not anatomical eye segmentation.
        assert candidate['left_opaque_eye_width']<=min(reference['left_opaque_eye_width']+3,normal_width_limit),f'{name}: eye width exceeds normal-size references'
        assert candidate['left_opaque_eye_height']<=reference['left_opaque_eye_height'],f'{name}: eye height exceeds reference'
        assert candidate['left_opaque_eye_width']<previous['left_opaque_eye_width'] and candidate['left_opaque_eye_height']<previous['left_opaque_eye_height'],f'{name}: eyes have not become smaller than the previous revision'
        assert candidate['light_sclera_pixels']<reference['light_sclera_pixels']*.95,f'{name}: exposed-white indicator has not decreased enough'
        assert candidate['light_sclera_pixels']<=previous['light_sclera_pixels'],f'{name}: exposed-white indicator exceeds previous revision'
    for _,_,index,*_ in SPECS:shutil.copy2(OUT/f'{index:02}.png',PACK/'male/eyes'/f'{index:02}.png')
    file=PACK/'portrait_manifest.json';manifest=json.loads(file.read_text(encoding='utf-8'))
    manifest['natural_eye_revision']='source/natural_eye_scale_20261005/registration.json'
    manifest['masculine_revision_status']='natural eye size, reduced sclera and short lashes; separate eye/brow layers; awaiting user visual review'
    manifest['visual_approved_by_user']=False;file.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Published three normal-sized eye layers; brows and rig unchanged.')

if __name__=='__main__':
    if '--publish' in sys.argv and (PACK/'source/eye_diversity_revision_20261006/registration.json').exists():
        raise SystemExit('A newer eye diversity revision replaces eye 04. Use build_eye_diversity_revision_20261006.py; this size-reduction stage is historical.')
    export()
    if '--publish' in sys.argv:publish()
