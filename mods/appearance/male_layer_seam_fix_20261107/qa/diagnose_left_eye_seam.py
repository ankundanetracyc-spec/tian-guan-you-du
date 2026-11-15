"""Read-only pixel/source diagnosis. Writes diagnostic files only in this QA folder."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
import json, hashlib
from scipy.ndimage import maximum_filter
Q=Path(__file__).resolve().parent
ROOT=Q.parents[3]
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004/male'
LOOSE=ROOT/'mods/appearance/male_loose_style_install_20261106'
BAL=ROOT/'mods/appearance/male_eye_balance_fix_20261107'
def load(p): return Image.open(p).convert('RGBA')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
eye_path=ACTIVE/'eyes/04.png'; eye=load(eye_path)
assert sha(eye_path)=='6c3f023df0d845a979727f4fab210c3ad95a03a881a40f3ea493d19dc0252f14'
head=load(ACTIVE/'face_shape/01.png'); front=load(ACTIVE/'hair_front/04.png')
old=load(BAL/'reference/current_male/eyes/04.png')
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
roi=(415,325,475,400)
def compose(eyes_on,hair_on,bg):
    out=Image.new('RGBA',(1024,1536),bg);out.alpha_composite(head)
    if eyes_on:out.alpha_composite(eye)
    if hair_on:out.alpha_composite(front)
    return out
for theme,bg,fg in [('light','#e8e1d6','#222222'),('dark','#202b31','#eeeeee')]:
    sheet=Image.new('RGB',(1920,640),bg);d=ImageDraw.Draw(sheet)
    native=Image.new('RGB',(400,120),bg);dn=ImageDraw.Draw(native)
    for i,(on,hair,label) in enumerate([(False,False,'Head only'),(True,False,'Eye04, no front'),(False,True,'Front04, no eye'),(True,True,'Eye04 + front04')]):
        patch=compose(on,hair,bg).crop(roi).convert('RGB')
        sheet.paste(patch.resize((480,600),Image.Resampling.NEAREST),(i*480,40));d.text((i*480+8,8),label,font=font,fill=fg)
        native.paste(patch,(i*100+20,40));dn.text((i*100+3,5),['Head','Eye','Hair','Both'][i],font=font,fill=fg)
    sheet.save(Q/f'left_seam_layers_8x_{theme}.png');native.save(Q/f'left_seam_layers_1x_{theme}.png')
nohair=compose(True,False,'#e8e1d6').crop((428,350,452,376)).resize((768,832),Image.Resampling.NEAREST).convert('RGB')
d=ImageDraw.Draw(nohair)
for x in range(0,768,128):d.line((x,0,x,832),fill='#a49d91');d.text((x+2,0),str(428+x//32),font=font,fill='red')
for y in range(0,832,128):d.line((0,y,768,y),fill='#a49d91');d.text((0,y+1),str(350+y//32),font=font,fill='red')
nohair.save(Q/'left_seam_nohair_grid32x.png')
record=json.loads((LOOSE/'source/feature_import.json').read_text(encoding='utf-8'))
e=next(r for r in record['records'] if r['part']=='eyes')
F=np.array(e['shared_source_to_frame']);M=e['combined_native_to_frame']
Y,X=np.mgrid[:1536,:1024];den=F[0,0]**2+F[1,0]**2
sx=(F[0,0]*(X-F[0,2])+F[1,0]*(Y-F[1,2]))/den
sy=(-F[1,0]*(X-F[0,2])+F[0,0]*(Y-F[1,2]))/den
guard=maximum_filter(np.array(front)[:,:,3]>=64,size=3)
fixed=sx<440
hidden=guard|fixed|(sx>571)
origin=np.zeros((1536,1024,3),dtype=np.uint8)
origin[~hidden]=[80,190,100];origin[guard]=[50,120,225];origin[fixed]=[235,140,45]
Image.fromarray(origin).crop((428,350,452,376)).resize((768,832),Image.Resampling.NEAREST).save(Q/'left_seam_rgb_origin32x.png')
alpha=np.array(eye)[:,:,3]
Image.fromarray(alpha).crop((428,350,452,376)).resize((768,832),Image.Resampling.NEAREST).save(Q/'left_seam_alpha32x.png')
native=load(LOOSE/e['raw_source']);n=np.array(native,dtype=float)
approved=np.array(load(LOOSE/'qa/features/approved_in_shared_frame.png'))
actual=np.array(eye);h=np.array(head)
def sample_native(x,y):
    dx=x-M['tx'];dy=y-M['ty'];den=M['a']**2+M['b']**2
    nx=(M['a']*dx+M['b']*dy)/den;ny=(-M['b']*dx+M['a']*dy)/den
    xi=int(np.floor(nx));yi=int(np.floor(ny));fx=nx-xi;fy=ny-yi
    out=np.zeros(4)
    for i,j,w in [(0,0,(1-fx)*(1-fy)),(1,0,fx*(1-fy)),(0,1,(1-fx)*fy),(1,1,fx*fy)]:
        p=n[yi+j,xi+i];a=p[3]/255;out[:3]+=p[:3]*a*w;out[3]+=a*w
    if out[3]:out[:3]/=out[3]
    out[3]*=255
    return np.floor(out+.5).astype(int).tolist()
rows=[]
for y in range(354,367):
    row=[]
    for x in range(436,446):
        row.append({'xy':[x,y],'rgba':actual[y,x].tolist(),'native_resampled_rgba':sample_native(x,y),'approved_rgb':approved[y,x,:3].tolist(),'source_x':float(sx[y,x]),'branch':'fixed_native' if fixed[y,x] else 'hair_guard_native' if guard[y,x] else 'approved_rgb','head_rgb':h[y,x,:3].tolist()})
    rows.append(row)
result={'schema':'left-eye04-seam-readonly-diagnosis/v1','scope':'Only this QA directory written; no production/candidate/art PNG edited and no generation invoked','current_eye_sha256':sha(eye_path),'old_left_rgba_differing_pixels':int(np.any(actual[:,:515]!=np.array(old)[:,:515],2).sum()),'roi_xyxy':roi,'source_pipeline':'male_loose_style_install_20261106/source/import_features.cjs hidden(): fixed sx<440 || sx>571 and boolean hair guard choose native RGB, otherwise approved RGB; generated Alpha remains independent','fixed_sx440_runtime_x_at_y360':float((440*den-F[1,0]*(360-F[1,2]))/F[0,0]+F[0,2]),'pixel_rows_y354_366_x436_445':rows}
(Q/'left_eye_seam_pixel_diagnosis.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'sha256':result['current_eye_sha256'],'old_left_rgba_differing_pixels':result['old_left_rgba_differing_pixels'],'fixed_sx440_runtime_x_at_y360':result['fixed_sx440_runtime_x_at_y360']}))
