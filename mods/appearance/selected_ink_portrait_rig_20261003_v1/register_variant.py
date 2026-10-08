"""Register one declared portrait part to our selected portrait frame.

Run: python register_variant.py binding.json
One similarity transform only; output is a full transparent shared canvas.
This checks positioning, not painted style or semantic layer purity.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parent
RIG_PATH=ROOT/'rig/portrait_rig.json'

def fit_similarity(source,target):
    source=np.asarray(source,np.float64);target=np.asarray(target,np.float64)
    if not np.isfinite(source).all() or not np.isfinite(target).all():
        raise ValueError('Anchor coordinates must be finite')
    source_center=source.mean(0);target_center=target.mean(0)
    a=source-source_center;b=target-target_center
    if np.linalg.matrix_rank(a)<2 or np.linalg.matrix_rank(b)<2:
        raise ValueError('Use at least three non-collinear frame controls')
    u,s,vh=np.linalg.svd(a.T@b)
    sign=np.ones(2)
    if np.linalg.det(u@vh)<0:sign[-1]=-1
    rotation=u@np.diag(sign)@vh
    scale=float(np.sum(s*sign)/np.sum(a*a))
    if scale<=0 or not math.isfinite(scale):raise ValueError('Invalid uniform scale')
    translation=target_center-source_center@(scale*rotation)
    matrix=np.column_stack((scale*rotation.T,translation))
    predicted=source@(scale*rotation)+translation
    errors=np.linalg.norm(predicted-target,axis=1)
    return matrix,errors,scale

def register(binding_path):
    binding_path=Path(binding_path).resolve()
    spec=json.loads(binding_path.read_text(encoding='utf-8'))
    rig=json.loads(RIG_PATH.read_text(encoding='utf-8'))
    if spec.get('rig_id')!=rig['id']:raise ValueError('The source binding names another rig')
    sex=spec.get('sex');slot=spec.get('slot')
    if sex not in rig['families']:raise ValueError('Unknown sex family')
    family=rig['families'][sex]
    if spec.get('compatibility_family')!=family['compatibility_family']:
        raise ValueError('The source binding belongs to another portrait family')
    if slot not in family['slots']:raise ValueError('Unknown nine-part slot')
    landmarks=spec.get('landmarks',{})
    required=family['slots'][slot]['recommended_registration_controls']
    if len(landmarks)<rig['registration']['minimum_controls'] or not set(required).issubset(landmarks):
        raise ValueError('Declare all recommended frame controls and at least three points')
    unknown=set(landmarks)-set(family['anchors'])
    if unknown:raise ValueError('Unknown frame control: '+', '.join(sorted(unknown)))
    keys=list(landmarks)
    matrix,errors,scale=fit_similarity([landmarks[k] for k in keys],
                                      [family['anchors'][k]['px'] for k in keys])
    maximum=float(errors.max())
    if maximum>rig['registration']['maximum_residual_px']:
        raise ValueError(f'Controls do not share one transform: maximum residual {maximum:.3f}px')
    source_path=(binding_path.parent/spec['source_image']).resolve()
    output_path=(binding_path.parent/spec['output_image']).resolve()
    if output_path==source_path:raise ValueError('Export to a new file; do not overwrite the source')
    if output_path.exists():raise ValueError('Output already exists; use a new export filename')
    with Image.open(source_path) as source:
        if source.mode!='RGBA':raise ValueError('Source must have a real RGBA alpha channel')
        pixels=np.asarray(source).copy()
    canvas=tuple(rig['canvas'])
    # Preserve exact pixels for integer translations; otherwise resample in
    # premultiplied space, avoiding black or colored fringes on transparent edges.
    linear=matrix[:,:2]
    shift=matrix[:,2]
    if np.allclose(linear,np.eye(2),atol=1e-8) and np.allclose(shift,np.round(shift),atol=1e-8):
        result=Image.new('RGBA',canvas)
        result.paste(Image.fromarray(pixels),tuple(np.round(shift).astype(int)))
        result=np.asarray(result).copy()
    else:
        rgba=pixels.astype(np.float32)/255
        rgba[:,:,:3]*=rgba[:,:,3:4]
        shifted=cv2.warpAffine(rgba,matrix,canvas,flags=cv2.INTER_LANCZOS4,
                              borderMode=cv2.BORDER_CONSTANT,borderValue=0)
        shifted=np.clip(shifted,0,1)
        shifted[:,:,:3]/=np.maximum(shifted[:,:,3:4],1e-8)
        result=np.round(np.clip(shifted,0,1)*255).astype(np.uint8)
    result[result[:,:,3]==0,:3]=0
    output_path.parent.mkdir(parents=True,exist_ok=True)
    Image.fromarray(result).save(output_path)
    report={
      'rig_id':rig['id'],'compatibility_family':family['compatibility_family'],'slot':slot,
      'source':str(source_path),'output':str(output_path),'canvas':list(canvas),
      'similarity_matrix':matrix.tolist(),'uniform_scale':scale,
      'landmark_residual_px':dict(zip(keys,errors.tolist())),
      'maximum_residual_px':maximum,'export_offset_px':[0,0],
      'source_sha256':hashlib.sha256(source_path.read_bytes()).hexdigest(),
      'output_sha256':hashlib.sha256(output_path.read_bytes()).hexdigest(),
      'semantic_purity_and_art_review':'Still required; geometric registration does not approve pixels',
    }
    output_path.with_suffix('.registration.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    return report

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('binding',type=Path)
    args=parser.parse_args()
    try:report=register(args.binding)
    except (ValueError,KeyError) as error:raise SystemExit(str(error))
    print(json.dumps(report,ensure_ascii=False,indent=2))
