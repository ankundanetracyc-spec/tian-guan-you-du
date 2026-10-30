"""Crop transparent margins and downsample generated UI skins for nine-slice use."""
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'textures/ui/inventory_index_20261029'
specs=[('index_panel_paper.png',640,1280,44),('index_row_normal.png',960,320,42),('index_row_selected.png',960,320,42)]
records=[]
for name,max_w,max_h,slice_px in specs:
    source=ART/'raw'/name
    with Image.open(source) as image:
        image=image.convert('RGBA')
        original=image.size
        bounds=image.getchannel('A').point(lambda v:255 if v>=2 else 0).getbbox()
        assert bounds, name
        image=image.crop(bounds)
        factor=min(1,max_w/image.width,max_h/image.height)
        size=(max(1,round(image.width*factor)),max(1,round(image.height*factor)))
        if image.size!=size:image=image.resize(size,Image.Resampling.LANCZOS)
        image.save(ART/name)
    records.append({'file':name,'source':str(source),'source_size':list(original),'alpha_bounds':bounds,'size':size,'slice':slice_px,'scale':factor,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
(ART/'ui_manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print('Packed 3 original UI materials, preserving aspect ratio and alpha; no upscaling.')
