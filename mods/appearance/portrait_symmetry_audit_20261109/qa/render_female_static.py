"""Read-only female feature audit visuals; all compositions are static, not game captures."""
from pathlib import Path
import hashlib,json
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[4]
PACK=Path(__file__).resolve().parents[1]
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def rgba(p):return Image.open(p).convert('RGBA')

def main():
    (PACK/'source').mkdir(parents=True,exist_ok=True);(PACK/'qa').mkdir(parents=True,exist_ok=True)
    (PACK/'source/.gdignore').touch();(PACK/'qa/.gdignore').touch()
    files=sorted([*(ACTIVE/'male').rglob('*.png'),*(ACTIVE/'female').rglob('*.png')])+[ACTIVE/'appearance_parts.json',ACTIVE/'portrait_manifest.json']
    assert len([p for p in files if p.suffix=='.png'])==67
    initial={p.relative_to(ROOT).as_posix():sha(p) for p in files}
    config=json.loads((ACTIVE/'appearance_parts.json').read_text(encoding='utf-8-sig'))
    female=[r for r in config['components'] if r['gender']=='female']
    bypart={r['part']:r for r in female}
    assert bypart['brow']['variant_indices']==[1,2,3,4] and bypart['eye']['variant_indices']==[1,2,3]
    face_idx=bypart['face']['variant_indices'][0]
    images={p.relative_to(ACTIVE/'female').as_posix():rgba(p) for p in (ACTIVE/'female').rglob('*.png')}
    def img(part,n=1):return images[f'{part}/{n:02}.png']
    def compose(brow=None,eye=None,hair=None,full=False):
        out=Image.new('RGBA',(1024,1536),(231,225,214,255));layers=[]
        if hair:layers += [img('hair_back',hair)]
        layers += [img('face_shape',face_idx)]
        if full:layers += [img('clothing')]
        layers += [img('nose')]
        if eye:layers += [img('eyes',eye)]
        if brow:layers += [img('eyebrows',brow)]
        layers += [img('mouth')]
        if hair:layers += [img('hair_front',hair)]
        for layer in layers:out.alpha_composite(layer)
        return out.convert('RGB')
    crop=(350,290,685,585);width=335;height=295
    table=Image.new('RGB',(width*3,4*(height+35)),(231,225,214))
    for row,brow in enumerate([1,2,3,4]):
        for col,eye in enumerate([1,2,3]):
            view=compose(brow,eye).crop(crop)
            table.paste(view,(col*width,row*(height+35)+35))
            ImageDraw.Draw(table).text((col*width+6,row*(height+35)+6),f'女性静态拼合 · 眉{brow:02} 眼{eye:02}',font=FONT,fill=(40,45,40))
    table.save(PACK/'qa/female_static_twelve_brows_eyes_1x.png')
    # Each layer is shown over the unchanged actual face, without the other
    # variable layer. Single-layer detail magnification uses nearest pixels.
    for part,ids,region in [('eyes',[1,2,3],(410,345,630,420)),('eyebrows',[1,2,3,4],(410,330,630,385))]:
        w,h=region[2]-region[0],region[3]-region[1]
        strip=Image.new('RGB',(w,len(ids)*(h+32)),(231,225,214))
        details=Image.new('RGB',(w*4,len(ids)*(h*4+32)),(231,225,214))
        for row,n in enumerate(ids):
            view=compose(eye=n if part=='eyes' else None,brow=n if part=='eyebrows' else None).crop(region)
            for target,yy in [(strip,row*(h+32)),(details,row*(h*4+32))]:
                ImageDraw.Draw(target).text((5,yy+5),f'女性静态单层 · {part}{n:02}',font=FONT,fill=(40,45,40))
            strip.paste(view,(0,row*(h+32)+32))
            details.paste(view.resize((w*4,h*4),Image.Resampling.NEAREST),(0,row*(h*4+32)+32))
        strip.save(PACK/f'qa/female_static_{part}_layers_1x.png');details.save(PACK/f'qa/female_static_{part}_layers_4x.png')
    # Three actual hairstyles over the three eye choices, with a constant brow.
    hair_table=Image.new('RGB',(335*3,3*(295+35)),(231,225,214))
    for row,eye in enumerate([1,2,3]):
        for col,hair in enumerate([1,2,3]):
            view=compose(1,eye,hair,True).crop(crop)
            hair_table.paste(view,(col*335,row*330+35))
            ImageDraw.Draw(hair_table).text((col*335+6,row*330+6),f'女性静态 · 眉01 眼{eye:02} 发{hair:02}',font=FONT,fill=(40,45,40))
    hair_table.save(PACK/'qa/female_static_three_hair_eye_context_1x.png')
    brow_hair=Image.new('RGB',(335*3,2*330),(231,225,214))
    for row,brow in enumerate([2,3]):
        for col,hair in enumerate([1,2,3]):
            brow_hair.paste(compose(brow,1,hair,True).crop(crop),(col*335,row*330+35))
            ImageDraw.Draw(brow_hair).text((col*335+6,row*330+6),f'女性静态 · 眉{brow:02} 眼01 发{hair:02}',font=FONT,fill=(40,45,40))
    brow_hair.save(PACK/'qa/female_static_brows02_03_three_hair_context_1x.png')
    for file,before in initial.items():assert sha(ROOT/file)==before,file
    outputs=[p for p in (PACK/'qa').glob('female_static*.png')]
    (PACK/'source/source_hashes_pipeline.json').write_text(json.dumps({'schema':'portrait-readonly-symmetry-audit-pipeline/v1',
        'scope':'Current female4brows/3eyes; static actual-asset composition only, not runtime. No female pixel, active file or document modified.',
        'active_png_count':67,'configurations':2,'female_face_physical_index':face_idx,'female_components':female,
        'initial_readonly_hashes':initial,'all_source_hashes_preserved':True,
        'face_crop_xyxy':list(crop),'normal_view_resizing':False,'detail_only_magnification':'4x nearest pixels',
        'outputs':[{'file':p.relative_to(PACK).as_posix(),'sha256':sha(p)} for p in outputs]},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'static_outputs':len(outputs),'active_pngs':67,'female_face':face_idx,'active_writes':False}))

if __name__=='__main__':main()
