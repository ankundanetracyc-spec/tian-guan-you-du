"""Read-only native-alpha inspection of bone/hair fit and facial cleanliness.

Composites/crops here are review artifacts only; no asset pixels are edited.
"""
from pathlib import Path
import json,numpy as np,cv2
from PIL import Image,ImageDraw,ImageFont
from export_character_skin_ears import ROOT,PACKS
from export_character_ink_hair import validate_front_envelope

font=ImageFont.truetype(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),18)
order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','decoration','hair_front']
for sex,(pack,assets) in PACKS.items():
 folder=ROOT/'mods/appearance'/pack;out=folder/'preview/anatomy_v15';out.mkdir(parents=True,exist_ok=True)
 layers={p:[Image.open(folder/assets/f'{p}_{i}.png').convert('RGBA') for i in range(5)] for p in order if p!='decoration'}
 layers['decoration']=[Image.open(folder/assets/f'{"beard" if sex=="male" else "makeup"}_{i}.png').convert('RGBA') for i in range(5)]
 def compose(color,choices=None,old=False,hide=None):
  choices=choices or {};result=Image.new('RGBA',(1024,1536),color)
  for part in order:
   if part in (hide or []):continue
   if old:
    sub='test_sources/anatomy_v15' if sex=='male' else 'source/anatomy_v11'
    filename=f'{"beard" if sex=="male" else "makeup"}_{choices.get(part,0)}.png' if part=='decoration' else f'{part}_{choices.get(part,0)}.png'
    im=Image.open(folder/sub/'before'/filename).convert('RGBA')
   else:im=layers[part][choices.get(part,0)]
   result.alpha_composite(im)
  return result
 for i in range(5):
  validate_front_envelope(layers['hair_front'][i],sex,f'{sex} front{i}')
  skin=np.asarray(layers['face'][i].getchannel('A'));crown=int(np.flatnonzero(skin[:,512]>128).min())
  width=len(np.flatnonzero(skin[378 if sex=='male' else 380]>128))
  assert 197<=crown<=203 and abs(width-(224 if sex=='male' else 216))<=4,(sex,i,crown,width)
  for j in range(5):
   ear=np.asarray(layers['ear'][j].getchannel('A'))
   roots=[405,619] if sex=='male' else [409,615]
   for x in roots:assert ((skin[383:420,x-12:x+12]>200)&(ear[383:420,x-12:x+12]>128)).sum()>8,(sex,i,j,'ear root')
  brow=np.asarray(layers['brow'][i].getchannel('A'));yb=np.where(brow>12)[0]
  for eye in layers['eye']:
   ye=np.where(np.asarray(eye.getchannel('A'))>12)[0]
   assert ye.min()-yb.max()-1>=4,(sex,i,'brow gap')
 print(sex,'five skulls fitted, ear roots checked against all faces, all 25 brow/eye pairs clear')
 for theme,color in [('light','#ccc8bd'),('dark','#202526')]:
  label_color='black' if theme=='light' else 'white'
  comparison=Image.new('RGB',(1000,750),color)
  for n,old in enumerate([True,False]):
   comp=compose(color,{'hair_back':2,'hair_front':1},old=old)
   comparison.paste(comp.crop((262,0,762,710)).convert('RGB'),(500*n,35))
   ImageDraw.Draw(comparison).text((500*n+10,8),['调整前','调整后：贴合头骨、清理眼角'][n],font=font,fill=label_color)
  comparison.save(out/f'before_after_{theme}.png')
  compose(color,{'hair_back':2,'hair_front':1}).save(out/f'portrait_{sex}_{theme}.png')
  sheets={name:Image.new('RGB',(1500,620),color) for name in ['faces','fronts','features','bone_fit']}
  for i in range(5):
   # These views deliberately avoid ornaments for ordinary-crown inspection.
   comp=compose(color,{'face':i,'hair_back':0,'hair_front':4})
   sheets['faces'].paste(comp.crop((345,170,679,720)).resize((300,494)).convert('RGB'),(i*300,30))
   comp=compose(color,{'hair_back':0,'hair_front':i})
   sheets['fronts'].paste(comp.crop((302,150,722,780)).resize((300,450)).convert('RGB'),(i*300,30))
   comp=compose(color,{'eye':i,'brow':i,'hair_back':0,'hair_front':4})
   sheets['features'].paste(comp.crop((392,328,632,415)).resize((300,109)).convert('RGB'),(i*300,30))
   for n,p in enumerate(['brow','eye']):
    isolated=Image.new('RGBA',(1024,1536),color);isolated.alpha_composite(layers[p][i])
    sheets['features'].paste(isolated.crop((392,328 if p=='brow' else 358,632,365 if p=='brow' else 410)).resize((300,46 if p=='brow' else 65)).convert('RGB'),(i*300,180+n*120))
   comp=compose(color,{'face':0,'hair_front':i},hide=['hair_back','ear','eye','brow','nose','mouth','decoration'])
   skin=np.asarray(layers['face'][0].getchannel('A'));contours,_=cv2.findContours((skin>128).astype(np.uint8),cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE)
   draw=ImageDraw.Draw(comp)
   for contour in contours:draw.line([tuple(point[0]) for point in contour]+[tuple(contour[0][0])],fill='#d55545',width=2)
   for y,col in [(185,'#394d80'),(200,'#d55545'),(300,'#518050')]:draw.line((350,y,674,y),fill=col,width=1)
   sheets['bone_fit'].paste(comp.crop((302,150,722,720)).resize((300,407)).convert('RGB'),(i*300,30))
   for name,sheet in sheets.items():ImageDraw.Draw(sheet).text((i*300+8,5),f'{name} {i}',font=font,fill=label_color)
  for name,sheet in sheets.items():sheet.save(out/f'{name}_{theme}.jpg')
  # Full cross-products are essential: individual layer views miss cap seams.
  for kind in ['hair','face_ear']:
   sheet=Image.new('RGB',(1400,1750),color)
   for i in range(5):
    for j in range(5):
     choices={'hair_front':i,'hair_back':j} if kind=='hair' else {'face':i,'ear':j,'hair_back':0,'hair_front':4}
     comp=compose(color,choices)
     sheet.paste(comp.crop((292,0,732,550)).resize((280,350)).convert('RGB'),(i*280,j*350))
   sheet.save(out/f'{kind}_25_{theme}.jpg')
print('PORTRAIT_ANATOMY_PASS: native alpha, complete skull, compact crowns, clean eye pairs, all ear and brow combinations.')
