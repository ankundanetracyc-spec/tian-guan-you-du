from PIL import Image,ImageDraw
import build_recovered_ink_parts as b

def previews():
 out=Image.new('RGB',(1500,1710),'#e8e3dc');d=ImageDraw.Draw(out)
 for i,(sex,v,n) in enumerate([(s,v,n) for s,ns in b.NUMBERS.items() for v,n in enumerate(ns,1)]):
  a=b.compose(sex,[v]*7+[1,1]);a.save(b.R/'preview'/f'{n:02}_assembled.png')
  out.paste(a.resize((375,562)),(i%4*375,i//4*570));d.text((i%4*375+8,i//4*570+540),f'{sex} - {n:02}',fill='black')
 out.save(b.R/'preview'/'assembled_overview.jpg')
 heads=Image.new('RGB',(1440,1200),'#e8e3dc');d=ImageDraw.Draw(heads)
 for i,(sex,v,n) in enumerate([(s,v,n) for s,ns in b.NUMBERS.items() for v,n in enumerate(ns,1)]):
  a=b.compose(sex,[v]*7+[1,1]);heads.paste(a.crop((335,200,705,590)).resize((360,380)),(i%4*360,i//4*400));d.text((i%4*360,i//4*400+380),f'{sex} - {n:02}',fill='black')
 heads.save(b.R/'preview'/'heads.jpg')
 eyes=Image.new('RGB',(900,15*110),'#e8d1bf');d=ImageDraw.Draw(eyes)
 i=0
 for sex,ns in b.NUMBERS.items():
  for v in range(1,len(ns)+4):
   for j,slot in enumerate(['eyebrows','eyes']):
    a=Image.open(b.R/sex/slot/f'{v:02}.png').convert('RGBA').crop((415,320,625,413)).resize((420,186))
    # Separate rows, with sufficient vertical room for each isolated feature.
    eyes.paste(a,(j*450,i*110-25),a)
   d.text((5,i*110),f'{sex} {v}',fill='black');i+=1
 eyes.save(b.R/'preview'/'brows_eyes.jpg')

if __name__=='__main__':previews()
