from pathlib import Path
from PIL import Image
import hashlib,json,shutil
R=Path(__file__).resolve().parent.parent;B=R.parent;P=B.parents[2];A=B.parent/'ink_three_variants_20261004';H=R/'head_only';I=H/'installation';I.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
res=lambda p:'res://'+p.relative_to(P).as_posix()
baseline=json.loads((R/'installation/baseline.json').read_text(encoding='utf8'))
expected=baseline.copy()
if (I/'install_report.json').exists():
 for record in json.loads((I/'install_report.json').read_text(encoding='utf8'))['records']:expected[record['resource_path']]=record['sha256']
for file,digest in expected.items():assert sha(P/file[6:])==digest,f'Activity changed outside head revision: {file}'
records=[]
for part,n in {'hair_front':3,'hair_back':3,'eyebrows':3,'eyes':2,'nose':1,'mouth':1,'face_shape':1,'clothing':7,'clothing_back':7}.items():
 for i in range(1,n+1):
  rel=Path(part)/f'{i:02}.png';before=R/'before_male'/rel;target=H/'male'/rel;source=R/'male'/rel if part=='face_shape' else before
  target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
  with Image.open(target) as im:assert im.size==(1024,1536) and im.mode=='RGBA'
  records.append({'gender':'male','part':part,'index':i,'candidate_path':res(target),'resource_path':res(A/'male'/rel),'sha256':sha(target),'before_resource_path':res(before),'before_sha256':sha(before)})
changed=[r for r in records if r['sha256']!=r['before_sha256']];assert len(changed)==1 and changed[0]['part']=='face_shape'
review=json.loads((R/'qa/fresh_head_review.json').read_text(encoding='utf8'));assert review['root_visual_review_complete'] and not review['user_visual_approval']
assert review['selected_sha256']==changed[0]['sha256']
for name in ['appearance_parts.json','portrait_manifest.json']:
 backup=I/name.replace('.json','.before.json')
 if not backup.exists():shutil.copy2(A/name,backup)
configpath=A/'appearance_parts.json';config=json.loads(configpath.read_text(encoding='utf8'));manifestpath=A/'portrait_manifest.json';manifest=json.loads(manifestpath.read_text(encoding='utf8'))
manifest['latest_male_rebuild']['fresh_high_skull_head']={'source':res(H),'paint_source':res(R/'source/head_final_native.png'),'mode':'Fresh built-in image_gen head, native PNG selected byte for byte without RGB or Alpha editing','old_high_skull_reference_only':True,'canvas_and_origin_preserved':True,'seven_collars_visually_verified':True,'smooth_neck_contour_no_notch':True,'scope':'One new head only; posterior repartition candidates are staged, not installed, per user request to do head first.','user_visual_approval':False}
r=changed[0];shutil.copy2(P/r['candidate_path'][6:],P/r['resource_path'][6:]);shutil.copy2(P/r['candidate_path'][6:],B/'male/face_shape/01.png')
manifestpath.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
unchanged={k:v for k,v in baseline.items() if k!=r['resource_path']}
for k,v in unchanged.items():assert sha(P/k[6:])==v
overlay=config['anchor_overlays']['male'];preserved={k:v for k,v in baseline.items() if k not in [r['resource_path'] for r in records]}
report={'expected_installed_count':28,'expected_changed_count':1,'records':records,'baseline_sha256':preserved,'all_unchanged_sha256':unchanged,'female_baseline_sha256':{k:v for k,v in baseline.items() if '/female/' in k},'config_sha256':sha(configpath),'config_before_sha256':sha(configpath),'config_previous_overlay':overlay,'anchor_overlay_resource_path':overlay,'anchor_overlay_sha256':sha(P/overlay[6:]),'manifest_after_sha256':sha(manifestpath),'physical_png_count':51,'active_option_count':44,'source_mode':'Fresh built-in image_gen high round head and smooth neck; native PNG selected without pixel editing','native_source_sha256':sha(R/'source/head_final_native.png'),'visual_acceptance':'Root reviewed clean scalp/ears/jaw, high skull, seven neck/collar pairings and original separate features; user visual approval remains false'}
(I/'install_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
test=(P/'tools/verify_portrait_female_hair_match_20261105.gd').read_text(encoding='utf8').replace('portrait_original_restore_20261105_v6/female_hair_match_20261105/','portrait_original_restore_20261105_v6/skull_partition_fix_20261105/head_only/').replace('28 selected male assets checked, six hair color/texture edits','28 selected male assets checked, one fresh head')
(P/'tools/verify_portrait_fresh_high_head_20261105.gd').write_text(test,encoding='utf8')
print('Installed one fresh high-skull head; 50 textures and configuration unchanged; rear candidates not installed')
