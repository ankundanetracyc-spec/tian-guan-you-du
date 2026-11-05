"""Read-only audit of installed official portrait example and live game sprites.

Writes research evidence only. Never modifies the game or active project art.
"""
from pathlib import Path
import re,json,hashlib,zipfile,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import UnityPy

P=Path(__file__).resolve().parents[1]
GAME=Path('E:/SteamLibrary/steamapps/common/鬼谷八荒')
EXAMPLE=next(x for x in (GAME/'Mod/modFQA/MOD模板例子').iterdir() if '静态' in x.name)/'ModProject'
ASSET=EXAMPLE/'ModRes/ResBuildABProject/Assets'
OUT=P/'docs/美术规范/鬼谷八荒立绘拼接复核_20261103'
OUT.mkdir(parents=True,exist_ok=True)
F=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
S=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)
DIRS={'Body':'Body（yifu）','Head':'Head（lian）','Hair':'Hair_（toufahou）','HairFront':'HairFront（toufaqian）','Eyebrows':'Eyebrows（meimao）','Eyes':'Eyes（yanjing）','Nose':'Nose（bizi）','Mouth':'Mouth（zuiba）'}
def vector(value):return [float(n) for n in re.findall(r'[xyz]: ([^,}]+)',value)]
def alpha_stats(im):
 a=np.array(im);v=a[:,:,3]>0;c=a[v,:3]
 return {'size':list(im.size),'bbox':im.getbbox(),'nontransparent':int(v.sum()),'partial_alpha':int(((a[:,:,3]>0)&(a[:,:,3]<255)).sum()),'alpha_gt240':int((a[:,:,3]>240).sum()),'rgb_channel_difference_max':int(np.max(c.max(1).astype(int)-c.min(1).astype(int))) if len(c) else 0}
node=(ASSET/'立绘资源/立绘预览节点/静态立绘节点.prefab').read_text(encoding='utf-8-sig')
blocks=re.split(r'--- !u!\d+ &\d+\s*\n',node)[1:]
names={};depths={}
for block in blocks:
 if block.startswith('GameObject:'):
  name=re.search(r'  m_Name: (.+)',block).group(1)
  if name.startswith('"'):name=json.loads(name)
  for tid in re.findall(r'- component: \{fileID: (\d+)\}',block):names[tid]=name
ids=re.findall(r'--- !u!\d+ &(\d+)\s*\n',node)
for ident,block in zip(ids,blocks):
 if block.startswith('Transform:') and ident in names:
  depths[names[ident]]=vector(re.search(r'm_LocalPosition: (\{[^}]+\})',block).group(1))[2]
records={};images={}
for part,dirname in DIRS.items():
 png=ASSET/'立绘资源/Portrait（静态立绘）/Woman'/dirname/'88888101.png'
 im=Image.open(png).convert('RGBA');images[part]=im
 prefab=ASSET/'Resources/Game/Portrait/Woman'/part/'88888101.prefab'
 text=prefab.read_text(encoding='utf-8-sig');meta=Path(str(png)+'.meta').read_text(encoding='utf-8-sig')
 positions=[vector(x) for x in re.findall(r'm_LocalPosition: (\{[^}]+\})',text)]
 pos=next(x for x in positions if abs(x[1])>1)
 records[part]={**alpha_stats(im),'sprite_local_position':pos,'pivot':[float(n) for n in re.search(r'spritePivot: \{x: ([^,]+), y: ([^}]+)',meta).groups()],'pixels_per_unit':float(re.search(r'spritePixelsToUnits: ([\d.]+)',meta).group(1)),'png_path':str(png),'prefab_path':str(prefab),'sha256':hashlib.sha256(png.read_bytes()).hexdigest()}
def compose(hide=()):
 c=Image.new('RGBA',(600,1000),(231,224,211,255))
 order=['Hair','Body','Head','Nose','Eyes','Mouth','Eyebrows','HairFront']
 for part in order:
  if part in hide:continue
  im=images[part];x,y,z=records[part]['sprite_local_position']
  c.alpha_composite(im,(round(300+x*100-im.width/2),round(1000-y*100-im.height/2)))
 return c
compose().save(OUT/'official_original_assembled.png')
board=Image.new('RGB',(1200,630),(231,224,211));d=ImageDraw.Draw(board)
for i,(label,hide) in enumerate([('原节点坐标组合',()),('隐藏前发',('HairFront',)),('隐藏后发',('Hair',)),('只看脸和五官',('HairFront','Hair','Body'))]):
 im=compose(hide).crop((175,95,425,395)).resize((300,360))
 board.paste(im.convert('RGB'),(i*300,40));d.text((i*300+8,10),label,font=F,fill='#333b35')
for i,part in enumerate(['HairFront','Hair','Head','Eyes','Nose','Mouth']):
 im=images[part].copy();im.thumbnail((180,170));x=i*200;y=430
 board.paste(im,(x+(200-im.width)//2,y),im)
 d.text((x+8,405),part+' '+str(images[part].size),font=S,fill='#333b35')
board.save(OUT/'official_layers_and_occlusion.jpg',quality=96)
board=Image.new('RGB',(1100,520),(231,224,211));d=ImageDraw.Draw(board)
for col,(name,color) in enumerate([('浅底',(240,216,203,255)),('深底',(45,49,52,255))]):
 for row,part in enumerate(['Eyes','Nose','Mouth']):
  im=images[part];b=Image.new('RGBA',im.size,color);b.alpha_composite(im);b=b.resize((im.width*4,im.height*4),Image.Resampling.NEAREST)
  if b.width>500:b.thumbnail((500,130))
  board.paste(b.convert('RGB'),(col*550+15,row*165+35));d.text((col*550+15,row*165+6),part+' '+name,font=F,fill='#333b35')
board.save(OUT/'official_facial_alpha.jpg',quality=97)
# Read the official XLSX as ZIP/XML without altering or recalculating it.
with zipfile.ZipFile(EXAMPLE/'ModExcel/RoleDress.xlsx') as z:
 ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
 strings=[''.join(e.itertext()) for e in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si',ns)] if 'xl/sharedStrings.xml' in z.namelist() else []
 sheets=[]
 for name in z.namelist():
  if re.fullmatch(r'xl/worksheets/sheet\d+\.xml',name):
   rows=[]
   for row in ET.fromstring(z.read(name)).findall('s:sheetData/s:row',ns):
    vals={}
    for c in row.findall('s:c',ns):
     v=c.find('s:v',ns)
     if v is not None:vals[c.attrib['r']]=strings[int(v.text)] if c.attrib.get('t')=='s' else v.text
    if vals:rows.append(vals)
   sheets.append({'sheet':name,'rows':rows})
(OUT/'official_template_measurements.json').write_text(json.dumps({'example':str(EXAMPLE),'parts':records,'preview_node_z':depths,'role_dress':sheets},ensure_ascii=False,indent=2),encoding='utf-8')
print('Official template measured; reading installed ResourceManager...',flush=True)
data=GAME/'guigubahuang_Data';env=UnityPy.Environment();env.path=str(data)
mapping=env.load_file(str(data/'globalgamemanagers'))
manager=next(o for o in mapping.objects.values() if o.type.name=='ResourceManager').read_typetree()
refs=[(key,pointer) for key,pointer in manager['m_Container'] if key.startswith('game/portrait/') and any('/'+s+'/' in key for s in ['hairfront','hair','head','eyes'])]
counts={}
for key,ptr in refs:
 group='/'.join(key.split('/')[2:4]);counts[group]=counts.get(group,0)+1
keys=[]
for sex in ['woman','man']:
 for slot in ['hairfront','hair']:
  group=[x for x in refs if f'/{sex}/{slot}/' in x[0]]
  keys.extend([group[0],group[len(group)//2]])
env.load_file(str(data/'resources.assets.resS'));asset=env.load_file(str(data/'resources.assets'))
runtime=[]
def collect(path_id,parent_position=(0,0,0),parent_scale=(1,1,1)):
 go=asset.objects[path_id].read();components=[]
 for component in go.m_Component:
  ptr=component.component if hasattr(component,'component') else component[1]
  obj=ptr.deref();components.append((obj.type.name,obj.read()))
 transform=next((v for t,v in components if t in ['Transform','RectTransform']),None)
 pos=np.array(parent_position);scale=np.array(parent_scale)
 if transform:
  v=transform.m_LocalPosition;s=transform.m_LocalScale
  pos=pos+scale*np.array([v.x,v.y,v.z]);scale=scale*np.array([s.x,s.y,s.z])
 found=[]
 for typ,obj in components:
  if typ=='SpriteRenderer' and obj.m_Sprite.path_id:
   sprite=obj.m_Sprite.deref().read();found.append((sprite,pos.tolist(),scale.tolist(),str(obj.m_Color)))
 if transform:
  for child in transform.m_Children:found.extend(collect(child.deref().read().m_GameObject.path_id,pos,scale))
 return found
(OUT/'installed_samples').mkdir(exist_ok=True)
for key,ptr in keys:
 assert mapping.externals[ptr['m_FileID']-1].path.endswith('resources.assets')
 for j,(s,pos,scale,color) in enumerate(collect(ptr['m_PathID'])):
  im=s.image.convert('RGBA');file=key.replace('/','_')+f'_{j}.png';im.save(OUT/'installed_samples'/file)
  runtime.append({'resource':key,'sprite':s.m_Name,'file':file,'position':pos,'scale':scale,'sprite_pivot':[s.m_Pivot.x,s.m_Pivot.y],'pixels_to_units':s.m_PixelsToUnits,'renderer_color':color,**alpha_stats(im)})
 print('Read '+key,flush=True)
board=Image.new('RGB',(1200,580),(221,211,193));d=ImageDraw.Draw(board)
for i,r in enumerate(runtime):
 im=Image.open(OUT/'installed_samples'/r['file']);im.thumbnail((275,220));x=i%4*300;y=i//4*290
 board.paste(im,(x+(300-im.width)//2,y+45),im);d.text((x+5,y+5),'/'.join(r['resource'].split('/')[-3:]),font=S,fill='#30372f');d.text((x+5,y+25),str(tuple(r['size'])),font=S,fill='#30372f')
board.save(OUT/'installed_front_back_examples.jpg',quality=96)
(OUT/'installed_runtime_measurements.json').write_text(json.dumps({'game':str(GAME),'catalog_counts':counts,'samples':runtime,'scope':'Read-only serialized transforms and sprite alpha; no claim to reconstructed runtime recoloring implementation.'},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'template_parts':len(records),'counts':counts,'fresh_game_samples':len(runtime)},ensure_ascii=False),flush=True)
