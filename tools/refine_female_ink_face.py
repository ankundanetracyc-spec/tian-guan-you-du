"""Apply the requested restrained female skin/feature revision to modular PNGs."""
from pathlib import Path
import json,shutil
import numpy as np
import cv2
from PIL import Image,ImageDraw,ImageFont
P=Path(__file__).resolve().parents[1]
R=P/'mods/appearance/new_ink_hair_20261003_v1'
S=R/'source/face_revision'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
def read(p):return np.array(Image.open(p).convert('RGBA'))
def save(p,a):
    a=np.clip(np.round(a),0,255).astype(np.uint8);a[a[:,:,3]==0,:3]=0;Image.fromarray(a).save(p)
def toned(a):
    a=a.astype(np.float32);rgb=a[:,:,:3]
    weight=np.clip((rgb.mean(2)-120)/100,0,1)
    rgb-=weight[:,:,None]*np.array([4,7,11],np.float32)
    return a
def compose(sex,old=False,hidden=''):
    a=Image.new('RGBA',(1024,1536))
    for k in ORDER:
        if k==hidden:continue
        p=S/f'before_{sex}_{k}.png' if old and (S/f'before_{sex}_{k}.png').exists() else R/sex/f'{k}.png'
        a.alpha_composite(Image.open(p).convert('RGBA'))
    b=Image.new('RGBA',a.size,'#e8e3dc');b.alpha_composite(a);return b.convert('RGB')
def main():
    S.mkdir(exist_ok=True)
    for sex in ['male','female']:
        for key in ORDER:
            dst=S/f'before_{sex}_{key}.png'
            if not dst.exists():shutil.copy2(R/sex/f'{key}.png',dst)
    # Skin keeps the original hue relationships and paper grain. Shadows are
    # affected less than highlights; ears and neck use the same curve.
    for key in ['face_shape','nose','mouth','eyebrows']:
        a=toned(read(S/f'before_female_{key}.png'))
        if key in ['nose','mouth']:
            y,x=np.indices(a.shape[:2]);rgb=a[:,:,:3]
            cx,cy,rx,ry=(519,445,11,10) if key=='nose' else (519,488,33,18)
            zone=np.exp(-(((x-cx)/rx)**2+((y-cy)/ry)**2)/2)
            shine=np.clip((rgb.min(2)-173)/55,0,1)*zone*.82
            matte=np.array([216,184,164] if key=='nose' else [207,162,139])
            rgb[:]=rgb*(1-shine[:,:,None])+matte*shine[:,:,None]
            # Feather the old polygon boundary while preserving the organ.
            distance=cv2.distanceTransform((a[:,:,3]>0).astype(np.uint8),cv2.DIST_L2,3)
            a[:,:,3]*=np.clip(distance/4,0,1)
        dy={'face_shape':0,'nose':-1,'mouth':-2,'eyebrows':3}[key]
        if dy:a=cv2.warpAffine(a,np.float32([[1,0,0],[0,1,dy]]),(1024,1536))
        save(R/'female'/f'{key}.png',a)
    generated=Path('D:/CodexData/generated_images/01a0fd74-de00-7ab2-9dfc-c15a97009561/exec-99d4402f-f7a1-4262-ac2f-a8ca8b772189.png')
    shutil.copy2(generated,S/'female_eyes_generated.png')
    a=read(generated).astype(np.float32);h,w=a.shape[:2]
    # Two eyeball openings. The outer ink wash is exported as pigment, with
    # the generated pale flesh rim removed instead of retaining an eye patch.
    core=Image.new('L',(w,h));d=ImageDraw.Draw(core)
    left=[(226,485),(277,477),(326,475),(390,480),(459,491),(523,520),(580,557),(622,584),(557,583),(485,584),(416,582),(348,570),(299,548),(264,521)]
    right=[(w-x,y) for x,y in left]
    d.polygon(left,fill=255);d.polygon(right,fill=255)
    mask=np.array(core)>0;rgb=a[:,:,:3];alpha=a[:,:,3]/255
    base=np.array([238,219,204],np.float32)
    coverage=np.clip(np.mean((base-rgb)/np.array([175,169,162]),axis=2),0,1)
    out_alpha=np.minimum(alpha,coverage)
    pigment=(rgb-(1-out_alpha[:,:,None])*base)/np.maximum(out_alpha[:,:,None],.01)
    rgb[~mask]=np.clip(pigment[~mask],0,255);alpha[~mask]=out_alpha[~mask]
    # Tame both highlights in their shared eye-local frame.
    yy,xx=np.indices((h,w));pupils=(((xx-431)/42)**2+((yy-498)/34)**2<1)|(((xx-1107)/42)**2+((yy-498)/34)**2<1)
    bright=pupils&(rgb.min(2)>190);rgb[bright]=[187,174,155]
    a[:,:,3]=alpha*255
    # One whole-pair transform; no independent eye stretch.
    s=.16;tx=396;ty=297
    premul=rgb*alpha[:,:,None]
    mat=np.float32([[s,0,tx],[0,s,ty]])
    small_size=(round(w*s),round(h*s))
    small_a=cv2.resize(alpha,small_size,interpolation=cv2.INTER_AREA)
    small_c=cv2.resize(premul,small_size,interpolation=cv2.INTER_AREA)
    patch=np.zeros((small_size[1],small_size[0],4),np.float32)
    patch[:,:,:3]=small_c/np.maximum(small_a[:,:,None],.001);patch[:,:,3]=small_a*255
    result=np.zeros((1536,1024,4),np.float32)
    result[ty:ty+small_size[1],tx:tx+small_size[0]]=patch
    save(R/'female/eyes.png',result)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',20)
    board=Image.new('RGB',(1536,690),'#e8e3dc')
    for i,(sex,old,label) in enumerate([('male',False,'男性基准'),('female',True,'女性修改前'),('female',False,'女性修改后')]):
        crop=compose(sex,old).crop((355,255,685,680)).resize((495,638))
        board.paste(crop,(i*512+8,40));ImageDraw.Draw(board).text((i*512+12,8),label,font=font,fill='#302a25')
    board.save(R/'preview/face_revision_comparison.png')
    for sex in ['male','female']:
        for hidden in ['','hair_front','hair_back']:
            compose(sex,hidden=hidden).save(R/'preview'/f'{sex}_{"full" if not hidden else "without_"+hidden}.png')
    board=Image.new('RGB',(1536,1576),'#e8e3dc')
    pair=Image.new('RGB',(1024,850),'#e8e3dc')
    for row,sex in enumerate(['male','female']):
        pair.paste(compose(sex).crop((260,100,770,950)),(row*512,0))
        for col,hidden in enumerate(['','hair_back','hair_front']):
            board.paste(compose(sex,hidden=hidden).resize((512,768)),(col*512,row*788+20))
            ImageDraw.Draw(board).text((col*512+10,row*788),['全部显示','仅前发','仅后发'][col],font=font,fill='#302a25')
    board.save(R/'preview/hair_ownership_check.png');pair.save(R/'preview/frontal_revision_pair.png')
    # Variant-specific anchor record; the frozen selected-master rig remains
    # available as the historical baseline.
    original=P/'mods/appearance/selected_ink_portrait_rig_20261003_v1/rig/portrait_rig.json'
    rig=json.loads(original.read_text(encoding='utf-8'));rig['id']='our_ink_face_revision_20261003_v1'
    rig['parent_rig']='../../selected_ink_portrait_rig_20261003_v1/rig/portrait_rig.json'
    rig['rig_review_status']='Female eye and feature placement revised at user request; visual candidate'
    anchors=rig['families']['female']['anchors']
    eye_points={'eye_left':[464,379],'eye_right':[574,379],'eye_outer_left':[432,375],'eye_inner_left':[496,390],'eye_inner_right':[542,390],'eye_outer_right':[606,375]}
    for k,v in anchors.items():
        if k in eye_points:v['px']=eye_points[k]
        elif k.startswith('eye_'):v['px'][1]-=3.5
        elif k.startswith('brow_'):v['px'][1]+=3
        elif k.startswith(('nose_','nostril_')):v['px'][1]-=1
        elif k.startswith('mouth_'):v['px'][1]-=2
        else:continue
        v['uv']=[v['px'][0]/1024,v['px'][1]/1536];v['source']='Current female face revision measurement/translation'
    for k,dy in [('eyes',-3.5),('eyebrows',3),('nose',-1),('mouth',-2)]:
        slot=rig['families']['female']['slots'][k];slot['pivot_px'][1]+=dy
        slot['pivot_uv']=[slot['pivot_px'][0]/1024,slot['pivot_px'][1]/1536]
        slot['translation_from_canvas_center_px'][1]+=dy
    (R/'rig').mkdir(exist_ok=True)
    for sex in ['male','female']:
        overlay=Image.new('RGBA',(1024,1536));draw=ImageDraw.Draw(overlay)
        for k,v in rig['families'][sex]['anchors'].items():
            if v['kind']=='scaffold':continue
            x,y=v['px'];draw.ellipse((x-3,y-3,x+3,y+3),fill=(18,107,117,230))
        overlay.save(R/'rig'/f'{sex}_overlay.png')
    (R/'rig/portrait_rig.json').write_text(json.dumps(rig,ensure_ascii=False,indent=2),encoding='utf-8')
    report={'skin_curve_highlight_subtraction_rgb':[4,7,11],'feature_translation_px':{'eyebrows':[0,3],'nose':[0,-1],'mouth':[0,-2]},'eyes_pair_similarity':{'scale':s,'translation':[tx,ty]},'visual_approved_by_user':False}
    (R/'face_revision.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
if __name__=='__main__':main()
