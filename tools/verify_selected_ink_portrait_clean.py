"""Inspect the delivered PNGs and selected-reference pixels independently."""
from pathlib import Path
import hashlib
import json

import cv2
import numpy as np
from PIL import Image, ImageDraw

PROJECT=Path(__file__).resolve().parents[1]
PACK=PROJECT/'mods/appearance/selected_ink_portrait_clean_20261003_v1'
RIG=PROJECT/'mods/appearance/selected_ink_portrait_rig_20261003_v1'
ORDER=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front']
W,H=1024,1536


def array(path):return np.array(Image.open(path).convert('RGBA'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def core_mask(paths):
    im=Image.new('L',(W*4,H*4))
    for path in paths:ImageDraw.Draw(im).polygon([(round(x*4),round(y*4)) for x,y in path],fill=255)
    return np.array(im.resize((W,H),Image.Resampling.LANCZOS))


def composite(layers):
    im=Image.new('RGBA',(W,H))
    for key in ORDER:im.alpha_composite(Image.fromarray(layers[key]))
    return im


def main():
    geometry=json.loads((PACK/'authoring_geometry.json').read_text(encoding='utf-8'))
    rig=json.loads((RIG/'rig/portrait_rig.json').read_text(encoding='utf-8'))
    reports={}
    for sex in ['male','female']:
        paths={k:PACK/sex/f'{k}.png' for k in ORDER}
        layers={k:array(p) for k,p in paths.items()}
        assert all(a.shape==(H,W,4) for a in layers.values())
        assert all(not np.any(a[:,:,:3][a[:,:,3]==0]) for a in layers.values())
        assert not np.any(layers['face_accessory'])
        actual=np.array(composite(layers));saved=array(PACK/f'{sex}_portrait.png')
        assert np.array_equal(actual,saved)
        target=array(RIG/'reference'/f'{sex}_selected_master.png')
        eyes=core_mask(geometry['eye_core'][sex])
        eye=layers['eyes'];r,g,b=eye[:,:,:3].astype(int).transpose(2,0,1)
        opaque_skin=(eye[:,:,3]>240)&(eyes<12)&(r>130)&(r-g>12)&(g-b>5)
        assert not np.any(opaque_skin),'An opaque skin plate remains around the eyes'
        # Iris centers and lip center are real reference pixels, rather than
        # test-generated fixtures or a shared implementation prediction.
        anchors=rig['families'][sex]['anchors']
        sample_error={}
        for name in ['eye_left','eye_right','mouth_center','nose_tip']:
            x,y=anchors[name]['px'];sample_error[name]=int(np.abs(actual[y-2:y+3,x-2:x+3,:3].astype(int)-target[y-2:y+3,x-2:x+3,:3].astype(int)).max())
            assert sample_error[name]<=2,(sex,name,sample_error[name])
        # The complete rear painting has no carved face/neck opening.
        rear=layers['hair_back'][:,:,3]
        assert rear[300:570,502:534].min()>230
        count,_,stats,_=cv2.connectedComponentsWithStats((rear>80).astype(np.uint8),8)
        areas=stats[1:,4]
        connected_fraction=float(areas.max()/areas.sum())
        # Tiny isolated flyaway strokes are allowed. The actual head/neck
        # underpainting must be one connected mass, rather than two drapes.
        assert connected_fraction>.99,'Rear underpainting was fragmented'
        face=layers['face_shape'][:,:,3]
        assert all(face[y,x]>240 for x,y in [(517,200),(517,420),(517,580),(517,660),(517,695)])
        # Posterior drapes stay behind the body even from a frontal camera.
        # These fixed inspection patches are within the shoulder-back hair,
        # outside the two chest locks, and catch the previous wrong split.
        front=layers['hair_front'][:,:,3]
        assert front[200:265,450:585].max()>240
        for box in [(345,470,370,525),(680,500,700,540)]:
            x0,y0,x1,y1=box
            assert not np.any(front[y0:y1,x0:x1]),'Shoulder-back hair remains in front'
            assert np.count_nonzero(rear[y0:y1,x0:x1]>200)>100,'Posterior drape missing'
        assert np.count_nonzero(front[710:940,330:455]>20)>100
        assert np.count_nonzero(front[710:940,630:730]>20)>100
        assert not np.any(front[343:390,430:605]),'Brow/eye fragments remain in front hair'
        diffs={}
        for name,color in [('light','#e8e3dc'),('dark','#252423')]:
            a=Image.new('RGBA',(W,H),color);a.alpha_composite(Image.fromarray(actual))
            t=Image.new('RGBA',(W,H),color);t.alpha_composite(Image.fromarray(target))
            d=np.abs(np.array(a.convert('RGB')).astype(int)-np.array(t.convert('RGB')).astype(int))
            diffs[name]={'mean_channel_difference':float(d.mean()),'max_channel_difference':int(d.max()),'pixel_max_p99':float(np.percentile(d.max(2),99))}
        reports[sex]={'canvas_and_transparency_pass':True,'opaque_eye_skin_outside_opening':0,
            'reference_organ_sample_max_rgb_difference':sample_error,
            'continuous_rear_head_and_neck_pass':True,'rear_largest_component_fraction':connected_fraction,'front_crown_and_both_tresses_pass':True,
            'posterior_drapes_not_in_front_pass':True,'brow_eye_interior_free_of_front_hair_pass':True,
            'full_neck_underpainting_pass':True,'recomposition_matches_saved_png':True,
            'reference_comparison':diffs,
            'png_sha256':{k:sha(p) for k,p in paths.items()},
            'user_visual_approval':False,
            'limits':'Pixel checks support visual review; whole-painting max difference includes semantic edge repairs and hidden-underpainting effects. They do not claim every semantic pixel is classified correctly.'}
    (PACK/'asset_verification.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({s:{k:v for k,v in r.items() if k!='png_sha256'} for s,r in reports.items()},ensure_ascii=False,indent=2))


if __name__=='__main__':main()
