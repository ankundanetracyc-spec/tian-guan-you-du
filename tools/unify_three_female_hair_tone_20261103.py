from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib,shutil
from scipy.interpolate import PchipInterpolator
R=Path(__file__).resolve().parents[1];O=R/'mods/appearance/portrait_rebuild_20261103/three_style_parts';B=O/'revisions/hair_tone_01';B.mkdir(parents=True,exist_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
untouched={str(p.relative_to(O)):sha(p) for k in ['hair_front','hair_back'] for p in (O/'female'/k).glob('*.png') if p.stem!='01'}
records=[]
for part in ['hair_front','hair_back']:
 dst=O/'female'/part/'01.png';before=B/f'{part}_before.png'
 if not before.exists():shutil.copyfile(dst,before)
 a=np.array(Image.open(before).convert('RGBA'));rgb=a[:,:,:3].astype(float);lum=rgb@np.array([.2126,.7152,.0722]);yy,xx=np.indices(lum.shape);roi=(xx>=350)&(xx<690)&(yy>=145)&(yy<350)&(a[:,:,3]>=240)
 ref=np.array(Image.open(R/'mods/appearance/ink_three_variants_20261004/female'/part/'01.png').convert('RGBA'));ref_l=ref[:,:,:3]@np.array([.2126,.7152,.0722]);ref_roi=(xx>=350)&(xx<690)&(yy>=145)&(yy<350)&(ref[:,:,3]>=240)
 source_q=np.percentile(lum[roi],[5,25,50,75,95]);target_q=np.percentile(ref_l[ref_roi],[5,25,50,75,95]);goals=source_q+.85*(target_q-source_q)
 x=np.r_[0,16,source_q,170,220,255];y=np.r_[0,13,goals,166,218,255];curve=PchipInterpolator(x,y);slope=curve.derivative()(np.linspace(0,255,4096));new_l=curve(lum);factor=np.divide(new_l,lum,out=np.ones_like(lum),where=lum>0);out=a.copy();out[:,:,:3]=np.rint(np.clip(rgb*factor[:,:,None],0,255));out[out[:,:,3]==0,:3]=0
 Image.fromarray(out).save(dst);after_l=out[:,:,:3]@np.array([.2126,.7152,.0722]);alpha_equal=np.array_equal(a[:,:,3],out[:,:,3]);dark_black_additions=int(((rgb.min(2)>0)&(out[:,:,:3].min(2)==0)&(a[:,:,3]>0)).sum())
 records.append({'part':part,'before':str(before.relative_to(R)),'before_sha256':sha(before),'after':str(dst.relative_to(R)),'after_sha256':sha(dst),'curve':'Monotonic cubic PCHIP luminance curve; target85percent toward original crown quantiles, with tails protected','luma_definition':[.2126,.7152,.0722],'hue_method':'Multiply R,G,B by the same positive factor Lnew/Lold; preserves RGB chromaticity/hue before 8-bit rounding. Alpha untouched.','curve_input_anchors':x.tolist(),'curve_output_anchors':y.tolist(),'minimum_curve_derivative':float(slope.min()),'before_crown_P05_P25_P50_P75_P95':source_q.tolist(),'original_reference_quantiles':target_q.tolist(),'after_crown_quantiles':np.percentile(after_l[roi],[5,25,50,75,95]).tolist(),'alpha_identical':alpha_equal,'new_channel_zero_pixels':dark_black_additions,'no_geometry_changes':True})
assert all(sha(O/k)==v for k,v in untouched.items())
report={'status':'applied_candidate01_only_pending_visual_review','records':records,'style02_03_sha256_unchanged':untouched,'limits':['Not a whole-histogram or mean match. Spatial folds and highlight densities remain different.','No generated or redrawn pixels; existing color values only changed with strictly monotonic tone mapping.','Only isolated candidate01 front/rear changed. Single sample sources, fixed face04,02/03 and active pack untouched.']}
(B/'curve_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
source_report=O/'female_hair_sources_and_limits.json';manifest=json.loads(source_report.read_text(encoding='utf8'))
for rec in manifest['records']:
 if rec['style']==1:
  rec['output_sha256']=sha(O/'female'/rec['part']/'01.png');rec['tone_revision']='revisions/hair_tone_01/curve_report.json'
source_report.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(report,ensure_ascii=False,indent=2))
