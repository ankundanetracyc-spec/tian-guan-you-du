from pathlib import Path
import json,hashlib,shutil
R=Path(__file__).resolve().parent.parent;P=R.parents[4];A=P/'mods/appearance/ink_three_variants_20261004'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
res=lambda p:'res://'+p.relative_to(P).as_posix()
records={}
previous=json.loads((R.parents[1]/'collar_hair_fixed_neck_20261105/collars_only/installation/install_report.json').read_text(encoding='utf8'))
paths=list(previous['baseline_sha256'])+[r['resource_path'] for r in previous['records']]
for file in paths:
 p=P/file[6:]
 rel=p.relative_to(A);q=R/'before'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q);records[res(p)]=sha(p)
assert len(records)==51,len(records)
for name in ['appearance_parts.json','portrait_manifest.json']:shutil.copy2(A/name,R/'installation'/name.replace('.json','.before.json'))
(R/'installation/baseline.json').write_text(json.dumps(records,indent=2),encoding='utf8')
p=R.parent/'source/whole_review_manifest.json';data=json.loads(p.read_text(encoding='utf8'));assert sha(R.parent/data['image'])==data['image_sha256'];data.update(status='用户已明确认可并授权接入，正在制作同源部件',user_visual_approval=True,approval_text='可以，加',integration='integration/');p.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Immutable approved master verified; 51 current textures and both configurations snapshotted')
