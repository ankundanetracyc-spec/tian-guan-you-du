"""Original-coordinate matte repair authorized by the user on 2026-10-03.

The generator's moved eye candidates are rejected. Original eye pixels are
kept in place; only semantic alpha is changed. No old project art is read.
"""
from __future__ import annotations
import importlib.util
import json
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
V3 = ROOT.parent / 'ink_portrait_pair_20261003_v3'
V1 = ROOT.parent / 'ink_portrait_pair_20261003_v1'
W, H = 1024, 1536
YY, XX = np.indices((H, W))
ORDER = ['hair_back', 'face_shape', 'clothing', 'eyes', 'eyebrows', 'nose', 'mouth', 'face_accessory', 'hair_front']


def read(path):
    return np.array(Image.open(path).convert('RGBA')).copy()


def save(path, a):
    path.parent.mkdir(parents=True, exist_ok=True)
    a = np.clip(a, 0, 255).astype(np.uint8).copy()
    a[a[:, :, 3] == 0, :3] = 0
    Image.fromarray(a).save(path)


def polygon(points, scale=4):
    mask = Image.new('L', (W * scale, H * scale))
    ImageDraw.Draw(mask).polygon([(round(x*scale), round(y*scale)) for x,y in points], fill=255)
    return np.array(mask.resize((W,H), Image.Resampling.LANCZOS))


EYE_PATHS = {
 'male': [
  [(426,369),(435,367),(447,365.8),(459,365.7),(470,366.8),(478,369),(485,373),(491,379.7),(486,379.8),(478,381),(468,382.5),(456,382.7),(446,380.5),(437,376.5),(433,372.5),(428,371)],
  [(539,379.8),(547,373),(555,368.5),(565,366),(580,365.2),(594,366),(606,367),(610,368),(603,371.5),(596,376),(587,380),(577,382.1),(565,382.5),(554,381),(546,379.8)],
 ],
 'female': [
  [(424.5,374.3),(429,376.4),(436,376.7),(444,373.4),(454,371.5),(464,371.5),(474,373.5),(482,378),(487,383),(491,389),(487,388.4),(478,389.6),(469,392.1),(459,393.1),(449,390.6),(442,386),(437,380),(432,378.2),(428,377.3)],
  [(546,389.2),(550,383),(556,377),(565,372.4),(577,370.6),(589,371),(599,374),(605,376),(611,375.5),(615,374.2),(611,377),(603,378.3),(599,382),(592,389),(582,393),(572,393.4),(561,391.5),(553,389.4)],
 ]
}


def facial_layers(bald, previous_face, sex):
    """Keep the authored skin, and extract pigment against an inpainted base.

    Like the official Guigu example, the wash is colored pigment with
    continuous alpha, not a rectangle of opaque skin. Eye openings and lips
    retain their original RGB; crease/shadow pixels are unpremultiplied ink.
    """
    rgb = bald[:,:,:3].copy()
    support = {}
    eye_paths = {
      'male': [
        [(423,362),(437,355),(459,354),(478,360),(493,376),(493,386),(476,395),(449,394),(430,383)],
        [(537,375),(554,358),(577,353),(600,354),(611,361),(608,382),(588,393),(563,394),(542,386)]],
      'female': [
        [(422,369),(433,359),(454,357),(475,360),(492,380),(492,398),(471,402),(448,397),(431,384)],
        [(543,381),(559,361),(580,357),(601,361),(617,369),(605,390),(584,401),(561,401),(543,395)]]}
    support['eyes'] = np.maximum.reduce([polygon(p) for p in eye_paths[sex]])
    brow_paths = {
      'male': [[(417,350),(430,342),(449,341),(470,345),(489,351),(495,355),(478,355),(455,351),(434,350)],
               [(539,354),(551,347),(576,341),(600,340),(611,346),(590,347),(566,351),(549,356)]],
      'female': [[(417,355),(429,348),(450,346),(468,348),(489,355),(498,360),(484,360),(464,353),(440,352)],
                 [(540,360),(552,352),(574,347),(599,345),(615,349),(599,352),(576,353),(554,361)]]}
    support['eyebrows'] = np.maximum.reduce([polygon(p) for p in brow_paths[sex]])
    support['nose'] = polygon([(503,408),(531,408),(537,429),(543,444),(540,458),(532,465),(515,472),(502,464),(493,456),(491,444),(499,429)])
    support['mouth'] = polygon([(477,478),(490,471),(508,470),(519,474),(533,473),(548,480),(557,485),(550,497),(534,506),(510,507),(491,500),(479,491)])
    erase = np.zeros((H,W),np.uint8)
    # Clear the whole original feature plus neighboring ink before sampling
    # the skin. A tight erase copied surviving lashes back into the face.
    for x in (456,577):
        cv2.ellipse(erase,(x,369),(49,41),0,0,360,255,-1)
    erase[(YY>363)&((XX<417)|(XX>620))]=0
    cv2.ellipse(erase,(517,444),(36,40),0,0,360,255,-1)
    cv2.ellipse(erase,(517,487),(45,27),0,0,360,255,-1)
    clean = cv2.inpaint(rgb, erase, 13, cv2.INPAINT_NS)
    # Restore the same authored paper grain to the featureless skin, using
    # clear forehead texture. This avoids blurred feature-shaped patches.
    clear=rgb[275:328,450:588].astype(np.float32)
    grain=clear-cv2.GaussianBlur(clear,(0,0),4)
    yy,xx=np.indices((H,W))
    texture=grain[(yy-275)%53,(xx-450)%138]
    weight=np.clip(cv2.distanceTransform((erase>0).astype(np.uint8),cv2.DIST_L2,3)/6,0,1)
    clean=np.clip(clean.astype(np.float32)+texture*weight[:,:,None]*.85,0,255).astype(np.uint8)
    # Inpainting sampled a heavy socket shadow at the erase boundary. The
    # current independently generated featureless painting has no eye
    # organs; use its continuous skin texture only inside this field.
    blank=previous_face[:,:,:3].astype(np.float32)
    tone=np.median(rgb[300:325,470:565].astype(np.float32)-blank[300:325,470:565],axis=(0,1))
    blank=np.clip(blank+tone,0,255)
    eye_field=((erase>0)&(YY<412)).astype(np.uint8)
    blend=np.clip(cv2.distanceTransform(eye_field,cv2.DIST_L2,3)/10,0,1)
    clean=np.clip(clean.astype(np.float32)*(1-blend[:,:,None])+blank*blend[:,:,None],0,255).astype(np.uint8)
    face = previous_face.copy()
    spec=importlib.util.spec_from_file_location('current_base_geometry',V3/'build_assets.py')
    geometry=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(geometry)
    visible,_=geometry.face_geometry(bald,sex)
    exposed=visible&(face[:,:,3]>0)&(bald[:,:,3]>0)
    face[exposed,:3]=clean[exposed]
    layers={}
    skin=face[:,:,:3].astype(np.float32)
    original=rgb.astype(np.float32)
    for key,mask in support.items():
        pigment=np.array([52,45,41] if key in ('eyes','eyebrows') else [121,85,69],np.float32)
        direction=skin-pigment
        alpha=np.sum((skin-original)*direction,axis=2)/np.maximum(np.sum(direction*direction,axis=2),1)
        alpha=np.clip(alpha,0,.93 if key=='eyes' else 1)*(mask/255.)
        width={'eyes':2,'eyebrows':1,'nose':5,'mouth':4}[key]
        edge=cv2.distanceTransform((mask>10).astype(np.uint8),cv2.DIST_L2,3)
        alpha*=np.clip(edge/width,0,1)
        # Ignore the tiny changes created by texture interpolation. The
        # mask follows the anatomy; no global rectangle truncates the wash.
        alpha[alpha<.025]=0
        out=np.zeros_like(bald)
        out[:,:,:3]=pigment
        out[:,:,3]=np.round(alpha*255)
        layers[key]=out
    core=np.maximum.reduce([polygon(p) for p in EYE_PATHS[sex]])
    core[core<10]=0
    layers['eyes'][:,:,:3][core>0]=rgb[core>0]
    layers['eyes'][:,:,3]=np.maximum(layers['eyes'][:,:,3],core)
    if sex=='male':
        glint=np.zeros_like(bald)
        glint[:,:,:3]=[245,242,235]
        strength=np.zeros((H,W),np.float32)
        for x in (466,567):
            strength+=np.exp(-.5*(((XX-x)/2.4)**2+((YY-360)/1.3)**2))
        glint[:,:,3]=np.round(strength*.08*255)
        layers['eyes']=np.array(Image.alpha_composite(Image.fromarray(layers['eyes']),Image.fromarray(glint)))
    # Only the colored lip organ is opaque. Adjacent mouth shadows stay ink.
    lip=polygon([(485,482),(499,478),(510,477),(518,480),(529,478),(541,482),(550,486),(538,490),(532,496),(520,499),(508,496),(500,491),(489,488)])
    if sex=='male':
        lip=polygon([(481,480),(494,476),(507,475),(517,478),(531,476),(544,480),(553,483),(539,486),(529,490),(516,492),(503,490),(492,486)])
    lip[lip<10]=0
    layers['mouth'][:,:,:3][lip>0]=rgb[lip>0]
    layers['mouth'][:,:,3]=np.maximum(layers['mouth'][:,:,3],lip)
    if sex=='male':
        # Female reference is the shared complexion palette. Match its clear
        # forehead median while preserving ink contours and paper contrast.
        delta=np.array([6,12,15],np.float32)
        rgb_face=face[:,:,:3].astype(np.float32)
        skin_weight=np.clip((rgb_face.mean(axis=2)-65)/90,0,1)
        face[:,:,:3]=np.clip(rgb_face+skin_weight[:,:,None]*delta,0,255)
    return layers,face,core


def rear_layer(sex):
    painted=read(ROOT/'source/rear_hair_generated_candidate.png')
    # One uniform transform for the complete rear painting: hidden areas
    # stay painted; no head/ear holes or shoulder-level opacity gradients.
    s=.75
    center=517 if sex=='male' else 518
    crown=152 if sex=='male' else 146
    matrix=np.array([[s,0,center-s*512],[0,s,crown-s*66]],np.float32)
    result=cv2.warpAffine(painted,matrix,(W,H),flags=cv2.INTER_LANCZOS4,borderMode=cv2.BORDER_CONSTANT)
    result[:,:,3][result[:,:,3]<10]=0
    return result, matrix.tolist()


def generated_front_layer(sex):
    painted=read(ROOT/'source'/f'{sex}_front_hair_generated.png')
    # Register the whole painting at once. No hair pixel is taken from a
    # flattened face/robe, and no shoulder or chest strip truncates a lock.
    scale=.62
    center=517 if sex=='male' else 518
    crown=143 if sex=='male' else 137
    source_crown=50 if sex=='male' else 45
    matrix=np.array([[scale,0,center-scale*512],[0,scale,crown-scale*source_crown]],np.float32)
    result=cv2.warpAffine(painted,matrix,(W,H),flags=cv2.INTER_LANCZOS4,borderMode=cv2.BORDER_CONSTANT)
    result[:,:,3][result[:,:,3]<10]=0
    # The same ink pigment family is used in front and rear, for both sexes.
    gain=.80
    result[:,:,:3]=np.round(np.clip(result[:,:,:3].astype(np.float32)*gain,0,255))
    return result,matrix.tolist(),gain


def composite(layers):
    im=Image.new('RGBA',(W,H))
    for key in ORDER:
        im=Image.alpha_composite(im,Image.fromarray(layers[key]))
    return np.array(im)


def background(a, color):
    bg=Image.new('RGBA',(W,H),color)
    bg.alpha_composite(Image.fromarray(a))
    return bg.convert('RGB')


def contact(sex,layers):
    sheet=Image.new('RGB',(1020,1410),'#d8d2c9')
    draw=ImageDraw.Draw(sheet)
    for n,key in enumerate(ORDER):
        tile=Image.new('RGBA',(320,420),'#ece7df')
        im=Image.fromarray(layers[key])
        bbox=im.getchannel('A').getbbox()
        if bbox:
            crop=im.crop((max(bbox[0]-6,0),max(bbox[1]-6,0),min(bbox[2]+6,W),min(bbox[3]+6,H)))
            crop.thumbnail((304,402),Image.Resampling.LANCZOS)
            tile.alpha_composite(crop,((320-crop.width)//2,(420-crop.height)//2))
        x,y=(n%3)*340,(n//3)*470
        sheet.paste(tile.convert('RGB'),(x+10,y+35))
        draw.text((x+12,y+10),sex+' / '+key,fill='#25211e')
    sheet.save(ROOT/'preview'/f'{sex}_nine_parts.png')


def main():
    report={}
    rig=json.loads((V3/'rig/portrait_rig_v3.json').read_text(encoding='utf-8'))
    rig['id']=ROOT.name
    rig['anchor_source']='Facial landmarks from the user references. New front hair crown, part, temples and natural lock tips remeasured on final alpha. Each hair painting registered by one uniform transform.'
    rig['slot_contract']['hair_back']='Complete continuous rear hair underpainting including regions hidden by head, ears, neck and clothing; no cutouts or broad fades'
    pairs=[]
    for sex in ('male','female'):
        layers={key:read(V3/sex/(key+'.png')) for key in ORDER}
        source=read(V3/(sex+'_portrait.png'))
        bald=read(V1/'reference'/(sex+'_bald_user.png'))
        features,layers['face_shape'],eye_mask=facial_layers(bald,layers['face_shape'],sex)
        layers.update(features)
        layers['hair_front'],front_transform,front_gain=generated_front_layer(sex)
        layers['hair_back'],transform=rear_layer(sex)
        for key,img in layers.items():
            img[img[:,:,3]==0,:3]=0
            save(ROOT/sex/(key+'.png'),img)
        combined=composite(layers)
        save(ROOT/(sex+'_portrait.png'),combined)
        save(ROOT/'preview'/(sex+'_recomposed.png'),combined)
        for name,color in [('light','#e8e3dc'),('dark','#252423')]:
            background(combined,color).save(ROOT/'preview'/f'{sex}_{name}.png')
        pairs.append(background(combined,'#e8e3dc'))
        for key in ['eyes','eyebrows','hair_front','hair_back','face_shape','clothing']:
            background(layers[key],'#e8e3dc').save(ROOT/'preview'/f'{sex}_{key}_light.png')
        for key in ['eyes','hair_front','hair_back']:
            copy=dict(layers)
            copy[key]=np.zeros_like(source)
            background(composite(copy),'#e8e3dc').save(ROOT/'preview'/f'{sex}_without_{key}.png')
        for key in ['eyes','eyebrows']:
            eye=Image.fromarray(layers[key]).crop((414,328,620,410))
            for name,color in [('light','#e8e3dc'),('dark','#252423')]:
                tile=Image.new('RGBA',eye.size,color)
                tile.alpha_composite(eye)
                tile.convert('RGB').resize((1236,492)).save(ROOT/'preview'/f'{sex}_{key}_close_{name}.png')
        contact(sex,layers)
        alpha_bounds={k:Image.fromarray(v[:,:,3]).getbbox() for k,v in layers.items()}
        rig[sex]['measured_alpha_bounds']=alpha_bounds
        rig[sex]['rear_hair_source_transform']=transform
        rig[sex]['front_hair_source_transform']=front_transform
        rig[sex]['front_hair_material_gain']=front_gain
        front=layers['hair_front'][:,:,3]
        axis=517 if sex=='male' else 518
        anchors=rig[sex]['anchors']
        centerline=np.where(front[:340,axis]>190)[0]
        anchors['hair_crown']=[axis,int(centerline[0])]
        partline=np.where(front[:340,axis]>35)[0]
        anchors['hair_part']=[axis,int(partline[-1])]
        for side,condition,search in [('left',XX<axis,slice(350,500)),('right',XX>axis,slice(536,710))]:
            for key,y in [('hairline',280),('temple',348)]:
                line=np.where(front[y,search]>100)[0]+search.start
                if line.size:anchors[f'{key}_{side}']=[int(line[-1] if side=='left' else line[0]),y]
            roots=np.where(front[542,search]>100)[0]+search.start
            if roots.size:anchors[f'front_lock_root_{side}']=[int(roots[-1] if side=='left' else roots[0]),542]
            support=(front>32)&condition&(YY>700)
            last_y=int(np.where(support)[0].max())
            at_tip=support&(YY>=last_y-2)
            anchors[f'front_lock_tip_{side}']=[int(np.median(XX[at_tip])),last_y]
        rig[sex]['skin_palette_offset_rgb']=[6,12,15] if sex=='male' else [0,0,0]
        without=dict(layers);without['hair_back']=np.zeros_like(source)
        delta=np.max(np.abs(np.array(background(combined,'#e8e3dc')).astype(np.int16)-np.array(background(composite(without),'#e8e3dc')).astype(np.int16)),axis=2)
        eye_visible=layers['eyes'][:,:,3]>0
        report[sex]={
          'canvas':[W,H], 'alpha_bounds':alpha_bounds,
          'eyes_pixels':int(np.count_nonzero(eye_visible)),
          'eye_wash_pixels':int(np.count_nonzero(eye_visible&(eye_mask==0))),
          'opaque_eye_skin_pixels_outside_opening':int(np.count_nonzero((layers['eyes'][:,:,3]>240)&(eye_mask==0))),
          'opaque_eye_rgb_changed_from_user_reference':int(np.count_nonzero(np.any(layers['eyes'][:,:,:3]!=bald[:,:,:3],axis=2)&(eye_mask==255))),
          'rear_exposed_pixels_over_12':int(np.count_nonzero(delta>12)),
          'rear_center_is_painted':bool(layers['hair_back'][440,517,3]>240),
          'clear_skin_forehead_median_rgb':np.median(layers['face_shape'][300:325,470:565,:3],axis=(0,1)).tolist(),
          'fully_transparent_pixels_have_zero_rgb':all(not np.any(v[:,:,:3][v[:,:,3]==0]) for v in layers.values()),
          'checks_are_technical_not_visual_acceptance':True,
        }
    pair=Image.new('RGB',(2048,H),'#e8e3dc')
    for n,img in enumerate(pairs):pair.paste(img,(n*W,0))
    pair.save(ROOT/'preview/pair_light.png')
    (ROOT/'rig/portrait_rig_v4.json').write_text(json.dumps(rig,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'qa_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    manifest=json.loads((V3/'portrait_manifest.json').read_text(encoding='utf-8'))
    composer=ROOT.parents[2]/'csharp/panel/InkPortraitTestComposer.cs'
    manifest.update({'id':ROOT.name,'rig':'rig/portrait_rig_v4.json','test_entry_integrated':ROOT.name in composer.read_text(encoding='utf-8'),'runtime_integrated':False,'source':'Current base set only; original-coordinate pigment extraction authorized by user; independently generated complete front/rear hair'})
    (ROOT/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
