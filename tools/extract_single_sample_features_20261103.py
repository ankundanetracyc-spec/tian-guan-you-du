"""Extract approved source pixels; no facial painting is synthesized."""
from pathlib import Path
import json, hashlib
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/portrait_rebuild_20261103'
OUT=PACK/'single_style_sample'
SRC=PACK/'source/female_hair_final/reference_01.png'
rgb=np.array(Image.open(SRC).convert('RGB')).astype(np.float32)
face=np.array(Image.open(OUT/'parts/face_shape.png').convert('RGBA'))
base=face[:,:,:3].astype(np.float32)
specs={
 'eyes':{'box':(212,306,412,352),'supports':[
 [(222,324),(234,314),(253,311),(274,314),(287,326),(295,339),(277,346),(252,347),(233,338)],
 [(332,339),(339,326),(353,314),(374,311),(393,315),(404,324),(393,339),(374,347),(351,346)]],
 'cores':[
 [(226,325),(239,322),(251,319),(263,319),(275,324),(286,333),(287,335),(271,339),(255,340),(242,336),(234,331)],
 [(338,335),(348,324),(361,319),(374,319),(388,322),(400,325),(390,332),(382,337),(368,340),(353,339)]],
 'controls':[[259,329],[366,329]],'targets':[[464,379],[574,379]],'feather':3},
 'eyebrows':{'box':(218,285,407,314),'supports':[
 [(230,296),(242,292),(264,293),(284,298),(295,308),(279,309),(258,304),(239,302),(229,300)],
 [(334,307),(344,300),(361,294),(381,293),(397,296),(398,300),(382,302),(364,304),(343,310)]],
 'cores':[], 'controls':[[249,298],[375,298]],'targets':[[457,354],[582,353]],'feather':2},
 'mouth':{'box':(272,417,355,458),'supports':[
 [(280,428),(295,424),(307,419),(314,422),(322,419),(336,425),(348,429),(346,438),(331,450),(313,455),(297,449),(282,439)]],
 'cores':[
 [(283,432),(296,430),(307,427),(314,430),(322,427),(334,430),(343,432),(334,436),(324,442),(312,444),(301,441),(290,436)]],
 'controls':[[282,432],[344,432]],'targets':[[487,483],[551,483]],'feather':4}
}

def poly_mask(shape, polygons, offset):
    h,w=shape;im=Image.new('L',(w*4,h*4))
    for pts in polygons:ImageDraw.Draw(im).polygon([((x-offset[0])*4,(y-offset[1])*4) for x,y in pts],fill=255)
    return np.array(im.resize((w,h),Image.Resampling.LANCZOS)).astype(np.float32)/255

records=[]
for part,spec in specs.items():
    x0,y0,x1,y1=spec['box'];region=rgb[y0:y1,x0:x1];h,w=region.shape[:2]
    ownership=poly_mask((h,w),spec['supports'],(x0,y0));core=poly_mask((h,w),spec['cores'],(x0,y0))
    yy,xx=np.indices((h,w));x=(xx-w/2)/w;y=(yy-h/2)/h
    basis=np.stack([np.ones_like(x),x,y,x*x,x*y,y*y],axis=-1)
    # Fit only unpainted surrounding skin, excluding hair and neighboring marks.
    valid=(ownership<.01)&(region[:,:,0]>180)&(region[:,:,1]>145)&(region[:,:,0]-region[:,:,2]>10)
    for _ in range(3):
        coeff=np.linalg.lstsq(basis[valid],region[valid],rcond=None)[0]
        under=basis@coeff
        error=np.max(np.abs(region-under),axis=2)
        valid &= error<15
    distance=cv2.distanceTransform(np.uint8(ownership>.05),cv2.DIST_L2,5)
    feather=np.clip(distance/spec['feather'],0,1);feather=feather*feather*(3-2*feather)*ownership
    delta=(region-under)*feather[:,:,None]
    source=np.array(spec['controls'],np.float32);target=np.array(spec['targets'],np.float32)
    v=source[1]-source[0];u=target[1]-target[0]
    aa=float(np.dot(u,v)/np.dot(v,v));bb=float((u[1]*v[0]-u[0]*v[1])/np.dot(v,v))
    linear=np.array([[aa,-bb],[bb,aa]],np.float32);translation=target.mean(0)-linear@source.mean(0)
    matrix=np.column_stack([linear,translation+linear@np.array([x0,y0])])
    warp=lambda im:cv2.warpAffine(im.astype(np.float32),matrix,(1024,1536),flags=cv2.INTER_LANCZOS4)
    ink=warp(delta);coverage=np.clip(warp(core),0,1)
    core_rgb=warp(region*core[:,:,None])/np.maximum(coverage[:,:,None],1e-5)
    desired=np.clip(base+ink,0,255)*(1-coverage[:,:,None])+np.clip(core_rgb,0,255)*coverage[:,:,None]
    d=desired-base;denom=np.where(d<0,np.maximum(base,1),np.maximum(255-base,1))
    alpha=np.maximum(np.max(np.abs(d)/denom,axis=2)*1.1,coverage)
    alpha=np.ceil(np.clip(alpha,0,1)*255)/255
    alpha[(np.max(np.abs(d),axis=2)<.8)&(coverage<.01)]=0
    fg=(desired-(1-alpha[:,:,None])*base)/np.maximum(alpha[:,:,None],1/255)
    result=np.zeros((1536,1024,4),np.uint8);result[:,:,:3]=np.rint(np.clip(fg,0,255));result[:,:,3]=np.rint(alpha*255);result[alpha==0]=0
    Image.fromarray(result).save(OUT/'parts'/(part+'.png'))
    records.append({'part':part,'source':str(SRC),'source_sha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),'source_controls':spec['controls'],'target_controls':spec['targets'],'uniform_similarity_matrix':matrix.tolist(),'method':'Original eye/lip core pixels plus skin-relative painted edge pigment; unchanged original face underneath','limitations':'Source skin background estimated from surrounding pixels; hidden eyebrow ends remain as visible in approved reference.'})
(OUT/'revisions/features_pixel_extraction.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print('Extracted original eyebrows, eyes and mouth as three independent 1024x1536 RGBA files.')
