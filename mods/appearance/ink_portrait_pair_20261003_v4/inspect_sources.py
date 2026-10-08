from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np

ROOT = Path(__file__).resolve().parent
V1 = ROOT.parent / 'ink_portrait_pair_20261003_v1'
for sex in ('male', 'female'):
    source = Image.open(V1 / f'{sex}_portrait.png').convert('RGBA')
    bald = Image.open(V1 / 'reference' / f'{sex}_bald_user.png').convert('RGBA')
    for name, img, bounds, scale in (
        ('features_source', source, (410, 330, 625, 409), 5),
        ('features_bald', bald, (410, 330, 625, 409), 5),
        ('hair_source', source, (240, 130, 800, 1110), 1),
    ):
        crop = img.crop(bounds)
        back = Image.new('RGBA', crop.size, '#ddd8cf')
        back.alpha_composite(crop)
        back = back.convert('RGB').resize((crop.width*scale, crop.height*scale))
        back.save(ROOT / 'preview' / f'{sex}_{name}.png')
    a = np.array(source)
    print(sex, [(x,y,a[y,x].tolist()) for x,y in [(350,300),(330,400),(300,570),(320,580),(355,600),(410,770),(420,1000),(650,775),(700,600),(710,570),(280,650)]])

rear = np.array(Image.open(ROOT / 'source/rear_hair_generated_candidate.png').convert('RGBA'))
print('rear', [(x,y,rear[y,x].tolist()) for x,y in [(512,70),(512,150),(300,350),(250,500),(200,1000),(500,1450),(100,1000),(500,1500)]])
