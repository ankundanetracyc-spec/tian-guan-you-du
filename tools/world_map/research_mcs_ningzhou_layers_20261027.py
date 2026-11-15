"""Read local Ningzhou scene metadata; never export or modify game artwork."""
import json, collections
from pathlib import Path
import UnityPy
ROOT=Path(__file__).resolve().parents[2]
GAME=Path('E:/SteamLibrary/steamapps/common/觅长生/觅长生_Data')
env=UnityPy.load(str(GAME/'level9'))
objects={obj.path_id:obj for obj in env.objects}
gos={};transforms={};mono=collections.Counter();backgrounds=[];roots=[]
for obj in env.objects:
 if obj.type.name=='GameObject':gos[obj.path_id]=obj.read()
 elif obj.type.name in ('Transform','RectTransform'):transforms[obj.path_id]=obj.read()
scripts={}
for obj in env.objects:
 if obj.type.name!='MonoBehaviour':continue
 data=obj.read(check_read=False);pointer=data.m_Script;key=(pointer.m_FileID,pointer.m_PathID)
 if key not in scripts:
  try:scripts[key]=pointer.read().m_ClassName
  except Exception:scripts[key]='Unresolved'
 mono[scripts[key]]+=1
for obj in env.objects:
 if obj.type.name not in ('Transform','RectTransform'):continue
 transform=transforms[obj.path_id];go=gos.get(transform.m_GameObject.m_PathID)
 if not go:continue
 if not transform.m_Father.m_PathID:roots.append({'name':go.m_Name,'children':len(transform.m_Children)})
 if 'bigmap2020' not in go.m_Name:continue
 record={'object':go.m_Name,'position':{k:getattr(transform.m_LocalPosition,k) for k in ('x','y','z')},'scale':{k:getattr(transform.m_LocalScale,k) for k in ('x','y','z')}}
 for component in go.m_Component:
  reader=objects.get(component.component.m_PathID)
  if reader is None:continue
  if reader.type.name!='SpriteRenderer':continue
  renderer=reader.read();sprite=renderer.m_Sprite.read();rect=sprite.m_Rect
  record.update(sprite=sprite.m_Name,rect={'width':rect.width,'height':rect.height},pixelsPerUnit=sprite.m_PixelsToUnits,sortingOrder=renderer.m_SortingOrder)
 backgrounds.append(record)
report={'source':str(GAME/'level9'),'scope':'本机安装AllMaps行走场景；不等同UIMapPanel的宁州总览／传送界面','backgrounds':backgrounds,'roots':roots,'components':{k:v for k,v in mono.items() if any(s in k for s in ['Map','WASD'])}}
out=ROOT/'docs/prototypes/觅长生宁州地图层级只读核验_20261027.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=True))
