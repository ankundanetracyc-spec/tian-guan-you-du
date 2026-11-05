from pathlib import Path
import hashlib,json
B=Path(__file__).resolve().parent.parent
P=B.parents[2]
A=B.parent/'ink_three_variants_20261004'
OLD=B.parent/'portrait_male_faithful_restore_20261104_v5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
old=json.loads((OLD/'installation/current_active_baseline.json').read_text(encoding='utf8'))
registered={k:sha(P/k[6:]) for k in old['registered_png_sha256']}
for k,v in registered.items():
 if '/male/' in k:
  backup=B/'installation/before_male'/k.split('/male/')[1]
  assert sha(backup)==v, f'Active male changed since snapshot: {k}'
configuration={}
for name in ['appearance_parts.json','portrait_manifest.json']:
 assert sha(A/name)==sha(B/'installation'/name.replace('.json','.snapshot.json')),f'Configuration changed: {name}'
 configuration[name]=sha(A/name)
write(B/'installation/current_active_baseline.json',{'registered_png_sha256':registered,'female_registered_png_sha256':{k:v for k,v in registered.items() if '/female/' in k},'configuration_sha256':configuration,'registered_option_count':44})
script=(OLD/'source/install_after_review.cjs').read_text(encoding='utf8')
script=script.replace('portrait_male_faithful_restore_20261104_v5','portrait_original_restore_20261105_v6').replace('installation/current_active_snapshot/male','installation/before_male').replace('expected_changed_count:28','expected_changed_count:14').replace("source_mode:'built-in image_gen'","source_mode:'user-authorized original pixel separation; generated hidden underpainting and unseen ends'").replace('new_native_head_assets:14','original_pixel_head_assets:14').replace('clothing_depth_updated:true','clothing_depth_updated:false')
(B/'source/install_after_review.cjs').write_text(script,encoding='utf8')
test=(P/'tools/verify_portrait_faithful_restore_20261105.gd').read_text(encoding='utf8').replace('portrait_male_faithful_restore_20261104_v5','portrait_original_restore_20261105_v6').replace('14 new male head components and 14 adapted clothing layers installed','14 original-pixel male head components and 14 unchanged clothing layers installed')
(P/'tools/verify_portrait_original_pixels_20261105.gd').write_text(test,encoding='utf8')
print('Snapshot verified: 51 textures; installer and runtime verification prepared')
