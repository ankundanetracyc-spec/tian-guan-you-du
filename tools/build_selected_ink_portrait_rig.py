"""Measure and freeze the user's selected portrait; no artwork is repainted."""
from pathlib import Path
import hashlib
import json
import shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont

PROJECT=Path(__file__).resolve().parents[1]
ROOT=PROJECT/'mods/appearance/selected_ink_portrait_rig_20261003_v1'
SOURCE=PROJECT/'mods/appearance/ink_portrait_pair_20261003_v2'
ATTACHMENT=Path('C:/Users/34784/AppData/Local/Temp/codex-clipboard-2631caed-266f-4ea0-afcb-0ac768e68ce5.png')
W,H=1024,1536
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
LABELS={
 'head_axis':'面部中轴','skull_crown_hidden':'头骨顶（原光头图）',
 'hair_root_center':'中央发际','hairline_left':'左发际','hairline_right':'右发际',
 'temple_left':'左鬓发接口','temple_right':'右鬓发接口',
 'eye_left':'左眼位','eye_right':'右眼位',
 'eye_outer_left':'左外眼角','eye_inner_left':'左内眼角',
 'eye_inner_right':'右内眼角','eye_outer_right':'右外眼角',
 'brow_left':'左眉定位','brow_right':'右眉定位','brow_inner_left':'左眉头','brow_inner_right':'右眉头',
 'nose_bridge':'鼻梁定位','nose_tip':'鼻尖定位','nostril_left':'左鼻孔','nostril_right':'右鼻孔',
 'mouth_center':'唇心','mouth_left':'左唇角','mouth_right':'右唇角',
 'chin':'下巴','ear_root_left':'左耳根','ear_root_right':'右耳根',
 'neck_upper_left':'左颈侧','neck_upper_right':'右颈侧',
 'collar_join_left':'左颈领接合','collar_join_right':'右颈领接合','collar_v':'交领开口底点',
 'cheek_left':'左面饰区','cheek_right':'右面饰区',
 'shoulder_left':'左肩参考','shoulder_right':'右肩参考','waist_center':'腰封参考',
 'hair_crown':'当前发顶参考','front_lock_tip_left':'当前左发梢参考','front_lock_tip_right':'当前右发梢参考',
}
# Pixel coordinates were inspected on the selected full-resolution painting.
# Eye locations mean iris horizontal axis / eye-opening vertical center;
# they are not opaque bounding-box centers.
MEASURED={
 'male':{
  'head_axis':[517,411], 'skull_crown_hidden':[516,173],
  'hair_root_center':[517,271], 'hairline_left':[447,310], 'hairline_right':[590,310],
  'temple_left':[425,348], 'temple_right':[610,348],
  'eye_left':[464,375], 'eye_right':[569,374],
  'eye_outer_left':[426,369], 'eye_inner_left':[491,380],
  'eye_inner_right':[540,380], 'eye_outer_right':[610,368],
  'brow_left':[456,346], 'brow_right':[579,345], 'brow_inner_left':[490,353], 'brow_inner_right':[541,353],
  'nose_bridge':[517,409], 'nose_tip':[517,444], 'nostril_left':[505,448], 'nostril_right':[528,449],
  'mouth_center':[517,481], 'mouth_left':[481,480], 'mouth_right':[551,480],
  'chin':[517,541], 'ear_root_left':[417,379], 'ear_root_right':[616,379],
  'neck_upper_left':[438,520], 'neck_upper_right':[597,520],
  'collar_join_left':[441,550], 'collar_join_right':[594,550], 'collar_v':[531,694],
  'cheek_left':[449,430], 'cheek_right':[584,430],
  'shoulder_left':[74,706], 'shoulder_right':[944,706], 'waist_center':[512,1250],
  'hair_crown':[517,151], 'front_lock_tip_left':[444,979], 'front_lock_tip_right':[650,978],
 },
 'female':{
  'head_axis':[519,414], 'skull_crown_hidden':[517,169],
  'hair_root_center':[518,257], 'hairline_left':[442,310], 'hairline_right':[596,310],
  'temple_left':[422,350], 'temple_right':[621,350],
  'eye_left':[464,383], 'eye_right':[574,382],
  'eye_outer_left':[425,375], 'eye_inner_left':[491,389],
  'eye_inner_right':[546,389], 'eye_outer_right':[615,375],
  'brow_left':[457,351], 'brow_right':[582,350], 'brow_inner_left':[492,358], 'brow_inner_right':[543,357],
  'nose_bridge':[519,410], 'nose_tip':[519,447], 'nostril_left':[509,454], 'nostril_right':[528,454],
  'mouth_center':[519,485], 'mouth_left':[486,484], 'mouth_right':[550,484],
  'chin':[519,541], 'ear_root_left':[412,380], 'ear_root_right':[626,380],
  'neck_upper_left':[455,520], 'neck_upper_right':[582,520],
  'collar_join_left':[450,552], 'collar_join_right':[584,552], 'collar_v':[520,690],
  'cheek_left':[450,434], 'cheek_right':[588,434],
  'shoulder_left':[124,711], 'shoulder_right':[906,711], 'waist_center':[512,1250],
  'hair_crown':[518,142], 'front_lock_tip_left':[419,941], 'front_lock_tip_right':[653,950],
 },
}
DESIGN_POINTS={'eye_outer_left','eye_inner_left','eye_inner_right','eye_outer_right',
 'nose_tip','nostril_left','nostril_right','mouth_left','mouth_right','chin',
 'shoulder_left','shoulder_right','waist_center','hair_crown',
 'front_lock_tip_left','front_lock_tip_right'}
HIDDEN_POINTS={'skull_crown_hidden','ear_root_left','ear_root_right','neck_upper_left','neck_upper_right'}
SLOT_ANCHORS={
 'hair_front':['hair_root_center','hairline_left','hairline_right','temple_left','temple_right'],
 'hair_back':['hair_root_center','temple_left','temple_right','neck_upper_left','neck_upper_right'],
 'face_shape':['head_axis','ear_root_left','ear_root_right','neck_upper_left','neck_upper_right'],
 'eyebrows':['brow_left','brow_right','brow_frame_top'],
 'eyes':['eye_left','eye_right','eye_frame_top'],
 'nose':['nose_bridge','nose_frame_bottom','nose_frame_side'],
 'mouth':['mouth_center','mouth_frame_left','mouth_frame_top'],
 'clothing':['collar_join_left','collar_join_right','collar_frame_top'],
 'face_accessory':[],
}
PIVOTS={
 'hair_front':'hair_root_center','hair_back':'hair_root_center','face_shape':'head_axis',
 'eyebrows':'brow_pair','eyes':'eye_pair','nose':'nose_bridge','mouth':'mouth_center',
 'clothing':'collar_pair','face_accessory':'mouth_center',
}

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def font(size):return ImageFont.truetype(str(PROJECT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),size)
def onto(im):
    bg=Image.new('RGBA',im.size,'#e8e3dc');bg.alpha_composite(im.convert('RGBA'));return bg.convert('RGB')
def midpoint(a,b):return [(a[0]+b[0])/2,(a[1]+b[1])/2]

def guide(sex,master,anchors):
    overlay=Image.new('RGBA',(W,H));d=ImageDraw.Draw(overlay)
    colors={'fixed':(18,107,117,245),'design':(185,76,43,245),'hidden':(105,82,138,245)}
    for a,b in [('eye_left','eye_right'),('mouth_left','mouth_right'),('collar_join_left','collar_v'),('collar_join_right','collar_v')]:
        d.line([anchors[a]['px'],anchors[b]['px']],fill=(18,107,117,145),width=2)
    axis=anchors['head_axis']['px'][0]
    d.line((axis,180,axis,710),fill=(18,107,117,120),width=1)
    display=['hair_root_center','hairline_left','hairline_right','eye_left','eye_right',
       'brow_left','brow_right','nose_bridge','nose_tip','mouth_center',
       'ear_root_left','ear_root_right','neck_upper_left','neck_upper_right',
       'collar_join_left','collar_join_right','collar_v','chin']
    for num,key in enumerate(display,1):
        x,y=anchors[key]['px'];color=colors[anchors[key]['kind']]
        d.ellipse((x-4,y-4,x+4,y+4),fill=color,outline=(255,255,255,240),width=1)
        d.text((x+5,y-14),str(num),font=font(13),fill=color,stroke_width=1,stroke_fill=(246,240,231,230))
    overlay.save(ROOT/'rig'/f'{sex}_overlay.png')
    full=onto(master).convert('RGBA');full.alpha_composite(overlay)
    full.convert('RGB').save(ROOT/'preview'/f'{sex}_full_anchors.png')
    board=Image.new('RGB',(1230,1040),'#e8e3dc');draw=ImageDraw.Draw(board)
    draw.text((25,15),('男修' if sex=='male' else '女修')+' · 选定母版的拼接锚点',font=font(27),fill='#243c3e')
    # Full crop spans all head and neck controls, preserving actual proportions.
    crop=full.crop((350,160,690,735)).resize((578,978),Image.Resampling.LANCZOS)
    board.paste(crop.convert('RGB'),(15,55))
    for row,key in enumerate(display):
        a=anchors[key];x,y=a['px'];color=colors[a['kind']]
        draw.text((620,80+row*43),f'{row+1:02d}  {LABELS[key]}  ({x}, {y})',font=font(20),fill=color[:3])
    draw.text((620,900),'青色：拼接定位  /  紫色：遮挡区补充依据',font=font(18),fill='#496162')
    draw.text((620,932),'橙色：当前款式形状，换款可变化',font=font(18),fill='#9d4d35')
    draw.text((620,974),'原点在单人画布左上；不按透明包围盒重置中心',font=font(17),fill='#496162')
    board.save(ROOT/'preview'/f'{sex}_anchor_guide.png')

def main():
    for folder in ['reference','rig','preview']:(ROOT/folder).mkdir(parents=True,exist_ok=True)
    selected=Image.open(ATTACHMENT).convert('RGB')
    assert selected.size==(2048,1536)
    shutil.copyfile(ATTACHMENT,ROOT/'reference/selected_pair_user.png')
    rig={
      'id':'our_selected_ink_portrait_rig_20261003_v1','canvas':[W,H],
      'coordinates':'Each character has its own top-left origin; x right, y down; pixels. Female pair-panel x must subtract 1024.',
      'portrait_origin_px':[0,0], 'canvas_pivot_px':[512,768],
      'placement':{'offset':[0,0],'scale':1,'rotation_degrees':0},
      'draw_order_back_to_front':ORDER,
      'source_style_approved_by_user':True,
      'rig_review_status':'Measured on the selected artwork; semantic PNG purity is a separate required gate',
      'source_pair':'../reference/selected_pair_user.png',
      'source_pair_sha256':sha(ROOT/'reference/selected_pair_user.png'),
      'artwork_source':'User-selected male/female portrait; no Guigu pixels, head shape, palette, dimensions, offsets or depth values copied',
      'guigui_methods_adapted':['one portrait coordinate frame','stable semantic placement','paired eyes and paired brows','continuous alpha','explicit layer ordering'],
      'registration':{'minimum_controls':3,'maximum_residual_px':2,'transform':'one uniform similarity transform; no reflection or per-feature warping','do_not_use_alpha_bbox_as_origin':True},
      'slot_contract':{
       'hair_front':'Visible crown, hairline, temple strands and all hair over the body; no skin or robe pixels',
       'hair_back':'Complete continuous rear underpainting including hidden regions; no face hole or shoulder fade',
       'face_shape':'Featureless face, original ears and full neck; no hair, features or clothing',
       'eyebrows':'Paired original-style painted eyebrows, continuous alpha',
       'eyes':'Paired original-style eye organs/lashes; no opaque surrounding skin',
       'nose':'Original-style nose pigment and local soft wash; no opaque skin cutout',
       'mouth':'Original-style lips and local soft wash; no opaque skin cutout',
       'clothing':'Whole robe, clean neckline and no hair',
       'face_accessory':'Male beard only; female blush/makeup only; empty defaults valid',
      },
      'families':{},
    }
    checks={}
    overview=Image.new('RGB',(2048,H),'#e8e3dc')
    for col,sex in enumerate(['male','female']):
        source=SOURCE/'preview'/f'{sex}_recomposed.png'
        master=Image.open(source).convert('RGBA')
        frozen=ROOT/'reference'/f'{sex}_selected_master.png'
        shutil.copyfile(source,frozen)
        target=np.asarray(selected)[:,col*W:(col+1)*W]
        error=np.abs(np.asarray(onto(master)).astype(int)-target.astype(int))
        assert error.max()==0, 'The selected reference was not recovered exactly'
        # Existing PNGs establish source identity, not qualified modular exports.
        recomposed=Image.new('RGBA',(W,H))
        for part in ORDER:recomposed.alpha_composite(Image.open(SOURCE/sex/f'{part}.png').convert('RGBA'))
        assert np.array_equal(np.asarray(recomposed),np.asarray(master))
        anchors={}
        for key,xy in MEASURED[sex].items():
            anchors[key]={'label':LABELS[key],'px':xy,'uv':[xy[0]/W,xy[1]/H],
              'kind':'hidden' if key in HIDDEN_POINTS else 'design' if key in DESIGN_POINTS else 'fixed',
              'source':'Own original bald reference, for the region hidden by hair; anatomical guide only' if key in HIDDEN_POINTS else 'Selected portrait measured at original resolution',
              'lock_shape_on_new_variant':False}
        points={k:v['px'] for k,v in anchors.items()}
        points['brow_pair']=midpoint(points['brow_left'],points['brow_right'])
        points['eye_pair']=midpoint(points['eye_left'],points['eye_right'])
        points['collar_pair']=midpoint(points['collar_join_left'],points['collar_join_right'])
        # Import scaffold follows this portrait's dimensions. Its invisible
        # controls calibrate the PNG's frame without locking painted corners,
        # lip widths, nostril widths or garment V shapes on future variants.
        eye_span=points['eye_right'][0]-points['eye_left'][0]
        brow_span=points['brow_right'][0]-points['brow_left'][0]
        lip_span=points['mouth_right'][0]-points['mouth_left'][0]
        frame_points={
          'eye_frame_top':[points['eye_pair'][0],points['eye_pair'][1]-eye_span/4],
          'brow_frame_top':[points['brow_pair'][0],points['brow_pair'][1]-brow_span/4],
          'nose_frame_bottom':midpoint(points['nostril_left'],points['nostril_right']),
          'nose_frame_side':[points['nose_bridge'][0]+eye_span/8,points['nose_bridge'][1]],
          'mouth_frame_left':[points['mouth_center'][0]-lip_span/2,points['mouth_center'][1]],
          'mouth_frame_top':[points['mouth_center'][0],points['mouth_center'][1]-lip_span/4],
          'collar_frame_top':[points['collar_pair'][0],midpoint(points['neck_upper_left'],points['neck_upper_right'])[1]],
        }
        for key,xy in frame_points.items():
            points[key]=xy
            anchors[key]={'label':key,'px':xy,'uv':[xy[0]/W,xy[1]/H],
              'kind':'scaffold','source':'Import frame derived from this portrait, not a visible pigment landmark',
              'lock_shape_on_new_variant':False}
        slots={}
        for slot,controls in SLOT_ANCHORS.items():
            if slot=='face_accessory':controls=['mouth_center','mouth_frame_left','mouth_frame_top'] if sex=='male' else ['cheek_left','cheek_right','nose_bridge']
            pivot=points[PIVOTS[slot]] if slot!='face_accessory' or sex=='male' else midpoint(points['cheek_left'],points['cheek_right'])
            slots[slot]={
              'pivot_px':pivot,'pivot_uv':[pivot[0]/W,pivot[1]/H],
              'translation_from_canvas_center_px':[pivot[0]-512,pivot[1]-768],
              'recommended_registration_controls':controls,
              'export_canvas':[W,H],'export_offset_px':[0,0],
              'controls_are_registration_scaffold':'The measured guide frames import calibration; guide shape points do not force every variant to have the same silhouette',
            }
        rig['families'][sex]={
          'compatibility_family':'our_selected_ink_'+sex+'_20261003_v1',
          'master':'../reference/'+sex+'_selected_master.png','master_sha256':sha(frozen),
          'head_axis_x':points['head_axis'][0], 'body_axis_x':512,
          'anchors':anchors,'slots':slots,
          'unseen_geometry_source':'../reference/'+sex+'_bald_support.png',
          'unseen_geometry_is_not_new_artwork':True,
        }
        shutil.copyfile(PROJECT/'mods/appearance/ink_portrait_pair_20261003_v1/reference'/f'{sex}_bald_user.png',
                        ROOT/'reference'/f'{sex}_bald_support.png')
        guide(sex,master,anchors)
        overview.paste(Image.open(ROOT/'preview'/f'{sex}_full_anchors.png'),(col*W,0))
        checks[sex]={'selected_image_max_rgb_error':int(error.max()),
          'source_layers_recover_selected_rgba':True,'landmark_count':len(MEASURED[sex]),'import_frame_controls':len(frame_points),
          'frozen_master_sha256':sha(frozen),
          'nine_part_semantic_cleanup':'Original source layers establish the selected appearance only; purity and corrected exports must be verified separately'}
    overview.save(ROOT/'preview/pair_anchor_overview.png')
    (ROOT/'rig/portrait_rig.json').write_text(json.dumps(rig,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'source_verification.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(checks,ensure_ascii=False))

if __name__=='__main__':main()
