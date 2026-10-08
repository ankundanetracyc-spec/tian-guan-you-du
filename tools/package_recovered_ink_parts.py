"""Write the catalog and measured registration contract for the recovered portraits."""
import json,hashlib
from PIL import Image,ImageDraw
import build_recovered_ink_parts as b

def package():
 cfg=json.loads((b.OLD/'appearance_parts.json').read_text(encoding='utf-8'))
 cfg.update(enabled=True,package_id=b.R.name,rig_id=b.R.name,rig_file='res://'+str((b.R/'rig/portrait_rig.json').relative_to(b.P)).replace('\\','/'))
 for entry in cfg['components']:
  sex=entry['gender'];slot=entry['file'].split('/')[1];count=3 if slot in ('clothing','face_accessory') else len(b.NUMBERS[sex])+(3 if slot in ('eyes','eyebrows') else 0)
  entry['variant_count']=count
 (b.R/'appearance_parts.json').write_text(json.dumps(cfg,ensure_ascii=False,indent=2),encoding='utf-8')
 old=json.loads((b.OLD/'appearance_parts.json').read_text(encoding='utf-8'));old['enabled']=False
 (b.OLD/'appearance_parts.json').write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf-8')
 rig={'id':b.R.name,'canvas':[1024,1536],'origin_px':[0,0],'export_offset_px':[0,0],'draw_order_back_to_front':[b.SLOTS[k] for k in b.ORDER],
 'registration_method':'Master pupil pair defines one uniform similarity transform for ALL master features; hair uses source correspondences; hidden blank faces use measured ears/chin. No horizontal/vertical independent deformation.',
 'registration_file':'../registration.json','scale_pivots_normalized':{'face_and_feature_scale':{'x':519/1024,'y':430/1536},'eye_variant_scale':{'x':519/1024,'y':377/1536}},'families':{},
 'hair_ownership':'Front: visible scalp hair, hairline, temples and chest locks; back: nape, bun, rear ponytail, ribbons at rear ties and shoulder-behind curtain. No ear-shaped cutouts.',
 'palette':'Matte warm light skin; charcoal ink strands; preserve coloured ribbons; runtime skin/hair tint disabled.',
 'visual_review':'Artist/user acceptance remains pending; see rendered previews for actual composition.'}
 assets=[]
 for sex,ns in b.NUMBERS.items():
  y=375 if sex=='male' else 379;rig['families'][sex]={'eye_pupils_px':[[464,y],[569 if sex=='male' else 574,y]],'original_numbers':ns,'masters':{str(n):{'source_landmarks':b.POINTS[n],'master_to_canvas':b.master_matrix(n).tolist()} for n in ns}}
  overlay=Image.new('RGBA',(1024,1536));d=ImageDraw.Draw(overlay)
  d.line((519,120,519,730),fill=(0,160,185,180),width=2);d.line((360,y,675,y),fill=(0,160,185,180),width=2)
  for px,py in rig['families'][sex]['eye_pupils_px']:
   d.ellipse((px-6,py-6,px+6,py+6),outline=(0,160,185,240),width=2)
  overlay.save(b.R/'rig'/f'{sex}_overlay.png')
  for slot in b.SLOTS:
   for p in sorted((b.R/sex/slot).glob('*.png')):
    im=Image.open(p);assert im.mode=='RGBA' and im.size==(1024,1536)
    assets.append({'path':str(p.relative_to(b.R)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'alpha_bounds':im.getchannel('A').getbbox()})
 assert len(assets)==87,len(assets)
 (b.R/'rig/portrait_rig.json').write_text(json.dumps(rig,ensure_ascii=False,indent=2),encoding='utf-8')
 manifest={'package':b.R.name,'canvas':[1024,1536],'slots':b.SLOTS,'layer_order':[b.SLOTS[k] for k in b.ORDER],'asset_count':len(assets),'old_facial_and_hair_options':'withdrawn; old pack disabled, files retained as history','additional_brow_eye_pairs':{'male':3,'female':3},'clothing_and_accessories':'Existing three clothing/accessory variants retained per sex; no new old-style facial or hair parts mixed in.','assets':assets}
 (b.R/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
 print('Catalog ready:',len(assets),'RGBA parts')

if __name__=='__main__':
 raise SystemExit('新稿已由用户于 2026-10-04 暂时作废；当前使用 ink_three_variants_20261004。')
