"""QA only: authored frontal symmetry from the approved generated LEFT eye.

No independent painted right eye is claimed. Reflection is explicit provenance;
one common scale/translation preserves old pupil spacing, with no local warps.
"""
import numpy as np
from PIL import Image,ImageDraw
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,write_json,freeze,image,centroid
from register_features import sample
from render_candidate_qa import candidate

def reflect_left(a):
    out=np.zeros_like(a)
    out[:,:513]=a[:,:513]
    for x in range(514,1024):out[:,x]=out[:,1026-x]
    return out

def face(eye,dark=False):
    out=Image.new('RGBA',(1024,1536),(38,43,43,255) if dark else (231,225,214,255))
    for layer in [image('face_shape'),candidate('nose'),eye,candidate('eyebrows',4),candidate('mouth')]:out.alpha_composite(layer)
    return out

def main():
    baseline=freeze();frozen=PACK/'source/intermediate/eyes04_after_edge_before_pairing.png';srcpath=frozen if frozen.exists() else PACK/'male/eyes/04.png'
    assert SHA(srcpath)=='400303fd0da9bdd76834a11a82e9da20c8006ea681cc507e2dd2504a61baad83'
    source=Image.open(srcpath).convert('RGBA');a=np.asarray(source)
    lp=centroid(a,[450,355,473,376],100)
    target_distance=105.05914324783518;target_y=363.27045313240114
    current_pair_distance=2*(513-lp[0]);scale=target_distance/current_pair_distance
    transform=[scale,0,513*(1-scale),target_y-scale*lp[1]]
    # Reflection commutes with the uniform geometry around the fixedaxis513.
    # Resample the source-left once; reflect the final complete RGBA left half.
    # This avoids unequal rounding on two nominally symmetric sampling grids.
    left=np.array(a,copy=True);left[:,513:]=0
    placed=np.asarray(sample(Image.fromarray(left,'RGBA'),transform))
    paired=reflect_left(placed)
    final=Image.fromarray(paired,'RGBA');dest=QA/'eye04_paired_geometry_candidate.png';final.save(dest)
    pupils=[centroid(paired,r,100) for r in [[450,355,473,376],[554,355,577,376]]]
    # Compare matched visible domains, not the asymmetric viewport margins.
    symmetry_differences=0
    for x in range(3,513):symmetry_differences+=int(np.count_nonzero(paired[:,x]!=paired[:,1026-x]))
    report={'schema':'male-eye04-strict-front-pair-geometry-candidate/v1','scope':'QA only; currentfinal04 and other12 candidate PNGs unchanged',
            'source':srcpath.relative_to(PACK).as_posix(),'source_sha256':SHA(srcpath),
            'source_paint_origin':'Current image_gen eyes04_v3, reviewedAlpha-only edge cleanup400303fd; complete left-eye RGBA, not the input/helper guide.',
            'source_side':'left','source_left_pupil':lp,'right_eye_origin':'Whole-RGBA horizontal reflection of final left aboutx513; explicitly mechanical, not independently generated painting',
            'axis_x':513,'desired_old_pupil_distance':target_distance,'source_reflected_pupil_distance':current_pair_distance,
            'common_similarity':{'a':transform[0],'b':0,'tx':transform[2],'ty':transform[3],'scale':scale,'rotation_deg':0},
            'one_common_premultiplied_alpha_sampling':True,'local_warps':False,'RGB_repainting':False,'old_source_RGB_paste':False,
            'candidate':dest.relative_to(PACK).as_posix(),'candidate_sha256':SHA(dest),'actual_pupils':pupils,
            'actual_pupil_midpoint':np.mean(pupils,axis=0).tolist(),'mirrored_domain_RGBA_difference_channel_count':symmetry_differences,
            'lighting_note':'Catchlights and scleral shading are mechanically reflected with the left painting. Inspect actual normal-size face for acceptable frontal lighting; no independent lighting correction painted.'}
    write_json(PACK/'source/eye04_paired_geometry.json',report)
    for dark in [False,True]:
        label='dark' if dark else 'light'
        panel=Image.new('RGB',(600,305),(38,43,43) if dark else 'white')
        fullpanel=Image.new('RGB',(1260*2,284),(38,43,43) if dark else 'white')
        for col,eye in enumerate([source,final]):
            full=face(eye,dark)
            panel.paste(full.crop((365,285,665,565)).convert('RGB'),(col*300,25))
            fullpanel.paste(full.crop((410,345,620,388)).resize((1260,258),Image.Resampling.NEAREST).convert('RGB'),(col*1260,25))
            for target,stride in [(panel,300),(fullpanel,1260)]:ImageDraw.Draw(target).text((col*stride+5,5),'current generated pair before' if col==0 else 'strict mechanical pair from its same LEFT',fill='white' if dark else 'black')
        panel.save(QA/f'eye04_paired_geometry_face_{label}_1x.png');panel.resize((1200,610),Image.Resampling.NEAREST).save(QA/f'eye04_paired_geometry_face_{label}_2x.png')
        fullpanel.save(QA/f'eye04_paired_geometry_eyes_{label}_6x.png')
    originals=Image.new('RGB',(900,305),'white')
    for col,eye in enumerate([image('eyes',4),source,final]):
        originals.paste(face(eye).crop((365,285,665,565)).convert('RGB'),(col*300,25))
        ImageDraw.Draw(originals).text((col*300+5,5),['frozen original04','generated v3+reviewed edge','strict paired-left geometry'][col],fill='black')
    originals.save(QA/'eye04_frozen_generated_paired_three_faces_1x.png')
    for old in baseline['files']:assert SHA(ROOT/old['file'])==old['sha256']
    print({'candidate':str(dest),'sha256':SHA(dest),'scale':scale,'actual_pupils':pupils,'symmetry_different_RGBA_channels':symmetry_differences})

if __name__=='__main__':main()
