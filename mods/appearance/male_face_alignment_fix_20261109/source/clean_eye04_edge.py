"""Technical Alpha-only matte limit for current04; RGB and actual core frozen.

The same distance/feather rule applies to both eyes. The guide contributes only
an external support envelope. Current upper/lower lid and iris pixels, including
new lower core throughy379, are explicitly protected. No source painting is read.
"""
import numpy as np
from PIL import Image,ImageDraw
from scipy.ndimage import distance_transform_edt
from diagnose_geometry import ROOT,PACK,ACTIVE,QA,SHA,write_json,freeze,image
from render_candidate_qa import candidate

def smooth(v):
    v=np.clip(v,0.,1.)
    return v*v*(3-2*v)

def main():
    baseline=freeze()
    source=PACK/'source/intermediate/eyes04_v3_registered_before_edge.png'
    assert SHA(source)=='7d6037cdd3278308513dbb4519b2085c43ef4affeb3cc20839de6a34e91bd77d'
    guide=QA/'eye04_single_left_geometry_guide.png'
    before=np.asarray(Image.open(source).convert('RGBA')).copy()
    ga=np.asarray(Image.open(guide).convert('RGBA'))[:,:,3]
    mask=Image.new('L',(1024,1536),0);d=ImageDraw.Draw(mask)
    polygons=[
      [[428,362],[442,358],[459,357],[474,360],[485,369],[488,374],[481,377],[469,379],[457,379],[445,377],[435,373],[428,368]],
      [[536,374],[543,366],[551,360],[565,357],[580,359],[595,362],[600,365],[596,370],[586,375],[575,379],[565,379],[553,378],[543,376]]
    ]
    for polygon in polygons:d.polygon([tuple(p) for p in polygon],fill=255)
    core=np.asarray(mask)>0
    gray=before[:,:,:3].mean(2)
    # Actual opaque dark main-lash ink is preserved even when a1px tip lies
    # beyond the hand-read aperture polygon. This never protects a faint brown
    # carrier solely because its alpha/support happens to be large.
    dark_core=(gray<100)&(before[:,:,3]>=230)
    yy,xx=np.indices(core.shape)
    anatomical_zone=(((xx>=423)&(xx<=489))|((xx>=533)&(xx<=602)))&(yy>=354)&(yy<=379)
    core |= dark_core&anatomical_zone
    distance_guide=distance_transform_edt(ga<230)
    distance_core=distance_transform_edt(~core)
    guide_cap=255*smooth((3.5-distance_guide)/3.5)
    core_cap=255*smooth((4.0-distance_core)/4.0)
    cap=np.maximum(guide_cap,core_cap)
    after=before.copy();after[:,:,3]=np.minimum(before[:,:,3],np.rint(cap).astype(np.uint8))
    # Keep all original RGB, including fully transparent pixels, byte-for-byte.
    assert np.array_equal(after[:,:,:3],before[:,:,:3])
    assert np.array_equal(after[core],before[core])
    assert np.all(after[:,:,3]<=before[:,:,3])
    outpath=QA/'eye04_edge_candidate.png';Image.fromarray(after,'RGBA').save(outpath)
    Image.fromarray((core*255).astype(np.uint8),'L').save(QA/'eye04_edge_protected_core_mask.png')
    changed=after[:,:,3]!=before[:,:,3];cy,cx=np.where(changed)
    significant=changed&(before[:,:,3]>=5);sy,sx=np.where(significant)
    report={'schema':'male-eye04-alpha-only-edge-candidate/v1','source':source.relative_to(PACK).as_posix(),'source_sha256':SHA(source),
            'guide':guide.relative_to(PACK).as_posix(),'guide_sha256':SHA(guide),'candidate':outpath.relative_to(PACK).as_posix(),'candidate_sha256':SHA(outpath),
            'guide_usage':'OnlyAlpha230 solid support as external3.5px smooth distance feather; not old uneven04 Alpha as a whole-eye crop.',
            'actual_core_protection':{'polygons':polygons,'main_dark_core':'Current meanRGB<100 andAlpha>=230 within actual two-lid zone; same rule both sides',
                                      'lower_core_includes_y379':True,'protected_pixels':int(core.sum()),'protected_supported_pixels':int(np.count_nonzero(core&(before[:,:,3]>0))),
                                      'protected_core_RGBA_changed_pixels':int(np.count_nonzero(np.any(after[core]!=before[core],axis=1)))},
            'RGB_changed_pixels':int(np.count_nonzero(np.any(after[:,:,:3]!=before[:,:,:3],axis=2))),
            'Alpha_added_pixels':int(np.count_nonzero(after[:,:,3]>before[:,:,3])),
            'Alpha_reduced_pixels':int(changed.sum()),'Alpha_changed_bbox_half_open':[int(cx.min()),int(cy.min()),int(cx.max()+1),int(cy.max()+1)],
            'Alpha_reduced_source_ge5_pixels':int(significant.sum()),'Alpha_changed_source_ge5_bbox_half_open':[int(sx.min()),int(sy.min()),int(sx.max()+1),int(sy.max()+1)],
            'native_Alpha1_to4_speck_pixels_removed':int(np.count_nonzero(changed&(before[:,:,3]<=4))),
            'outer_rule':'min(currentAlpha, max(guide3.5px_distance_cap, current_core4px_distance_cap)); cubic smooth falloff around all contours, no hard rectangular clip',
            'probes':{f'{x},{y}':{'before':before[y,x].tolist(),'after':after[y,x].tolist(),'protected':bool(core[y,x])} for x,y in [(592,377),(426,370),(422,374),(459,379),(565,379),(460,365),(565,365)]},
            'other12_candidate_PNGs_written':False,'active67_PNG_two_configs_preserved':True,'visual_status':'pending independent actual-face matte/core review'}
    write_json(PACK/'source/eye04_edge_cleanup.json',report)
    for background in ['light','dark']:
        face_pair=Image.new('RGB',(600,300),'white' if background=='light' else (38,43,43))
        eye_pair=Image.new('RGB',(2520,284),'white' if background=='light' else (38,43,43))
        for col,rgba in enumerate([before,after]):
            full=Image.new('RGBA',(1024,1536),(231,225,214,255) if background=='light' else (38,43,43,255))
            for layer in [image('face_shape'),candidate('nose'),Image.fromarray(rgba,'RGBA'),candidate('eyebrows',4),candidate('mouth')]:full.alpha_composite(layer)
            face_pair.paste(full.crop((365,290,665,565)).convert('RGB'),(col*300,25))
            eye_pair.paste(full.crop((410,345,620,388)).resize((1260,258),Image.Resampling.NEAREST).convert('RGB'),(col*1260,25))
            for panel,stride in [(face_pair,300),(eye_pair,1260)]:ImageDraw.Draw(panel).text((col*stride+5,5),'source current04v3' if col==0 else 'Alpha-only edge candidate, identical actual core/RGB',fill='black' if background=='light' else 'white')
        face_pair.save(QA/f'eye04_edge_before_after_face_{background}_1x.png')
        face_pair.resize((1200,600),Image.Resampling.NEAREST).save(QA/f'eye04_edge_before_after_face_{background}_2x.png')
        eye_pair.save(QA/f'eye04_edge_source_left_after_right_{background}_6x.png')
    for old in baseline['files']:assert SHA(ROOT/old['file'])==old['sha256']
    print({k:report[k] for k in ['candidate','candidate_sha256','RGB_changed_pixels','Alpha_reduced_pixels','Alpha_changed_bbox_half_open','probes']})

if __name__=='__main__':main()
