from pathlib import Path
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parent.parent
B=R.parent
def layer(part,i,before=False):
 p=R/('before_male' if before else 'male')/part/f'{i:02}.png'
 if not p.exists():p=R/'before_male'/part/f'{i:02}.png'
 return Image.open(p).convert('RGBA')
def compose(f,b,e=1,br=1,c=1,bg='#e8e1d6',before=False,hide=[]):
 out=Image.new('RGBA',(1024,1536),bg)
 for part,i in [('hair_back',b),('clothing_back',c),('face_shape',1),('clothing',c),('nose',1),('eyes',e),('eyebrows',br),('mouth',1),('hair_front',f)]:
  if part not in hide:out.alpha_composite(layer(part,i,before))
 return out
for bg,name in [('#273238','dark'),('#e8e1d6','light')]:
 out=Image.new('RGB',(900,1320),bg);d=ImageDraw.Draw(out)
 for f in range(1,4):
  for b in range(1,4):
   crop=compose(f,b,2 if f==3 else 1,f,bg=bg).crop((280,60,760,735)).resize((300,422))
   out.paste(crop,((b-1)*300,(f-1)*440+18));d.text(((b-1)*300+5,(f-1)*440+2),f'front {f} / back {b}',fill='white' if name=='dark' else 'black')
 out.save(R/'qa'/f'nine_mixes_{name}.png')
 compare=Image.new('RGB',(1200,1080),bg)
 for i in range(1,4):
  compose(i,i,2 if i==3 else 1,i,bg=bg,before=True).crop((280,270,760,670)).resize((400,333)).save(R/'qa'/f'before_face0{i}_{name}.png')
  compare.paste(compose(i,i,2 if i==3 else 1,i,bg=bg,before=True).crop((280,220,760,860)).resize((400,533)),((i-1)*400,0))
  compare.paste(compose(i,i,2 if i==3 else 1,i,bg=bg).crop((280,220,760,860)).resize((400,533)),((i-1)*400,540))
 compare.save(R/'qa'/f'before_after_{name}.png')
for i in range(1,4):compose(i,i,2 if i==3 else 1,i).save(R/'qa'/f'style0{i}.png')
print('Updated hair, face and mix review images')
