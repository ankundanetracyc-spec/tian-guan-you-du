from pathlib import Path
import json,hashlib,shutil
from PIL import Image
R=Path(__file__).resolve().parent.parent;P=R.parents[3];A=P/'mods/appearance/ink_three_variants_20261004';H=R/'collars_only';I=H/'installation';I.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
res=lambda p:'res://'+p.relative_to(P).as_posix()
baseline=json.loads((R/'installation/baseline.json').read_text(encoding='utf8'))
for p,d in baseline.items():assert sha(P/p[6:])==d,p
old=json.loads((R.parent/'skull_partition_fix_20261105/hair_set/installation/install_report.json').read_text(encoding='utf8'))
records=[]
for previous in old['records']:
 rel=Path(previous['part'])/f"{previous['index']:02}.png";before=R/'before/male'/rel;target=H/'male'/rel;source=R/'male'/rel if previous['part'] in ['clothing','clothing_back'] else before
 target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
 with Image.open(target) as im:assert im.size==(1024,1536) and im.mode=='RGBA'
 records.append(dict(gender='male',part=previous['part'],index=previous['index'],candidate_path=res(target),resource_path=res(A/'male'/rel),sha256=sha(target),before_resource_path=res(before),before_sha256=sha(before)))
changed=[r for r in records if r['sha256']!=r['before_sha256']];assert len(changed)==14 and all(r['part'] in ['clothing','clothing_back'] for r in changed)
for r in changed:
 shutil.copy2(P/r['candidate_path'][6:],P/r['resource_path'][6:]);shutil.copy2(P/r['candidate_path'][6:],R.parent/'male'/r['part']/f"{r['index']:02}.png")
manifestpath=A/'portrait_manifest.json';manifest=json.loads(manifestpath.read_text(encoding='utf8'))
manifest['latest_male_rebuild']['fixed_neck_collars']={'source':res(H),'paint_sources':res(R/'source'),'scope':'Seven front garments and seven rear collars. Head, neck, facial parts, female assets and current hair unchanged.','head_sha256':sha(A/'male/face_shape/01.png'),'registration':res(R/'source/registration.json'),'new_hair_status':'Old hairstyle revisions abandoned; separate new pairs await explicit user selection before installation','user_visual_approval':False}
manifestpath.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
report=dict(old);report.update(records=records,expected_changed_count=14,all_unchanged_sha256={p:d for p,d in baseline.items() if p not in [r['resource_path'] for r in changed]},manifest_after_sha256=sha(manifestpath),source_mode='Seven continuous generated garments and rear collars registered to frozen head/neck; no hairstyle installation')
for p,d in report['all_unchanged_sha256'].items():assert sha(P/p[6:])==d
(I/'install_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
(H/'source').mkdir(exist_ok=True)
shutil.copy2(R.parent/'skull_partition_fix_20261105/hair_set/source/refresh_imports.py',H/'source/refresh_imports.py')
# This package is one directory shallower than the previous nested hair_set package.
p=H/'source/refresh_imports.py';p.write_text(p.read_text(encoding='utf8').replace('P=R.parents[4]','P=R.parents[4]'),encoding='utf8')
test=(P/'tools/verify_portrait_hair_set_20261105.gd').read_text(encoding='utf8').replace('skull_partition_fix_20261105/hair_set/','collar_hair_fixed_neck_20261105/collars_only/').replace('three posterior hair replacements','fourteen garment layers; head and hair unchanged')
(P/'tools/verify_portrait_fixed_neck_collars_20261105.gd').write_text(test,encoding='utf8')
print('Installed 14 garment layers; 37 other textures and configuration unchanged; new hair NOT installed')
