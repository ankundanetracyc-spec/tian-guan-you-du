"""Diagnostic composites only; candidate/source pixels remain unchanged."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np,cv2,json
Q=Path(__file__).resolve().parent;P=Q.parent;S=P/'reference/current_male'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
def layer(part,i):return Image.open(S/part/f'{i:02}.png').convert('RGBA')
variants=[('Before',layer('eyes',4)),('Symmetric8',Image.open(P/'candidate/left_eye_symmetric_8px.png').convert('RGBA')),('Native corner6',Image.open(P/'candidate/left_eye_native_corner_6px.png').convert('RGBA'))]
def compose(eyes,hair,bg):
    out=Image.new('RGBA',(1024,1536),bg)
    for im in ([layer('hair_back',4)] if hair else [])+[layer('face_shape',1),layer('nose',1),eyes,layer('eyebrows',4),layer('mouth',1)]+([layer('hair_front',hair)] if hair else []):out.alpha_composite(im)
    return out.convert('RGB')
for theme,bg,fg in [('light','#e8e1d6','#222222'),('dark','#202b31','#eeeeee')]:
    normal=Image.new('RGB',(1020,450),bg);dn=ImageDraw.Draw(normal)
    close=Image.new('RGB',(1344,1416),bg);dc=ImageDraw.Draw(close)
    for row,hair in enumerate((None,4,5)):
        for col,(label,eyes) in enumerate(variants):
            im=compose(eyes,hair,bg);tag='no front' if hair is None else f'front{hair:02}'
            normal.paste(im.crop((350,318,690,433)),(col*340,row*150+32));dn.text((col*340+5,row*150+6),f'{label} / {tag} / native1x',font=font,fill=fg)
            close.paste(im.crop((425,350,453,377)).resize((448,432),Image.Resampling.NEAREST),(col*448,row*472+36));dc.text((col*448+5,row*472+8),f'{label} / {tag} /16x',font=font,fill=fg)
    normal.save(Q/f'left_seam_variants_native1x_{theme}.png');close.save(Q/f'left_seam_variants_closeup16x_{theme}.png')
target=np.array(Image.open(P/'reference/reported_seam.png').convert('RGB'))
baseline=compose(variants[0][1],4,'#e8e1d6');roi=(410,325,480,405);area=np.array(baseline.crop(roi));best=None
def scan(scales):
    global best
    for scale in scales:
        img=cv2.resize(area,None,fx=float(scale),fy=float(scale),interpolation=cv2.INTER_LINEAR)
        if img.shape[0]<target.shape[0] or img.shape[1]<target.shape[1]:continue
        score,_,xy,_=cv2.minMaxLoc(cv2.matchTemplate(img,target,cv2.TM_SQDIFF_NORMED))
        if best is None or score<best[0]:best=(float(score),float(scale),xy)
scan(np.arange(.95,3.51,.025));scan(np.arange(max(.95,best[1]-.035),best[1]+.036,.0025))
score,scale,(xx,yy)=best;x0=roi[0]+xx/scale;y0=roi[1]+yy/scale;rect=[x0,y0,x0+target.shape[1]/scale,y0+target.shape[0]/scale]
report={'reported_crop_size_wh':[target.shape[1],target.shape[0]],'normalized_sqdiff_score':score,'matched_scale':scale,'approx_runtime_rect_xyxy':rect,'seam_x439_46_in_reported_crop':(439.4608-x0)*scale,'note':'Continuous-scale template approximation for diagnostics; raster sampling/viewport filtering make subpixel rect approximate.'}
(Q/'reported_crop_runtime_match.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
comparison=Image.new('RGB',(54*3,100),'#e8e1d6');d=ImageDraw.Draw(comparison)
for col,(name,eyes) in enumerate(variants):
    # Resample full compose using the inferred viewport scale/translation, not per-eye alignment.
    im=compose(eyes,4,'#e8e1d6').transform((54,75),Image.Transform.AFFINE,(1/scale,0,x0,0,1/scale,y0),Image.Resampling.BILINEAR)
    comparison.paste(im,(col*54,25));d.text((col*54+1,1),['Before','Mix8','Native6'][col],font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',10),fill='#222222')
comparison.save(Q/'reported_crop_variant_comparison_1x.png');comparison.resize((1296,800),Image.Resampling.NEAREST).save(Q/'reported_crop_variant_comparison_8x.png')
print(json.dumps(report))
