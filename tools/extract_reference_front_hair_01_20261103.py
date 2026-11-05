"""User-authorized ownership extraction from the exact approved reference.

Preserves the source hair pixels, with continuous source-background matting at
hair edges. No new hair strokes, tips, ears or skin are synthesized.
"""
from pathlib import Path
import json, hashlib
import cv2
import numpy as np
from scipy.ndimage import distance_transform_edt
from scipy.interpolate import PchipInterpolator
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
PACK=ROOT/'mods/appearance/portrait_rebuild_20261103'
SRC=PACK/'source/female_hair_final/reference_01.png'
OUT=PACK/'alignment_skin_revision/source_extracted'

CAP=[(313,90),(278,94),(248,101),(223,114),(200,132),(175,151),(148,174),(130,195),(130,263),(138,311),(160,332),(178,354),(195,372),(208,348),(217,321),(224,293),(228,274),(236,245),(246,225),(263,211),(281,204),(300,204),(313,209),(325,203),(345,204),(365,214),(380,236),(390,270),(400,299),(409,329),(418,358),(425,372),(445,354),(465,329),(489,300),(490,195),(468,162),(445,143),(425,140),(402,118),(375,103),(346,94)]
LEFT=[(237,222),(226,259),(216,300),(206,339),(200,376),(202,412),(213,451),(222,475),(220,503),(215,534),(206,563),(193,590),(182,618),(171,647),(164,677),(163,707),(167,736),(178,763),(195,786),(210,814),(220,840),(224,865),(226,886),(214,886),(212,868),(207,849),(197,827),(184,808),(172,790),(157,770),(146,749),(141,727),(141,703),(144,680),(150,652),(158,625),(169,593),(178,562),(185,535),(189,504),(190,477),(186,451),(180,421),(179,392),(183,365),(190,336),(202,300),(212,265),(224,234)]
RIGHT=[(376,228),(388,269),(397,306),(404,342),(410,379),(417,412),(420,445),(420,475),(421,503),(427,532),(435,562),(444,594),(451,622),(454,649),(455,677),(449,706),(440,732),(426,757),(414,778),(407,803),(407,827),(409,852),(417,886),(408,886),(398,864),(392,842),(390,820),(392,800),(398,777),(410,752),(424,726),(433,700),(435,674),(433,646),(428,622),(420,597),(409,569),(403,540),(400,512),(400,484),(402,456),(402,428),(399,405),(392,379),(390,350),(388,319),(380,282),(370,246)]
LEFT_BOUNDS=[(340,189,218),(375,179,215),(400,179,216),(450,188,225),(500,189,230),(550,182,221),(580,173,208),(600,162,197),(625,153,187),(650,151,180),(675,150,181),(700,156,188),(725,163,194),(750,164,197),(775,183,210),(800,195,218),(825,206,229),(850,206,225),(875,211,232),(886,209,234)]
RIGHT_BOUNDS=[(340,402,428),(375,395,428),(400,393,427),(450,399,432),(500,401,442),(550,410,448),(580,420,454),(600,424,459),(625,430,465),(650,434,469),(675,433,469),(700,434,467),(725,424,453),(750,414,440),(775,402,430),(800,397,419),(825,398,418),(850,404,422),(875,418,437),(886,427,442)]


def bounds_polygon(rows):
    rows=np.asarray(rows,float);ys=np.arange(rows[0,0],rows[-1,0]+1)
    left=PchipInterpolator(rows[:,0],rows[:,1])(ys)
    right=PchipInterpolator(rows[:,0],rows[:,2])(ys)
    return list(zip(left,ys))+list(zip(right[::-1],ys[::-1]))


def ownership_mask(size):
    width,height=size
    image=Image.new('L',(width*4,height*4))
    draw=ImageDraw.Draw(image)
    for polygon in [CAP,bounds_polygon(LEFT_BOUNDS),bounds_polygon(RIGHT_BOUNDS)]:
        draw.polygon([(x*4,y*4) for x,y in polygon],fill=255)
    # Visible ears and the neighboring eye/brow corners are separate assets.
    for polygon in [
        [(174,329),(184,338),(191,350),(189,367),(184,374),(175,360),(169,343)],
        [(449,328),(456,338),(455,352),(448,366),(440,375),(436,362),(441,344)],
        [(229,289),(250,289),(250,316),(224,316)],
        [(378,289),(394,289),(398,316),(385,316)],
        [(157,326),(177,325),(197,344),(197,373),(182,388),(162,372)],
        [(438,325),(468,320),(468,375),(441,391),(429,372)]]:
        draw.polygon([(x*4,y*4) for x,y in polygon],fill=0)
    return np.asarray(image.resize(size,Image.Resampling.LANCZOS)).astype(np.float32)/255


def main():
    (OUT/'hair_front').mkdir(parents=True,exist_ok=True)
    (OUT/'masks').mkdir(parents=True,exist_ok=True)
    (OUT/'qa').mkdir(parents=True,exist_ok=True)
    source=Image.open(SRC).convert('RGB')
    rgb=np.asarray(source).astype(np.float32)
    owner=ownership_mask(source.size)
    luminance=rgb.mean(2)
    color_alpha=np.clip((195-luminance)/40,0,1)
    color_alpha*=np.clip((31-(rgb[:,:,0]-rgb[:,:,2]))/7,0,1)
    color_alpha*=np.clip((rgb[:,:,0]-rgb[:,:,2]+5)/10,0,1)
    color_alpha*=np.clip((rgb[:,:,0]-rgb[:,:,1]+2)/5,0,1)
    # Nearby plain skin/paper/cloth is used only to remove edge contamination.
    # Core hair pixels retain their exact source RGB values.
    nonhair=(color_alpha<.01)
    _,indices=distance_transform_edt(~nonhair,return_indices=True)
    under=rgb[indices[0],indices[1]]
    foreground=rgb.copy()
    alpha=np.clip(owner*color_alpha,0,1)
    # Remove isolated flyaways while preserving the main source locks.
    core=np.uint8(alpha>.45)*255
    cleaned=cv2.morphologyEx(core,cv2.MORPH_OPEN,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(3,3)))
    envelope=cv2.dilate(cleaned,np.ones((3,3),np.uint8))
    alpha*=cv2.GaussianBlur(envelope.astype(np.float32)/255,(3,3),.5)
    for rows in [LEFT_BOUNDS,RIGHT_BOUNDS]:
        table=np.array(rows,float)
        for y in range(390,min(887,rgb.shape[0])):
            lo=int(np.interp(y,table[:,0],table[:,1]));hi=int(np.interp(y,table[:,0],table[:,2]))+1
            line=alpha[y,lo:hi]>.35
            edges=np.diff(np.r_[False,line,False].astype(int));starts=np.where(edges==1)[0];ends=np.where(edges==-1)[0]
            if len(starts):
                widths=ends-starts;j=int(np.argmax(widths))
                if widths[j]<3:alpha[y,lo:hi]=0;continue
                left=max(lo,lo+starts[j]-1);right=min(hi,lo+ends[j]+1)
                alpha[y,lo:left]=0;alpha[y,right:hi]=0
            else:alpha[y,lo:hi]=0
    rgba=np.dstack([foreground,alpha*255]).round().astype(np.uint8)
    rgba[alpha<1/255]=0
    Image.fromarray(np.uint8(owner*255)).save(OUT/'masks/front_01_ownership.png')
    Image.fromarray(np.uint8(alpha*255)).save(OUT/'masks/front_01_source_alpha.png')
    Image.fromarray(rgba).save(OUT/'masks/front_01_source_rgba.png')

    source_points=np.array([[259.183,325.313],[367.106,325.374]],np.float64)
    targets=np.array([[464,379],[574,379]],np.float64)
    v=source_points[1]-source_points[0];u=targets[1]-targets[0]
    aa=np.dot(u,v)/np.dot(v,v);bb=(u[1]*v[0]-u[0]*v[1])/np.dot(v,v)
    linear=np.array([[aa,-bb],[bb,aa]],np.float64)
    translation=targets.mean(0)-linear@source_points.mean(0)
    matrix=np.column_stack([linear,translation])
    af=rgba[:,:,3].astype(np.float32)/255
    premult=rgba[:,:,:3].astype(np.float32)*af[:,:,None]
    warped_alpha=np.clip(cv2.warpAffine(af,matrix,(1024,1536),flags=cv2.INTER_LANCZOS4),0,1)
    warped_rgb=cv2.warpAffine(premult,matrix,(1024,1536),flags=cv2.INTER_LANCZOS4)/np.maximum(warped_alpha[:,:,None],1/255)
    result=np.zeros((1536,1024,4),np.uint8)
    result[:,:,:3]=np.clip(warped_rgb,0,255).round().astype(np.uint8)
    result[:,:,3]=np.uint8(np.rint(warped_alpha*255));result[result[:,:,3]==0]=0
    result_image=Image.fromarray(result);result_image.save(OUT/'hair_front/01.png')

    skin_pixels=(result[:,:,3]>64)&(result[:,:,0]-result[:,:,2]>36)&(result[:,:,0]>130)
    source_lastrow_alpha_count=int((rgba[-2:,:,3]>64).sum())
    record={'source':str(SRC),'source_sha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),
        'source_eye_centers':source_points.tolist(),'target_eye_centers':targets.tolist(),
        'matrix':matrix.tolist(),'uniform_scale':float(np.hypot(aa,bb)),
        'source_hairline':[313,205],'mapped_hairline':(linear@np.array([313,205])+translation).tolist(),
        'output':str(OUT/'hair_front/01.png'),'output_bbox':result_image.getbbox(),
        'source_regions':{'cap':CAP,'left_front_lock_row_bounds':LEFT_BOUNDS,'right_front_lock_row_bounds':RIGHT_BOUNDS},
        'warm_skin_pixels_alpha_gt64':int(skin_pixels.sum()),
        'source_bottom_two_rows_alpha_gt64':source_lastrow_alpha_count,
        'limits':['Rear bun and hairpin are assigned to the separate rear-hair layer.','The source bottom edge crops the ends of both front locks; invisible tips are not invented.','This preserves the reference hairline under eye-distance mapping; it does not apply the prior 22px hairline lowering.','Dark neutral boundary pixels may include inseparable source clothing marks and must be visually inspected.'],
        'method':'Exact source-pixel ownership masks, local background unmatting for edge alpha, single whole-layer pupil-defined similarity transform; no new drawing.'}
    (OUT/'front_01_extraction.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
    for color,label in [((235,229,217,255),'light'),((45,50,53,255),'dark')]:
        image=Image.new('RGBA',(1024,1536),color);image.alpha_composite(result_image)
        image.convert('RGB').save(OUT/'qa'/f'front_01_{label}.jpg',quality=97)
    basepath=PACK/'alignment_skin_revision/female/face_shape/04.png'
    if not basepath.exists():basepath=ROOT/'mods/appearance/ink_three_variants_20261004/female/face_shape/04.png'
    image=Image.new('RGBA',(1024,1536),(235,229,217,255))
    for part in ['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','hair_front']:
        if part=='hair_front':p=OUT/'hair_front/01.png'
        elif part=='face_shape':p=basepath
        elif part=='hair_back':
            p=OUT/'hair_back/01.png'
            if not p.exists():p=ROOT/'mods/appearance/ink_three_variants_20261004/female/hair_back/01.png'
        elif part=='clothing':p=ROOT/'mods/appearance/ink_three_variants_20261004/female/clothing/01.png'
        else:
            p=PACK/f'alignment_skin_revision/female/{part}/01.png'
            if not p.exists():p=ROOT/f'mods/appearance/ink_three_variants_20261004/female/{part}/01.png'
        image.alpha_composite(Image.open(p).convert('RGBA'))
    image.convert('RGB').save(OUT/'qa/front_01_composite.jpg',quality=97)
    image.crop((300,70,735,1000)).convert('RGB').save(OUT/'qa/front_01_head_chest.jpg',quality=97)
    print(json.dumps({key:record[key] for key in ['uniform_scale','mapped_hairline','warm_skin_pixels_alpha_gt64','source_bottom_two_rows_alpha_gt64']},ensure_ascii=False))


if __name__=='__main__':main()
