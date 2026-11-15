"""Candidates only: original frozen brushwork, one whole-pair dx, dy0.

No generation, reflection, rotation, resize, Alpha trimming or active writes.
"""
from pathlib import Path
import sys,json,hashlib,shutil,math
import numpy as np
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parents[4]
PACK=Path(__file__).resolve().parents[1]
OLD=ROOT/'mods/appearance/male_face_alignment_fix_20261109'
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004'
sys.path.insert(0,str(OLD/'source'))
from diagnose_geometry import centroid,ink_geometry

def integer_shift(source,dx):
    """Exact RGBA relocation; no interpolation and no visible clipped pixels."""
    arr=np.asarray(source);out=np.zeros_like(arr)
    if dx<0:
        out[:,:dx]=arr[:,-dx:]
        assert not arr[:,:-dx,3].any()
        assert np.array_equal(out[:,:dx],arr[:,-dx:])
    elif dx>0:
        out[:,dx:]=arr[:,:-dx]
        assert not arr[:,-dx:,3].any()
        assert np.array_equal(out[:,dx:],arr[:,:-dx])
    else:out[:]=arr
    return Image.fromarray(out,'RGBA')

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def rgba(p):return Image.open(p).convert('RGBA')

def main():
    for folder in ['source','qa','reference']:
        dest=PACK/folder;dest.mkdir(parents=True,exist_ok=True);(dest/'.gdignore').touch()
    fixed=['face_shape/01.png','nose/01.png','mouth/01.png']+[f'face_accessory/{n:02}.png' for n in range(1,7)]
    support={}
    for file in fixed:
        src=ACTIVE/'male'/file;dst=PACK/'reference/current_male'/file;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
        support[file]={'active_sha256':sha(src),'reference':dst.relative_to(PACK).as_posix()}
    records=[]
    # Original04 is first to make a reviewable recovery available immediately.
    for n in [4,1,5]:
        for part in ['eyes','eyebrows']:
            src=OLD/f'source/frozen/male/{part}/{n:02}.png';a=np.asarray(rgba(src))
            if part=='eyes':
                controls=[centroid(a,r,100) for r in [[453,358,475,374],[557,358,581,374]]]
                center=float(np.mean(controls,axis=0)[0]);method='paired actual dark-pupil kernels, fixed smallROIs, threshold100 timesAlpha'
            else:
                controls=[ink_geometry(a,r,s,'brow')['inner'] for r,s in zip([[410,326,503,359],[527,325,617,360]],['left','right'])]
                center=float(np.mean(controls,axis=0)[0]);method='paired actual inner brow-head ink centers, excluding transparent support'
            exact_dx=513-center
            dx=int(math.copysign(math.floor(abs(exact_dx)+.5),exact_dx))
            expected={'eyes':{1:-3,4:-2,5:-2},'eyebrows':{1:-1,4:-1,5:1}}[part][n]
            assert dx==expected,(part,n,dx,expected)
            out=integer_shift(rgba(src),dx)
            dst=PACK/f'male/{part}/{n:02}.png';dst.parent.mkdir(parents=True,exist_ok=True);out.save(dst)
            frozen=PACK/f'reference/original_features/{part}/{n:02}.png';frozen.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,frozen)
            records.append({'part':part,'index':n,'source':src.relative_to(ROOT).as_posix(),'source_sha256':sha(src),
                            'immutable_reference':frozen.relative_to(PACK).as_posix(),'candidate':dst.relative_to(PACK).as_posix(),'candidate_sha256':sha(dst),
                            'semantic_controls':controls,'center_before_x':center,'desired_fractional_dx_not_used':exact_dx,
                            'translation':[dx,0],'matrix':[[1,0,dx],[0,1,0]],'center_after_x':center+dx,'axis_residual_px':center+dx-513,
                            'rounding_rule':'nearest integer, exact half away fromzero; no input is a halfcase',
                            'measurement_method':method,'scale':1,'rotation_deg':0,'integer_RGBA_copy':True,'interpolation':False,
                            'retained_translated_pixels_RGBA_difference_count':0,'visible_pixels_clipped':0,
                            'generation':False,'reflection':False,'alpha_cleanup':False,'original_tilt_eye_distance_shape_retained':True})
    def face(n,original=False):
        out=Image.new('RGBA',(1024,1536),(231,225,214,255))
        fixed=PACK/'reference/current_male'
        root=PACK/'reference/original_features' if original else PACK/'male'
        for layer in [rgba(fixed/'face_shape/01.png'),rgba(fixed/'nose/01.png'),rgba(root/f'eyes/{n:02}.png'),rgba(root/f'eyebrows/{n:02}.png'),rgba(fixed/'mouth/01.png')]:out.alpha_composite(layer)
        return out
    table=Image.new('RGB',(660*2,3*690),'white')
    for row,n in enumerate([1,4,5]):
        pair=Image.new('RGB',(660,350),'white')
        for col,original in enumerate([True,False]):
            full=face(n,original);crop=full.crop((350,245,680,570)).convert('RGB')
            pair.paste(crop,(col*330,25));ImageDraw.Draw(pair).text((col*330+5,5),f'original{n:02}'+(' frozen' if original else ' whole-pair dx only'),fill='black')
            table.paste(crop.resize((660,650),Image.Resampling.NEAREST),(col*660,row*690+27));ImageDraw.Draw(table).text((col*660+5,row*690+5),f'original{n:02}'+(' frozen' if original else ' original brushwork, dx only'),fill='black')
        pair.save(PACK/f'qa/original{n:02}_pair_dx_face_1x.png');pair.resize((1320,700),Image.Resampling.NEAREST).save(PACK/f'qa/original{n:02}_pair_dx_face_2x.png')
        full=face(n);full.crop((350,200,680,590)).convert('RGB').save(PACK/f'qa/natural_face_{n:02}_1x.png')
    table.save(PACK/'qa/three_original_pairs_dx_before_after_2x.png')
    whole_table=Image.new('RGB',(3*512,800),'white')
    (PACK/'preview').mkdir(exist_ok=True)
    for col,n in enumerate([1,4,5]):
        whole=Image.new('RGBA',(1024,1536),(231,225,214,255))
        current=PACK/'reference/current_male'
        for layer in [rgba(ACTIVE/f'male/hair_back/{n:02}.png'),rgba(ACTIVE/'male/clothing_back/01.png'),rgba(current/'face_shape/01.png'),
                      rgba(ACTIVE/'male/clothing/01.png'),rgba(current/'nose/01.png'),rgba(PACK/f'male/eyes/{n:02}.png'),
                      rgba(PACK/f'male/eyebrows/{n:02}.png'),rgba(current/'mouth/01.png'),rgba(ACTIVE/f'male/hair_front/{n:02}.png')]:whole.alpha_composite(layer)
        whole.save(PACK/f'preview/natural_whole_{n:02}.png')
        whole_table.paste(whole.resize((512,768),Image.Resampling.LANCZOS).convert('RGB'),(col*512,28))
        ImageDraw.Draw(whole_table).text((col*512+5,7),f'original natural eyes/brows{n:02}; original hair{n:02}; current centered nose/mouth',fill='black')
    whole_table.save(PACK/'qa/three_natural_whole_contact.png')
    write(PACK/'source/natural_restore_candidates.json',{'schema':'male-original-brushwork-integer-dx-only/v1','scope':'Six original frozen eye/brow candidates only; active not written. Current centered nose/mouth/5 beards retained in QA references.',
          'axis_x':513,'pixel_policy':'Exact integer whole-pair RGBA relocation, no interpolation. Residual at most0.5px is the deliberate pixel-discreteness tradeoff.',
          'records':records,'current_support':support,'note':'Natural original small tilt and unequal local brush marks intentionally retained. This restores original brushwork and shared placement; it does not claim all local asymmetry has been repaired.'})
    print(json.dumps({'candidate_count':len(records),'records':[{'part':r['part'],'n':r['index'],'dx':r['translation'][0]} for r in records],'qa04':'qa/original04_pair_dx_face_1x.png','active_writes':False}))

if __name__=='__main__':main()
