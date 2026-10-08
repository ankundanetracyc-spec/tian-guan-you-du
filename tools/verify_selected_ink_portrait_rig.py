"""Check genuine shifted/cropped PNG inputs and reject incorrect bindings."""
from pathlib import Path
import importlib.util
import json
import tempfile
import numpy as np
from PIL import Image

PROJECT=Path(__file__).resolve().parents[1]
PACK=PROJECT/'mods/appearance/selected_ink_portrait_rig_20261003_v1'
spec=importlib.util.spec_from_file_location('selected_registration',PACK/'register_variant.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
rig=json.loads((PACK/'rig/portrait_rig.json').read_text(encoding='utf-8'))
records={}
with tempfile.TemporaryDirectory(prefix='portrait_rig_',dir=PROJECT/'tmp') as scratch:
    scratch=Path(scratch)
    for sex in ['male','female']:
        family=rig['families'][sex]
        # Source only tests placement; this is not a qualified new eyes asset.
        source=Image.open(PROJECT/f'mods/appearance/ink_portrait_pair_20261003_v2/{sex}/eyes.png').convert('RGBA')
        controls=family['slots']['eyes']['recommended_registration_controls']
        moved=Image.new('RGBA',source.size);moved.paste(source,(7,-5));moved.save(scratch/f'{sex}_moved.png')
        binding={'rig_id':rig['id'],'sex':sex,'slot':'eyes',
          'compatibility_family':family['compatibility_family'],
          'source_image':f'{sex}_moved.png','output_image':f'{sex}_registered.png',
          'landmarks':{k:[family['anchors'][k]['px'][0]+7,family['anchors'][k]['px'][1]-5] for k in controls}}
        path=scratch/f'{sex}.json';path.write_text(json.dumps(binding),encoding='utf-8')
        report=module.register(path)
        output=np.asarray(Image.open(scratch/f'{sex}_registered.png')).copy()
        expected=np.asarray(source).copy();expected[expected[:,:,3]==0,:3]=0
        assert np.array_equal(output,expected), 'Integer translation changed genuine source pixels'
        # A cropped input keeps explicit local control coordinates, rather
        # than recentring the opaque bounding box. It must register identically.
        bbox=source.getchannel('A').getbbox()
        source.crop(bbox).save(scratch/f'{sex}_cropped.png')
        cropped=dict(binding,source_image=f'{sex}_cropped.png',output_image=f'{sex}_crop_registered.png',
          landmarks={k:[family['anchors'][k]['px'][0]-bbox[0],family['anchors'][k]['px'][1]-bbox[1]] for k in controls})
        path.write_text(json.dumps(cropped),encoding='utf-8')
        crop_report=module.register(path)
        assert np.array_equal(np.asarray(Image.open(scratch/f'{sex}_crop_registered.png')),expected)
        bad=dict(binding,output_image=f'{sex}_bad.png',landmarks={k:v[:] for k,v in binding['landmarks'].items()})
        bad['landmarks'][controls[-1]][0]+=18
        path.write_text(json.dumps(bad),encoding='utf-8')
        try:module.register(path)
        except ValueError as error:bad_reason=str(error)
        else:raise AssertionError('Misaligned frame controls were accepted')
        wrong=dict(binding,compatibility_family=rig['families']['female' if sex=='male' else 'male']['compatibility_family'])
        path.write_text(json.dumps(wrong),encoding='utf-8')
        try:module.register(path)
        except ValueError as error:family_reason=str(error)
        else:raise AssertionError('A different sex family was accepted')
        records[sex]={'integer_shift_px':[7,-5],'registered_png_exactly_matches_source':True,
          'cropped_png_with_explicit_local_frame_matches_source':True,
          'maximum_shifted_landmark_residual_px':report['maximum_residual_px'],
          'maximum_cropped_landmark_residual_px':crop_report['maximum_residual_px'],
          'incorrect_control_rejected':bad_reason,'different_family_rejected':family_reason}
    # Exercise a genuinely rotated/rescaled source frame, not only translation.
    target=np.array([[464,375],[569,374],[516.5,348.25]],float)
    theta=np.deg2rad(8);rotation=np.array([[np.cos(theta),-np.sin(theta)],[np.sin(theta),np.cos(theta)]])
    transformed=target@(1.17*rotation)+np.array([13,-19])
    matrix,errors,scale=module.fit_similarity(transformed,target)
    assert errors.max()<1e-6 and abs(scale-1/1.17)<1e-6
report={'rig_id':rig['id'],'sexes':records,'uniform_rotation_and_scale_fit_max_residual_px':float(errors.max()),
        'source_style_reference':'User-selected picture, recovered exactly; no game artwork used',
        'scope':'Placement and wrong-binding gates; semantic PNG cleanup and new variants are not approved by this check'}
(PACK/'registration_verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
