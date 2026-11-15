"""Alternative only: one similarity on frozen old06, retaining pigment/density."""
import math
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,write_json,freeze,image
from diagnose_beards import measure
from center_nose_mouth import current_comp
from register_features import sample

def main():
    baseline=freeze();source=PACK/'source/frozen/male/face_accessory/06.png'
    src=Image.open(source).convert('RGBA');arr=np.asarray(src)
    upper=measure(arr,[462,449,576,480]);cx=upper['endpoint_midpoint_x'];cy=upper['ink_centroid'][1]
    scale=.95;angle=1.0;down=2.0
    a=scale*math.cos(math.radians(angle));b=scale*math.sin(math.radians(angle))
    transform=[a,b,513-a*cx+b*cy,cy+down-b*cx-a*cy]
    candidate=sample(src,transform)
    dest=PACK/'male/face_accessory/06_geometry_alternative.png';candidate.save(dest)
    aa=np.asarray(candidate);upper_after=measure(aa,[458,449,572,491]);lower_after=measure(aa,[421,491,606,585])
    face=np.asarray(image('face_shape'))[:,:,3];yy,xx=np.indices(face.shape)
    outside=(aa[:,:,3]>=64)&(face<128)&(yy>=435)&(yy<=545)
    nose=Image.open(PACK/'male/nose/01.png').convert('RGBA');mouth=Image.open(PACK/'male/mouth/01.png').convert('RGBA')
    for background in ['light','dark']:
        panel=Image.new('RGB',(860,330),'white' if background=='light' else (38,43,43))
        small=Image.new('RGB',(430,178),'white' if background=='light' else (38,43,43))
        for col,beard in enumerate([src,candidate]):
            out=current_comp(1,nose,mouth)
            if background=='dark':
                opaque=Image.new('RGBA',out.size,(38,43,43,255))
                for layer in [image('face_shape'),nose,image('eyes'),image('eyebrows'),mouth]:opaque.alpha_composite(layer)
                out=opaque
            out.alpha_composite(beard)
            crop=out.crop((405,435,620,585)).convert('RGB')
            v=crop.resize((430,300),Image.Resampling.NEAREST)
            ImageDraw.Draw(v).line(((513-405)*2,0,(513-405)*2,299),fill=(0,170,130),width=1)
            panel.paste(v,(col*430,28));small.paste(crop,(col*215,26))
            ImageDraw.Draw(panel).text((col*430+5,6),'frozen original06' if col==0 else 'same original06: s.95, rotate1deg, whole-frame',fill='black' if background=='light' else 'white')
        panel.save(QA/f'beard06_geometry_alternative_{background}_2x.png');small.save(QA/f'beard06_geometry_alternative_{background}_1x.png')
    report={'schema':'male-frozen-connected-beard-geometry-alternative/v1','source':source.relative_to(PACK).as_posix(),'source_sha256':SHA(source),
            'candidate':dest.relative_to(PACK).as_posix(),'candidate_sha256':SHA(dest),'not_current06_production_candidate_yet':True,
            'source_upper_center':[cx,cy],'transform':{'a':transform[0],'b':transform[1],'tx':transform[2],'ty':transform[3],'scale':scale,'rotation_deg':angle},
            'upper_after':upper_after,'lower_after':lower_after,'outside_face_alpha128_y435_545_alpha64':int(outside.sum()),
            'one_pass_premultiplied_alpha_similarity':True,'alpha_density_adjustment':False,'RGB_repainting':False,'local_warps':False,
            'selection_basis':'Search s.90..1,rotation0..1.2deg,upper vertical shift0..4px with chin lower15%y540..544,upper ends<=477.5,chin x513..514. Chosen.95/1deg/+2 keeps original beard style and tail contact while reducing side spill. Never clips or erases any stroke.'}
    write_json(PACK/'source/beard06_geometry_alternative.json',report)
    for old in baseline['files']:assert SHA(ROOT/old['file'])==old['sha256']
    print({'s':scale,'rotation':angle,'upper_x':upper_after['endpoint_midpoint_x'],'chin':lower_after['lower15pct_center'],'outside':int(outside.sum()),'path':str(dest)})

if __name__=='__main__':main()
