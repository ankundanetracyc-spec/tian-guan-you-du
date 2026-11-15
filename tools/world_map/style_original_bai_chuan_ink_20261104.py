"""Manually grade the original yellow-green artwork without moving its pixels.

The user explicitly chose this composition and requested manual style changes.
No image generation, geometry resampling or new scenery is used.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image, ImageChops
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'textures/world/bai_chuan/acceptance_20261030/master_3762.png'
BASE = ROOT / 'textures/world/bai_chuan/original_ink_3x3_20261104'
PAPER = ROOT / 'textures/ui/world_ink_20261104/ink_ui_atlas_v1.png'


def smoothstep(lo, hi, value):
    weight = np.clip((value-lo)/(hi-lo), 0, 1)
    return weight*weight*(3-2*weight)


def tint_at_value(colour, value):
    colour = np.asarray(colour, dtype=np.float32)
    return colour * (value / np.dot(colour, [0.2126,0.7152,0.0722]))[...,None]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    BASE.mkdir(parents=True, exist_ok=True)
    with Image.open(SOURCE) as image:
        if image.size != (3762,3762):
            raise ValueError('Original composition dimensions changed.')
        rgb = np.array(image.convert('RGB'), dtype=np.float32)/255
    red, green, blue = rgb.transpose(2,0,1)
    value = rgb @ np.array([0.2126,0.7152,0.0722], dtype=np.float32)
    chroma = rgb.max(2)-rgb.min(2)
    water = smoothstep(.10,.25,blue-red)*smoothstep(.02,.10,blue-green)*smoothstep(.42,.60,blue)
    forest = smoothstep(.008,.055,green-red)*smoothstep(.035,.14,green-blue)*(1-smoothstep(.59,.76,value))*(1-water)
    neutral = 1-smoothstep(.13,.27,chroma)
    rock = neutral*(1-smoothstep(.64,.80,value))*(1-water)*(1-forest)
    # Existing linework and silhouettes remain; the broad colour families change.
    ground_value = np.clip(.895+(value-.70)*.28, .74, .97)
    result = tint_at_value([.900,.902,.863], ground_value)
    rock_value = np.clip(.19+value*.82, .18, .86)
    rock_ink = tint_at_value([.345,.384,.384], rock_value)
    result = result*(1-rock[...,None])+rock_ink*rock[...,None]
    forest_value = np.clip(.15+value*.82, .20, .79)
    forest_ink = tint_at_value([.327,.389,.357], forest_value)
    result = result*(1-forest[...,None])+forest_ink*forest[...,None]
    # Keep the original channel silhouette and gradients, with restrained blue.
    water_value = np.clip(.635+(value-.60)*.44, .46, .83)
    mineral_water = tint_at_value([.490,.639,.684], water_value)
    result = result*(1-water[...,None])+mineral_water*water[...,None]
    # Subtle grain from our own existing paper, rather than synthetic new detail.
    with Image.open(PAPER) as paper:
        patch = np.asarray(paper.convert('RGB').crop((94,134,416,441)), dtype=np.float32).mean(2)/255
    fibre = patch-ndimage.gaussian_filter(patch, 4)
    fibre = np.tile(fibre, (13,12))[:3762,:3762]
    subject = np.maximum(rock, forest)
    result += fibre[...,None]*(.10+.30*rock+.18*forest)[...,None]*(1-water[...,None]*.65)
    # Sharpen the source ink locally; this recovers line contrast, not new detail.
    contour_detail = value-ndimage.gaussian_filter(value, .8)
    result += contour_detail[...,None]*.20*subject[...,None]*(1-water[...,None])
    # Slightly unify the original dark contour ink; no edge displacement or blur.
    dark_line = (1-smoothstep(.20,.34,value))*(1-water)
    line_ink = tint_at_value([.28,.32,.31], np.clip(.18+value*.35,.17,.32))
    result = result*(1-dark_line[...,None]*.36)+line_ink*dark_line[...,None]*.36
    pixels = np.rint(np.clip(result,0,1)*255).astype(np.uint8)
    master = Image.fromarray(pixels)
    master_path = BASE/'master_8000.png'
    master.save(master_path)
    master.resize((1881,1881), Image.Resampling.LANCZOS).save(BASE/'preview_1881.png')
    folder = BASE/'tiles'
    folder.mkdir(exist_ok=True)
    records=[]
    for row in range(3):
        for column in range(3):
            box=(column*1254,row*1254,(column+1)*1254,(row+1)*1254)
            tile=master.crop(box)
            path=folder/f'bc_r{row}c{column}.png'
            tile.save(path)
            with Image.open(path) as saved:
                if ImageChops.difference(tile,saved).getbbox() is not None:
                    raise ValueError('Tile packaging changed the styled pixels.')
            records.append({'file':path.name,'rectangle':box,'pixels':[1254,1254],'sha256':sha(path),'pixel_mismatches':0})
    record={'method':'Manual colour mapping, contour-ink tuning and own-paper grain; no image generation.',
            'source':str(SOURCE),'source_sha256':sha(SOURCE),'source_pixels':[3762,3762],
            'master_pixels':[3762,3762],'runtime_grid':[3,3],'spatial_resampling_of_art':False,
            'geometry':'Source mountain, woodland, river and coast positions/proportions retained pixel-for-pixel.',
            'contour_refinement':'Local source-luminance contrast at sigma 0.8; no new contours or geometry.',
            'master_sha256':sha(master_path),'paper_source':str(PAPER),'tiles':records}
    (BASE/'manual_style_record.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'master':str(master_path),'sha256':record['master_sha256'],'tiles':len(records),'geometry_resampled':False}))


if __name__=='__main__':
    main()
