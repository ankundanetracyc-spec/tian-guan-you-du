from pathlib import Path
import json,hashlib,shutil
from PIL import Image
R=Path(__file__).resolve().parent.parent
P=R.parents[3]; A=P/'mods/appearance/ink_three_variants_20261004'; H=R/'hair_set'; I=H/'installation'
I.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
res=lambda p:'res://'+p.relative_to(P).as_posix()
old=json.loads((R/'head_only/installation/install_report.json').read_text(encoding='utf8'))
baseline={**old['baseline_sha256'],**{r['resource_path']:r['sha256'] for r in old['records']}}
for path,digest in baseline.items():assert sha(P/path[6:])==digest,path
records=[]
for previous in old['records']:
 rel=Path(previous['part'])/f"{previous['index']:02}.png"
 active=A/'male'/rel; before=H/'before_male'/rel; target=H/'male'/rel
 before.parent.mkdir(parents=True,exist_ok=True);target.parent.mkdir(parents=True,exist_ok=True)
 shutil.copy2(active,before)
 source=R/'male'/rel if previous['part']=='hair_back' else active
 if previous['part']=='hair_front':assert sha(source)==sha(R/'male'/rel)
 shutil.copy2(source,target)
 with Image.open(target) as im:assert im.size==(1024,1536) and im.mode=='RGBA'
 records.append(dict(gender='male',part=previous['part'],index=previous['index'],candidate_path=res(target),resource_path=res(active),sha256=sha(target),before_resource_path=res(before),before_sha256=sha(before)))
changed=[r for r in records if r['sha256']!=r['before_sha256']]
assert len(changed)==3 and all(r['part']=='hair_back' for r in changed)
for r in changed:
 shutil.copy2(P/r['candidate_path'][6:],P/r['resource_path'][6:])
 shutil.copy2(P/r['candidate_path'][6:],R.parent/'male/hair_back'/f"{r['index']:02}.png")
manifestpath=A/'portrait_manifest.json';manifest=json.loads(manifestpath.read_text(encoding='utf8'))
manifest['latest_male_rebuild']['fresh_high_skull_head']['scope']='Fresh native head installed; three posterior hair replacements now installed separately.'
manifest['latest_male_rebuild']['posterior_partition']={'source':res(H),'front_hair':'Existing three female-color-matched front hair options retained','rear_hair':'Three narrowed posterior layers installed','canvas':[1024,1536],'offset':[0,0],'user_visual_approval':False}
manifestpath.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
report=dict(old);report.update(records=records,expected_changed_count=3,all_unchanged_sha256={p:d for p,d in baseline.items() if p not in [r['resource_path'] for r in changed]},manifest_after_sha256=sha(manifestpath),source_mode='Three posterior layers paired with existing three front layers and final native head')
report.pop('native_source_sha256',None)
for p,d in report['all_unchanged_sha256'].items():assert sha(P/p[6:])==d
(I/'install_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
(H/'source').mkdir(exist_ok=True)
shutil.copy2(R/'head_only/source/refresh_imports.py',H/'source/refresh_imports.py')
test=(P/'tools/verify_portrait_fresh_high_head_20261105.gd').read_text(encoding='utf8').replace('skull_partition_fix_20261105/head_only/','skull_partition_fix_20261105/hair_set/').replace('one fresh head','three posterior hair replacements')
(P/'tools/verify_portrait_hair_set_20261105.gd').write_text(test,encoding='utf8')
print('Installed three posterior hair layers; three front options retained; 48 other PNGs unchanged')
