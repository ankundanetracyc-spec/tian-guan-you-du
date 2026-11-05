from PIL import Image,ImageDraw
from pathlib import Path
import hashlib,json,numpy as np
root=Path('mods/appearance');qa=root/'portrait_rebuild_20261103/single_style_sample/qa'; paths=[root/'ink_three_variants_20261004/female/clothing/01.png',root/'new_ink_hair_20261003_v1/female/clothing.png',root/'new_ink_hair_20261003_v1/source/face_revision/before_female_clothing.png',root/'selected_ink_portrait_clean_20261003_v1/female/clothing.png']+[root/f'ink_portrait_pair_20261003_v{i}/female/clothing.png' for i in [2,3,4,5]]
board=Image.new('RGB',(800*4,700*2),(235,229,217));dr=ImageDraw.Draw(board);records=[]
for i,p in enumerate(paths):
 im=Image.open(p).convert('RGBA');bg=Image.new('RGBA',im.size,(235,229,217,255));bg.alpha_composite(im);crop=bg.crop((240,500,800,1000)).resize((784,660));board.paste(crop.convert('RGB'),((i%4)*800,(i//4)*700+35));dr.text(((i%4)*800+5,(i//4)*700+5),str(p.parent.parent.name)+' '+p.name,fill='black');records.append({'path':str(p),'sha':hashlib.sha256(p.read_bytes()).hexdigest(),'size':list(im.size)})
board.resize((1600,700)).save(qa/'historical_clothing_chest_comparison.jpg');(qa/'historical_clothing_sources.json').write_text(json.dumps(records,indent=2),encoding='utf8');print(json.dumps(records,indent=2))
# Individual clothing versus actual hair at same native ROI, no changes to source pixels.
canvas=Image.new('RGB',(1680,720),(235,229,217))
for i,(name,p) in enumerate([('clothing',paths[0]),('front',root/'portrait_rebuild_20261103/single_style_sample/parts/hair_front.png'),('back',root/'portrait_rebuild_20261103/single_style_sample/parts/hair_back.png')]):
 im=Image.open(p).convert('RGBA');bg=Image.new('RGBA',im.size,(235,229,217,255));bg.alpha_composite(im);canvas.paste(bg.crop((240,320,800,1040)).convert('RGB'),(i*560,0))
canvas.save(qa/'clothing_front_back_chest.png')
