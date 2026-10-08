"""Register three imagegen eye edits without changing brows or portrait anchors."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
from pathlib import Path
import hashlib,json,shutil,sys
import numpy as np
import cv2
cv2.setNumThreads(1)
from PIL import Image,ImageDraw,ImageFont
from build_ink_three_variants import read,save,place,paired_transform
from build_soft_masculine_ink_revision import isolated

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/ink_three_variants_20261004'
SRC=PACK/'source/sclera_refinement_20261005'
OUT=SRC/'candidate'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
SPECS=[
 ('ningyue','凝岳',4,(280,762),(744,762),[(135,750),(179,743),(224,739),(280,742),(336,757),(399,787),(441,811),(375,809),(302,810),(235,802),(174,786),(149,766)]),
 ('panshi','磐石',5,(439,475),(1095,475),[(232,470),(300,451),(367,453),(438,459),(507,482),(574,517),(650,556),(579,548),(507,554),(442,551),(361,536),(283,510),(250,484)]),
 ('lingfeng','凌锋',6,(449,485),(1088,485),[(248,489),(307,467),(381,465),(449,472),(514,495),(579,535),(640,575),(566,574),(484,577),(421,565),(343,546),(281,521)]),
]

def prepare(key,left,right,outline,source_dir=None,skin_feather=None):
    a=isolated(read((source_dir or SRC)/f'{key}_eyes.png'));h,w=a.shape[:2]
    organ=Image.new('L',(w,h));draw=ImageDraw.Draw(organ)
    draw.polygon(outline,fill=255)
    draw.polygon([(left[0]+right[0]-x,y) for x,y in outline],fill=255)
    core=np.array(organ)>0
    rgb=a[:,:,:3];alpha=a[:,:,3]/255
    pale=(~core)&(rgb.mean(2)>130)&(a[:,:,3]>0)
    base=np.array([238,218,202],np.float32)
    coverage=np.clip(np.mean((base-rgb)/[173,165,155],axis=2),0,1)
    pigment_alpha=np.minimum(alpha,coverage)
    foreground=(rgb-(1-pigment_alpha[:,:,None])*base)/np.maximum(pigment_alpha[:,:,None],.01)
    rgb[pale]=np.clip(foreground[pale],0,255)
    a[:,:,3][pale]=pigment_alpha[pale]*255*.85
    if skin_feather is not None:
        # Optional matte cleanup for new sources with a larger pale underpaint.
        # Colour core protects the eye organ; dark short lashes retain alpha.
        distance=cv2.distanceTransform((~core).astype(np.uint8),cv2.DIST_L2,3)
        falloff=np.exp(-(distance/(w*skin_feather))**2)
        a[:,:,3][pale]*=falloff[pale]
    rgb[core&(rgb.min(2)>220)]*=.90
    transform=paired_transform(left,right,(464,375),(569,375))
    return place(a,transform),transform

def compose(index,old=False,face=1,bg='#e8e3dc',hide_front=False):
    result=Image.new('RGBA',(1024,1536))
    for part in ORDER:
        if hide_front and part=='hair_front':continue
        n=index if part in ['eyes','eyebrows'] else face if part=='face_shape' else 1
        file=PACK/'male'/part/f'{n:02}.png'
        if part=='eyes':file=(SRC/'previous/male/eyes' if old else OUT)/f'{index:02}.png'
        result.alpha_composite(Image.open(file).convert('RGBA'))
    base=Image.new('RGBA',result.size,bg);base.alpha_composite(result)
    return base.convert('RGB')

def previews():
    preview=PACK/'preview';font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    board=Image.new('RGB',(1500,800),'#e8e3dc');d=ImageDraw.Draw(board)
    for col,(_,name,index,*_) in enumerate(SPECS):
        for row,old in enumerate([True,False]):
            cut=compose(index,old).crop((375,310,660,525)).resize((450,340))
            board.paste(cut,(col*500+20,row*400+52))
            d.text((col*500+20,row*400+12),name+' · '+('修改前' if old else '减少露白后'),font=font,fill='#352f2b')
    board.save(preview/'male_sclera_before_after_20261005.jpg',quality=95)
    full=Image.new('RGB',(1500,660),'#e8e3dc');fd=ImageDraw.Draw(full)
    parts=Image.new('RGB',(1500,370),'#e8e3dc');pd=ImageDraw.Draw(parts)
    qa=Image.new('RGB',(1350,1380),'#e8e3dc');qd=ImageDraw.Draw(qa)
    for col,(key,name,index,*_) in enumerate(SPECS):
        full.paste(compose(index).crop((335,250,700,690)).resize((465,560)),(col*500+15,65))
        fd.text((col*500+20,18),name,font=font,fill='#352f2b')
        for row,bg in enumerate(['#e8e3dc','#28302d']):
            cut=Image.open(OUT/f'{index:02}.png').convert('RGBA').crop((405,355,630,405))
            base=Image.new('RGBA',cut.size,bg);base.alpha_composite(cut)
            parts.paste(base.resize((480,106)).convert('RGB'),(col*500+10,row*175+53))
            pd.text((col*500+15,row*175+12),name+' · 独立眼睛层',font=font,fill='#352f2b' if row==0 else '#352f2b')
        for face in range(1,4):
            for row,bg in enumerate(['#e8e3dc','#28302d']):
                slot=(face-1)*2+row
                qa.paste(compose(index,face=face,bg=bg,hide_front=True).crop((345,295,690,560)).resize((290,223)),(col*450+5,slot*230+3))
                qd.text((col*450+302,slot*230+20),name+'\n脸型 '+str(face)+'\n无前发',font=font,fill='#352f2b')
        compose(index).save(preview/f'male_sclera_{key}_20261005.png')
    full.save(preview/'male_sclera_three_20261005.jpg',quality=95)
    parts.save(preview/'male_sclera_split_20261005.jpg',quality=95)
    qa.save(preview/'male_sclera_qa_20261005.jpg',quality=95)

def export():
    records=[]
    for key,name,index,left,right,outline in SPECS:
        a,t=prepare(key,left,right,outline);save(OUT/f'{index:02}.png',a)
        records.append({'name':name,'index':index,'source':f'{key}_eyes.png','sha256':hashlib.sha256((SRC/f'{key}_eyes.png').read_bytes()).hexdigest(),
            'source_pupils':[left,right],'target_pupils':[[464,375],[569,375]],'whole_pair_uniform_transform':list(t),
            'organ_colour_mask_only':outline,'lashes_not_clipped_by_contour':True,'eyebrows_separate_and_unchanged':True,'visual_approved_by_user':False})
    (SRC/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    previews()

def publish():
    for _,_,index,*_ in SPECS:shutil.copy2(OUT/f'{index:02}.png',PACK/'male/eyes'/f'{index:02}.png')
    file=PACK/'portrait_manifest.json';manifest=json.loads(file.read_text(encoding='utf-8'))
    manifest['sclera_revision']='source/sclera_refinement_20261005/registration.json'
    manifest['masculine_revision_status']='soft brows, short lashes and reduced sclera; eyes/brows separate; awaiting user visual review'
    manifest['visual_approved_by_user']=False
    file.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Published three eye layers; brows, names and saved indexes unchanged.')

if __name__=='__main__':
    if '--publish' in sys.argv and (PACK/'source/natural_eye_scale_20261005/registration.json').exists():
        raise SystemExit('A newer revision controls both eye size and sclera. Use build_natural_eye_scale_20261005.py; do not republish enlarged eyes.')
    export()
    if '--publish' in sys.argv:publish()
