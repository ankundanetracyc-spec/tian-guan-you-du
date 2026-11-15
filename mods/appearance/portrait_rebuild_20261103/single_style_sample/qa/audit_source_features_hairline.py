from pathlib import Path
from PIL import Image,ImageDraw
import ast,cv2,numpy as np,json,hashlib
root=Path('.');base=root/'mods/appearance/portrait_rebuild_20261103/single_style_sample';qa=base/'qa';r=json.loads((qa/'independent_readonly_audit.json').read_text(encoding='utf8'))
old=np.array(Image.open(base/'revisions/hair_front_before_hairline.png').convert('RGBA'));new=np.array(Image.open(base/'parts/hair_front.png').convert('RGBA'))
visible=((old[:,:,3]>0)|(new[:,:,3]>0));diff=(old!=new).any(axis=2)&visible
fixed={}
for label,roi in {'above175':(0,175),'below355':(355,1536)}.items():
 y0,y1=roi;fixed[label]={'changed_visible_pixels':int(diff[y0:y1].sum()),'changed_alpha_pixels':int((old[y0:y1,:,3]!=new[y0:y1,:,3]).sum())}
def edge(a):
 ys=np.where(a[180:340,516:521,3].mean(1)>128)[0];return int(ys.max()+180)
r['hairline_local_revision']={'before_y':edge(old),'after_y':edge(new),'fixed_region_checks':fixed,'changed_visible_bbox':list(Image.fromarray(diff).getbbox()),'actual_sample_hairline_override':[518,edge(new)],'original_rig_hairline_y':257,'scope':'This sample local inner edge only; rig and all other parts unmodified.'}
records=json.loads((base/'revisions/features_pixel_extraction.json').read_text(encoding='utf8'));tree=ast.parse((root/'tools/extract_single_sample_features_20261103.py').read_text(encoding='utf8'));specs=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='specs' for t in n.targets));src=np.array(Image.open(root/'mods/appearance/portrait_rebuild_20261103/source/female_hair_final/reference_01.png').convert('RGB')).astype(float)
checks={}
for rec in records:
 k=rec['part'];s=specs[k];x0,y0,x1,y1=s['box'];h,w=y1-y0,x1-x0;mask=Image.new('L',(w*4,h*4))
 for poly in s['cores']:ImageDraw.Draw(mask).polygon([((x-x0)*4,(y-y0)*4) for x,y in poly],fill=255)
 core=np.array(mask.resize((w,h),Image.Resampling.LANCZOS)).astype(np.float32)/255;matrix=np.array(rec['uniform_similarity_matrix'],np.float32);coverage=np.clip(cv2.warpAffine(core,matrix,(1024,1536),flags=cv2.INTER_LANCZOS4),0,1);expected=cv2.warpAffine((src[y0:y1,x0:x1]*core[:,:,None]).astype(np.float32),matrix,(1024,1536),flags=cv2.INTER_LANCZOS4)/np.maximum(coverage[:,:,None],1e-5);actual=np.array(Image.open(base/'parts'/f'{k}.png'));strict=coverage>=.9999
 if strict.any():
  err=np.abs(actual[:,:,:3].astype(float)-np.rint(np.clip(expected,0,255)));maxerr=int(err[strict].max());meanerr=float(err[strict].mean())
 else:maxerr=None;meanerr=None
 checks[k]={'source_sha_matches':hashlib.sha256(Path(rec['source']).read_bytes()).hexdigest()==rec['source_sha256'],'source_core_pixel_count':int(strict.sum()),'core_alpha_min':int(actual[:,:,3][strict].min()) if strict.any() else None,'core_source_rgb_max_channel_error':maxerr,'core_source_rgb_mean_error':meanerr,'caveat':'Strict warped coverage>=0.9999 compares exported RGB against independently resampled source core, not source-control certification; eyebrow has no opaque source core.'}
r['source_core_checks']=checks
r['status']='single_sample_source_pixel_features_and_local_hairline_under_user_review'
r['visual_observations']['style']='Previous generated eye/lip style observations are superseded by the source-pixel extraction; new appearance requires user review.'
r['needed_changes_or_decisions']=[s for s in r['needed_changes_or_decisions'] if not s.startswith('Review eye size')];r['needed_changes_or_decisions'].append('Local sample hairline y277 overrides original rig hairline257 for this sample; user visual review pending.')
(qa/'independent_readonly_audit.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps({'hairline':r['hairline_local_revision'],'sourcecores':checks,'actual_pupils':r['coordinate_evidence']['pupils'],'actual_mouth':r['coordinate_evidence']['mouth'],'face_equal':r['face_source_equal_bytes'],'face_sha':r['face_source_sha256']},indent=2))
