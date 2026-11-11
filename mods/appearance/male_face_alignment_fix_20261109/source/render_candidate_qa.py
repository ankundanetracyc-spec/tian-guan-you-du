"""Current candidate compositing QA only, never writes active assets."""
from PIL import Image,ImageDraw
from diagnose_geometry import PACK,QA,image

def candidate(part,n=1):
    p=PACK/f'male/{part}/{n:02}.png'
    return Image.open(p).convert('RGBA') if p.exists() else image(part,n)

def compose(eye,brow,hair=None,beard=None,old=False,dark=False):
    out=Image.new('RGBA',(1024,1536),(38,43,43,255) if dark else (231,225,214,255))
    get=image if old else candidate
    layers=[]
    if hair is not None:layers+=[image('hair_back',hair)]
    layers+=[image('face_shape'),get('nose'),get('eyes',eye),get('eyebrows',brow),get('mouth')]
    if beard is not None:layers+=[get('face_accessory',beard)]
    if hair is not None:layers+=[image('hair_front',hair)]
    for layer in layers:out.alpha_composite(layer)
    return out

def main():
    for n in [1,4,5]:
        for dark in [False,True]:
            label='dark' if dark else 'light'
            view=compose(n,n,dark=dark).crop((350,245,680,570)).convert('RGB')
            view.save(QA/f'candidate_face_{n:02}_{label}_1x.png')
            view.resize((660,650),Image.Resampling.NEAREST).save(QA/f'candidate_face_{n:02}_{label}_2x.png')
    crop=(365,300,665,570)
    for old in [False,True]:
        table=Image.new('RGB',(900,3*298),'white')
        for row,brow in enumerate([1,4,5]):
            for col,eye in enumerate([1,4,5]):
                full=compose(eye,brow,old=old);small=full.crop(crop).convert('RGB')
                table.paste(small,(col*300,row*298+26))
                ImageDraw.Draw(table).text((col*300+3,row*298+5),('OLD ' if old else 'NEW ')+f'brow{brow:02} eye{eye:02}',fill='black')
                if not old:small.save(QA/f'candidate_nofair_brow{brow:02}_eye{eye:02}_1x.png')
        table.save(QA/('old_nine_nohair_1x.png' if old else 'candidate_nine_nohair_1x.png'))
    old=Image.open(QA/'old_nine_nohair_1x.png');new=Image.open(QA/'candidate_nine_nohair_1x.png')
    pair=Image.new('RGB',(1800,894),'white');pair.paste(old,(0,0));pair.paste(new,(900,0));pair.save(QA/'old_left_candidate_right_nine_nohair_1x.png')
    hairs=[1,4,5,6,7,8,9]
    hair_table=Image.new('RGB',(7*246,3*340),'white')
    for row,n in enumerate([1,4,5]):
        for col,hair in enumerate(hairs):
            v=compose(n,n,hair=hair).crop((310,100,720,620)).resize((246,312),Image.Resampling.LANCZOS).convert('RGB')
            hair_table.paste(v,(col*246,row*340+26));ImageDraw.Draw(hair_table).text((col*246+3,row*340+5),f'new face{n:02} hair{hair:02}',fill='black')
    hair_table.save(QA/'candidate_seven_front_hairs_three_faces.png')
    beard_table=Image.new('RGB',(6*300,3*308),'white')
    for row,n in enumerate([1,4,5]):
        for col,beard in enumerate([1,2,3,4,5,6]):
            v=compose(n,n,beard=beard).crop((365,300,665,580)).convert('RGB')
            beard_table.paste(v,(col*300,row*308+26));ImageDraw.Draw(beard_table).text((col*300+3,row*308+5),f'new face{n:02} beard{beard:02}',fill='black')
    beard_table.save(QA/'candidate_six_beards_three_faces_1x.png')
    for kind,cols in [('nine_nohair',3),('seven_front_hairs_three_faces',7),('six_beards_three_faces',6)]:
        width,height=(300,298) if kind=='nine_nohair' else (246,340) if cols==7 else (300,308)
        table=Image.new('RGB',(cols*width,3*height),(38,43,43))
        for row,n in enumerate([1,4,5]):
            for col in range(cols):
                eye=[1,4,5][col] if cols==3 else n
                hair=hairs[col] if cols==7 else None
                beard=col+1 if cols==6 else None
                full=compose(eye,n,hair=hair,beard=beard,dark=True)
                v=full.crop((310,100,720,620)).resize((246,312),Image.Resampling.LANCZOS).convert('RGB') if cols==7 else full.crop((365,300,665,570 if cols==3 else 580)).convert('RGB')
                table.paste(v,(col*width,row*height+26));ImageDraw.Draw(table).text((col*width+3,row*height+5),f'new brow{n:02} eye{eye:02} '+(f'hair{hair:02}' if hair else f'beard{beard:02}' if beard else ''),fill='white')
        table.save(QA/f'candidate_{kind}_dark.png')
    print('candidate_nine_nohair_1x.png and old_left_candidate_right_nine_nohair_1x.png')

if __name__=='__main__':main()
