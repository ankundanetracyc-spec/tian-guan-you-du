from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import cv2
import json

ROOT = Path(__file__).resolve().parent
PROJECT = Path('E:/stacklands')
SOURCE = PROJECT / 'mods/appearance/portrait_rebuild_20261103/source/female_hair_final/reference_01.png'
HIDDEN = PROJECT / 'mods/appearance/ink_three_variants_20261004/female/hair_back/01.png'
source = Image.open(SOURCE).convert('RGB')
rgb = np.asarray(source).astype(np.float64)
h, w = rgb.shape[:2]
source_left = np.array([259.183, 325.313])
source_right = np.array([367.106, 325.374])
target_left = np.array([464.0, 379.0])
target_right = np.array([574.0, 379.0])
delta = source_right-source_left
ratio = complex(*(target_right-target_left)) / complex(*delta)
a, b = ratio.real, ratio.imag
M = np.array([[a,-b],[b,a]])
t = target_left-M@source_left
inverse = np.linalg.inv(M)
it = -inverse@t

def polygon(points, size=(592,1120), feather=0):
    mask = Image.new('L', size)
    ImageDraw.Draw(mask).polygon(points,fill=255)
    if feather:
        mask = mask.filter(ImageFilter.GaussianBlur(feather))
    return mask

# Rear crown and pin. The front crown is supplied by the separate front layer.
bun = [(142,89),(209,89),(217,72),(249,54),(295,45),(346,51),(379,66),(405,90),
       (466,88),(470,116),(410,116),(388,111),(360,112),(329,108),(301,104),
       (275,105),(245,111),(214,113),(143,115)]
# Visible rear curtains stop above the source garment, with no chest-front locks.
left = [(166,314),(184,325),(189,350),(198,377),(204,407),(218,446),(240,480),
        (245,500),(234,538),(224,565),(191,582),(152,603),(111,620),(62,637),
        (39,642),(28,603),(44,549),(81,490),(115,439),(141,383)]
right = [(442,316),(455,320),(464,364),(475,417),(502,459),(531,508),(551,551),
         (566,598),(582,645),(549,634),(512,621),(473,603),(433,587),(403,575),
         (392,549),(381,518),(380,485),(400,450),(421,410),(430,373)]
v_mask = np.maximum.reduce([np.asarray(polygon(p,(w,h))) for p in [bun,left,right]])/255.0
yy,xx = np.mgrid[:h,:w]
# The reference garment slopes are occluders, not part of this rear-hair asset.
left_y=np.array([497,525,550,570,590,610,630,647])
left_x=np.array([242,235,225,216,174,123,69,16])
right_y=np.array([497,525,550,570,590,610,630,647])
right_x=np.array([391,396,405,415,447,498,549,582])
left_edge=np.interp(yy,left_y,left_x)-2
right_edge=np.interp(yy,right_y,right_x)+2
v_mask[(yy>=497)&(xx<w/2)&(xx>=left_edge)]=0
v_mask[(yy>=497)&(xx>=w/2)&(xx<=right_edge)]=0

# Fit the unpainted paper from strips outside the character; preserve fine edge alpha.
yy,xx = np.mgrid[:h,:w]
bg_samples = ((yy<28)|((xx<24)&(yy<600))|((xx>581)&(yy<600)))
xnorm,ynorm = xx/w,yy/h
design = np.stack([np.ones((h,w)),xnorm,ynorm,xnorm*xnorm,xnorm*ynorm,ynorm*ynorm],axis=-1)
coefs = np.linalg.lstsq(design[bg_samples],rgb[bg_samples],rcond=None)[0]
bg = np.clip(design@coefs,0,255)
lum = rgb@np.array([.2126,.7152,.0722])
bgl = bg@np.array([.2126,.7152,.0722])
contrast = bgl-lum
alpha = np.clip((contrast-4)/np.maximum(bgl-65,1),0,1)
solid = ((lum<180)&(contrast>25)&(v_mask>0)).astype(np.uint8)
interior = cv2.erode(solid,np.ones((3,3),np.uint8),iterations=1)>0
alpha[interior]=1
# Skin has distinctly red/yellow chroma; neither the rear hair nor dark pin does.
skin = (rgb[:,:,0]-rgb[:,:,1]>14)&(rgb[:,:,0]>90)&(rgb[:,:,1]-rgb[:,:,2]>4)
alpha[skin]=0
blue_cloth=(rgb[:,:,2]-rgb[:,:,0]>9)&(rgb[:,:,1]-rgb[:,:,0]>4)
alpha[blue_cloth]=0
alpha*=v_mask
alpha[alpha<0.018]=0
fg = np.clip((rgb-bg*(1-alpha[:,:,None]))/np.maximum(alpha[:,:,None],1e-8),0,255)
foreground = np.dstack([fg,np.rint(alpha*255)]).astype(np.uint8)
foreground[foreground[:,:,3]==0,:3]=0
visible = Image.fromarray(foreground,'RGBA')

# Continuity underneath the face/neck/robe is borrowed from the existing rear-hair
# painting. Its mask is inset behind the front crown and the visible source edges.
hidden_polygon = [(220,107),(397,107),(427,200),(427,337),(420,421),(398,478),
                  (406,550),(448,599),(490,660),(490,980),(441,1050),(304,1100),
                  (171,1050),(112,980),(112,656),(167,599),(209,549),(231,485),
                  (210,421),(190,338),(193,210)]
hidden_mask = polygon(hidden_polygon,feather=.8)
old = Image.open(HIDDEN).convert('RGBA')
hidden_image = old.transform((w,1120),Image.Transform.AFFINE,(a,-b,t[0],b,a,t[1]),Image.Resampling.BICUBIC)
ha=np.asarray(hidden_image).copy()
ha[:,:,3]=np.minimum(ha[:,:,3],np.asarray(hidden_mask))
ha[ha[:,:,3]==0,:3]=0
combined=Image.fromarray(ha,'RGBA')
combined.alpha_composite(visible)

output=combined.transform((1024,1536),Image.Transform.AFFINE,
  (inverse[0,0],inverse[0,1],it[0],inverse[1,0],inverse[1,1],it[1]),Image.Resampling.BICUBIC)
arr=np.asarray(output).copy();arr[arr[:,:,3]==0,:3]=0
output=Image.fromarray(arr,'RGBA')
(ROOT/'hair_back').mkdir(exist_ok=True)
(ROOT/'qa').mkdir(exist_ok=True)
output.save(ROOT/'hair_back/01.png')
visible.save(ROOT/'qa/back01_source_visible.png')
Image.fromarray(np.uint8(v_mask*255)).save(ROOT/'qa/back01_source_regions.png')
combined.save(ROOT/'qa/back01_source_with_hidden.png')
light=Image.new('RGBA',output.size,(232,225,214,255));light.alpha_composite(output)
light.resize((512,768)).convert('RGB').save(ROOT/'qa/back01_light.jpg')
report={'source':str(SOURCE),'hidden_painting':str(HIDDEN),'source_pupils':[source_left.tolist(),source_right.tolist()],
 'target_pupils':[target_left.tolist(),target_right.tolist()],'similarity_matrix':M.tolist(),'translation':t.tolist(),
 'output_size':[1024,1536],'visible_region_source':'source crown bun, pin and rear side curtains',
 'limitations':['Fine external strands use estimated paper-color matting, not lossless source alpha.',
 'Hidden face, neck and torso coverage uses the existing rear-hair painting; it is not reconstructed original pixels.',
 'Similarity resampling changes pixels slightly; no separate local stretching or bbox centering was used.',
 'Source clothing occludes the original lower back hair; hidden continuation is a compatible underpainting.'],
 'active_replaced':False}
(ROOT/'back01_extraction.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
