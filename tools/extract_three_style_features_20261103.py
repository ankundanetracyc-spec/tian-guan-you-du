"""User-authorized original-pixel extraction for three approved female variants.

No face pixels or active game assets are edited. Source skin is estimated only
to separate the actual painted feature pigment from its original skin base.
Each part gets a single similarity transform into the existing portrait rig.
"""
from pathlib import Path
import hashlib
import json
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / 'mods/appearance/portrait_rebuild_20261103'
OUT = PACK / 'three_style_parts'
REF = PACK / 'source/female_hair_final'
FACE = ROOT / 'mods/appearance/ink_three_variants_20261004/female/face_shape/04.png'
RIG = ROOT / 'mods/appearance/new_ink_hair_20261003_v1/rig/portrait_rig.json'
base = np.asarray(Image.open(FACE).convert('RGBA'))[:, :, :3].astype(np.float32)

SPECS = {
    1: {
        'eyes': {
            'box': (212, 306, 412, 352),
            'supports': [
                [(222,324),(234,314),(253,311),(274,314),(287,326),(295,339),(277,346),(252,347),(233,338)],
                [(332,339),(339,326),(353,314),(374,311),(393,315),(404,324),(393,339),(374,347),(351,346)]],
            'cores': [
                [(226,325),(239,322),(251,319),(263,319),(275,324),(286,333),(287,335),(271,339),(255,340),(242,336),(234,331)],
                [(338,335),(348,324),(361,319),(374,319),(388,322),(400,325),(390,332),(382,337),(368,340),(353,339)]],
            'controls': [[259.183,325.313],[367.106,325.374]],
            'targets': [[464,379],[574,379]], 'feather': 3},
        'eyebrows': {
            'box': (218,285,407,314),
            'supports': [
                [(230,296),(242,292),(264,293),(284,298),(295,308),(279,309),(258,304),(239,302),(229,300)],
                [(334,307),(344,300),(361,294),(381,293),(397,296),(398,300),(382,302),(364,304),(343,310)]],
            'cores': [], 'controls': [[249,298],[375,298]],
            'targets': [[457,354],[582,353]], 'feather': 2},
        'mouth': {
            'box': (272,417,355,458),
            'supports': [[(280,428),(295,424),(307,419),(314,422),(322,419),(336,425),(348,429),(346,438),(331,450),(313,455),(297,449),(282,439)]],
            'cores': [[(283,432),(296,430),(307,427),(314,430),(322,427),(334,430),(343,432),(334,436),(324,442),(312,444),(301,441),(290,436)]],
            'controls': [[282,432],[344,432]], 'targets': [[487,483],[551,483]], 'feather': 4},
        'nose': {'center': [313,398], 'source_box': [270,344,356,426], 'polygon_offset_x':0}},
    2: {
        'eyes': {
            'box': (201,302,403,353),
            'supports': [
                [(211,324),(217,319),(221,311),(239,306),(256,308),(271,316),(286,336),(283,343),(262,349),(241,348),(222,340),(216,333),(213,328)],
                [(322,337),(331,322),(345,310),(362,306),(382,311),(389,318),(393,324),(389,329),(382,341),(363,349),(342,348),(324,342)]],
            'cores': [
                [(217,325),(230,319),(240,315),(251,315),(261,319),(272,326),(281,335),(270,338),(253,342),(239,340),(225,334)],
                [(328,335),(340,322),(352,315),(362,315),(374,318),(389,325),(379,333),(367,339),(351,341),(337,339)]],
            'controls': [[246.754109829,323.509327271],[355.991360142,323.712280033]],
            'targets': [[464,379],[574,379]], 'feather': 3},
        'eyebrows': {
            'box': (203,281,402,314),
            'supports': [
                [(218,291),(229,287),(244,288),(264,295),(277,304),(281,310),(266,305),(247,300),(229,297),(217,296)],
                [(322,309),(331,299),(345,292),(367,287),(383,290),(389,295),(385,299),(370,298),(350,301),(334,306)]],
            'cores': [], 'controls': [[237,294],[363,294]],
            'targets': [[457,354],[582,353]], 'feather': 2},
        'mouth': {
            'box': (261,417,345,458),
            'supports': [[(269,428),(284,424),(296,419),(303,422),(311,419),(325,425),(337,429),(335,438),(320,450),(302,455),(286,449),(271,439)]],
            'cores': [[(273,432),(286,430),(297,427),(303,430),(311,427),(324,430),(334,432),(324,436),(314,442),(302,444),(291,441),(280,436)]],
            'controls': [[272,432],[335,432]], 'targets': [[487,483],[551,483]], 'feather': 4},
        'nose': {'center': [302,398], 'source_box': [259,344,345,426], 'polygon_offset_x':-11}},
    3: {
        'eyes': {
            'box': (199,306,402,352),
            'supports': [
                [(210,323),(220,314),(240,310),(257,313),(272,324),(282,338),(274,345),(254,347),(235,346),(219,337),(210,330)],
                [(319,339),(326,326),(342,313),(360,310),(381,314),(394,324),(383,338),(364,347),(341,347)]],
            'cores': [
                [(215,325),(227,322),(239,319),(251,319),(263,324),(274,333),(275,336),(259,340),(243,340),(230,336),(222,331)],
                [(325,335),(335,324),(348,319),(361,319),(375,322),(387,325),(377,332),(369,337),(355,340),(340,339)]],
            'controls': [[245.365449657,325.547220536],[355.348564531,325.461074231]],
            'targets': [[464,379],[574,379]], 'feather': 3},
        'eyebrows': {
            'box': (201,286,400,316),
            'supports': [
                [(211,297),(228,293),(245,294),(266,300),(279,309),(269,310),(249,305),(229,303),(212,302)],
                [(319,310),(330,302),(348,296),(369,293),(389,295),(393,300),(375,303),(355,306),(334,310)]],
            'cores': [], 'controls': [[236,300],[362,300]],
            'targets': [[457,354],[582,353]], 'feather': 2},
        'mouth': {
            'box': (259,417,343,458),
            'supports': [[(267,428),(282,424),(294,419),(301,422),(309,419),(323,425),(335,429),(333,438),(318,450),(300,455),(284,449),(269,439)]],
            'cores': [[(271,432),(284,430),(295,427),(301,430),(309,427),(322,430),(332,432),(322,436),(312,442),(300,444),(289,441),(278,436)]],
            'controls': [[270,432],[333,432]], 'targets': [[487,483],[551,483]], 'feather': 4},
        'nose': {'center': [300,398], 'source_box': [257,344,343,426], 'polygon_offset_x':-13}}
}


def poly_mask(shape, polygons, offset):
    h,w=shape
    image=Image.new('L',(w*4,h*4))
    draw=ImageDraw.Draw(image)
    for pts in polygons:
        draw.polygon([((x-offset[0])*4,(y-offset[1])*4) for x,y in pts],fill=255)
    return np.asarray(image.resize((w,h),Image.Resampling.LANCZOS)).astype(np.float32)/255


def similarity(source, target, offset):
    source=np.asarray(source,np.float32);target=np.asarray(target,np.float32)
    v=source[1]-source[0];u=target[1]-target[0]
    aa=float(np.dot(u,v)/np.dot(v,v));bb=float((u[1]*v[0]-u[0]*v[1])/np.dot(v,v))
    linear=np.array([[aa,-bb],[bb,aa]],np.float32)
    translation=target.mean(0)-linear@source.mean(0)
    return np.column_stack([linear,translation+linear@np.asarray(offset,np.float32)]), float(np.hypot(aa,bb))


def encode(desired, coverage, tolerance=.8, headroom=1.1):
    d=desired-base
    denominator=np.where(d<0,np.maximum(base,1),np.maximum(255-base,1))
    alpha=np.maximum(np.max(np.abs(d)/denominator,axis=2)*headroom,coverage)
    alpha=np.ceil(np.clip(alpha,0,1)*255)/255
    alpha[(np.max(np.abs(d),axis=2)<tolerance)&(coverage<.01)]=0
    foreground=(desired-(1-alpha[:,:,None])*base)/np.maximum(alpha[:,:,None],1/255)
    result=np.zeros((1536,1024,4),np.uint8)
    result[:,:,:3]=np.rint(np.clip(foreground,0,255))
    result[:,:,3]=np.rint(alpha*255)
    result[alpha==0]=0
    return result


def extract_feature(rgb, spec):
    x0,y0,x1,y1=spec['box'];region=rgb[y0:y1,x0:x1];h,w=region.shape[:2]
    ownership=poly_mask((h,w),spec['supports'],(x0,y0))
    core=poly_mask((h,w),spec['cores'],(x0,y0))
    yy,xx=np.indices((h,w));x=(xx-w/2)/w;y=(yy-h/2)/h
    basis=np.stack([np.ones_like(x),x,y,x*x,x*y,y*y],axis=-1)
    valid=(ownership<.01)&(region[:,:,0]>180)&(region[:,:,1]>145)&(region[:,:,0]-region[:,:,2]>10)
    for _ in range(3):
        coeff=np.linalg.lstsq(basis[valid],region[valid],rcond=None)[0]
        under=basis@coeff
        error=np.max(np.abs(region-under),axis=2)
        valid &= error<15
    distance=cv2.distanceTransform(np.uint8(ownership>.05),cv2.DIST_L2,5)
    feather=np.clip(distance/spec['feather'],0,1)
    feather=feather*feather*(3-2*feather)*ownership
    delta=(region-under)*feather[:,:,None]
    matrix,scale=similarity(spec['controls'],spec['targets'],(x0,y0))
    warp=lambda array:cv2.warpAffine(array.astype(np.float32),matrix,(1024,1536),flags=cv2.INTER_LANCZOS4)
    ink=warp(delta);coverage=np.clip(warp(core),0,1)
    core_rgb=warp(region*core[:,:,None])/np.maximum(coverage[:,:,None],1e-5)
    desired=np.clip(base+ink,0,255)*(1-coverage[:,:,None])+np.clip(core_rgb,0,255)*coverage[:,:,None]
    result=encode(desired,coverage)
    return result, matrix, scale


def extract_nose(rgb, spec):
    x0,y0,x1,y1=spec['source_box'];region=rgb[y0:y1,x0:x1].astype(np.uint8)
    points=np.array([(303,355),(321,355),(326,381),(337,394),(334,409),(325,417),(308,417),(292,409),(286,398),(297,380)])
    points[:,0]+=spec['polygon_offset_x']
    mask=poly_mask(region.shape[:2],[points.tolist()],(x0,y0))
    under=cv2.inpaint(region,np.uint8(mask>.01)*255,7,cv2.INPAINT_TELEA).astype(np.float32)
    under=cv2.GaussianBlur(under,(0,0),3)
    distance=cv2.distanceTransform(np.uint8(mask>.05),cv2.DIST_L2,5)
    weight=np.clip(distance/5,0,1);weight=weight*weight*(3-2*weight)*mask
    delta=(region.astype(np.float32)-under)*weight[:,:,None]
    cx,cy=spec['center']
    matrix=np.array([[1,0,519-cx+x0],[0,1,446-cy+y0]],np.float32)
    ink=cv2.warpAffine(delta,matrix,(1024,1536),flags=cv2.INTER_LANCZOS4)
    result=encode(np.clip(base+ink,0,255),np.zeros((1536,1024),np.float32),headroom=1.15)
    return result,matrix,1.0


def main():
    records=[]
    qa=OUT/'qa/features';qa.mkdir(parents=True,exist_ok=True)
    face_hash=hashlib.sha256(FACE.read_bytes()).hexdigest()
    for variant,specs in SPECS.items():
        source=REF/f'reference_{variant:02}.png'
        rgb=np.asarray(Image.open(source).convert('RGB')).astype(np.float32)
        for part,spec in specs.items():
            result,matrix,scale=(extract_nose if part=='nose' else extract_feature)(rgb,spec)
            path=OUT/'female'/part/f'{variant:02}.png';path.parent.mkdir(parents=True,exist_ok=True)
            image=Image.fromarray(result);image.save(path)
            records.append({'part':part,'variant':variant,'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                'output':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'spec':spec,
                'uniform_similarity_matrix_on_crop':matrix.tolist(),'uniform_scale':scale,'canvas':[1024,1536],'bbox':image.getbbox(),
                'opaque_pixels':int((result[:,:,3]>=240).sum()),'nonzero_pixels':int((result[:,:,3]>0).sum()),
                'method':'Original feature core pixels plus source skin-relative edge pigment; same original face04 underneath. Nose preserves source nose-specific light/shadow without an opaque skin base.'})
        face=Image.open(FACE).convert('RGBA')
        for part in ['eyes','eyebrows','nose','mouth']:
            face.alpha_composite(Image.open(OUT/'female'/part/f'{variant:02}.png').convert('RGBA'))
        for color,label in [((235,229,217,255),'light'),((53,57,58,255),'dark')]:
            canvas=Image.new('RGBA',(1024,1536),color);canvas.alpha_composite(face)
            canvas.crop((375,315,660,565)).resize((855,750),Image.Resampling.NEAREST).convert('RGB').save(qa/f'face_features_{variant:02}_{label}.png')
    assert hashlib.sha256(FACE.read_bytes()).hexdigest()==face_hash
    manifest={'user_authorized_original_pixel_extraction':True,'status':'isolated_review_assets_not_installed',
        'face':str(FACE),'face_sha256':face_hash,'rig':str(RIG),'rig_sha256':hashlib.sha256(RIG.read_bytes()).hexdigest(),
        'output_count':len(records),'records':records,
        'limits':['The source skin background is estimated from surrounding unpainted skin.','Two visible pupil anchors define each eye-pair similarity transform, not a three-point certification.','Source art that is genuinely hidden by hair is not invented.','Each part uses one uniform similarity transform; no per-eye stretch or local facial shape changes.']}
    (OUT/'feature_extraction.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps({'exported':len(records),'base_face_unchanged':True,'directory':str(OUT/'female')},ensure_ascii=False))


if __name__=='__main__':
    main()
