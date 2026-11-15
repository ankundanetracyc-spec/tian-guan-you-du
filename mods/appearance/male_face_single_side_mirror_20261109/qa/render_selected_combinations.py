"""Static selected-asset QA only; never runs Godot or writes any active file."""
from pathlib import Path
import json,hashlib,shutil
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[4]
P=Path(__file__).resolve().parents[1]
ACTIVE=ROOT/'mods/appearance/ink_three_variants_20261004'
QA=P/'qa';REF=P/'reference/current_male'
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def rgba(p):return Image.open(p).convert('RGBA')

def main():
    selected=json.loads((P/'source/selected_features.json').read_text(encoding='utf-8-sig'))
    selected_hash={r['candidate']:r['candidate_sha256'] for r in selected['records']}
    baseline=json.loads((P/'source/baseline.json').read_text(encoding='utf-8-sig'))['active_inventory_readonly_hashes']
    sources={}
    needed=[f'{part}/{n:02}.png' for part in ['hair_front','hair_back'] for n in [1,4,5,6,7,8,9]]
    needed += [f'{part}/{n:02}.png' for part in ['clothing','clothing_back'] for n in range(1,8)]
    needed += ['face_shape/01.png','nose/01.png','mouth/01.png']+[f'face_accessory/{n:02}.png' for n in range(1,7)]
    for file in needed:
        dst=REF/file
        if not dst.exists():
            src=ACTIVE/'male'/file
            assert sha(src)==baseline[src.relative_to(ROOT).as_posix()],str(src)
            dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
        sources[dst.relative_to(P).as_posix()]=sha(dst)
    def compose(n,dark=False,hair=None,beard=1,clothes=1):
        out=Image.new('RGBA',(1024,1536),(38,43,43,255) if dark else (231,225,214,255))
        layers=[]
        if hair is not None:layers += [rgba(REF/f'hair_back/{hair:02}.png')]
        layers += [rgba(REF/f'clothing_back/{clothes:02}.png'),rgba(REF/'face_shape/01.png'),rgba(REF/f'clothing/{clothes:02}.png'),
                   rgba(REF/'nose/01.png'),rgba(P/f'male/eyes/{n:02}.png'),rgba(P/f'male/eyebrows/{n:02}.png'),rgba(REF/'mouth/01.png'),rgba(REF/f'face_accessory/{beard:02}.png')]
        if hair is not None:layers += [rgba(REF/f'hair_front/{hair:02}.png')]
        for layer in layers:out.alpha_composite(layer)
        return out
    outputs=[]
    for category,values,crop in [('seven_hair',[1,4,5,6,7,8,9],(320,100,710,650)),('six_beards',list(range(1,7)),(365,320,665,585)),('seven_collars',list(range(1,8)),(280,300,760,800))]:
        width,height=crop[2]-crop[0],crop[3]-crop[1]
        for dark in [False,True]:
            label='dark' if dark else 'light'
            table=Image.new('RGB',(len(values)*width,3*(height+35)),(38,43,43) if dark else (231,225,214))
            for row,n in enumerate([1,4,5]):
                for col,value in enumerate(values):
                    kwargs={'hair':value} if category=='seven_hair' else {'beard':value} if category=='six_beards' else {'clothes':value,'hair':n}
                    view=compose(n,dark=dark,**kwargs).crop(crop).convert('RGB')
                    table.paste(view,(col*width,row*(height+35)+35))
                    kind={'seven_hair':'发','six_beards':'须','seven_collars':'衣'}[category]
                    ImageDraw.Draw(table).text((col*width+6,row*(height+35)+6),f'静态·脸{n:02} {kind}{value:02} · 眼左眉右',font=FONT,fill='white' if dark else (40,45,40))
            path=QA/f'selected_static_{category}_{label}_1x.png';table.save(path)
            outputs.append({'file':path.relative_to(P).as_posix(),'sha256':sha(path),'crop':list(crop),'image_resizing':False})
    # Compact native-size view of the three selected complete faces.
    for dark in [False,True]:
        label='dark' if dark else 'light';table=Image.new('RGB',(990,305),(38,43,43) if dark else (231,225,214))
        for col,n in enumerate([1,4,5]):
            table.paste(compose(n,dark=dark).crop((350,295,680,560)).convert('RGB'),(col*330,40))
            ImageDraw.Draw(table).text((col*330+7,7),f'静态·脸{n:02} · 眼左眉右',font=FONT,fill='white' if dark else (40,45,40))
        path=QA/f'selected_static_three_faces_{label}_1x.png';table.save(path);outputs.append({'file':path.relative_to(P).as_posix(),'sha256':sha(path),'image_resizing':False})
    for file,before in selected_hash.items():assert sha(P/file)==before,file
    for file,before in sources.items():assert sha(P/file)==before,file
    (QA/'selected_static_combinations_sources.json').write_text(json.dumps({'schema':'single-side-selected-static-combinations/v1','scope':'Static composition from frozen support and selected six PNGs; not actual game captures; noGodot/noactive writes',
        'selected_input_hashes':selected_hash,'support_input_hashes':sources,'outputs':outputs,'candidate_PNGs_modified':False},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'static_tables':len(outputs),'selected_six_pngs_preserved':True,'active_writes':False}))

if __name__=='__main__':main()
