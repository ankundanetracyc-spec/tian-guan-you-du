"""Read the game's supplied static portrait example, without changing it."""
from pathlib import Path
import re, json
from PIL import Image, ImageDraw, ImageFont

ROOT=Path('E:/SteamLibrary/steamapps/common/鬼谷八荒/Mod/modFQA/MOD模板例子/制作“静态和动态”立绘例子/ModProject/ModRes/ResBuildABProject/Assets')
OUT=Path('E:/stacklands/tmp/guigu_facial_study_20261003')
OUT.mkdir(parents=True,exist_ok=True)
PARTS={'Head':'Head（lian）','Eyebrows':'Eyebrows（meimao）','Eyes':'Eyes（yanjing）','Nose':'Nose（bizi）','Mouth':'Mouth（zuiba）'}
report={}
images={}
for key,directory in PARTS.items():
    img=Image.open(ROOT/'立绘资源/Portrait（静态立绘）/Woman'/directory/'88888101.png').convert('RGBA')
    text=(ROOT/'Resources/Game/Portrait/Woman'/key/'88888101.prefab').read_text(encoding='utf-8')
    positions=[[float(n) for n in p] for p in re.findall(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',text)]
    position=next(p for p in positions if abs(p[1])>1)
    report[key]={'size':list(img.size),'sprite_local_position':position,'pixels_per_unit':100,'pivot':[.5,.5]}
    images[key]=img

head=report['Head']
left=head['sprite_local_position'][0]*100-head['size'][0]/2
top=head['sprite_local_position'][1]*100+head['size'][1]/2
merged=Image.new('RGBA',images['Head'].size)
for key in ['Head','Nose','Eyes','Mouth','Eyebrows']:
    im=images[key];x,y,_=report[key]['sprite_local_position']
    offset=(round(x*100-im.width/2-left),round(top-y*100-im.height/2))
    merged.alpha_composite(im,offset)
    report[key]['position_on_head_canvas']=list(offset)

font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',23)
small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
sheet=Image.new('RGB',(1040,650),'#e8e3dc')
d=ImageDraw.Draw(sheet)
d.text((22,15),'鬼谷八荒 · 官方静态立绘示例（原素材研究）',font=font,fill='#2e302c')
for n,(title,im) in enumerate([('完整无五官脸底',images['Head']),('按原节点坐标叠加五官',merged)]):
    bg=Image.new('RGBA',im.size,'#e8e3dc');bg.alpha_composite(im)
    sheet.paste(bg.convert('RGB').resize((408,513)),(24+n*450,95))
    d.text((24+n*450,60),title,font=small,fill='#44463f')
sheet.save(OUT/'official_facial_composition.png')

eye=images['Eyes']
sheet=Image.new('RGB',(1040,380),'#e8e3dc');d=ImageDraw.Draw(sheet)
d.text((20,15),'眼睛层：原灰度笔触与连续透明度',font=font,fill='#2e302c')
for n,(label,color) in enumerate([('叠在浅肤色底上','#e6c9b4'),('叠在深色底上','#302d29')]):
    bg=Image.new('RGBA',eye.size,color);bg.alpha_composite(eye)
    sheet.paste(bg.convert('RGB').resize((436,148)),(24+n*520,110))
    d.text((24+n*520,75),label,font=small,fill='#44463f')
d.text((24,300),'眼缘与阴影随透明度过渡，周围没有不透明肤色底片。',font=small,fill='#44463f')
sheet.save(OUT/'official_eye_alpha.png')
(OUT/'measured_sources.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
