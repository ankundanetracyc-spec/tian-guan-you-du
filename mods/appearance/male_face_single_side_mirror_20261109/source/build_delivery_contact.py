"""Arrange existing actual-game captures at native size for review."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

PACK=Path(__file__).resolve().parents[1]
ROOT=PACK.parents[2]
PREVIOUS=ROOT/'mods/appearance/male_face_natural_restore_20261109/installation'
OUTPUT=PACK/'qa'
OUTPUT.mkdir(exist_ok=True)
FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
BG=(232,226,214,255)
CROP=(350,295,680,560)
records=[]


def tile(path,label,crop=CROP):
    original=Image.open(path).convert('RGBA')
    face=original.crop(crop)
    result=Image.new('RGBA',(face.width,face.height+40),BG)
    result.alpha_composite(face,(0,40))
    ImageDraw.Draw(result).text((8,10),label,font=FONT,fill=(42,48,44,255))
    records.append({'source':str(path.resolve()),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
                    'crop':list(crop),'resized':False,'label':label})
    return result


old=tile(PREVIOUS/'game_natural_bald_b04_e04_composite.png','镜像前')
current=tile(PACK/'installation/game_mirror_bald_b04_e04_composite.png','当前：单侧镜像')
pair=Image.new('RGBA',(660,305),BG)
pair.alpha_composite(old,(0,0))
pair.alpha_composite(current,(330,0))
pair.convert('RGB').save(OUTPUT/'actual_before_after_mirror04_1x.png')
three=Image.new('RGBA',(990,365),BG)
for position,index in enumerate((1,4,5)):
    face=tile(PACK/f'installation/game_mirror_whole_{index:02}_composite.png',f'当前实机 · {index:02}',(350,245,680,570))
    three.alpha_composite(face,(position*330,0))
three.convert('RGB').save(OUTPUT/'actual_three_mirrored_faces_1x.png')
(OUTPUT/'delivery_sources.json').write_text(json.dumps({'purpose':'Actual-game contact sheets, no face resizing', 'records':records},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
runtime=json.loads((PACK/'installation/runtime_check.json').read_text(encoding='utf-8-sig'))
print(json.dumps({'checks':runtime['checks'],'failures':runtime['failures'],'exact_mirrors':len(runtime['mirror_geometry']),
                  'geometry_different_pixels':[r['different_RGBA_pixels'] for r in runtime['mirror_geometry']],
                  'before_after':str(OUTPUT/'actual_before_after_mirror04_1x.png')},ensure_ascii=False))
