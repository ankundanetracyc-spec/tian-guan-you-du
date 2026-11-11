"""Install the user-requested exact single-side mirror of six original features."""
import argparse
import hashlib
import json
import os
import shutil
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT/'mods/appearance/ink_three_variants_20261004'
META = PACK.name
NAMES = {f'male/{part}/{index:02}.png' for part in ('eyes', 'eyebrows') for index in (1,4,5)}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def encoded(data):
    return (json.dumps(data, ensure_ascii=False, indent=2)+'\n').encode('utf-8')


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encoded(data))


def replace(path, data, expected):
    fd, temporary = tempfile.mkstemp(dir=path.parent, suffix='.tmp')
    try:
        with os.fdopen(fd,'wb') as out:
            out.write(data)
        assert sha(path)==expected, f'Concurrent active change: {path}'
        os.replace(temporary,path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def verify_candidate(row):
    name=row['candidate']
    assert name in NAMES and row['axis_x']==513
    source=PACK/row['source_ref']
    candidate=PACK/name
    assert sha(source)==row['source_sha256'] and sha(candidate)==row['candidate_sha256']
    src=np.array(Image.open(source).convert('RGBA'))
    got=np.array(Image.open(candidate).convert('RGBA'))
    assert src.shape==got.shape==(1536,1024,4)
    assert not src[:, :3, 3].any() and not got[:, :3, 3].any()
    assert np.array_equal(got[:,3:],got[:,1023:2:-1]), f'Pair not exact RGBA mirror: {name}'
    retained=slice(0,514) if row['side']=='left' else slice(513,1024)
    assert row['side'] in ('left','right')
    assert np.array_equal(got[:,retained],src[:,retained]), f'Source side changed: {name}'
    assert np.array_equal(got[:,513],src[:,513])


def main(apply):
    frame_path=ROOT/'docs/美术规范/男性五官统一定位与验收_20261109/feature_frame.json'
    frame=read(frame_path)
    assert frame['axis_x']==513
    assert sha(ACTIVE/frame['fixed_face']['file'])==frame['fixed_face']['sha256']
    spec=read(PACK/'source/selected_features.json')
    records={r['candidate']:r for r in spec['records']}
    assert set(records)==NAMES and len(records)==6
    for row in records.values():verify_candidate(row)
    review=read(PACK/'qa/preinstall_review.json')
    assert review['status']=='ready_for_user_requested_mirror_install'
    assert review['candidate_hashes']=={name:r['candidate_sha256'] for name,r in records.items()}
    pngs=sorted(p for sex in ('male','female') for p in (ACTIVE/sex).rglob('*.png'))
    assert len(pngs)==67
    baseline_path=PACK/'source/baseline.json'
    freeze_complete=not baseline_path.exists()
    if baseline_path.exists() and len(read(baseline_path)['files'])!=69:
        partial=read(baseline_path)
        inventory=partial['active_inventory_readonly_hashes']
        assert len(inventory)==69
        for name,digest in inventory.items():
            assert sha(ROOT/name)==digest, f'Changed since candidate creation: {name}'
        for row in partial['files']:
            assert sha(PACK/row['backup'])==row['sha256']
        write(PACK/'source/candidate_generation_baseline.snapshot.json',partial)
        freeze_complete=True
    if freeze_complete:
        for name,row in records.items():
            assert sha(ACTIVE/name)==row['source_sha256'], f'Source is not current natural original: {name}'
        files=[]
        for source in pngs+[ACTIVE/'appearance_parts.json',ACTIVE/'portrait_manifest.json']:
            relative=source.relative_to(ACTIVE)
            backup=PACK/'source/before_install'/relative
            if source.suffix=='.json':backup=backup.with_suffix('.snapshot.json')
            backup.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(source,backup)
            files.append({'file':source.relative_to(ROOT).as_posix(),'sha256':sha(source),'backup':backup.relative_to(PACK).as_posix()})
        write(baseline_path,{'schema':'single-side-mirror-before/v1','files':files})
    baseline=read(baseline_path)
    before={str((ROOT/r['file']).relative_to(ACTIVE)).replace('\\','/'):r for r in baseline['files']}
    assert len(before)==69
    for name,row in before.items():
        assert sha(PACK/row['backup'])==row['sha256'] and sha(ACTIVE/name)==row['sha256'], f'Before-state changed: {name}'
    if not apply:
        print('Six original-source exact mirrors ready; active unchanged.')
        return
    manifest=read(ACTIVE/'portrait_manifest.json')
    previous=manifest['male_face_natural_restore_20261109']
    previous['superseded_by']=META
    previous['status']='superseded_six_features_by_user_requested_single_side_mirror'
    manifest[META]={
        'status':'installed_single_side_mirror','axis_x':513,
        'user_direction':'不对称，你生成一边镜像一下不就好了吗',
        'method':'Exact original left eye and right brow RGBA reflection about the fixed face axis. No new generation, interpolation, scaling or rotation.',
        'frame_sha256':sha(frame_path),
        'records':list(records.values()),'other_pngs_unchanged':61,
        'appearance_parts_byte_identical':True,'runtime_status':'pending',
        'runtime_report':'res://mods/appearance/'+META+'/installation/runtime_check.json',
        'user_final_visual_approval':False,
    }
    data=encoded(manifest)
    written=[]
    try:
        for name in records:
            replace(ACTIVE/name,(PACK/name).read_bytes(),before[name]['sha256'])
            written.append(name)
        replace(ACTIVE/'portrait_manifest.json',data,before['portrait_manifest.json']['sha256'])
        written.append('portrait_manifest.json')
        for name,row in before.items():
            if name=='portrait_manifest.json':continue
            expected=records[name]['candidate_sha256'] if name in records else row['sha256']
            assert sha(ACTIVE/name)==expected
    except Exception:
        for name in reversed(written):
            expected=records[name]['candidate_sha256'] if name in records else hashlib.sha256(data).hexdigest()
            replace(ACTIVE/name,(PACK/before[name]['backup']).read_bytes(),expected)
        raise
    write(PACK/'installation/install_report.json',{
        'status':'installed_single_side_mirror','records':list(records.values()),
        'exact_mirrored_pngs':6,'retained_pngs':61,'active_png_count':67,
        'appearance_parts_unchanged':True,'runtime_status':'pending',
        'selected_features_sha256':sha(PACK/'source/selected_features.json'),
        'after_manifest_sha256':sha(ACTIVE/'portrait_manifest.json'),
        'user_final_visual_approval':False,
    })
    print('Installed six exact single-side mirror layers; other61 PNGs and selection config preserved.')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--apply',action='store_true')
    main(parser.parse_args().apply)
