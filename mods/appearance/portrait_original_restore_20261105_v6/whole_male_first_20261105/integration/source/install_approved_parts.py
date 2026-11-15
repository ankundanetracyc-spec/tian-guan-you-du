from pathlib import Path
import json,hashlib,shutil
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parent.parent;P=R.parents[4];A=P/'mods/appearance/ink_three_variants_20261004';I=R/'installation'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
res=lambda p:'res://'+p.relative_to(P).as_posix()
baseline=json.loads((I/'baseline.json').read_text(encoding='utf8'))
for file,digest in baseline.items():assert sha(P/file[6:])==digest,file
approval=json.loads((R.parent/'source/whole_review_manifest.json').read_text(encoding='utf8'));assert approval['user_visual_approval'] and sha(R.parent/'male_whole_01.png')==approval['image_sha256']
review=json.loads((R/'qa/structural_check.json').read_text(encoding='utf8'));assert review['head_neck_alpha_changed_pixels']==0
parts=['hair_front','hair_back','eyebrows','eyes','nose','mouth','face_shape','clothing','clothing_back'];records=[]
for part in parts:
 source=R/'male'/part/'01.png';target=A/'male'/part/'01.png';before=R/'before/male'/part/'01.png'
 with Image.open(source) as im:assert im.size==(1024,1536) and im.mode=='RGBA'
 records.append(dict(gender='male',part=part,index=1,candidate_path=res(source),resource_path=res(target),sha256=sha(source),before_resource_path=res(before),before_sha256=sha(before)))
configpath=A/'appearance_parts.json';config=json.loads(configpath.read_text(encoding='utf8'));before_config=json.loads((I/'appearance_parts.before.json').read_text(encoding='utf8'))
names={'hair_front':'木簪拢发','hair_back':'木簪半束长披','brow':'清直眉','eye':'浅长目','nose':'自然鼻型','mouth':'自然浅唇','face':'清俊长脸','beard':'无须'}
retired=[]
for entry in config['components']:
 if entry['gender']!='male' or entry['part']=='clothes':continue
 old_indices=entry.get('variant_indices',list(range(1,entry['variant_count']+1)))
 for index in old_indices:
  if index==1:continue
  retired.append(A/entry['file'].replace('{index}',f'{index:02}'))
 entry.update(variant_count=1,variant_names=[names[entry['part']]],variant_indices=[1],variant_style_ids=[0],variant_aliases={'1':0,'2':0})
assert len(retired)==7,len(retired)
# Guides describe the approved portrait's facial painting and the unchanged current neck interface.
old_head=Image.open(R/'before/male/face_shape/01.png').convert('RGBA');overlay=Image.new('RGBA',(1024,1536));draw=ImageDraw.Draw(overlay)
draw.line([(519,140),(519,745)],fill=(78,155,164,165),width=1)
neck=[]
for y in range(500,721):
 xs=[x for x in range(350,680) if old_head.getpixel((x,y))[3]>=230]
 if xs:neck.append([min(xs),max(xs),y])
for side in [0,1]:draw.line([(row[side],row[2]) for row in neck],fill=(226,148,47,190),width=1)
for x,y in [(465.78,365.09),(570.24,364.66),(520,444),(519.5,475),(520,537),(428,530),(601,530),(426,550),(603,550),(421,580),(608,580)]:
 draw.ellipse((x-3,y-3,x+3,y+3),outline=(224,150,50,230),width=1)
(R/'rig').mkdir(exist_ok=True);overlaypath=R/'rig/male_overlay.png';overlay.save(overlaypath)
previous_overlay=config.get('anchor_overlays',{}).get('male','');config.setdefault('anchor_overlays',{})['male']=res(overlaypath)
assert [e for e in config['components'] if e['gender']=='female']==[e for e in before_config['components'] if e['gender']=='female']
for record in records:shutil.copy2(P/record['candidate_path'][6:],P/record['resource_path'][6:])
# Old selectable variants leave the active directory after immutable backup.
for p in retired:
 assert sha(p)==baseline[res(p)]
 p.unlink()
 imp=Path(str(p)+'.import')
 if imp.exists():shutil.copy2(imp,R/'before'/imp.relative_to(A));imp.unlink()
configpath.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
manifestpath=A/'portrait_manifest.json';manifest=json.loads(manifestpath.read_text(encoding='utf8'))
manifest['latest_male_rebuild']['approved_whole_01']={'master':res(R.parent/'male_whole_01.png'),'master_sha256':approval['image_sha256'],'master_user_approved':True,'source':res(R),'mode':'Built-in image_gen part extraction and hidden-region completion from approved whole portrait; original visible feature RGB retained','head_neck_alpha_unchanged':True,'selected_parts':9,'retired_old_variants':7,'male_selectable':15,'female_selectable':22,'user_part_visual_approval':False}
manifestpath.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
changed={r['resource_path'] for r in records};retired_paths={res(p) for p in retired};preserved={p:d for p,d in baseline.items() if p not in changed|retired_paths};assert len(preserved)==35
for p,d in preserved.items():assert sha(P/p[6:])==d,p
report={'records':records,'expected_installed_count':9,'expected_changed_count':9,'baseline_sha256':preserved,'female_baseline_sha256':{p:d for p,d in preserved.items() if '/female/' in p},'config_sha256':sha(configpath),'config_before_sha256':sha(I/'appearance_parts.before.json'),'config_before_resource_path':res(I/'appearance_parts.before.json'),'config_previous_overlay':previous_overlay,'anchor_overlay_resource_path':res(overlaypath),'anchor_overlay_sha256':sha(overlaypath),'manifest_after_sha256':sha(manifestpath),'active_option_count':37,'physical_png_count':44,'registered_png_count':44,'retired_old_variants':[{'path':p,'sha256':baseline[p]} for p in sorted(retired_paths)],'approved_master_sha256':approval['image_sha256'],'head_neck_alpha_changed_pixels':0,'visual_acceptance':'Approved whole portrait authorized; root reviewed reconstructed parts and seven collar pairings. Generated hidden regions are not pixel-identical to master; individual parts await user judgement.'}
(I/'install_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
shutil.copy2(R.parents[1]/'collar_hair_fixed_neck_20261105/collars_only/source/refresh_imports.py',R/'source/refresh_imports.py')
print('Installed9 approved-source parts; retired7 old options;35 textures retained;37 options and44 registered PNGs')
