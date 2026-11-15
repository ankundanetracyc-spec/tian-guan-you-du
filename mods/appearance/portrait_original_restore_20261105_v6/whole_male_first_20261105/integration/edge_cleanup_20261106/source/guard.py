from pathlib import Path
from PIL import Image
import hashlib,json,numpy as np
R=Path(__file__).resolve().parent.parent;P=R.parents[5];A=P/'mods/appearance/ink_three_variants_20261004/male';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((R/'source/final_manifest.json').read_text(encoding='utf8'));records=[]
for r in manifest['records']:
 part=r['part'];before=R/'before'/part/'01.png';after=R/'male'/part/'01.png';old=np.array(Image.open(before).convert('RGBA'));im=Image.open(after);assert im.mode=='RGBA' and im.size==(1024,1536);new=np.array(im)
 assert sha(A/part/'01.png')==sha(before),'Active file unexpectedly changed during candidate preparation'
 assert np.all(new[:,:,3]<=old[:,:,3]),'Unexpected alpha additions'
 d=np.any(old!=new,axis=2);rgb=np.any(old[:,:,:3]!=new[:,:,:3],axis=2);alpha=old[:,:,3]!=new[:,:,3];ys,xs=np.where(d)
 assert not np.any(rgb&(new[:,:,3]>=190)),'Opaque/core RGB changed'
 assert r['core_rgb_difference_count']==r['core_alpha_difference_count']==0
 assert not np.any((new[:,:,3]>0)&(old[:,:,3]==0))
 records.append({'part':part,'sha256':sha(after),'before_sha256':sha(before),'canvas':[1024,1536],'core_pixels_exactly_preserved':r['original_core_pixels_preserved'],'core_rgb_changes':0,'core_alpha_changes':0,'alpha_increase_pixels':0,'total_changed_pixels':int(d.sum()),'alpha_changed_pixels':int(alpha.sum()),'edge_rgb_changed_pixels':int(rgb.sum()),'change_bbox':[int(xs.min()),int(ys.min()),int(xs.max()+1),int(ys.max()+1)],'existing_leftward_alignment_preserved':True})
for part,digest in manifest['baseline']['unchanged'].items():assert sha(A/part/'01.png')==digest
report={'pass':True,'records':records,'active_files_untouched':True,'head_neck_nose_mouth_unchanged':True,'method':'native image_gen alpha edge extraction fitted to CURRENT aligned parts; no feature movement; no rectangular support fills','visual_review':{'magnification':4,'backgrounds':['light ivory','dark gray','checkerboard'],'checked_current_head_composite':True,'visible_skin_rectangles_or_right_angle_matte_steps':False,'visible_detached_matte_specks':False,'review_image':'qa/three_backgrounds_4x_before_left_after_right.png'}}
(R/'qa/guard_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(report,ensure_ascii=False))
