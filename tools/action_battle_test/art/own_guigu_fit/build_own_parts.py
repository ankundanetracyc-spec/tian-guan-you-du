from PIL import Image, ImageDraw
from pathlib import Path
import json, math

# This script is stored at tools/action_battle_test/art/own_guigu_fit/build_own_parts.py
repo = Path(__file__).resolve().parents[4]
art = repo / 'tools/action_battle_test/art'
out = art / 'own_guigu_fit'
ref = art / 'guigu_reference'
layout = json.loads((ref / 'layout.json').read_text(encoding='utf-8'))
skins = json.loads((repo / 'tools/action_battle_test/guigu_rig/skins.json').read_text(encoding='utf-8'))
skin_meshes = {mesh['rendererPath']: mesh for mesh in skins['meshes']}
texture_scale = 4
atlas = Image.open(out / 'bilan_battle_atlas_flat_v2.png').convert('RGBA')
skin_asset = Image.open(repo / 'textures/characters/side_ink_portrait_outfits_20261015_v3/male_idle.png').convert('RGBA')

def sample_project_skin_tone():
    # The face region in the project's male idle artwork supplies the palette.
    region = skin_asset.crop((1000, 660, 1042, 735)).convert('RGB')
    samples=[]
    for r,g,b in region.get_flattened_data():
        if r > 205 and g > 130 and b > 80 and r > g > b and g-b > 20:
            samples.append((r,g,b))
    if not samples:
        raise RuntimeError('Could not sample skin tones from the project male_idle art')
    channels=tuple(sorted(pixel[i] for pixel in samples)[len(samples)//2] for i in range(3))
    return channels

project_skin = sample_project_skin_tone()

# Every visible color crop uses the same battle-only simplified atlas, which
# preserves the project's 碧岚云叶袍 while making its large layers readable.
source_rects = {
    'robe': (755, 25, 1005, 580),
    'arm_cloth': (1298, 135, 1460, 500),
    # Use the simplified outer sleeve motif; its alpha is still the source rig's.
    'sleeve_surface': (1298, 135, 1460, 390),
    'leg': (823, 590, 992, 913),
    'rear_hair': (1278, 566, 1554, 925),
}

part_source = {
    79450: ('bilan_robe.png', 'robe'),
    78517: ('bilan_left_arm.png', 'arm_cloth'),
    89721: ('bilan_right_arm.png', 'arm_cloth'),
    71535: ('bilan_sleeve_overlay.png', 'sleeve_surface'),
    56808: ('bilan_hair.png', 'rear_hair'),
    76416: ('bilan_featureless_face.png', None),
    61872: ('bilan_leg.png', 'leg'),
}

# Resample project-owned illustrations directly onto a 4x sprite canvas. The
# canvas aspect and normalized UV coordinates stay unchanged, while the extra
# texels preserve cloth details when the sprite is rendered at battlefield size.
def projected_texture(source_key, size):
    x0,y0,x1,y1 = source_rects[source_key]
    crop = atlas.crop((x0,y0,x1,y1))
    # Matte transparent pixels onto a sampled garment tone before resizing. The
    # original atlas has black RGB under transparent pixels; resizing straight
    # RGBA then replacing alpha would leak those colors into the source silhouette.
    matte_color = {
        'robe': (43, 74, 66),
        'arm_cloth': (43, 74, 66),
        'sleeve_surface': (42, 76, 66),
        'leg': (196, 185, 164),
        'rear_hair': (53, 49, 47),
    }[source_key]
    matte = Image.new('RGBA', crop.size, (*matte_color, 255))
    matte.alpha_composite(crop)
    # A matted, high-resolution crop avoids black RGB leakage at transparent
    # edges. LANCZOS keeps the project's textile marks crisp at the 4x output.
    rgb = matte.convert('RGB').resize(size, Image.Resampling.LANCZOS)
    # Lift the deepest ink toward robe teal and warm clipped ivory highlights;
    # this keeps sleeve and shoulder marks legible at the small in-game scale.
    px = rgb.load()
    for y in range(rgb.height):
        for x in range(rgb.width):
            r,g,b = px[x,y]
            if source_key in ('robe','arm_cloth','sleeve_surface'):
                luma=0.299*r+0.587*g+0.114*b
                if luma < 92:
                    blend=min(0.76,(92-luma)/92*0.76)
                    r=round(r*(1-blend)+43*blend)
                    g=round(g*(1-blend)+74*blend)
                    b=round(b*(1-blend)+66*blend)
                if luma > 232:
                    blend=min(0.65,(luma-232)/23*0.65)
                    r=round(r*(1-blend)+237*blend)
                    g=round(g*(1-blend)+226*blend)
                    b=round(b*(1-blend)+205*blend)
            if max(r,g,b)<12:
                r,g,b=22,31,29
            elif min(r,g,b)>248:
                r,g,b=239,229,207
            px[x,y]=(r,g,b)
    return rgb.convert('RGBA')

def skin_texture(size, alpha, face=False):
    w,h=size
    im=Image.new('RGBA',(w,h),(0,0,0,0)); pixels=im.load()
    # Median warm skin tone sampled from the project's existing male idle art.
    base=(236,186,149) if face else (224,171,132)
    for y in range(h):
        for x in range(w):
            dx=(x/(max(w-1,1))-0.40)
            dy=(y/(max(h-1,1))-0.35)
            # Subtle edge shading only; no facial marks or contour lines.
            shade=max(-0.11,min(0.08,-0.07*dx+0.05*dy))
            rgb=tuple(max(0,min(255,round(c*(1+shade)))) for c in base)
            pixels[x,y]=(*rgb,0)
    im.putalpha(alpha)
    return im

def sleeve_arm_texture(size, alpha):
    im=projected_texture('arm_cloth',size)
    pixels=im.load(); w,h=size
    for y in range(round(h*0.70),h):
        t=(y-round(h*0.70))/max(1,h-round(h*0.70)-1)
        for x in range(w):
            if y < round(h*0.77):
                # A thin warm-ivory cuff breaks cleanly into the sleeve.
                color=(229,215,193)
            else:
                shade=1.0-0.06*abs(x/max(1,w-1)-0.5)
                color=tuple(round(c*shade) for c in project_skin)
            pixels[x,y]=(*color,255)
    im.putalpha(alpha)
    return im

def featureless_face(size, alpha):
    w,h=size
    im=Image.new('RGBA', (w,h), (0,0,0,0))
    pix=im.load()
    # Quiet warm skin shading sampled in the project's ink palette; no facial marks.
    for y in range(h):
        for x in range(w):
            dx=(x/(max(w-1,1))-0.33)
            dy=(y/(max(h-1,1))-0.28)
            shade=max(-0.20,min(0.16, -0.11*dx + 0.07*dy))
            base=project_skin
            rgb=tuple(max(0,min(255,round(c*(1+shade)))) for c in base)
            pix[x,y]=(*rgb,0)
    im.putalpha(alpha)
    return im

# Export every unique source sprite at 4x while preserving the source crop aspect.
outputs={}
for sid, (png, source_key) in part_source.items():
    reference=Image.open(ref/f'sprite_{sid}.png').convert('RGBA')
    logical_size=reference.size
    size=(logical_size[0]*texture_scale,logical_size[1]*texture_scale)
    source_alpha=reference.getchannel('A')
    # Nearest replication preserves the original antialiased alpha values and
    # silhouette exactly on a 4x grid; runtime Linear filtering smooths it at
    # the final battlefield scale without introducing resampling halos.
    alpha=source_alpha.resize(size,Image.Resampling.NEAREST)
    if sid == 76416:
        own=featureless_face(size, alpha)
    elif sid in (78517,89721):
        own=sleeve_arm_texture(size,alpha)
    else:
        own=projected_texture(source_key,size)
        own.putalpha(alpha)
        # Use the resampled source alpha while retaining matted RGB under its edges.
        own_pixels=own.load(); ap=alpha.load()
        for y in range(size[1]):
            for x in range(size[0]):
                r,g,b,_=own_pixels[x,y]
                own_pixels[x,y]=(r,g,b,ap[x,y])
    own.save(out/png, optimize=True)
    outputs[sid]={
        'png':png,
        'width':size[0],
        'height':size[1],
        'logical_width':logical_size[0],
        'logical_height':logical_size[1],
        'source_alpha_bbox':list(source_alpha.getbbox()),
        'alpha_bbox':list(alpha.getbbox()),
    }

# Preserve the source transform entries and replace only the sprite filename.
path_to_sid={
    '/10002/Root/Body/yifu/yifu':79450,
    '/10002/Root/Body/yifu/yifu/shenti1/zuobi/zuobi':78517,
    '/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi':89721,
    '/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi/youbi1/youbi2/101_youbi_1':71535,
    '/10002/Root/Body/yifu/yifu/shenti1/tou/toufa/10002(Clone)/117':56808,
    '/10002/Root/Body/yifu/yifu/shenti1/tou/101(Clone)/lian':76416,
    '/10002/Root/Body/Foot/zuotui/zuotui':61872,
    '/10002/Root/Body/Foot/youtui/youtui':61872,
}
renderer_paths={
    79450:'/10002/Root/Body/yifu/yifu',
    78517:'/10002/Root/Body/yifu/yifu/shenti1/zuobi/zuobi',
    89721:'/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi',
    71535:'/10002/Root/Body/yifu/yifu/shenti1/youbi/youbi/youbi1/youbi2/101_youbi_1',
    56808:'/10002/Root/Body/yifu/yifu/shenti1/tou/toufa/10002(Clone)/117',
    76416:'/10002/Root/Body/yifu/yifu/shenti1/tou/101(Clone)/lian',
    61872:['/10002/Root/Body/Foot/zuotui/zuotui','/10002/Root/Body/Foot/youtui/youtui'],
}
rig_by_path={}
for entry in layout:
    mesh=skin_meshes[entry['path']]
    rig_by_path[entry['path']]={
        'rendererId':mesh['rendererComponentPathId'],
        'boneNames':[bone['name'] for bone in mesh.get('skinning',{}).get('spriteBones',[])],
        'activeSelf':mesh.get('activeSelf'),
        'activeInHierarchy':mesh.get('activeInHierarchy'),
        'pivotFraction':mesh.get('spritePivotFraction'),
        'uvCropLowerLeft':mesh.get('uvConvention',{}).get('uvCropLowerLeft'),
        'sourceTextureRectPixels':mesh.get('uvConvention',{}).get('sourceTextureRectPixels'),
    }
parts=[]
for entry in layout:
    sid=path_to_sid[entry['path']]
    item=dict(entry)
    item['sourceSpriteId']=sid
    item['sourceSprite']=entry['sprite']
    item['sprite']=outputs[sid]['png']
    item['width']=outputs[sid]['width']
    item['height']=outputs[sid]['height']
    item['sourceLogicalWidth']=outputs[sid]['logical_width']
    item['sourceLogicalHeight']=outputs[sid]['logical_height']
    item['textureScale']=texture_scale
    item['spriteRendererPath']=entry['path']
    item['anchorPath']=entry['path']
    item['rendererId']=rig_by_path[entry['path']]['rendererId']
    item['boneNames']=rig_by_path[entry['path']]['boneNames']
    item['pivotFraction']=rig_by_path[entry['path']]['pivotFraction']
    item['uvCropLowerLeft']=rig_by_path[entry['path']]['uvCropLowerLeft']
    item['sourceTextureRectPixels']=rig_by_path[entry['path']]['sourceTextureRectPixels']
    item['activeInHierarchy']=rig_by_path[entry['path']]['activeInHierarchy']
    item['rigRootPath']='/10002/Root'
    parts.append(item)
(out/'own_layout.json').write_text(json.dumps(parts,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
renderer_paths={sid:[entry['path'] for entry in layout if path_to_sid[entry['path']]==sid] for sid in part_source}
inactive_duplicate_renderers={}
for mesh in skins['meshes']:
    sid=mesh.get('sourceSpriteId')
    if sid in part_source and mesh['rendererPath'] not in renderer_paths[sid] and not mesh.get('activeInHierarchy',False):
        inactive_duplicate_renderers.setdefault(sid,[]).append({
            'spriteRendererPath':mesh['rendererPath'],
            'rendererId':mesh['rendererComponentPathId'],
            'activeSelf':mesh.get('activeSelf'),
            'activeInHierarchy':mesh.get('activeInHierarchy'),
        })
manifest={
  'schema':'own-guigubahuang-fit/v1',
  'textureScale':texture_scale,
  'uvPolicy':'Replacement images are 4x the cropped source sprite canvas dimensions. Copy source uvCropLowerLeft values unchanged; do not multiply UVs by 4. They are nominally normalized but source mesh values span about -0.000000011 to 1.00404 due to vertices slightly outside cropped bounds, so preserve source sampling/clamp behavior. Mesh, bones, weights and bindposes are unchanged.',
  'runtimeTextureFilter':'Linear recommended for 4x textures at battlefield display sizes; preserve clamp behavior for source UV edge overshoot.',
  'designReferences':['textures/items/clothing/portrait_clothing_male_bilan_yunye.png','tools/action_battle_test/art/bilan_puppet_parts_v2.png'],
  'sourceProjectTextures':['textures/characters/side_ink_portrait_outfits_20261015_v3/male_idle.png'],
  'battleArtSources':['tools/action_battle_test/art/own_guigu_fit/bilan_battle_atlas_flat_v2.png'],
  'sourceReferenceLayout':'tools/action_battle_test/art/guigu_reference/layout.json',
  'rigSource':'tools/action_battle_test/guigu_rig/skins.json',
  'coordinatePolicy':'Each replacement PNG is 4x the cropped source sprite canvas width and height; original alpha values are replicated to the 4x grid unchanged. All garment, leg and hair RGB comes from the single simplified battle atlas; arm cuff/hand and featureless face use the project palette. Original game RGB is not reused.',
  'interchangePolicy':'Use own_layout.json as the renderer mapping. It keeps each source renderer path/ID and bone-name list. Positions, angles, scale and z are copied from layout.json; its position is the full parent-chain transform in the character root space, scaled by 100 PPU and converted from Unity Y-up to canvas Y-down.',
  'parts':[
   {
    'sourceSpriteId':str(sid),
    'sourceSprite':f'sprite_{sid}.png',
    'png':outputs[sid]['png'],
    'width':outputs[sid]['width'],
    'height':outputs[sid]['height'],
    'sourceLogicalSize':[outputs[sid]['logical_width'],outputs[sid]['logical_height']],
    'textureScale':texture_scale,
    'sourceAlphaBBox':outputs[sid]['source_alpha_bbox'],
    'alphaBBox':outputs[sid]['alpha_bbox'],
    'spriteRendererPath':renderer_paths[sid],
    'rendererIds':[rig_by_path[p]['rendererId'] for p in renderer_paths[sid]],
    'boneNames':list(dict.fromkeys(b for p in renderer_paths[sid] for b in rig_by_path[p]['boneNames'])),
    'pivotFraction':rig_by_path[renderer_paths[sid][0]]['pivotFraction'],
    'uvCropLowerLeft':rig_by_path[renderer_paths[sid][0]]['uvCropLowerLeft'],
    'defaultVisibleRendererPaths':[p for p in renderer_paths[sid] if rig_by_path[p]['activeInHierarchy']],
    'inactiveDuplicateRenderers':inactive_duplicate_renderers.get(sid,[]),
   }
   for sid in part_source
  ],
  'notes':['sprite_61872.png is shared by the two currently visible Body/Foot leg renderers; duplicate Foot2 renderers are inactiveInHierarchy and listed for reference only.','The battle atlas uses one muted jade robe with a broad ivory inner layer and only a few large cloud-leaf motifs. Sprites 78517/89721 keep a short ivory cuff and project-palette hand tip.','sprite_76416.png is a separate featureless face texture.','Rig mesh, bone order, skin weights and bindposes remain the original data in tools/action_battle_test/guigu_rig/skins.json.','This package contains replacement sprite textures and mappings, not rebuilt mesh/skin or converted animation.']
}
(out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

# Render an apples-to-apples static layout comparison from the extracted anchors.
W,H=2280,1160
sheet=Image.new('RGBA',(W,H),(233,229,215,255)); draw=ImageDraw.Draw(sheet)
scale=7.0;ground=960;cx=[620,1710]
draw.text((220,38),'ORIGINAL PARTS · STATIC LAYOUT',fill=(40,48,42,255))
draw.text((1290,38),'BILAN CLOUD-LEAF ROBE · SAME ANCHORS',fill=(40,48,42,255))
draw.text((220,68),'layout.json transforms · no animation',fill=(93,100,89,255))
draw.text((1290,68),'project textures · 4x maps rendered at the same logical size',fill=(93,100,89,255))
draw.line((70,ground,W-70,ground),fill=(156,150,132,255),width=2)
# Back-to-front visual layer order matches the independent static arrangement.
order=['/Body/Foot/','/Body/yifu/yifu','/shenti1/zuobi/','/shenti1/youbi/youbi','/shenti1/tou/101(Clone)/lian','/shenti1/tou/toufa/']
# Custom order includes the right arm overlay after its parent.
def draw_figure(center_x, use_own):
    for key in order:
        for i,entry in enumerate(layout):
            p=entry['path']
            if key=='/Body/Foot/':
                if not '/Body/Foot/' in p: continue
            elif key=='/Body/yifu/yifu':
                if p!='/10002/Root/Body/yifu/yifu': continue
            elif key=='/shenti1/zuobi/':
                if not '/shenti1/zuobi/' in p: continue
            elif key=='/shenti1/youbi/youbi':
                if not ('/shenti1/youbi/youbi' in p): continue
            elif key=='/shenti1/tou/101(Clone)/lian':
                if not p.endswith('/lian'): continue
            elif key=='/shenti1/tou/toufa/':
                if '/tou/toufa/' not in p: continue
            sid=path_to_sid[p]
            filename=(outputs[sid]['png'] if use_own else entry['sprite'])
            im=Image.open((out if use_own else ref)/filename).convert('RGBA')
            if use_own:
                logical=(outputs[sid]['logical_width'],outputs[sid]['logical_height'])
                target=(round(logical[0]*scale),round(logical[1]*scale))
                im=im.resize(target,Image.Resampling.LANCZOS)
            else:
                im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.NEAREST)
            angle=float(entry['angle'])
            im=im.rotate(-math.degrees(angle),resample=Image.Resampling.BICUBIC,expand=True)
            px=center_x+float(entry['position'][0])*scale
            py=ground+float(entry['position'][1])*scale
            sheet.alpha_composite(im,(round(px-im.width/2),round(py-im.height/2)))
# show matching root center alignment
draw_figure(cx[0],False)
draw_figure(cx[1],True)
sheet.convert('RGB').save(out/'static_layout_alignment_preview.png')

# Pixel-level source/own view: each image is shown on a 4x logical pixel grid.
pcw,pch=560,470
pixel_sheet=Image.new('RGB',(pcw*4,pch*2),(233,229,215)); pd=ImageDraw.Draw(pixel_sheet)
labels={79450:'ROBE',78517:'LEFT ARM',89721:'RIGHT ARM',71535:'SLEEVE OVERLAY',56808:'HAIR',76416:'FEATURELESS FACE',61872:'LEG'}
for index,sid in enumerate(part_source):
    col=index%4;row=index//4;x=col*pcw;y=row*pch
    dims=f"{outputs[sid]['logical_width']}x{outputs[sid]['logical_height']} -> {outputs[sid]['width']}x{outputs[sid]['height']}"
    pd.text((x+12,y+10),f'{labels[sid]} · {dims}',fill=(40,48,42))
    ref_im=Image.open(ref/f'sprite_{sid}.png').convert('RGBA')
    own_im=Image.open(out/outputs[sid]['png']).convert('RGBA')
    factor=min(1.0,(pcw-30)/(2*max(own_im.width,own_im.width)+40),(pch-45)/own_im.height)
    if factor < 1.0:
        ref_im=ref_im.resize((round(ref_im.width*texture_scale*factor),round(ref_im.height*texture_scale*factor)),Image.Resampling.NEAREST)
        own_im=own_im.resize((round(own_im.width*factor),round(own_im.height*factor)),Image.Resampling.LANCZOS)
    else:
        ref_im=ref_im.resize((ref_im.width*texture_scale,ref_im.height*texture_scale),Image.Resampling.NEAREST)
    top=y+42+((pch-52-max(ref_im.height,own_im.height))//2)
    for j,im in enumerate((ref_im,own_im)):
        px=x+35+j*255+(250-im.width)//2
        checker=Image.new('RGBA',im.size,(240,237,225,255));checker.alpha_composite(im)
        pixel_sheet.paste(checker.convert('RGB'),(px,top))
        pd.text((x+50+j*250,y+25),'REF' if j==0 else 'OWN',fill=(93,100,89))
pixel_sheet.save(out/'pixel_size_and_alpha_check.png')

# Far-view check follows the user's 1280x512 battlefield reference. Both figures
# use the same source root-space anchors, at approximately 80 px full-body height.
far_w,far_h=1280,512
far=Image.new('RGBA',(far_w,far_h),(93,95,92,255)); fd=ImageDraw.Draw(far)
for yy in range(-10,far_h+60,54):
    offset=36 if (yy//54)%2 else 0
    for xx in range(-80,far_w+120,120):
        x=xx+offset; y=yy
        shade=((xx*17+yy*23)%13)-6
        fill=(max(0,94+shade),max(0,96+shade),max(0,92+shade),255)
        fd.polygon([(x,y),(x+106,y+3),(x+118,y+31),(x+92,y+51),(x+8,y+46),(x-9,y+23)],fill=fill,outline=(83,86,82,255))
far_scale=0.72; far_ground=382; centers=[420,860]
for center in centers:
    fd.ellipse((center-23,far_ground+3,center+23,far_ground+10),fill=(40,43,41,100))
fd.text((centers[0]-80,24),'ORIGINAL PARTS',fill=(232,229,215,255))
fd.text((centers[1]-130,24),'OWN BILAN ROBE',fill=(232,229,215,255))
def draw_far(center_x,use_own):
    facing=1.0 if center_x < far_w/2 else -1.0
    for key in order:
        for entry in layout:
            p=entry['path']
            if key=='/Body/Foot/':
                if '/Body/Foot/' not in p: continue
            elif key=='/Body/yifu/yifu':
                if p!='/10002/Root/Body/yifu/yifu': continue
            elif key=='/shenti1/zuobi/':
                if '/shenti1/zuobi/' not in p: continue
            elif key=='/shenti1/youbi/youbi':
                if '/shenti1/youbi/youbi' not in p: continue
            elif key=='/shenti1/tou/101(Clone)/lian':
                if not p.endswith('/lian'): continue
            elif key=='/shenti1/tou/toufa/':
                if '/tou/toufa/' not in p: continue
            sid=path_to_sid[p]
            filename=outputs[sid]['png'] if use_own else entry['sprite']
            im=Image.open((out if use_own else ref)/filename).convert('RGBA')
            if use_own:
                logical=(outputs[sid]['logical_width'],outputs[sid]['logical_height'])
                target=(max(1,round(logical[0]*far_scale)),max(1,round(logical[1]*far_scale)))
                im=im.resize(target,Image.Resampling.LANCZOS)
            else:
                im=im.resize((max(1,round(im.width*far_scale)),max(1,round(im.height*far_scale))),Image.Resampling.LANCZOS)
            if facing < 0:
                im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            im=im.rotate(-math.degrees(float(entry['angle'])*facing),resample=Image.Resampling.BICUBIC,expand=True)
            px=center_x+float(entry['position'][0])*far_scale*facing
            py=far_ground+float(entry['position'][1])*far_scale
            far.alpha_composite(im,(round(px-im.width/2),round(py-im.height/2)))
draw_far(centers[0],False)
draw_far(centers[1],True)
far.convert('RGB').save(out/'distant_readability_preview.png')

print('Exported replacement sprites:')
for sid,item in outputs.items(): print(sid,item)
