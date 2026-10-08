"""Clean and register three redrawn eyes; replace only named rejected layers."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['OMP_NUM_THREADS']='1'
from pathlib import Path
import json,hashlib,shutil,sys
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from build_ink_three_variants import save
from build_sclera_refinement_20261005 import prepare
from build_natural_eye_scale_20261005 import eye_indicators

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/ink_three_variants_20261004'
SRC=PACK/'source/eye_diversity_revision_20261006'
OUT=SRC/'candidate'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']

def compose(index,old=False,face=1,bg='#e8e3dc',brow=1,hide_front=False):
    result=Image.new('RGBA',(1024,1536))
    for part in ORDER:
        if hide_front and part=='hair_front':continue
        n=brow if part=='eyebrows' else face if part=='face_shape' else 1
        file=PACK/'male'/part/f'{n:02}.png'
        if part=='eyes':file=(SRC/'previous/male/eyes' if old else OUT)/f'{index:02}.png'
        result.alpha_composite(Image.open(file).convert('RGBA'))
    base=Image.new('RGBA',result.size,bg);base.alpha_composite(result)
    return base.convert('RGB')

def previews(specs):
    p=PACK/'preview';font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
    board=Image.new('RGB',(1440,800),'#e8e3dc');d=ImageDraw.Draw(board)
    for col,s in enumerate(specs):
        for row,old in enumerate([True,False]):
            board.paste(compose(s['index'],old).crop((365,290,668,540)).resize((450,371)),(col*480+15,row*400+30))
            d.text((col*480+16,row*400+3),s['name']+' · '+('修改前' if old else s['shape']),font=font,fill='#352f2b')
    board.save(p/'male_eye_diversity_before_after_20261006.jpg',quality=95)
    parts=Image.new('RGB',(1440,420),'#e8e3dc');pd=ImageDraw.Draw(parts)
    qa=Image.new('RGB',(1350,1380),'#e8e3dc');qd=ImageDraw.Draw(qa)
    for col,s in enumerate(specs):
        for row,bg in enumerate(['#e8e3dc','#28302d']):
            cut=Image.open(OUT/f"{s['index']:02}.png").convert('RGBA').crop((390,340,644,419))
            base=Image.new('RGBA',cut.size,bg);base.alpha_composite(cut)
            parts.paste(base.resize((450,140)).convert('RGB'),(col*480+15,row*210+48))
            pd.text((col*480+15,row*210+10),s['name']+' · 独立眼睛层',font=font,fill='#352f2b')
        for face in range(1,4):
            for row,bg in enumerate(['#e8e3dc','#28302d']):
                slot=(face-1)*2+row
                qa.paste(compose(s['index'],face=face,bg=bg,hide_front=True).crop((345,295,690,560)).resize((290,223)),(col*450+5,slot*230+3))
                qd.text((col*450+300,slot*230+20),s['name']+'\n脸型 '+str(face)+'\n无前发',font=font,fill='#352f2b')
        compose(s['index'],brow=s['index']).save(p/f"male_eye_diversity_{s['key']}_20261006.png")
    parts.save(p/'male_eye_diversity_split_20261006.jpg',quality=95)
    qa.save(p/'male_eye_diversity_qa_20261006.jpg',quality=95)

def export():
    specs=json.loads((SRC/'registration_inputs.json').read_text(encoding='utf-8'));records=[]
    for s in specs:
        a,t=prepare(s['key'],*s['pupils'],s['eye_colour_core'],source_dir=SRC,skin_feather=.025)
        save(OUT/f"{s['index']:02}.png",a)
        records.append({**s,'target_pupils':[[464,375],[569,375]],'whole_pair_uniform_transform':list(t),'no_local_warp':True,'eyebrows_unchanged':True,'eye_indicators':eye_indicators(OUT/f"{s['index']:02}.png"),'visual_approved_by_user':False})
    (SRC/'registration.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    previews(specs)
    for r in records:print(r['name'],r['eye_indicators'])
    return records

def publish(records):
    baseline=json.loads((SRC/'before_hashes.json').read_text(encoding='utf-8'))
    allowed={f"male/eyes/{r['index']:02}.png" for r in records}
    assert allowed=={'male/eyes/04.png','male/eyes/08.png','male/eyes/09.png'}
    for file,digest in baseline.items():assert hashlib.sha256((PACK/file).read_bytes()).hexdigest()==digest,file+' changed since baseline'
    for r in records:
        im=Image.open(OUT/f"{r['index']:02}.png")
        assert im.mode=='RGBA' and im.size==(1024,1536)
    for r in records:shutil.copy2(OUT/f"{r['index']:02}.png",PACK/'male/eyes'/f"{r['index']:02}.png")
    changed=[file for file,digest in baseline.items() if hashlib.sha256((PACK/file).read_bytes()).hexdigest()!=digest]
    assert set(changed)==allowed
    report={'pass':True,'replaced':changed,'unchanged_physical_parts':len(baseline)-len(changed),'preserved_all_brows':True,'preserved_skin_hair_and_first_eye':True,'preserved_names_and_selection_ordinals':True,'rgba_fullcanvas':True,'three_faces_two_backgrounds_no_front_hair_checks':18,'visual_approved_by_user':False}
    (SRC/'asset_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    f=PACK/'portrait_manifest.json';m=json.loads(f.read_text(encoding='utf-8'))
    m['eye_diversity_revision']={'date':'2026-10-06','replaced':changed,'registration':'source/eye_diversity_revision_20261006/registration.json','prompts':'source/eye_diversity_revision_20261006/selected_prompts.json','study':'docs/美术规范/鬼谷八荒眼型多样性研究_20261006.md','visual_approved_by_user':False}
    f.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Published exactly three replacement eye layers; 76 other physical parts unchanged.')

if __name__=='__main__':
    if (PACK/'source/eye_width_revision_20261006/asset_verification.json').exists():
        raise SystemExit('A newer width revision replaces eyes 04/08/09. Use build_eye_width_revision_20261006.py; the initial diversity builder is historical.')
    records=export()
    if '--publish' in sys.argv:publish(records)
