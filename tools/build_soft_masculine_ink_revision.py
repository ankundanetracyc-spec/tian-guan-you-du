"""Register separate soft brows/eyes; preserve lashes outside the eyeball."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
from pathlib import Path
import json, hashlib, shutil, sys
import numpy as np
import cv2
cv2.setNumThreads(1)
from PIL import Image, ImageDraw, ImageFont
from build_ink_three_variants import read, save, place, paired_transform, hair_tone
ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/ink_three_variants_20261004'
SRC=PACK/'source/masculine_soft_revision_20261005'
OUT=SRC/'candidate'
SPECS=[
 ('ningyue','凝岳',4,(296,758),(732,758),[(129,740),(182,733),(240,735),(296,739),(350,756),(395,786),(411,802),(364,803),(299,805),(249,800),(196,786),(158,763)]),
 ('panshi','磐石',5,(436,502),(1102,502),[(229,484),(309,471),(379,473),(440,479),(504,500),(554,537),(610,568),(540,568),(475,573),(407,570),(341,552),(277,528),(245,501)]),
 ('lingfeng','凌锋',6,(448,506),(1088,506),[(245,496),(308,479),(379,477),(444,482),(502,505),(556,538),(615,575),(542,578),(472,579),(412,571),(342,552),(280,528)]),
]
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']

def isolated(raw):
    a=raw.copy();a[a[:,:,3]<3]=0
    n,labels,stats,_=cv2.connectedComponentsWithStats((a[:,:,3]>28).astype(np.uint8))
    largest=sorted(range(1,n),key=lambda i:stats[i,4],reverse=True)[:2]
    envelope=np.zeros(a.shape[:2],np.uint8)
    for i in largest:
        x,y,w,h,area=stats[i];pad=round(a.shape[1]*.02)
        envelope[max(0,y-pad):min(a.shape[0],y+h+pad),max(0,x-pad):min(a.shape[1],x+w+pad)]=1
    a[envelope==0]=0
    return a

def export():
    records=[]
    for key,name,index,left,right,outline in SPECS:
        brows=hair_tone(isolated(read(SRC/f'{key}_eyebrows.png')))
        h,w=brows.shape[:2];yy,xx=np.indices((h,w));weight=(brows[:,:,3]/255)**3
        weight[brows[:,:,:3].mean(2)>160]=0
        centers=[]
        for lo,hi in [(0,w//2),(w//2,w)]:
            z=weight[:,lo:hi];centers.append(((xx[:,lo:hi]*z).sum()/z.sum(),(yy[:,lo:hi]*z).sum()/z.sum()))
        bt=paired_transform(*centers,(456,346),(579,345))
        save(OUT/'male/eyebrows'/f'{index:02}.png',place(brows,bt))
        eyes=isolated(read(SRC/f'{key}_eyes.png'));h,w=eyes.shape[:2]
        organ=Image.new('L',(w,h));d=ImageDraw.Draw(organ);d.polygon(outline,fill=255)
        d.polygon([(left[0]+right[0]-x,y) for x,y in outline],fill=255);core=np.array(organ)>0
        # Contour protects organ colour only. All dark lashes and lid strokes
        # outside it retain their alpha, instead of being clipped by a polygon.
        rgb=eyes[:,:,:3];alpha=eyes[:,:,3]/255
        pale=(~core)&(rgb.mean(2)>130)&(eyes[:,:,3]>0)
        base=np.array([238,218,202],np.float32)
        coverage=np.clip(np.mean((base-rgb)/[173,165,155],axis=2),0,1)
        pigment_alpha=np.minimum(alpha,coverage)
        fg=(rgb-(1-pigment_alpha[:,:,None])*base)/np.maximum(pigment_alpha[:,:,None],.01)
        rgb[pale]=np.clip(fg[pale],0,255);eyes[:,:,3][pale]=pigment_alpha[pale]*255*.85
        bright=core&(rgb.min(2)>220);rgb[bright]*=.90
        et=paired_transform(left,right,(464,375),(569,375))
        save(OUT/'male/eyes'/f'{index:02}.png',place(eyes,et))
        records.append({'name':name,'index':index,'eye_source':f'{key}_eyes.png','brow_source':f'{key}_eyebrows.png',
            'eye_source_sha256':hashlib.sha256((SRC/f'{key}_eyes.png').read_bytes()).hexdigest(),
            'brow_source_sha256':hashlib.sha256((SRC/f'{key}_eyebrows.png').read_bytes()).hexdigest(),
            'eye_transform':list(et),'brow_transform':list(bt),'source_pupils':[left,right],
            'target_pupils':[[464,375],[569,375]],'brow_target':[[456,346],[579,345]],
            'no_alpha_crop_by_eyeball_contour':True,'separately_painted_parts':True,
            'source_organ_colour_mask':outline,'visual_approved_by_user':False})
    (SRC/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    previews()

def layer(part,v):
    file=OUT/'male'/part/f'{v:02}.png'
    if not file.exists():file=PACK/'male'/part/f'{v:02}.png'
    return Image.open(file).convert('RGBA')

def compose(v,face=1,hair=1,background='#e8e3dc',hide_front=False):
    im=Image.new('RGBA',(1024,1536))
    for part in ORDER:
        if part=='hair_front' and hide_front:continue
        n=v if part in ['eyes','eyebrows'] else face if part=='face_shape' else hair if part.startswith('hair') else 1
        im.alpha_composite(layer(part,n))
    bg=Image.new('RGBA',im.size,background);bg.alpha_composite(im);return bg.convert('RGB')

def previews():
    preview=PACK/'preview';preview.mkdir(exist_ok=True)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
    board=Image.new('RGB',(1600,520),'#e8e3dc');d=ImageDraw.Draw(board)
    for col,(v,title) in enumerate([(1,'原有眉眼'),(4,'凝岳 · 柔和剑眉'),(5,'磐石 · 自然浓眉'),(6,'凌锋 · 微扬眉眼')]):
        board.paste(compose(v).crop((335,260,700,660)).resize((390,428)),(col*400+5,58))
        d.text((col*400+12,12),title,font=font,fill='#352f2b')
    board.save(preview/'male_soft_revision_20261005.jpg',quality=95)
    qa=Image.new('RGB',(1500,1500),'#e8e3dc');qd=ImageDraw.Draw(qa)
    for col,(key,name,v,*_) in enumerate(SPECS):
        for face in range(1,4):
            for row,bg in enumerate(['#e8e3dc','#28302d']):
                slot=(face-1)*2+row
                qa.paste(compose(v,face,face,bg,True).crop((330,280,700,575)).resize((295,235)),(col*500+10,slot*245+8))
                qd.text((col*500+310,slot*245+22),f'{name}\n脸型 {face}\n无前发\n'+('浅底' if row==0 else '深底'),font=font,fill='#352f2b')
        compose(v).save(preview/f'male_soft_{key}_20261005.png')
    qa.save(preview/'male_soft_revision_qa_20261005.jpg',quality=95)
    parts=Image.new('RGB',(1200,630),'#e8e3dc');pd=ImageDraw.Draw(parts)
    for col,(_,name,v,*_) in enumerate(SPECS):
        for row,bg in enumerate(['#e8e3dc','#28302d']):
            for i,part in enumerate(['eyebrows','eyes']):
                im=layer(part,v).crop((380,320,655,410));cut=Image.new('RGBA',im.size,bg);cut.alpha_composite(im)
                parts.paste(cut.resize((390,128)).convert('RGB'),(col*400+5,row*310+i*137+35))
            pd.text((col*400+10,row*310+3),name+' · 眉毛 / 眼睛',font=font,fill='#352f2b' if row==0 else '#f0e8dd')
    parts.save(preview/'male_soft_split_parts_20261005.jpg',quality=95)

def publish():
    for _,_,index,*_ in SPECS:
        for part in ['eyebrows','eyes']:shutil.copy2(OUT/'male'/part/f'{index:02}.png',PACK/'male'/part/f'{index:02}.png')
    p=PACK/'portrait_manifest.json';m=json.loads(p.read_text(encoding='utf-8'))
    m['masculine_revision']='source/masculine_soft_revision_20261005/registration.json'
    m['masculine_revision_status']='soft curves and full short lashes; eyes and brows separate; awaiting user visual review'
    m['visual_approved_by_user']=False;p.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Published six replacements; names and saved indexes unchanged.')

if __name__=='__main__':
    if '--publish' in sys.argv and (PACK/'source/sclera_refinement_20261005/registration.json').exists():
        raise SystemExit('Eyes have a later sclera refinement. Use build_sclera_refinement_20261005.py; do not republish these earlier eye drawings.')
    export()
    if '--publish' in sys.argv:publish()
