"""Pure-color vector construction guides; never edits generated artwork."""
from pathlib import Path
import argparse,json
import cv2
import numpy as np
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'tools/action_battle_test/art/ink_outfit_01_20261104/assets/template'
DATA=json.loads((OUT/'fullcanvas_template_anchors.json').read_text(encoding='utf-8'))
SS=2;K=24
BG='#939c9e';IVORY='#e8e4d8';BLUE='#70858d';LINING='#627077';SKIN='#d9bca4';EDGE='#617074'

def smooth(points):
    p=np.array(points,float);result=[]
    for i in range(len(p)):
        a,b,c,d=p[(i-1)%len(p)],p[i],p[(i+1)%len(p)],p[(i+2)%len(p)]
        for t in np.linspace(0,1,12,endpoint=False):
            result.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    return result

def mask_polygons(mask):
    contours,_=cv2.findContours((mask.astype(np.uint8)*255),cv2.RETR_EXTERNAL,cv2.CHAIN_APPROX_SIMPLE)
    return [smooth(cv2.approxPolyDP(c,1.3,True).reshape(-1,2)+.5) for c in contours if cv2.contourArea(c)>=1]

def paint(mask,size,color=IVORY):
    canvas=Image.new('RGBA',(size[0]*SS,size[1]*SS),(0,0,0,0));draw=ImageDraw.Draw(canvas)
    for poly in mask_polygons(mask):
        pts=[(p[0]*K*SS,p[1]*K*SS) for p in poly]
        draw.polygon(pts,fill=color);draw.line(pts+[pts[0]],fill=EDGE,width=3*SS,joint='curve')
    return canvas

def source(key):
    c=DATA['components'][key];im=np.array(Image.open(ROOT/c['sourceFullTexture']).convert('RGBA'))
    return c,im,im[:,:,3]>=128

def save(key,layer):
    c=DATA['components'][key];background=Image.new('RGBA',layer.size,BG);background.alpha_composite(layer)
    image=background.convert('RGB').resize(tuple(c['templateCanvasSize']),Image.Resampling.LANCZOS)
    path=OUT/(key+'_flat_region_guide.png');image.save(path);print(str(path),flush=True)

def body():
    c,im,mask=source('body');size=c['templateCanvasSize'];layer=paint(mask,size)
    inner=(im[:,:,:3].max(axis=2)<100)&mask
    lining=paint(inner,size,LINING);layer.alpha_composite(lining)
    band=Image.new('RGBA',layer.size,(0,0,0,0));draw=ImageDraw.Draw(band)
    draw.rectangle((0,(1035-60)*SS,size[0]*SS,(1035+60)*SS),fill=BLUE)
    band.putalpha(Image.composite(band.getchannel('A'),Image.new('L',layer.size,0),layer.getchannel('A')))
    layer.alpha_composite(band);save('body',layer)

def native_poly(draw,points,color):
    p=smooth(points);xy=[(float(v[0])*K*SS,float(v[1])*K*SS) for v in p]
    draw.polygon(xy,fill=color);draw.line(xy+[xy[0]],fill=EDGE,width=3*SS,joint='curve')

def round_finger(draw,start,end,width):
    a=(start[0]*K*SS,start[1]*K*SS);b=(end[0]*K*SS,end[1]*K*SS);w=width*K*SS
    draw.line((*a,*b),fill=EDGE,width=round(w+3*SS))
    draw.line((*a,*b),fill=SKIN,width=round(w))
    for x,y in [a,b]:draw.ellipse((x-w/2,y-w/2,x+w/2,y+w/2),fill=SKIN)

def sleeve(key):
    c,im,mask=source(key);size=c['templateCanvasSize'];near=key=='sleeve_near'
    cut=40 if near else 42;mask[cut:]=False
    if near: mask[37:,32:]=False
    cloth=paint(mask,size);hand=Image.new('RGBA',cloth.size,(0,0,0,0));draw=ImageDraw.Draw(hand)
    if near:
        # Small two-finger sword sign, following the wrist axis downward.
        # Tips end at logical canvas45/60=.75, not at the previous image.88.
        native_poly(draw,[(28.4,39.5),(31.3,39.6),(31.7,40.8),(31.4,42.1),(30.5,42.6),(28.9,42.5),(27.6,41.9),(27.5,40.8)],SKIN)
        round_finger(draw,(29.85,41.9),(29.85,44.6),.8)
        round_finger(draw,(30.85,41.8),(30.85,44.6),.8)
    else:
        # Distinct left/far hand. Relaxed compact silhouette, no finger lines,
        # nail/knuckle anatomy, or mirror of the complete arm image.
        native_poly(draw,[(32.2,41.3),(35.0,41.4),(36.5,42.6),(37.1,43.9),(36.9,45.5),(35.7,47.5),(34.1,47.7),(32.4,46.3),(31.9,43.4)],SKIN)
    hand.alpha_composite(cloth)
    band=Image.new('RGBA',cloth.size,(0,0,0,0));d=ImageDraw.Draw(band)
    y0,y1=(37,40) if near else (39,42)
    d.rectangle((0,y0*K*SS,size[0]*SS,y1*K*SS),fill=BLUE)
    band.putalpha(Image.composite(band.getchannel('A'),Image.new('L',cloth.size,0),cloth.getchannel('A')))
    hand.alpha_composite(band);save(key,hand)

def leg():
    c,im,mask=source('leg');size=c['templateCanvasSize'];layer=paint(mask,size,'#687176')
    boot=Image.new('RGBA',layer.size,(0,0,0,0));draw=ImageDraw.Draw(boot)
    draw.rectangle((0,53*K*SS,size[0]*SS,size[1]*SS),fill=BLUE)
    boot.putalpha(Image.composite(boot.getchannel('A'),Image.new('L',layer.size,0),layer.getchannel('A')))
    layer.alpha_composite(boot)
    binding=Image.new('RGBA',layer.size,(0,0,0,0));draw=ImageDraw.Draw(binding)
    draw.rectangle((0,48*K*SS,size[0]*SS,53*K*SS),fill='#8da0a4')
    binding.putalpha(Image.composite(binding.getchannel('A'),Image.new('L',layer.size,0),layer.getchannel('A')))
    layer.alpha_composite(binding);save('leg',layer)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('part',choices=['body','sleeve_near','sleeve_far','leg','all']);args=parser.parse_args()
    if args.part in ['body','all']:body()
    if args.part in ['sleeve_near','all']:sleeve('sleeve_near')
    if args.part in ['sleeve_far','all']:sleeve('sleeve_far')
    if args.part in ['leg','all']:leg()
