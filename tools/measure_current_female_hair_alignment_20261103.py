from pathlib import Path
from PIL import Image,ImageDraw
import cv2,numpy as np,json
P=Path('mods/appearance/portrait_rebuild_20261103');Q=P/'alignment_skin_revision';A=Path('mods/appearance/ink_three_variants_20261004/female');records=json.loads((P/'three_style_parts/feature_extraction.json').read_text(encoding='utf8'))['records'];out=[]
for i in range(1,4):
 rec=next(x for x in records if x['part']=='eyes' and x['variant']==i);src=np.array(rec['spec']['controls']);dst=np.array([[464,379],[574,379]]);v=src[1]-src[0];u=dst[1]-dst[0];aa=np.dot(u,v)/np.dot(v,v);bb=(u[1]*v[0]-u[0]*v[1])/np.dot(v,v);lin=np.array([[aa,-bb],[bb,aa]]);tr=dst.mean(0)-lin@src.mean(0);M=np.column_stack([lin,tr]);ref=np.array(Image.open(rec['source']).convert('RGB'));warp=cv2.warpAffine(ref,M.astype(np.float32),(1024,1536),borderValue=(235,229,217));Image.fromarray(warp).save(Q/f'reference{i:02}_eye_registered.png');f=np.array(Image.open(A/'hair_front'/f'{i:02}.png'));b=np.array(Image.open(A/'hair_back'/f'{i:02}.png'));alpha=1-(1-f[:,:,3]/255)*(1-b[:,:,3]/255);rows=[]
 for y in [180,220,260,300,350,379]:
  m=(warp[y,:,0]<150)&(warp[y,:,:3].mean(1)<140);m[:310]=False;m[730:]=False;rr=np.where(m)[0];cc=np.where(alpha[y]>=.5)[0];rows.append({'y':y,'reference_dark_hair_span':[int(rr.min()),int(rr.max())] if len(rr) else None,'candidate_opaque_hair_span':[int(cc.min()),int(cc.max())] if len(cc) else None})
 # Head crown excluding hairpin: first dark row with a substantial run in a head-centered ROI.
 mask=(warp[:300,400:630,:3].mean(2)<130);ref_top=next(y for y in range(300) if mask[y].sum()>=10);curmask=alpha[:300,400:630]>=.5;cur_top=next(y for y in range(300) if curmask[y].sum()>=10)
 item={'variant':i,'source_eye_controls':src.tolist(),'full_source_to_rig_matrix':M.tolist(),'reference_crown_y_dark10pixels':ref_top,'current_crown_y_alpha128_10pixels':cur_top,'rows':rows};out.append(item)
 cur=Image.new('RGBA',(1024,1536),(235,229,217,255))
 for k in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:cur.alpha_composite(Image.open(A/k/f'{4 if k=="face_shape" else i if k!="clothing" else 1:02}.png').convert('RGBA'))
 board=Image.new('RGB',(1000,1000),(235,229,217));dr=ImageDraw.Draw(board)
 for j,im in enumerate([Image.fromarray(warp),cur.convert('RGB')]):
  board.paste(im.crop((270,20,770,1020)),(j*500,0));dr.line([(j*500+249,0),(j*500+249,1000)],fill='red',width=1)
  for y in [180,220,280,350,379,550,650,750,850,930]:dr.line([(j*500,y-20),(j*500+500,y-20)],fill=(220,130,150),width=1)
 board.save(Q/f'reference{i:02}_vs_current_registered.jpg',quality=96)
(Q/'registered_hair_measurements.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8');print(json.dumps(out,ensure_ascii=False,indent=2))
