from pathlib import Path
import hashlib,json,shutil
from PIL import Image
R=Path(__file__).resolve().parent.parent
B=R.parent
P=B.parents[2]
A=B.parent/'ink_three_variants_20261004'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
resource=lambda p:'res://'+p.relative_to(P).as_posix()
baseline=json.loads((R/'installation/baseline.json').read_text(encoding='utf8'))
expected_active=baseline.copy()
prior_report=R/'installation/install_report.json'
if prior_report.exists():
 for record in json.loads(prior_report.read_text(encoding='utf8'))['records']:expected_active[record['resource_path']]=record['sha256']
for k,v in expected_active.items():assert sha(P/k[6:])==v,f'Active texture changed outside this cleanup: {k}'
parts={'hair_front':3,'hair_back':3,'eyebrows':3,'eyes':2,'nose':1,'mouth':1,'face_shape':1,'clothing':7,'clothing_back':7}
records=[]
for part,count in parts.items():
 for i in range(1,count+1):
  relative=Path(part)/f'{i:02}.png';candidate=R/'male'/relative;before=R/'before_male'/relative;destination=A/'male'/relative
  if not candidate.exists():
   candidate.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(before,candidate)
  with Image.open(candidate) as im:assert im.size==(1024,1536) and im.mode=='RGBA',str(candidate)
  assert sha(before)==baseline[resource(destination)]
  records.append({'gender':'male','part':part,'index':i,'candidate_path':resource(candidate),'resource_path':resource(destination),'sha256':sha(candidate),'before_resource_path':resource(before),'before_sha256':sha(before)})
changed=[r for r in records if r['sha256']!=r['before_sha256']]
assert len(changed)==12,f'Expected six hair, three brow, two eye and one face layers, got {len(changed)}'
with Image.open(R/'male/face_shape/01.png') as current,Image.open(R/'before_male/face_shape/01.png') as old:
 assert current.getchannel('A').tobytes()==old.getchannel('A').tobytes(),'Face/neck alpha changed'
review=json.loads((R/'qa/final_review.json').read_text(encoding='utf8'))
assert review['root_visual_review_complete'] and not review['user_visual_approval']
for r in records:assert review['selected_sha256'][r['candidate_path']]==r['sha256']
configuration=A/'appearance_parts.json';config=json.loads(configuration.read_text(encoding='utf8'))
manifest_path=A/'portrait_manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf8'))
for name in ['appearance_parts.json','portrait_manifest.json']:
 backup=R/'installation'/name.replace('.json','.before.json')
 if not backup.exists():shutil.copy2(A/name,backup)
manifest['latest_male_rebuild']['edge_cleanup']={'source':resource(R),'changed_pngs':12,'new_builtin_generated_layers':6,'scope':'Six hair layers regenerated from original and current version; ear remnants removed from front01, front03 interior wisps cleaned, back02 lower curtain reworked. Previous head restored as requested, skin matches third original; its face/neck alpha retained exactly. Three brows/two eyes retain original cores with contaminated ends masked.','user_visual_approval':False}
for r in changed:
 src=P/r['candidate_path'][6:]
 shutil.copy2(src,P/r['resource_path'][6:]);shutil.copy2(src,B/'male'/Path(r['part'])/f"{r['index']:02}.png")
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
preserved={k:v for k,v in baseline.items() if k not in [r['resource_path'] for r in records]}
for k,v in baseline.items():
 if k not in [r['resource_path'] for r in changed]:assert sha(P/k[6:])==v
overlay=config['anchor_overlays']['male']
report={'expected_installed_count':28,'expected_changed_count':len(changed),'records':records,'baseline_sha256':preserved,'female_baseline_sha256':{k:v for k,v in baseline.items() if '/female/' in k},'config_sha256':sha(configuration),'config_before_sha256':sha(configuration),'config_previous_overlay':overlay,'anchor_overlay_resource_path':overlay,'anchor_overlay_sha256':sha(P/overlay[6:]),'manifest_after_sha256':sha(manifest_path),'physical_png_count':51,'active_option_count':44,'source_mode':'Six built-in image_gen hair edits; previous head RGB cleanup with exact alpha retained; five original feature alpha cleanups','visual_acceptance':'Root reviewed isolated layers on light/dark, before/after faces and mixed hairstyles; user approval remains false'}
(R/'installation/install_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
test=(P/'tools/verify_portrait_original_pixels_20261105.gd').read_text(encoding='utf8')
test=test.replace('portrait_original_restore_20261105_v6/','portrait_original_restore_20261105_v6/edge_cleanup_20261105/')
test=test.replace('14 original-pixel male head components and 14 unchanged clothing layers installed','28 selected male assets checked, 12 cutout cleanup changes')
(P/'tools/verify_portrait_cutout_cleanup_20261105.gd').write_text(test,encoding='utf8')
print(f'Installed {len(changed)} cleanup PNGs; all other active PNGs and configuration preserved')
