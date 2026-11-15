"""Package native AI regional refinements with a shared composition edge band."""
from pathlib import Path
import json, zipfile
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'textures/world/bai_chuan/pale_nine_20261030'
DOC = ROOT / 'docs/美术规范/百川提浅九块底图_20261030'
DOC.mkdir(parents=True, exist_ok=True)
records = json.loads((ART / 'all_generation_records.json').read_text(encoding='utf-8'))
sizes = [Image.open(ART/'generated'/f"{q['id']}.png").size for q in records]
assert len(records) == 9 and len(set(sizes)) == 1
N = sizes[0][0]
assert sizes[0][0] == sizes[0][1]
source = Image.open(ROOT/'textures/world/bai_chuan/ink_depth_20261030/bai_chuan_pale_ground_depth_v8.png').convert('RGB')
reference = source.resize((N*3, N*3), Image.Resampling.LANCZOS)
raw = np.array(Image.open(ART/'raw_join.png').convert('RGB'))
canvas = np.empty_like(raw)
manifest = []
for q in records:
    r,c = q['row'],q['col']
    x,y = c*N,r*N
    native = np.array(Image.open(ART/'generated'/f"{q['id']}.png").convert('RGB')).astype(np.float32)
    guide = np.array(reference.crop((x,y,x+N,y+N))).astype(np.float32)
    # A small common edge band retains the original continuous geometry.
    # It is explicitly recorded as reference-derived, not native new detail.
    yy,xx = np.mgrid[:N,:N]
    dist = np.full((N,N),N,dtype=np.float32)
    if c: dist=np.minimum(dist,xx)
    if c<2: dist=np.minimum(dist,N-1-xx)
    if r: dist=np.minimum(dist,yy)
    if r<2: dist=np.minimum(dist,N-1-yy)
    t=np.clip((dist-6)/42,0,1)
    w=t*t*(3-2*t)
    pixels=np.rint(native*w[...,None]+guide*(1-w[...,None])).clip(0,255).astype('uint8')
    tile=Image.fromarray(pixels)
    tile.save(ART/'tiles'/f"{q['id']}.png")
    canvas[y:y+N,x:x+N]=pixels
    manifest.append({'id':q['id'],'row':r,'col':c,'native_pixels':[N,N],
                     'tile':f"tiles/{q['id']}.png",'generated':f"generated/{q['id']}.png",
                     'shared_reference_edge_pixels':6,'edge_transition_pixels':42,
                     'mean_reference_weight':float((1-w).mean())})
    (DOC/f"{q['id']}_提示词.txt").write_text(q['prompt'],encoding='utf-8')
master=Image.fromarray(canvas)
master.save(ART/'bai_chuan_nine_master_3762.png')
master.resize((1536,1536),Image.Resampling.LANCZOS).save(DOC/'百川九块整图预览.jpg',quality=95)
contact=master.resize((1500,1500),Image.Resampling.LANCZOS)
draw=ImageDraw.Draw(contact)
for v in [500,1000]:
    draw.line((v,0,v,1499),fill='#a56745',width=2)
    draw.line((0,v,1499,v),fill='#a56745',width=2)
for q in records:
    x,y=q['col']*500+10,q['row']*500+10
    draw.rectangle((x,y,x+52,y+24),fill='#f8f3df')
    draw.text((x+6,y+5),q['id'],fill='#413b30')
contact.save(DOC/'九块编号总览.jpg',quality=95)
seams=Image.new('RGB',(768,768),'#f4f0df')
k=0
for axis in ['vertical','horizontal']:
    for boundary in [N,2*N]:
        for segment in range(3):
            if axis=='vertical': box=(boundary-96,segment*N,boundary+96,(segment+1)*N)
            else: box=(segment*N,boundary-96,(segment+1)*N,boundary+96)
            patch=master.crop(box)
            if axis=='vertical': patch=patch.transpose(Image.Transpose.ROTATE_90)
            patch.resize((384,96),Image.Resampling.LANCZOS).save(DOC/f'接边_{k+1:02d}.png')
            seams.paste(patch.resize((384,96),Image.Resampling.LANCZOS),((k%2)*384,(k//2)*128))
            k+=1
seams.save(DOC/'十二处接边检查.jpg',quality=95)
assert np.array_equal(np.array(Image.open(ART/'tiles'/'bc_22.png')),canvas[N:2*N,N:2*N])
checks=[]
for axis in [0,1]:
    for boundary in [N,2*N]:
        a=np.take(canvas,boundary-1,axis=axis).astype(float)
        b=np.take(canvas,boundary,axis=axis).astype(float)
        checks.append({'axis':axis,'boundary':boundary,'mean_adjacent_rgb_difference':float(np.abs(a-b).mean())})
report={'grid':[3,3],'native_tile_pixels':[N,N],'master_pixels':[3*N,3*N],
        'generated_assets':9,'upscaled_native_assets':0,'production_2048_ready':False,
        'method':'Nine native AI refinements; 6px common composition edge plus 42px smooth transition; native interiors retained.',
        'reference_edge_detail':'Shared edge bands derive from the 1254 whole-map reference; they do not add native detail.',
        'checks':checks,'tiles':manifest,
        'limits':['Native size below requested 2048.','Seam metrics do not prove visual approval.','No formal node placement or runtime integration.']}
(DOC/'尺寸与接边记录.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(DOC/'百川九块底图_1254.zip','w',zipfile.ZIP_DEFLATED) as z:
    for q in records: z.write(ART/'tiles'/f"{q['id']}.png",f"{q['id']}.png")
    z.write(DOC/'尺寸与接边记录.json','manifest.json')
print(json.dumps({'generated':9,'tile_size':N,'master_size':N*3,'seam_checks':checks},ensure_ascii=False))
