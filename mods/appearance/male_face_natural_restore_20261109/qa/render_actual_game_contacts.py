"""Only QA outputs, from existing real game composite captures, no face resize."""
from pathlib import Path
import hashlib,json
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[4]
PACK=Path(__file__).resolve().parents[1]
QA=PACK/'qa'
GAME=PACK/'installation'
OLD=ROOT/'mods/appearance/male_face_alignment_fix_20261109/installation'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
CROP=(350,295,680,560)
PAPER=(231,225,214)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def load(path):
    im=Image.open(path).convert('RGBA')
    assert im.size==(1024,1536),(str(path),im.size)
    bg=Image.new('RGBA',im.size,PAPER+(255,));bg.alpha_composite(im)
    return bg.convert('RGB')

def main():
    sources=[]
    def record(path,role):
        sources.append({'role':role,'path':str(path.resolve()),'project_relative_path':path.relative_to(ROOT).as_posix(),'sha256':sha(path)})
    grid=Image.new('RGB',(990,3*300),PAPER)
    for row,brow in enumerate([1,4,5]):
        for col,eye in enumerate([1,4,5]):
            p=GAME/f'game_natural_bald_b{brow:02}_e{eye:02}_composite.png'
            record(p,f'current actual bald brow{brow:02} eye{eye:02}')
            face=load(p).crop(CROP)
            grid.paste(face,(col*330,row*300+32))
            ImageDraw.Draw(grid).text((col*330+8,row*300+4),f'实机：眉{brow:02} · 眼{eye:02}',font=FONT,fill=(40,45,40))
    grid.save(QA/'game_natural_nine_faces_contact_1x.png')
    whole=Image.new('RGB',(3072,1576),PAPER)
    for col,n in enumerate([1,4,5]):
        p=GAME/f'game_natural_whole_{n:02}_composite.png';record(p,f'current actual whole{n:02}')
        whole.paste(load(p),(col*1024,40))
        ImageDraw.Draw(whole).text((col*1024+12,8),f'当前实机整稿 {n:02}',font=FONT,fill=(40,45,40))
    whole.save(QA/'game_natural_three_whole_contact_1x.png')
    current=GAME/'game_natural_bald_b04_e04_composite.png'
    # Old capture names encode saved style IDs0/3/4, not physical PNG1/4/5.
    # Existing fixture line451 records variant_style_ids in these names.
    previous=OLD/'game_bald_b03_e03_composite.png'
    record(previous,'previous installed rejected generated/mirrored physical04, saved style03')
    pair=Image.new('RGB',(660,305),PAPER)
    for col,(path,label) in enumerate([(previous,'上一版：错误重绘'),(current,'当前：恢复原笔触并校中')]):
        pair.paste(load(path).crop(CROP),(col*330,40))
        ImageDraw.Draw(pair).text((col*330+10,8),label,font=FONT,fill=(40,45,40))
    pair.save(QA/'game_eye04_rejected_vs_natural_current_1x.png')
    load(current).crop(CROP).save(QA/'game_current04_bald_face_1x.png')
    load(GAME/'game_natural_whole_04_composite.png').crop(CROP).save(QA/'game_current04_with_hair_face_1x.png')
    for item in sources:assert sha(Path(item['path']))==item['sha256'],item['path']
    report={'schema':'natural-face-real-game-contact-sources/v1','crop_xyxy':list(CROP),'face_crop_size':[330,265],
            'face_resize':False,'whole_contact_resize':False,'presentation':'Original game RGBA alpha-composited on one common uniform paper background; labels only. No rendered/reconstructed face substituted.',
            'previous04_mapping':'Prior fixture uses saved style03 for physical eyes04/brows04; game_bald_b03_e03_composite.png is the actual installed rejected04 capture. Not frozen_raw_before and not game_bald_b04_e04 (that would be physical05).',
            'previous04_mapping_evidence':'mods/appearance/male_face_alignment_fix_20261109/installation/verify_face_alignment.gd:451',
            'sources':sources,'source_images_and_installation_reports_modified':False}
    (QA/'game_natural_contact_sources.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'outputs':['qa/game_natural_nine_faces_contact_1x.png','qa/game_natural_three_whole_contact_1x.png','qa/game_eye04_rejected_vs_natural_current_1x.png','qa/game_current04_bald_face_1x.png','qa/game_current04_with_hair_face_1x.png'],'face_resize':False}))

if __name__=='__main__':main()
