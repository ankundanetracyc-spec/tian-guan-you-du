"""Position the newly generated connected beard as a single unchanged layer."""
import argparse
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,write_json,freeze,image
from diagnose_beards import measure
from center_nose_mouth import shift_horizontal,current_comp

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--native',default='native/beard06.png');args=parser.parse_args()
    baseline=freeze();srcpath=PACK/args.native;src=Image.open(srcpath).convert('RGBA')
    original_upper=measure(np.asarray(src),[462,449,576,480]);dx=513-original_upper['endpoint_midpoint_x']
    candidate=shift_horizontal(src,dx);dest=PACK/'male/face_accessory/06.png';dest.parent.mkdir(parents=True,exist_ok=True);candidate.save(dest)
    a=np.asarray(candidate);upper=measure(a,[458,449,572,480]);lower=measure(a,[421,490,606,585])
    face=np.asarray(image('face_shape'))[:,:,3];yy,xx=np.indices(face.shape)
    outside=(a[:,:,3]>=64)&(face<128)&(yy>=435)&(yy<=545)
    report={'schema':'male-new-connected-beard-registration/v1','native':args.native,'native_sha256':SHA(srcpath),
            'candidate':dest.relative_to(PACK).as_posix(),'sha256':SHA(dest),'axis_x':513,'source_upper_center_x':original_upper['endpoint_midpoint_x'],
            'translation':[dx,0],'matrix':[[1,0,dx],[0,1,0]],'scale':1,'rotation_deg':0,'one_pass_premultiplied_alpha_bilinear_sampling':True,
            'local_warps':False,'RGB_repainting':False,'source_RGB_paste':False,'upper_moustache_after':upper,'lower_chin_jaw_after':lower,
            'outside_fixedface_alpha128_y435_545_alpha64':int(outside.sum())}
    write_json(PACK/'source/beard06_registration.json',report)
    nose=Image.open(PACK/'male/nose/01.png').convert('RGBA');mouth=Image.open(PACK/'male/mouth/01.png').convert('RGBA')
    panel=Image.new('RGB',(860,330),'white')
    for col,beard in enumerate([image('face_accessory',6),candidate]):
        out=current_comp(1,nose,mouth);out.alpha_composite(beard);v=out.crop((405,435,620,585)).resize((430,300),Image.Resampling.NEAREST).convert('RGB')
        ImageDraw.Draw(v).line(((513-405)*2,0,(513-405)*2,299),fill=(0,170,130),width=1)
        panel.paste(v,(col*430,28));ImageDraw.Draw(panel).text((col*430+5,6),'old06' if col==0 else 'generated06, single translation',fill='black')
    panel.save(QA/'beard06_old_left_candidate_right_2x.png')
    for old in baseline['files']:assert SHA(ROOT/old['file'])==old['sha256']
    print({'dx':dx,'upper_x':upper['endpoint_midpoint_x'],'chin_x':lower['lower15pct_center'][0],'outside_fixedface':int(outside.sum()),'baseline_preserved':True})

if __name__=='__main__':main()
