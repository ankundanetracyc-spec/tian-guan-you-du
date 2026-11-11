"""Copy the selected exact-RGBA candidates; no active writes."""
from pathlib import Path
import json,shutil,hashlib
P=Path(__file__).resolve().parents[1]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
data=json.loads((P/'source/mirror_candidates.json').read_text(encoding='utf-8-sig'))
records=[]
for r in data['records']:
    if r['source_side']!=('left' if r['part']=='eyes' else 'right'):continue
    dest=P/'male'/r['part']/(f"{r['index']:02}.png")
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(P/r['candidate'],dest)
    records.append({'part':r['part'],'index':r['index'],'candidate':dest.relative_to(P).as_posix(),'candidate_sha256':sha(dest),
      'source_ref':r['source'],'source_sha256':r['source_sha256'],'side':r['source_side'],'axis_x':513,
      'reflection':'x_prime=1026-x','center_column_policy':'original513 retained','scale':1,'rotation_deg':0,'interpolation':False,
      'source_side_RGBA_difference_channels':0,'mirror_RGBA_difference_channels':0,'center_Alpha_nonzero_pixels':r['central_band_x508_518_Alpha_nonzero_pixels']})
(P/'source/selected_features.json').write_text(json.dumps({'schema':'male-original-single-side-selected/v1',
  'selection':'Three eyes sourceLEFT; three brows sourceRIGHT. Root selected after original-side previews.',
  'records':records,'active_writes':False},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'selected':len(records),'files':[r['candidate'] for r in records]}))
