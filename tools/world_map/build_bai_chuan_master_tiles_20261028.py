"""Crop shared source, record AI refinements, and mechanically assemble shared boundaries."""
import hashlib
import json
import shutil
import sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/美术规范/百川母图与九块细化_20261028'
OUT = ROOT / 'textures/world/bai_chuan/nine_v03'
MANIFEST = DOC / '制作清单.json'
PROMPT = '''Enhance ONLY the FIRST reference image, an exact cropped tile of one continuous overworld map. Preserve the FIRST image's composition pixel-for-pixel in spirit: same field of view, same mountain silhouettes and positions, same river centerlines, river widths, shorelines, lake shapes, ground colors and empty spaces. Do not zoom, crop, shift, widen the view, move a bend, add a hill or lake, or invent any objects. The SECOND image is overlapping neighbor CONTEXT ONLY, not another subject to paint or a replacement composition. Output only the FIRST tile, not the wider context.
Re-render the first tile as a clean crisp high-detail square at the largest native square resolution, target1254 square: very fine grey-brown dry ink, simple mountain folds, pale cool-grey ridge wash, restrained grey-sage valley wash and small ochre dry-slope wash, quiet light ivory ground. Keep it flat and sparse, NOT realistic detailed rock faces or lush landscape painting. Clarify existing lines and subtle washes, reduce coarse paper grain, no new landforms. Match original low saturation and lightness exactly. Same miniature map scale. Existing river and lake outline takes priority over prettier new bends. Keep every edge's color and landform continuation exactly faithful to the crop for seamless reassembly.
ABSOLUTELY NO trees, grass, reeds, shrubs, bushes, forest, vegetation dots, rocks, buildings, bridges, roads, dotted paths, node circles, labels, symbols, text, borders, clouds, fog, leather, cast shadows, horizon or watermark. No frame or margin. Terrain-only continuation. Only enhance clarity of THIS supplied crop.'''

def write(data):
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')

def prepare():
    DOC.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    with Image.open(DOC / '百川整体母图_v01.png') as source:
        source = source.convert('RGB')
        width, height = source.size
        assert width == height and width % 3 == 0
        step = width // 3
        jobs = []
        for row in range(3):
            for col in range(3):
                key = f'{row+1}{col+1}'
                rect = (col*step, row*step, (col+1)*step, (row+1)*step)
                context = (max(0,rect[0]-48), max(0,rect[1]-48), min(width,rect[2]+48), min(height,rect[3]+48))
                core_path = DOC / f'裁切引导/core_{key}.png'
                context_path = DOC / f'裁切引导/context_{key}.png'
                source.crop(rect).save(core_path)
                source.crop(context).save(context_path)
                jobs.append({'key':key,'row':row,'col':col,'masterCrop':list(rect),'contextRect':list(context),'core':str(core_path),'context':str(context_path),'prompt':PROMPT,'status':'pending'})
        data={'master':'百川整体母图_v01.png','masterNativeSize':[width,height],'cropNativeSize':[step,step],'targetTileSize':[1254,1254],'sharedBoundaryPixels':96,'transitionPixels':128,'jobs':jobs}
        write(data)
        (DOC/'分块清晰化提示词.txt').write_text(PROMPT, encoding='utf-8')
        print(json.dumps({'master':[width,height],'crop':[step,step],'jobs':9}))

def record(key, src):
    data=json.loads(MANIFEST.read_text(encoding='utf-8'))
    job=next(j for j in data['jobs'] if j['key']==key)
    target=DOC/f'细化原图/refined_{key}.png'
    shutil.copy2(src,target)
    with Image.open(target) as im:
        native=list(im.size)
    job.update(status='refined',raw=str(target),source=src,nativeSize=native)
    write(data)
    print(key,native)

def assemble():
    data=json.loads(MANIFEST.read_text(encoding='utf-8'))
    jobs=data['jobs']
    assert len(jobs)==9 and all(j['status']=='refined' for j in jobs)
    native_sizes=[min(j['nativeSize']) for j in jobs]
    size=min(1254,*native_sizes)
    assert all(j['nativeSize'][0]==j['nativeSize'][1] for j in jobs)
    with Image.open(DOC/'百川整体母图_v01.png') as master:
        base=master.convert('RGB').resize((size*3,size*3),Image.Resampling.LANCZOS)
    whole=Image.new('RGB',base.size)
    locked=96
    transition=128
    axis=np.arange(size,dtype=np.float32)
    def weight(d):
        t=np.clip((d-locked)/transition,0,1)
        return t*t*(3-2*t)
    for j in jobs:
        row,col=j['row'],j['col']
        rect=(col*size,row*size,(col+1)*size,(row+1)*size)
        b=np.asarray(base.crop(rect),dtype=np.float32)
        with Image.open(j['raw']) as im:
            refined=im.convert('RGB')
            if refined.size!=(size,size):
                refined=refined.resize((size,size),Image.Resampling.LANCZOS)
            r=np.asarray(refined,dtype=np.float32)
        # Crop/assembly only. AI creates the details; a common master supplies shared edges.
        wx=np.ones(size,dtype=np.float32)
        wy=np.ones(size,dtype=np.float32)
        if col>0: wx=np.minimum(wx,weight(axis))
        if col<2: wx=np.minimum(wx,weight(size-1-axis))
        if row>0: wy=np.minimum(wy,weight(axis))
        if row<2: wy=np.minimum(wy,weight(size-1-axis))
        mask=np.minimum(wy[:,None],wx[None,:])[...,None]
        composed=np.round(b*(1-mask)+r*mask).clip(0,255).astype(np.uint8)
        tile=Image.fromarray(composed)
        path=OUT/f'bc_{j["key"]}.png'
        tile.save(path)
        whole.paste(tile,(col*size,row*size))
        j['final']=str(path.relative_to(ROOT))
        j['finalSize']=[size,size]
        j['sha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    whole.save(DOC/'百川九块清晰化拼接_v03.png')
    whole.resize((1254,1254),Image.Resampling.LANCZOS).save(OUT/'overview.png')
    # Shared boundary bands must match the same resampled master exactly, not merely look similar.
    a=np.asarray(whole,dtype=np.int16)
    b=np.asarray(base,dtype=np.int16)
    reports=[]
    for seam in [size,size*2]:
        reports.append({'axis':'x','position':seam,'bandMaxError':int(np.abs(a[:,seam-locked:seam+locked]-b[:,seam-locked:seam+locked]).max())})
        reports.append({'axis':'y','position':seam,'bandMaxError':int(np.abs(a[seam-locked:seam+locked]-b[seam-locked:seam+locked]).max())})
    assert all(r['bandMaxError']==0 for r in reports)
    assert len({j['sha256'] for j in jobs})==9
    data.update(finalWorldSize=[size*3,size*3],finalTileSize=[size,size],seamAudit=reports,assembly='AI-refined interiors with master-locked 96px internal boundary bands; smooth 128px transition; not nine independently painted edges')
    write(data)
    (DOC/'接缝验收.json').write_text(json.dumps({'masterNativeSize':data['masterNativeSize'],'finalWorldSize':data['finalWorldSize'],'audit':reports,'uniqueTiles':9},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'tile':size,'world':size*3,'seamMasterErrors':[r['bandMaxError'] for r in reports],'unique':9}))

if sys.argv[1]=='prepare': prepare()
elif sys.argv[1]=='record': record(*sys.argv[2:4])
elif sys.argv[1]=='assemble': assemble()
