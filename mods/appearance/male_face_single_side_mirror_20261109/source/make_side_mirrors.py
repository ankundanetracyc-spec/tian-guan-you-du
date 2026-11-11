"""Exact whole-side pixel pairing from CURRENT natural-restoration originals.

Two candidate groups only; no active writes, interpolation or Alpha/RGB edits.
"""
from pathlib import Path
import json,hashlib,shutil
import numpy as np
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[4]
PACK=Path(__file__).resolve().parents[1]
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004'
NATURAL=ROOT/'mods/appearance/male_face_natural_restore_20261109'
AXIS=513
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rgba(p):return Image.open(p).convert('RGBA')

def main():
    for name in ['source','qa','reference','preview']:
        (PACK/name).mkdir(parents=True,exist_ok=True);(PACK/name/'.gdignore').touch()
    inventory=sorted([*(ACTIVE/'male').rglob('*.png'),*(ACTIVE/'female').rglob('*.png')])+[ACTIVE/'appearance_parts.json',ACTIVE/'portrait_manifest.json']
    hashes={p.relative_to(ROOT).as_posix():sha(p) for p in inventory}
    natural=json.loads((NATURAL/'source/natural_restore_candidates.json').read_text(encoding='utf-8-sig'))
    expected={(r['part'],r['index']):r['candidate_sha256'] for r in natural['records']}
    original={};records=[];frozen=[]
    for n in [1,4,5]:
        for part in ['eyes','eyebrows']:
            active=ACTIVE/f'male/{part}/{n:02}.png'
            assert sha(active)==expected[(part,n)],'Source is not installed natural-restoration original: '+str(active)
            src=PACK/f'source/frozen_current/male/{part}/{n:02}.png';src.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(active,src)
            image=rgba(src);assert image.size==(1024,1536)
            a=np.asarray(image);original[(part,n)]=image
            assert not a[:,:3,3].any(),'Unmappable source strip contains visible paint'
            frozen.append({'file':active.relative_to(ROOT).as_posix(),'sha256':sha(active),'backup':src.relative_to(PACK).as_posix()})
            for side in ['left','right']:
                out=np.zeros_like(a)
                if side=='left':
                    out[:,:514]=a[:,:514]
                    out[:,514:]=a[:,3:513][:,::-1]
                    source_difference=int(np.count_nonzero(out[:,:514]!=a[:,:514]))
                else:
                    out[:,513:]=a[:,513:]
                    out[:,3:513]=a[:,514:1024][:,::-1]
                    source_difference=int(np.count_nonzero(out[:,513:]!=a[:,513:]))
                pair_difference=int(np.count_nonzero(out[:,3:513]!=out[:,514:1024][:,::-1]))
                axis_difference=int(np.count_nonzero(out[:,513]!=a[:,513]))
                assert source_difference==pair_difference==axis_difference==0
                assert not out[:,:3,3].any()
                dest=PACK/f'candidate_{side}/male/{part}/{n:02}.png';dest.parent.mkdir(parents=True,exist_ok=True);Image.fromarray(out,'RGBA').save(dest)
                band=out[:,508:519,3]
                records.append({'part':part,'index':n,'source_side':side,'source':src.relative_to(PACK).as_posix(),'source_active_file':active.relative_to(ROOT).as_posix(),
                    'source_sha256':sha(src),'candidate':dest.relative_to(PACK).as_posix(),'candidate_sha256':sha(dest),'axis_x':513,
                    'reflection':'x_prime=1026-x, integer pixel centers','axis_column_policy':'Keep original column513 RGBA unchanged',
                    'valid_paired_domain_x_inclusive':[3,1023],'unmappable_left_columns':[0,1,2],
                    'source_side_RGBA_difference_channels':source_difference,'mirror_RGBA_difference_channels':pair_difference,'axis_column_RGBA_difference_channels':axis_difference,
                    'unmappable_original_Alpha_nonzero_pixels':int(np.count_nonzero(a[:,:3,3])),'unmappable_candidate_Alpha_nonzero_pixels':int(np.count_nonzero(out[:,:3,3])),
                    'central_band_x508_518_Alpha_nonzero_pixels':int(np.count_nonzero(band)),'central_band_max_Alpha':int(band.max()),
                    'scale':1,'rotation_deg':0,'interpolation':False,'RGB_repainting':False,'Alpha_cleanup':False,'independently_painted_two_sides':False})
    reference_files=['face_shape/01.png','nose/01.png','mouth/01.png','clothing/01.png','clothing_back/01.png']
    reference_files += [f'{part}/{n:02}.png' for part in ['hair_front','hair_back'] for n in [1,4,5]]
    reference_files += [f'face_accessory/{n:02}.png' for n in range(1,7)]
    for file in reference_files:
        active=ACTIVE/'male'/file;dst=PACK/'reference/current_male'/file;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(active,dst)
        frozen.append({'file':active.relative_to(ROOT).as_posix(),'sha256':sha(active),'backup':dst.relative_to(PACK).as_posix()})
    for name in ['appearance_parts','portrait_manifest']:
        active=ACTIVE/(name+'.json');dst=PACK/'source/frozen_current'/(name+'.snapshot.json');shutil.copyfile(active,dst)
        frozen.append({'file':active.relative_to(ROOT).as_posix(),'sha256':sha(active),'backup':dst.relative_to(PACK).as_posix()})
    def feature(part,n,kind):
        if kind=='original':return original[(part,n)]
        side='left' if kind=='mixed' and part=='eyes' else 'right' if kind=='mixed' else kind
        return rgba(PACK/f'candidate_{side}/male/{part}/{n:02}.png')
    def compose(n,kind,hair=False):
        ref=PACK/'reference/current_male';out=Image.new('RGBA',(1024,1536),(231,225,214,255))
        layers=[]
        if hair:layers += [rgba(ref/f'hair_back/{n:02}.png'),rgba(ref/'clothing_back/01.png')]
        layers += [rgba(ref/'face_shape/01.png')]
        if hair:layers += [rgba(ref/'clothing/01.png')]
        layers += [rgba(ref/'nose/01.png'),feature('eyes',n,kind),feature('eyebrows',n,kind),rgba(ref/'mouth/01.png')]
        if hair:layers += [rgba(ref/f'hair_front/{n:02}.png')]
        for layer in layers:out.alpha_composite(layer)
        return out
    crop=(350,295,680,560)
    full=Image.new('RGB',(990,4*305),(231,225,214))
    for row,kind in enumerate(['original','left','right','mixed']):
        title={'original':'当前原画','left':'取左侧原画镜像','right':'取右侧原画镜像','mixed':'待选组合：眼取左、眉取右'}[kind]
        faces=Image.new('RGB',(990,305),(231,225,214));whole_contact=Image.new('RGB',(3072,1576),(231,225,214))
        for col,n in enumerate([1,4,5]):
            face=compose(n,kind).crop(crop).convert('RGB')
            faces.paste(face,(col*330,40));ImageDraw.Draw(faces).text((col*330+8,8),f'{n:02} · {title}',font=FONT,fill=(40,45,40))
            full.paste(faces.crop((col*330,0,col*330+330,305)),(col*330,row*305))
            if kind!='original':
                whole=compose(n,kind,True);whole.save(PACK/f'preview/{kind}_whole_{n:02}.png')
                whole_contact.paste(whole.convert('RGB'),(col*1024,40));ImageDraw.Draw(whole_contact).text((col*1024+12,9),f'{n:02} · {title}',font=FONT,fill=(40,45,40))
                whole.crop(crop).convert('RGB').save(PACK/f'qa/{kind}_hair_face_{n:02}_1x.png')
        faces.save(PACK/f'qa/three_faces_source_{kind}_1x.png');faces.resize((1980,610),Image.Resampling.NEAREST).save(PACK/f'qa/three_faces_source_{kind}_2x.png')
        if kind!='original':whole_contact.save(PACK/f'qa/three_whole_source_{kind}_1x.png')
    full.save(PACK/'qa/original_left_right_mixed_faces_1x.png')
    for file,before in hashes.items():assert sha(ROOT/file)==before,file
    write(PACK/'source/baseline.json',{'schema':'male-current-natural-mirror-baseline/v1','files':frozen,'active_inventory_readonly_hashes':hashes})
    write(PACK/'source/mirror_candidates.json',{'schema':'male-natural-original-single-side-mirror/v1','source_authority':'Current installed natural-restore six PNGs; all six SHA matched integer-restoration manifest. Rejected regenerated/soft/mirrored previous04 never used.',
        'user_authorization':'Latest explicit single-side mirror instruction overrides earlier no-mirror default for this candidate task.',
        'records':records,'source_axis':513,'painting_or_relighting':False,'active_png_configuration_writes':False,
        'preview_note':'Mixed eyesLEFT+browsRIGHT is only an unselected visual option suggested by independent original-side inspection. All variants need actual normal-scale visual choice; no approval assumed.'})
    print(json.dumps({'candidates':len(records),'all_source_mirror_axis_RGBA_difference_channels':0,'central_alpha_nonzero_max':max(r['central_band_x508_518_Alpha_nonzero_pixels'] for r in records),'active_writes':False,'qa':'qa/original_left_right_mixed_faces_1x.png'}))

if __name__=='__main__':main()
