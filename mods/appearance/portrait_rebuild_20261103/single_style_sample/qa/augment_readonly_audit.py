from PIL import Image
from pathlib import Path
import numpy as np,json,math
p=Path('mods/appearance/portrait_rebuild_20261103/single_style_sample');q=p/'qa/independent_readonly_audit.json';r=json.loads(q.read_text(encoding='utf8'))
r['status']='partial_readonly_audit_nose_revision_pending_not_visual_approval'
r['coordinate_evidence']={'origin':'All layers composited unshifted at (0,0), scale 1 on a 1024x1536 canvas. Canvas compatibility is verified; a complete registration scaffold is absent, so full multi-point residual certification is unverified.','pupils':[],'mouth':{},'eyebrows':[]}
for k in ['eyes','eyebrows','mouth']:
 a=np.array(Image.open(p/'parts'/f'{k}.png')).astype(float)
 regions=({'left':(453,374,475,389),'right':(563,374,585,389)} if k=='eyes' else {'left':(455,340,460,365),'right':(580,340,585,365)} if k=='eyebrows' else {'center':(507,479,531,488)})
 targets=({'left':(464,379),'right':(574,379)} if k=='eyes' else {'left':(457,354),'right':(582,353)} if k=='eyebrows' else {'center':(519,483)})
 for label,(x0,y0,x1,y1) in regions.items():
  v=a[y0:y1,x0:x1];g=v[:,:,:3].mean(axis=2);w=np.maximum(0,75-g)*v[:,:,3]/255 if k=='eyes' else v[:,:,3]*(255-g)/255;yy,xx=np.indices(w.shape);xy=[float((xx*w).sum()/w.sum()+x0),float((yy*w).sum()/w.sum()+y0)];target=targets[label]
  e={'side':label,'measured':xy,'rig':list(target),'roi':[x0,y0,x1,y1],'measurement':'Dark-core weighted center, gray<75' if k=='eyes' else 'Dark pigment weighted center in fixed ROI','distance_px':math.dist(xy,target),'caveat':'Pigment estimate supports visual placement; it is not a source-scaffold control point and is not full registration certification.'}
  if k=='eyes':r['coordinate_evidence']['pupils'].append(e)
  elif k=='eyebrows':r['coordinate_evidence']['eyebrows'].append(e)
  else:r['coordinate_evidence']['mouth']=e
r['visual_observations']={'ear_roi':'Viewed isolated front hair on dark background at native ROI x380..690,y300..600 enlarged 3x; no recognizable ear contour or skin fragment was seen. This does not prove pixel-perfect semantic absence.','front_hairline':'Viewed x400..640,y160..320 at 4x. Warm highlights are concentrated in the center part/root, with no obvious detached skin patch. Complete skull cap and forehead join look covered in the unshifted assembly.','front_chest_locks':'Both isolated front locks are continuous from crown/temples through the chest to roughly y940..951; no shoulder-edge truncation visible.','rear_hair':'Isolated rear layer is a continuous filled head/neck/shoulder curtain. Center alpha253 at sampled y300,379,450,520,600; no large face hole found.','style':'Against documented female original composite, new eyes have a rounder opening and stronger pupil/iris contrast. Lip includes discrete pale highlights. These require style review under the restrained eye line/highlight requirements; not approved by pixel geometry alone.','nose':'Warm shading on audited nose was visibly stronger than original; root is revising this layer. Nose verdict deferred until new hash is received.'}
a=np.array(Image.open(p/'parts/hair_front.png'));r['front_hair_alpha_bounds']={str(t):list(Image.fromarray(a[:,:,3]>=t).getbbox()) for t in [1,8,16,32,64,128,200]}
r['needed_changes_or_decisions']=['Clean very low-alpha stray front-hair pixels beyond the actual silhouette without cropping the full chest strands. Alpha1 bbox is [0,23,986,1520]; alpha8 bbox is [341,98,703,952].','Review eye size/line strength and lip highlights against original style before extending batches.','Reaudit revised nose; previous inspected hash is recorded under parts.nose.','Full three-point registration cannot be certified from declared rig targets alone; actual source scaffold evidence is unavailable.']
r['not_verified']=['User style approval','Complete multi-point source control registration','Semantic skin absence for every front-hair pixel','Runtime selection/UI installation (sample remains isolated)','Original clothing embedded hair remains a separate concern']
q.write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(r['coordinate_evidence'],ensure_ascii=False,indent=2))
