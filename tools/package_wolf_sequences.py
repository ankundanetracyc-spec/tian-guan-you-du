"""Describe the original generated cels; do not recolor, warp or redraw pixels."""
from pathlib import Path
from PIL import Image
import numpy as np
import hashlib
import json

root = Path(__file__).resolve().parents[1]
folder = root / "tools/action_battle_test/art/forest_wolf_frames_v2"
grids = {"walk16.png": (4,4), "trot16.png": (4,4), "idle12.png": (4,3),
         "bite24.png": (6,4), "hurt_death18.png": (6,3)}
sheets = {}
qa = {}
for name, (columns, rows) in grids.items():
    im = Image.open(folder / name)
    assert im.mode == "RGBA"
    width, height = im.size
    cell_w, cell_h = width//columns, height//rows
    cells, boxes, hashes = [], [], []
    for index in range(columns*rows):
        x, y = (index%columns)*width//columns, (index//columns)*height//rows
        pixels = np.asarray(im)[y:y+cell_h,x:x+cell_w]
        bbox = Image.fromarray((pixels[:,:,3]>128).astype("uint8")*255).getbbox()
        assert bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<cell_w and bbox[3]<cell_h, (name,index,bbox)
        cells.append({"rect":[x,y,cell_w,cell_h]})
        boxes.append(list(bbox))
        hashes.append(hashlib.sha256(pixels.tobytes()).hexdigest())
    assert len(set(hashes)) == len(hashes), f"Exact duplicate cel in {name}"
    # One density conversion per entire sheet, based on its neutral first cel.
    # It never rescales individual poses; a leap may extend, a corpse may flatten.
    neutral_width = boxes[0][2]-boxes[0][0]
    pixels_to_local = (68.0/0.55)/neutral_width
    ground = boxes[0][3]
    if name in ["walk16.png","trot16.png","idle12.png"]:
        ground = round(float(np.median([b[3] for b in boxes])))
    for index, cell in enumerate(cells):
        # Translation-only cel registration on known planted poses. Airborne
        # bite cels retain their lift; no per-cel body scaling or deformation.
        register_ground = name=="hurt_death18.png" or (name=="bite24.png" and (index<8 or index>=14))
        if register_ground:
            cell["offset"]=[0,ground-boxes[index][3]]
    sheets[name]={"cells":cells,"anchor":[cell_w/2,ground],"canvas_world_size":[cell_w*pixels_to_local,cell_h*pixels_to_local]}
    qa[name]={"dimensions":[width,height],"columns":columns,"rows":rows,"cells":len(cells),
              "unique_exact_cels":len(set(hashes)),"alpha_corners":[im.getpixel((0,0))[3],im.getpixel((width-1,height-1))[3]],
              "cell_boxes":boxes,"neutral_width":neutral_width,"pixels_to_local":pixels_to_local}

def clip(sheet, start, count, fps, loop=False, duration=None, distance=None):
    s=sheets[sheet]
    result={"sheet":sheet,"frame_count":count,"frames":s["cells"][start:start+count],
            "anchor":s["anchor"],"canvas_world_size":s["canvas_world_size"],"fps":fps,"loop":loop}
    if duration is not None: result["duration"]=duration
    if distance is not None: result["cycle_distance_local"]=distance/0.55
    assert len(result["frames"])==count
    return result

clips = {
    "idle":clip("idle12.png",0,12,5,True,2.4),
    "walk":clip("walk16.png",0,16,16,True,distance=14.0),
    "trot":clip("trot16.png",0,16,24,True,distance=28.0),
    "windup":clip("bite24.png",0,8,10,duration=.8),
    "lunge":clip("bite24.png",8,6,30,duration=.2),
    "recover":clip("bite24.png",14,10,10/1.5,duration=1.5),
    "hurt":clip("hurt_death18.png",0,6,6/.28,duration=.28),
    "death":clip("hurt_death18.png",6,12,10,duration=1.2),
}
manifest={"version":2,"backend":"whole_animal_sequence","canvas_world_size":[142,142],
          "clips":clips,"total_source_cels":sum(c["frame_count"] for c in clips.values())}
(folder/"sequence.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
(folder/"sheet_qa.json").write_text(json.dumps({"sheets":qa,"clips":{n:c["frame_count"] for n,c in clips.items()},
    "total_source_cels":manifest["total_source_cels"],"per_frame_scaling":False,
    "registration":"translation only on planted poses"},ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps({n:c["frame_count"] for n,c in clips.items()}), "total=",manifest["total_source_cels"])
