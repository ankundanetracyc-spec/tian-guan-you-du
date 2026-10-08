"""Restore our own painted face; Guigu supplies only layering experience."""
from pathlib import Path
import importlib.util
import json
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parent
V4=ROOT.parent/'ink_portrait_pair_20261003_v4'
V1=ROOT.parent/'ink_portrait_pair_20261003_v1'
spec=importlib.util.spec_from_file_location('current_split',V4/'build_assets.py')
old=importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)
W,H=1024,1536
ORDER=old.ORDER
YY,XX=np.indices((H,W))

def save(path,a):
    path.parent.mkdir(parents=True,exist_ok=True)
    a=np.clip(a,0,255).astype(np.uint8).copy()
    a[a[:,:,3]==0,:3]=0
    Image.fromarray(a).save(path)

def mask(points):
    return old.polygon(points).astype(np.float32)/255

def skin_palette(rgb,sex,organ):
    result=rgb.astype(np.float32).copy()
    if sex=='male':
        # The same small complexion adjustment as v4, preserving ink/lips.
        weight=np.clip((result.mean(2)-65)/90,0,1)
        weight[organ>0]=0
        result+=weight[:,:,None]*np.array([6,12,15],np.float32)
    return np.clip(result,0,255)

def facial_parts(sex):
    authored=old.read(V1/'reference'/f'{sex}_bald_user.png')
    technical={key:old.read(V4/sex/f'{key}.png') for key in ORDER}
    eye_core=np.maximum.reduce([mask(p) for p in old.EYE_PATHS[sex]])
    lip_paths={
      'male':[(481,480),(494,476),(507,475),(517,478),(531,476),(544,480),(553,483),(539,486),(529,490),(516,492),(503,490),(492,486)],
      'female':[(485,482),(499,478),(510,477),(518,480),(529,478),(541,482),(550,486),(538,490),(532,496),(520,499),(508,496),(500,491),(489,488)]}
    lip_core=mask(lip_paths[sex])
    target=skin_palette(authored[:,:,:3],sex,np.maximum(eye_core,lip_core))
    if sex=='male':
        # Correct only the paired upper-lid glints, retaining our eye painting.
        baseline=technical['face_shape'][:,:,:3].astype(np.float32)
        for x in (466,567):
            repair=np.exp(-.5*(((XX-x)/4.5)**2+((YY-360)/2.5)**2))
            repair[repair<.015]=0
            target=target*(1-repair[:,:,None])+baseline*repair[:,:,None]
        for x in (466,567):
            highlight=np.exp(-.5*(((XX-x)/2.4)**2+((YY-360)/1.3)**2))
            target+=highlight[:,:,None]*np.array([4,3.5,3],np.float32)
        target=np.clip(target,0,255)
    face=technical['face_shape'].copy()
    geom_spec=importlib.util.spec_from_file_location('user_head_geometry',old.V3/'build_assets.py')
    geom=importlib.util.module_from_spec(geom_spec)
    geom_spec.loader.exec_module(geom)
    visible,_=geom.face_geometry(authored,sex)
    face[visible,:3]=target[visible]
    face[:,:,3][visible & (authored[:,:,3]>240)]=255
    baseline=technical['face_shape'][:,:,:3].astype(np.float32)
    fields={key:technical[key][:,:,3].astype(np.float32)/255 for key in ('eyebrows','eyes','nose','mouth')}
    # The nose's warm highlights belong to the nose as much as its dark ink.
    nose=mask([(503,408),(531,408),(537,429),(543,444),(540,458),(532,465),(515,472),(502,464),(493,456),(491,444),(499,429)])
    mouth=mask([(477,478),(490,471),(508,470),(519,474),(533,473),(548,480),(557,485),(550,497),(534,506),(510,507),(491,500),(479,491)])
    for key,field in [('nose',nose),('mouth',mouth)]:
        edge=np.clip(cv2.distanceTransform((field>.05).astype(np.uint8),cv2.DIST_L2,3)/4,0,1)
        fields[key]=np.maximum(fields[key],field*edge*.12)
    # The v4 pigment projection omitted lighter strokes and brow edges.
    # Use complete anatomical supports to keep every feature off the face.
    eye_paths={
      'male': [[(423,362),(437,355),(459,354),(478,360),(493,376),(493,386),(476,395),(449,394),(430,383)],
               [(537,375),(554,358),(577,353),(600,354),(611,361),(608,382),(588,393),(563,394),(542,386)]],
      'female': [[(422,369),(433,359),(454,357),(475,360),(492,380),(492,398),(471,402),(448,397),(431,384)],
                 [(543,381),(559,361),(580,357),(601,361),(617,369),(605,390),(584,401),(561,401),(543,395)]]}
    brow_paths={
      'male': [[(417,350),(430,342),(449,341),(470,345),(489,351),(495,355),(478,355),(455,351),(434,350)],
               [(539,354),(551,347),(576,341),(600,340),(611,346),(590,347),(566,351),(549,356)]],
      'female': [[(417,355),(429,348),(450,346),(468,348),(489,355),(498,360),(484,360),(464,353),(440,352)],
                 [(540,360),(552,352),(574,347),(599,345),(615,349),(599,352),(576,353),(554,361)]]}
    supports={
        'eyes':np.maximum.reduce([mask(p) for p in eye_paths[sex]]),
        'eyebrows':np.maximum.reduce([mask(p) for p in brow_paths[sex]]),
        'nose':nose, 'mouth':mouth,
    }
    supports={key:cv2.dilate((field>.05).astype(np.uint8),
              np.ones((15,15),np.uint8)) for key,field in supports.items()}
    # Every pixel has one owner. Brow pigment cannot be duplicated by eyes.
    supports['eyes'][supports['eyebrows']>0]=0
    fields['eyes'][supports['eyebrows']>0]=0
    supports['nose'][YY>=470]=0
    supports['mouth'][YY<470]=0
    full_support=np.maximum.reduce(list(supports.values()))
    distance=cv2.distanceTransform(full_support,cv2.DIST_L2,3)
    fade=np.clip(distance/5,0,1)
    fade=np.maximum(fade,np.maximum(eye_core,lip_core))
    clean=target*(1-fade[:,:,None])+baseline*fade[:,:,None]
    face[full_support>0,:3]=clean[full_support>0]
    result={}
    for key in ('eyebrows','eyes','nose','mouth'):
        alpha=fields[key].copy()
        region=supports[key]>0
        alpha[~region]=0
        # Use one continuous skin base across adjoining feature supports.
        # Feathering each part separately left a crease at ownership joins.
        # Straight-alpha unmixing, retaining the original warm pigment RGB.
        # Find the smallest legal opacity for all three channels; this
        # preserves our painted color rather than forcing a gray ink palette.
        minimum=np.where(target<clean,(clean-target)/np.maximum(clean,1),
                         (target-clean)/np.maximum(255-clean,1)).max(2)
        alpha=np.maximum(alpha,minimum)
        alpha[~region]=0
        if key=='eyes':alpha=np.maximum(alpha,eye_core)
        if key=='mouth':alpha=np.maximum(alpha,lip_core)
        aa=np.round(np.clip(alpha,0,1)*255)/255
        foreground=(target-(1-aa[:,:,None])*clean)/np.maximum(aa[:,:,None],1/255)
        part=np.zeros_like(authored)
        part[:,:,:3]=np.round(np.clip(foreground,0,255))
        part[:,:,3]=np.round(aa*255)
        part[part[:,:,3]==0,:3]=0
        result[key]=part
    technical.update(result)
    technical['face_shape']=face
    return technical,authored,target,visible,eye_core,lip_core

def compose(layers,hidden=None):
    image=Image.new('RGBA',(W,H))
    for key in ORDER:
        if key!=hidden:image.alpha_composite(Image.fromarray(layers[key]))
    return np.array(image)

def on_background(a,color):
    bg=Image.new('RGBA',(W,H),color)
    bg.alpha_composite(Image.fromarray(a))
    return bg.convert('RGB')

def main():
    (ROOT/'preview').mkdir(parents=True,exist_ok=True)
    rig=json.loads((V4/'rig/portrait_rig_v4.json').read_text(encoding='utf-8'))
    rig['id']=ROOT.name
    rig['art_style_source']='User male/female reference paintings only, including ears; Guigu is technical layering reference only'
    reports={}
    pair=[]
    comparison=Image.new('RGB',(1400,1140),'#e8e3dc')
    draw=ImageDraw.Draw(comparison)
    for n,sex in enumerate(('male','female')):
        layers,authored,target,visible,eye_core,lip_core=facial_parts(sex)
        for key,a in layers.items():save(ROOT/sex/f'{key}.png',a)
        combined=compose(layers)
        save(ROOT/f'{sex}_portrait.png',combined)
        pair.append(on_background(combined,'#e8e3dc'))
        rig[sex]['measured_alpha_bounds']={k:Image.fromarray(a[:,:,3]).getbbox() for k,a in layers.items()}
        for name,color in [('light','#e8e3dc'),('dark','#252423')]:
            on_background(combined,color).save(ROOT/'preview'/f'{sex}_{name}.png')
        for key in ORDER:
            on_background(layers[key],'#e8e3dc').save(ROOT/'preview'/f'{sex}_{key}_light.png')
        for key in ['eyes','eyebrows','nose','mouth']:
            crop=Image.fromarray(layers[key]).crop((410,330,630,512))
            for name,color in [('light','#e8e3dc'),('dark','#252423')]:
                bg=Image.new('RGBA',crop.size,color)
                bg.alpha_composite(crop)
                bg.convert('RGB').resize((880,728)).save(ROOT/'preview'/f'{sex}_{key}_close_{name}.png')
        contact_sheet=Image.new('RGB',(1020,1410),'#d8d2c9')
        for slot,key in enumerate(ORDER):
            tile=Image.new('RGBA',(320,420),'#ece7df')
            item=Image.fromarray(layers[key])
            bounds=item.getchannel('A').getbbox()
            if bounds:
                crop=item.crop((max(bounds[0]-6,0),max(bounds[1]-6,0),min(bounds[2]+6,W),min(bounds[3]+6,H)))
                crop.thumbnail((304,402),Image.Resampling.LANCZOS)
                tile.alpha_composite(crop,((320-crop.width)//2,(420-crop.height)//2))
            x,y=slot%3*340,slot//3*470
            contact_sheet.paste(tile.convert('RGB'),(x+10,y+35))
            ImageDraw.Draw(contact_sheet).text((x+12,y+10),sex+' / '+key,fill='#25211e')
        contact_sheet.save(ROOT/'preview'/f'{sex}_nine_parts.png')
        for key in ['eyes','eyebrows','hair_front','hair_back']:
            on_background(compose(layers,key),'#e8e3dc').save(ROOT/'preview'/f'{sex}_without_{key}.png')
        face_layers={k:layers[k] for k in ['face_shape','eyes','eyebrows','nose','mouth']}
        naked={k:face_layers.get(k,np.zeros_like(authored)) for k in ORDER}
        actual=compose(naked)
        original=authored.copy()
        original[:,:,:3]=np.round(target)
        # Compare the actual face composite without hair covering its shape.
        bounds=(360,160,670,555)
        for col,img in enumerate([original,actual]):
            crop=on_background(img,'#e8e3dc').crop(bounds).resize((465,593),Image.Resampling.LANCZOS)
            crop=crop.crop((0,0,465,530))
            x=n*700+col*330
            crop.thumbnail((310,530),Image.Resampling.LANCZOS)
            comparison.paste(crop,(x+20,40))
            draw.text((x+20,15),sex+(' / our reference' if col==0 else ' / five-part composite'),fill='#302c28')
        valid=visible&(authored[:,:,3]>240)
        error=np.abs(actual[:,:,:3].astype(np.float32)-target).max(2)
        ears=valid&(YY>344)&(YY<446)&((XX<423)|(XX>613))
        eye=layers['eyes']
        eye_skin=(eye[:,:,3]>240)&(eye_core<.01)&(eye[:,:,0]>130)&(eye[:,:,0].astype(int)-eye[:,:,1]>12)&(eye[:,:,1].astype(int)-eye[:,:,2]>5)
        reports[sex]={
            'style_source':'User reference; no Guigu artwork or color palette',
            'skin_reference_rgb_max_error':float(error[valid].max()),
            'skin_reference_rgb_mean_error':float(error[valid].mean()),
            'ears_reference_rgb_max_error':float(error[ears].max()),
            'eye_core_original_rgb_max_error':int(np.abs(layers['eyes'][:,:,:3].astype(int)-authored[:,:,:3].astype(int))[eye_core>.99].max()),
            'opaque_eye_skin_pixels_outside_opening':int(np.count_nonzero(eye_skin)),
            'fully_transparent_pixels_have_zero_rgb':all(not np.any(a[:,:,:3][a[:,:,3]==0]) for a in layers.values()),
            'clear_skin_forehead_median_rgb':np.median(layers['face_shape'][300:325,470:565,:3],axis=(0,1)).tolist(),
            'rear_center_is_painted':bool(layers['hair_back'][440,517,3]>240),
            'alpha_bounds':rig[sex]['measured_alpha_bounds'],
            'checks_are_not_user_art_approval':True,
        }
        # A natural-size comparison including our original silhouette/ears.
        board=Image.new('RGB',(1024,700),'#e8e3dc')
        review_layers=naked.copy()
        review_layers['clothing']=layers['clothing']
        review_face=compose(review_layers)
        for col,img in enumerate([original,review_face]):
            crop=on_background(img,'#e8e3dc').crop((320,135,715,700))
            board.paste(crop,(col*512+55,50))
        ImageDraw.Draw(board).text((55,15),'Our reference / actual modular face',fill='#302c28')
        board.save(ROOT/'preview'/f'{sex}_own_face_comparison.png')
    sheet=Image.new('RGB',(2048,H),'#e8e3dc')
    for n,img in enumerate(pair):sheet.paste(img,(n*W,0))
    sheet.save(ROOT/'preview/pair_light.png')
    comparison.save(ROOT/'preview/own_face_comparison.png')
    (ROOT/'rig').mkdir(exist_ok=True)
    (ROOT/'rig/portrait_rig_v5.json').write_text(json.dumps(rig,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'qa_report.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
    manifest=json.loads((V4/'portrait_manifest.json').read_text(encoding='utf-8'))
    manifest.update({'id':ROOT.name,'rig':'rig/portrait_rig_v5.json','source':'Our original painted facial colors, outlines and ears; straight-alpha unmixing; current generated hair reused','test_entry_integrated':False,'ui_verified':False,'visual_approval':'pending_user_review'})
    manifest.pop('verification',None)
    (ROOT/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(reports,ensure_ascii=False))

if __name__=='__main__':main()
