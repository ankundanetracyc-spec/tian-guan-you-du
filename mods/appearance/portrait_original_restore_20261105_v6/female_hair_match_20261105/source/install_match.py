from pathlib import Path
from PIL import Image
import json,hashlib,shutil
R=Path(__file__).resolve().parent.parent;B=R.parent;P=B.parents[2];A=B.parent/'ink_three_variants_20261004'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
res=lambda p:'res://'+p.relative_to(P).as_posix()
baseline=json.loads((R/'installation/baseline.json').read_text(encoding='utf8'))
for k,v in baseline.items():assert sha(P/k[6:])==v,f'Active file changed: {k}'
parts={'hair_front':3,'hair_back':3,'eyebrows':3,'eyes':2,'nose':1,'mouth':1,'face_shape':1,'clothing':7,'clothing_back':7}
records=[]
for part,count in parts.items():
 for i in range(1,count+1):
  rel=Path(part)/f'{i:02}.png';candidate=R/'male'/rel;before=R/'before_male'/rel;target=A/'male'/rel
  if not candidate.exists():candidate.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(before,candidate)
  with Image.open(candidate) as im:assert im.size==(1024,1536) and im.mode=='RGBA'
  records.append({'gender':'male','part':part,'index':i,'candidate_path':res(candidate),'resource_path':res(target),'sha256':sha(candidate),'before_resource_path':res(before),'before_sha256':sha(before)})
changed=[r for r in records if r['sha256']!=r['before_sha256']]
assert len(changed)==6 and all(r['part'] in ['hair_front','hair_back'] for r in changed)
review=json.loads((R/'qa/final_review.json').read_text(encoding='utf8'))
assert review['root_visual_review_complete'] and not review['user_visual_approval']
for r in records:assert review['selected_sha256'][r['candidate_path']]==r['sha256']
for name in ['appearance_parts.json','portrait_manifest.json']:shutil.copy2(A/name,R/'installation'/name.replace('.json','.before.json'))
config_path=A/'appearance_parts.json';config=json.loads(config_path.read_text(encoding='utf8'));manifest_path=A/'portrait_manifest.json';manifest=json.loads(manifest_path.read_text(encoding='utf8'))
manifest['latest_male_rebuild']['female_hair_match']={'source':res(R),'changed_pngs':6,'scope':'Local right-temple correction in front03; female-reference chroma and micro-highlight calibration for six hair layers. Other hair alpha unchanged. Head, neck, clothes and female PNGs unchanged.','user_visual_approval':False}
for r in changed:
 src=P/r['candidate_path'][6:];shutil.copy2(src,P/r['resource_path'][6:]);shutil.copy2(src,B/'male'/r['part']/f"{r['index']:02}.png")
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
unchanged={k:v for k,v in baseline.items() if k not in [r['resource_path'] for r in changed]}
for k,v in unchanged.items():assert sha(P/k[6:])==v
preserved={k:v for k,v in baseline.items() if k not in [r['resource_path'] for r in records]};overlay=config['anchor_overlays']['male']
report={'expected_installed_count':28,'expected_changed_count':6,'records':records,'baseline_sha256':preserved,'all_unchanged_sha256':unchanged,'female_baseline_sha256':{k:v for k,v in baseline.items() if '/female/' in k},'config_sha256':sha(config_path),'config_before_sha256':sha(config_path),'config_previous_overlay':overlay,'anchor_overlay_resource_path':overlay,'anchor_overlay_sha256':sha(P/overlay[6:]),'manifest_after_sha256':sha(manifest_path),'physical_png_count':51,'active_option_count':44,'source_mode':'Built-in image_gen local temple edit; female-reference RGB micro-highlight/chroma calibration','visual_acceptance':'Root reviewed reference and before/after crops, layer boundaries and mixed portraits; user visual approval remains false'}
(R/'installation/install_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
test=(P/'tools/verify_portrait_cutout_cleanup_20261105.gd').read_text(encoding='utf8').replace('portrait_original_restore_20261105_v6/edge_cleanup_20261105/','portrait_original_restore_20261105_v6/female_hair_match_20261105/').replace('28 selected male assets checked, 12 cutout cleanup changes','28 selected male assets checked, six hair color/texture edits')
(P/'tools/verify_portrait_female_hair_match_20261105.gd').write_text(test,encoding='utf8')
print('Installed six hair layers; 45 registered textures and selection configuration unchanged')
