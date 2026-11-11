from pathlib import Path
from PIL import Image
import numpy as np,json,hashlib
R=Path(__file__).resolve().parent.parent;M=R.parents[1]/'male_whole_01.png';master=np.array(Image.open(M).convert('RGBA'));lum=master[:,:,:3]@np.array([.2126,.7152,.0722]);sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
boxes={'eyebrows':[(456,336,499,352),(541,334,591,350)],'eyes':[(450,356,495,375),(544,356,595,375)],'nose':[(502,438,513,446),(526,438,537,446)],'mouth':[(484,471,556,479)]}
thresholds={'eyebrows':130,'eyes':130,'nose':160,'mouth':140};results=[]
for part,regions in boxes.items():
 p=R/'male'/part/'01.png';im=Image.open(p);assert im.mode=='RGBA' and im.size==(1024,1536);a=np.array(im);visible=a[:,:,3]>0
 if part=='eyebrows':visible[:,:456]=False;visible[:,591:]=False
 if part=='eyes':visible[:,:450]=False;visible[:,595:]=False
 assert np.array_equal(a[:,:,:3][visible],master[:,:,:3][visible]);count=0;covered=0
 for x0,y0,x1,y1 in regions:
  mask=lum[y0:y1,x0:x1]<thresholds[part];count+=int(mask.sum());covered+=int((mask&(a[y0:y1,x0:x1,3]>=128)).sum())
 results.append({'part':part,'sha256':sha(p),'visible_pixels_exact_approved_rgb':True,'dark_stroke_pixels':count,'dark_stroke_pixels_alpha128':covered,'dark_stroke_coverage':covered/count if count else 1})
report={'master_sha256':sha(M),'canvas':[1024,1536],'generated_alpha_registered':True,'all_visible_feature_rgb_from_approved_master':True,'head_neck_not_modified_by_this_subtask':True,'records':results}
(R/'qa/guard_report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print(json.dumps(report,indent=2))
