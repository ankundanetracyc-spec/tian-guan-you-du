"""Prepare fixed-canvas, independently selectable appearance test layers.

The AI paintings in test_sources are source drawings. This script only registers,
masks and composites them. No runtime image manipulation depends on this script.
"""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps
import numpy as np
import cv2

ROOT = Path(__file__).resolve().parents[1]
PKG = ROOT / 'mods/appearance/腰上男修拆件演示_v2'
SRC = PKG / 'test_sources'
OUT = PKG / 'test_assets_v6'
OUT.mkdir(exist_ok=True)
PREVIEW = PKG / 'preview/rework_v6'
PREVIEW.mkdir(parents=True, exist_ok=True)
SIZE = (1024, 1536)
REF = Image.open(PKG / 'reference/user_selected_portrait.png').convert('RGBA')
BLANK = Image.open(SRC / 'blank_full.png').convert('RGBA')
assert REF.size == BLANK.size == SIZE

def drawing(name):
    im = Image.open(SRC / (name + '.png')).convert('RGBA')
    if im.size != SIZE:
        raise ValueError(f'{name}: expected {SIZE}, got {im.size}')
    return im

def region(polygons, blur=0):
    m = Image.new('L', SIZE)
    d = ImageDraw.Draw(m)
    for poly in polygons:
        d.polygon(poly, fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m

def in_rects(rects, blur=0):
    m = Image.new('L', SIZE)
    d = ImageDraw.Draw(m)
    for box in rects:
        d.ellipse(box, fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m

def smoothstep(value):
    value = np.clip(value, 0, 1)
    return value * value * (3 - 2 * value)

def extract_eye(source, top):
    """Keep painted eye details, leaving surrounding skin fully transparent."""
    src = np.asarray(source.convert('RGB'), dtype=np.float32)
    base = np.asarray(BLANK.convert('RGB'), dtype=np.float32)
    light_src = src @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    light_base = base @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    delta = light_base - light_src
    ink = smoothstep((delta - 32) / 78)
    sclera = smoothstep((-delta - 8) / 52)
    detail = np.maximum(ink, sclera)

    broad = Image.new('L', SIZE)
    interior = Image.new('L', SIZE)
    broad_draw, inside_draw = ImageDraw.Draw(broad), ImageDraw.Draw(interior)
    for center in (459, 565):
        broad_draw.ellipse((center-54, top-14, center+54, top+39), fill=255)
        inside_draw.polygon([
            (center-39,top+14), (center-30,top+9), (center-10,top+6),
            (center+12,top+6), (center+31,top+10), (center+39,top+15),
            (center+26,top+25), (center,top+28), (center-28,top+24)
        ], fill=255)
    broad_arr = np.asarray(broad.filter(ImageFilter.GaussianBlur(4)), dtype=np.float32)/255
    inside_arr = np.asarray(interior.filter(ImageFilter.GaussianBlur(1.5)), dtype=np.float32)/255
    yy, xx = np.indices((SIZE[1], SIZE[0]), dtype=np.float32)
    upper = np.zeros_like(detail)
    for center, direction in ((459, 1), (565, -1)):
        innerness = np.clip(((xx-center)*direction-5)/35, 0, 1)
        lid_floor = top-1+14*innerness
        gate = smoothstep((yy-(lid_floor-2))/4)
        upper = np.where(np.abs(xx-center)<54, np.maximum(upper,gate), upper)
    lower = smoothstep(((top+36)-yy)/8)
    alpha = np.maximum(detail, inside_arr)*broad_arr*upper*lower
    for center in (459, 565):
        edge = np.clip((np.abs(xx-center)-25)/22,0,1)
        low = np.clip((yy-(top+8))/20,0,1)
        alpha *= np.where(np.abs(xx-center)<49,1-edge*(0.23+0.25*low),1)
    rgba = np.asarray(source).copy()
    rgba[:,:,3] = np.clip(alpha*rgba[:,:,3],0,255).astype(np.uint8)
    return Image.fromarray(rgba, 'RGBA')

def flatten_brows(layer, index):
    """Keep the original ink texture while removing the raised outer tails."""
    src = np.asarray(layer, dtype=np.float32)
    dst = np.zeros(src.shape, dtype=np.uint8)
    ys = np.arange(310, 371, dtype=np.float32)
    dest_ys = np.arange(314, 370, dtype=np.float32)
    # base height, gentle middle arch, inner-end tilt, thickness, ink opacity
    designs = (
        (355.0, 4.0, 0.0, 0.86, 0.90),  # natural arc
        (356.0, 0.8, 0.0, 0.80, 0.82),  # flat
        (356.0, 5.0, 0.5, 0.90, 0.90),  # round arc
        (354.0, 1.5, 2.5, 0.92, 0.94),  # mildly rising outer tail
        (356.0, 1.2, -0.8, 0.75, 0.80), # fine, slightly lower outer tail
    )
    base, arch, tilt, thickness, ink = designs[index]
    for start, stop, outer, sign in ((407, 503, 425, 1), (521, 620, 599, -1)):
        fit_x, fit_y, fit_w = [], [], []
        for x in range(start, stop):
            alpha = src[320:366, x, 3]
            weight = alpha.sum()
            if weight < 300:
                continue
            fit_x.append(np.clip((x-outer)*sign/70, 0, 1))
            fit_y.append(np.dot(np.arange(320,366), alpha)/weight)
            fit_w.append(weight)
        slope, intercept = np.polyfit(fit_x, fit_y, 1, w=np.sqrt(fit_w))
        for x in range(start, stop):
            innerness = np.clip((x-outer)*sign/70, 0, 1)
            source_center = intercept + slope*innerness
            target_center = base - arch*np.sin(np.pi*innerness) + tilt*innerness
            sample_y = source_center + (dest_ys-target_center)/thickness
            alpha = np.interp(sample_y, ys, src[310:371,x,3], left=0, right=0)
            dst[314:370,x,3] = np.clip(alpha*ink,0,255).astype(np.uint8)
            for channel in range(3):
                premul = src[310:371,x,channel]*src[310:371,x,3]/255
                sampled = np.interp(sample_y, ys, premul, left=0, right=0)
                dst[314:370,x,channel] = np.clip(
                    np.divide(sampled*255,alpha,out=np.zeros_like(alpha),where=alpha>0),0,255
                ).astype(np.uint8)
    return Image.fromarray(dst, 'RGBA')

def masked(im, mask):
    out = im.copy()
    out.putalpha(ImageChops.multiply(im.getchannel('A'), mask))
    return out

def save(kind, index, im):
    im.save(OUT / f'{kind}_{index}.png', optimize=True)

# Keep facial features anchored. Only the skin and jaw contour below the eyes
# changes shape; side hair, ears and the robe must not be warped into the face.
face_poly=[
 (459,274),(486,269),(512,268),(538,269),(565,274),
 (585,293),(599,326),(605,363),(604,408),(596,447),
 (579,481),(558,510),(535,538),(512,549),(489,538),
 (466,510),(445,481),(428,447),(420,408),(419,363),
 (425,326),(439,293)]
face_mask=region([face_poly],1.5)
neck_mask=region([[
 # Follow the painted neck edge from behind the jaw. The old mask began at
 # y=501 and left an uncovered wedge on each side of the lower cheeks.
 (445,466),(579,466),(588,492),(594,536),(590,616),(568,705),
 (512,751),(456,705),(437,616),(435,536),(438,492)]],1.5)
# The feature-free repaint is noticeably lighter along the neck edges. The
# selected portrait has the intended side shadow and needs no feature erasure
# in this region.
neck=masked(REF,neck_mask)
skin=np.asarray(masked(BLANK,face_mask)).copy()
grid_y,grid_x=np.indices((SIZE[1],SIZE[0]),dtype=np.float32)
face_stops=[
 [(270,1),(415,1),(465,1),(515,1),(550,1)],
 [(270,1),(415,1),(465,1.07),(515,1.25),(550,1.24)],
 [(270,1),(415,1),(465,.94),(515,.79),(550,.76)],
 [(270,1),(415,1),(465,1.10),(515,1.04),(550,.79)],
 [(270,1),(415,1),(465,1.045),(515,1.15),(550,1.15)],
]
for i,stops in enumerate(face_stops):
    scale=np.interp(np.arange(SIZE[1]),[y for y,s in stops],
                    [s for y,s in stops],left=1,right=1).astype(np.float32)[:,None]
    sample_x=np.broadcast_to(512+(grid_x-512)/scale,grid_x.shape).astype(np.float32)
    warped=cv2.remap(skin,sample_x,grid_y,cv2.INTER_LINEAR,
                     borderMode=cv2.BORDER_CONSTANT,borderValue=0)
    face=neck.copy()
    face.alpha_composite(Image.fromarray(warped,'RGBA'))
    if i == 4:
        # Use the repainted lower jaw to flatten the chin. Keep the generated
        # mouth and other features outside this face layer.
        square=drawing('face_square_rework_full')
        square_jaw=region([[(437,498),(459,497),(482,500),(512,504),
                            (542,500),(565,497),(587,498),(582,525),
                            (555,546),(539,552),(485,552),(469,546),
                            (442,525)]],4)
        face.alpha_composite(masked(square,square_jaw))
    save('face',i,face)

# Feature windows are deliberately narrow. Their soft difference masks remove
# surrounding skin so independently selected parts never leave a rectangular seam.
windows={
 'brow': ([(410,328,499,365),(525,328,614,365)], 2, 11),
 'eye': ([], 0, 18),
 'nose': ([(473,385,551,474)], 5, 5),
 'mouth': ([(459,461,565,506)], 4, 5),
 'beard': ([(398,434,626,565)], 1, 8),
}
for kind,(boxes,feather,threshold) in windows.items():
    for i in range(5):
        if kind=='beard' and i==0:
            save(kind,i,Image.new('RGBA',SIZE)); continue
        im=(drawing('nose_shaded_rework_full') if kind=='nose' else REF) if i==0 else drawing(f'{kind}_full_{i}')
        if kind=='eye':
            layer=extract_eye(im,(369,377,369,373,378)[i])
        else:
            roi=in_rects(boxes,feather)
            diff=ImageChops.difference(im.convert('RGB'),BLANK.convert('RGB')).convert('L')
            # Make dark strokes opaque; taper skin-tone differences away continuously.
            diff=diff.point(lambda v,t=threshold: max(0,min(255,(v-t)*6)))
            if kind in ('nose','mouth','beard'):
                diff=diff.filter(ImageFilter.GaussianBlur(1.2))
            alpha=ImageChops.multiply(roi,diff)
            if kind=='beard':
                # Preserve lips as an independently chosen layer.
                no_lips=ImageOps.invert(in_rects([(458,464,565,500)],2))
                alpha=ImageChops.multiply(alpha,no_lips)
            layer=masked(im,alpha)
        if kind=='brow':
            # Preserve each painted brow, then flatten the repeated raised
            # outer-tail silhouette shared by all source portraits.
            layer=ImageChops.offset(layer,0,-3)
            layer=flatten_brows(layer,i)
            clip=Image.new('L',SIZE)
            cd=ImageDraw.Draw(clip)
            cd.rectangle((0,0,1023,354),fill=255)
            for yy in range(355,360):
                cd.line((0,yy,1023,yy),fill=round(255*(360-yy)/6))
            layer=masked(layer,clip)
        elif kind=='eye':
            if i==2:
                # The rounded source eye introduced an unrelated drawing style.
                # Open the reference eyes slightly for a consistent fifth look.
                original=Image.open(OUT/'eye_0.png').convert('RGBA')
                scale=1.08
                layer=original.transform(SIZE,Image.Transform.AFFINE,
                    (1,0,0,0,1/scale,380-380/scale),Image.Resampling.BICUBIC)
            # Align the actual lash lines after discarding the old eye-socket
            # crease, rather than retaining that crease as a false upper lid.
            layer=ImageChops.offset(layer,0, 1 if i==2 else -2 if i==0 else -7 if i in (1,4) else -5)
        if kind=='nose' and i>0:
            # Share the repainted sidewall shadows while retaining each
            # option's own bridge and tip silhouette.
            shared=Image.open(OUT/'nose_0.png').convert('RGBA')
            shared=masked(shared,in_rects([(479,397,505,457),(519,397,545,457)],3))
            shared.alpha_composite(layer)
            layer=shared
        save(kind,i,layer)

def register(im, xscale, maps):
    # Piecewise Y map handles the head-to-torso ratio drift in generated cutouts.
    src=im.load(); dst=Image.new('RGBA',SIZE); out=dst.load()
    for y in range(SIZE[1]):
        for (a,b),(c,d) in zip(maps,maps[1:]):
            if b<=y<=d:
                sy=round(a+(c-a)*(y-b)/(d-b)); break
        else:
            if y<maps[0][1]: sy=-1
            else: sy=SIZE[1]
        if not 0<=sy<SIZE[1]: continue
        for x in range(SIZE[0]):
            sx=round(512+(x-512)/xscale)
            if 0<=sx<SIZE[0]: out[x,y]=src[sx,sy]
    return dst

# Five complete hairstyle options. The three exported layers share one ID;
# the game never offers independent front/back/ornament selectors.
hair_maps=[
 (0.82,[(30,35),(445,279),(1515,1280)]),
 (0.86,[(57,116),(425,280),(1515,1280)]),
 (0.84,[(28,55),(460,279),(1518,1280)]),
 (0.86,[(30,110),(420,280),(1525,1280)]),
 (0.9, [(30,90),(435,280),(1536,1280)]),
]
hair_full_names=[SRC/f'hair_clean_full_{i}.png' for i in range(5)]
face_hole=region([[
 (464,315),(486,302),(512,300),(538,302),(560,315),
 (585,344),(601,379),(600,421),(588,467),(565,510),
 (540,540),(512,553),(484,540),(459,510),(436,467),
 (424,421),(423,379),(439,344)]],3)
hole=np.asarray(face_hole,dtype=np.float32)/255
hair_y=np.arange(SIZE[1],dtype=np.float32)[:,None]
fade=smoothstep((hair_y-420)/160)
lower_fade=smoothstep((hair_y-550)/110)
ribbon_polys={
 0:[[(360,170),(411,189),(349,337),(286,512),(110,621),(107,561),(296,355)]],
 1:[], 2:[],
 3:[[(367,392),(415,405),(340,555),(114,710),(116,650)]],
 4:[[(367,286),(414,306),(357,490),(181,663),(178,598)]],
}
long_hair_polys={
 0:[[(290,555),(445,555),(435,770),(414,930),(410,1150),(320,1150),(315,825)],
    [(600,555),(735,555),(795,780),(810,1020),(725,1060),(655,900)]],
 1:[[(290,555),(440,555),(430,810),(415,990),(405,1160),(320,1160),(315,820)],
    [(600,555),(730,555),(790,800),(795,1110),(670,1120),(645,820)]],
 2:[[(275,555),(440,555),(425,810),(410,1020),(390,1160),(305,1160),(300,820)],
    [(600,555),(745,555),(800,820),(805,1090),(690,1090),(650,800)]],
 3:[[(290,555),(440,555),(425,800),(410,1020),(390,1160),(310,1160),(305,810)],
    [(600,555),(745,555),(795,800),(805,1090),(680,1090),(645,800)]],
 4:[[(270,530),(445,530),(430,835),(410,1100),(390,1300),(275,1300),(285,855)]],
}
for i,(xs,maps) in enumerate(hair_maps):
    # The full portrait has the sharp matching strands. The old isolated-hair
    # painting is only a coarse position guide, never a second visible layer.
    full=Image.open(hair_full_names[i]).convert('RGBA')
    rgba=np.asarray(full).copy()
    rgb=rgba[:,:,:3].astype(np.float32)
    red,green,blue=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
    luminance=rgb @ np.array([.299,.587,.114],dtype=np.float32)
    dark=np.clip((175-luminance)/40,0,1)
    neutral=np.clip((17-(red-green))/12,0,1)
    cool=np.clip((12-(blue-red))/16,0,1)
    hair_color=smoothstep((np.clip(dark*neutral*cool*1.8,0,1)-.18)/.6)
    cut=register(drawing(f'hair_cut_{i}'),xs,maps)
    guide=np.asarray(cut.getchannel('A'))>64
    corridor=cv2.dilate(guide.astype(np.uint8),np.ones((61,61),np.uint8)).astype(np.float32)
    corridor=cv2.GaussianBlur(corridor,(0,0),1.5)
    alpha_mask=(1-hole)*((1-fade)+fade*hair_color*corridor)
    ribbon_mask=region(ribbon_polys[i],2) if ribbon_polys[i] else Image.new('L',SIZE)
    alpha_mask=np.maximum(alpha_mask,np.asarray(ribbon_mask,dtype=np.float32)/255)
    if i==4:
        tie=region([[(303,1007),(355,1007),(368,1100),(359,1265),
                     (298,1300),(292,1110)]],2)
        alpha_mask=np.maximum(alpha_mask,np.asarray(tie,dtype=np.float32)/255*dark)
    long_mask=region(long_hair_polys[i],2)
    long_arr=np.asarray(long_mask,dtype=np.float32)/255
    alpha_mask*=((1-lower_fade)+lower_fade*long_arr)
    opacity=(rgba[:,:,3].astype(np.float32)/255)*alpha_mask
    # The full hairstyle paintings were made over a green robe. Keep the
    # neutral charcoal strands, but remove green robe paint that leaked into
    # the lower hair matte and showed as green flecks on other outfits.
    robe_green=smoothstep((green-red-5)/6)*smoothstep((green-blue)/4)
    opacity*=1-robe_green*smoothstep((hair_y-520)/50)
    rgba[:,:,3]=np.clip(smoothstep((opacity-.22)/.64)*255,0,255).astype(np.uint8)
    hair=Image.fromarray(rgba,'RGBA')
    # Front locks end at their own lengths. All shoulder-length tails belong
    # behind the robe, including the long braid; never use the robe-crossing
    # corridor as the foreground mask.
    front_ends=(535,510,525,515,495)
    front_mask=Image.new('L',SIZE)
    front_draw=ImageDraw.Draw(front_mask)
    front_draw.rectangle((0,0,1023,front_ends[i]),fill=255)
    cheek_locks={
        1:[[(383,465),(438,465),(428,565),(419,658),(402,692),(383,670)],
           [(595,465),(645,465),(654,565),(648,651),(628,684),(607,642)]],
        2:[[(595,470),(652,470),(672,585),(665,662),(644,694),(618,655)]],
        3:[[(379,465),(431,465),(423,565),(412,638),(394,658),(380,620)]],
    }
    for lock in cheek_locks.get(i,[]):
        front_draw.polygon(lock,fill=255)
    ornament_regions={
        0:[(405,83,618,181)],
        2:[(418,98,617,174)],
        3:[(347,430,431,548)],
        4:[(401,105,625,173)],
    }
    ornament=np.asarray(in_rects(ornament_regions.get(i,[]),2),dtype=np.float32)/255
    # Within each ornament region, retain blue cloth and the pale band only.
    # Dark hair stays with the front/back hairstyle layers.
    ornament_color=np.maximum(smoothstep((blue-red-8)/24),smoothstep((luminance-175)/35))
    ornament_alpha=np.clip(ornament*ornament_color,0,1)
    ornament_mask=Image.fromarray(np.uint8(ornament_alpha*255),'L')
    save('hair_accessory',i,masked(hair,ornament_mask))
    hair_without_ornament=masked(hair,ImageOps.invert(ornament_mask))
    save('hair_back',i,masked(hair_without_ornament,ImageOps.invert(front_mask)))
    save('hair_front',i,masked(hair_without_ornament,front_mask))

cloth_maps={
 0:(1.0,[(0,0),(540,548),(1240,1260),(1536,1536)]),
 2:(1.02,[(365,505),(600,660),(1120,1260),(1536,1536)]),
 3:(1.02,[(420,505),(600,660),(1110,1260),(1536,1536)]),
 4:(1.02,[(255,505),(450,660),(980,1260),(1536,1536)]),
}
for i,(xs,maps) in cloth_maps.items():
    save('clothes',i,register(drawing(f'clothes_cut_{i}'),xs,maps))
# The older dye pass was sparse: compositing it over white left white cracks
# through the shoulders and sleeves. Use the repainted complete robe, while
# keeping the registered garment-only matte from the clean cutout.
blue=drawing('clothes_blue_gray_rework_full')
white=Image.open(OUT/'clothes_0.png').convert('RGBA')
blue.putalpha(white.getchannel('A'))
save('clothes',1,blue)

def compose(parts):
    out=Image.new('RGBA',SIZE)
    for kind,index in [('hair_back',parts['hair']),('face',parts['face']),
                       ('clothes',parts['clothes']),('eye',parts['eye']),
                       ('brow',parts['brow']),('nose',parts['nose']),
                       ('mouth',parts['mouth']),('beard',parts['beard']),
                       ('hair_front',parts['hair']),('hair_accessory',parts['hair'])]:
        out.alpha_composite(Image.open(OUT/f'{kind}_{index}.png').convert('RGBA'))
    return out

base=dict(face=0,brow=0,eye=0,nose=0,mouth=0,beard=0,hair=0,clothes=0)
labels=['基准','脸型1','脸型2','脸型3','脸型4','眉1','眉2','眉3','眉4',
        '眼1','眼2','眼3','眼4','鼻1','鼻2','鼻3','鼻4','口1','口2','口3','口4',
        '胡1','胡2','胡3','胡4','发1','发2','发3','发4','衣1','衣2','衣3','衣4']
choices=[None]+[(kind,i) for kind in ('face','brow','eye','nose','mouth','beard','hair','clothes') for i in range(1,5)]
tw,th=256,384
sheet=Image.new('RGB',(tw*7,(th+26)*5),(222,219,210))
d=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
for j,choice in enumerate(choices):
    parts=base.copy()
    if choice: parts[choice[0]]=choice[1]
    pic=compose(parts).resize((tw,th),Image.Resampling.LANCZOS)
    x=j%7*tw; y=j//7*(th+26)
    sheet.paste(pic,(x,y+26),pic)
    d.text((x+4,y+2),labels[j],font=font,fill=(30,35,32))
sheet.save(PREVIEW/'appearance_options.png')
for name,parts in [('baseline',base),('mixed_a',dict(face=2,brow=3,eye=4,nose=2,mouth=3,beard=2,hair=4,clothes=3)),('mixed_b',dict(face=4,brow=1,eye=1,nose=4,mouth=2,beard=4,hair=2,clothes=4))]:
    compose(parts).save(PREVIEW/f'{name}.png')
compose(dict(base,clothes=1)).save(PREVIEW/'blue_gray_robe.png')
compose(dict(base,clothes=4)).save(PREVIEW/'green_robe.png')
for hair_index in range(5):
    compose(dict(base,hair=hair_index,clothes=4)).save(PREVIEW/f'green_robe_hair_{hair_index}.png')

for kind,crop,tile_size in [
    ('face',(390,270,634,580),(400,508)),
    ('hair',(230,25,794,1150),(360,718)),
]:
    detail=Image.new('RGB',(tile_size[0]*5,tile_size[1]+32),(222,219,210))
    detail_draw=ImageDraw.Draw(detail)
    for index in range(5):
        parts=base.copy();parts[kind]=index
        portrait=compose(parts).crop(crop).resize(tile_size,Image.Resampling.LANCZOS)
        x=index*tile_size[0]
        detail.paste(portrait,(x,32),portrait)
        detail_draw.text((x+8,4),f'{"脸型" if kind=="face" else "发型"} {index}',
                         font=font,fill=(30,35,32))
    detail.save(PREVIEW/f'{kind}_variants.png')
print(f'Wrote {len(list(OUT.glob("*.png")))} layers to {OUT}')
