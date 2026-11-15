"""Package the final registered-HD native capture without resampling its pixels."""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
MODEL = ROOT / "resources/appearance/guigu_male101"
HD = TOOLS / "art/guigu_male101_ink_hd"
DEFAULT_DEST = ROOT / "docs/美术规范/鬼谷原作网格与素材重构_20261104/本轮实装"
FONT = "C:/Windows/Fonts/msyh.ttc"
RUN_STOP_PHASES = [(None,"静站"),(4,"起步 0.17秒"),(74,"持续跑动 2.50秒"),
                   (92,"松键余势 0.10秒"),(108,"松键 0.63秒"),(148,"停步 1.97秒")]


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def shot(source: Path, mode: str, frame: int | None) -> Image.Image:
    tag = "idle" if frame is None else f"{frame:04d}"
    return Image.open(source / f"{mode}_{tag}.png").convert("RGB")


def fixed_crop(image: Image.Image, center: tuple[float,float], size: tuple[int,int]) -> tuple[Image.Image,list[int]]:
    width,height = size
    if width>image.width or height>image.height: raise ValueError("Requested native crop exceeds source")
    left = max(0,min(image.width-width,round(center[0]-width/2)))
    top = max(0,min(image.height-height,round(center[1]-height/2)))
    rect = [left,top,left+width,top+height]
    return image.crop(rect),rect


def board(cells: list[tuple[Image.Image,str]], columns: int, destination: Path, font_size: int = 18) -> None:
    width,height = cells[0][0].size
    if any(im.size!=(width,height) for im,_ in cells): raise ValueError("Mixed cell sizes")
    rows = math.ceil(len(cells)/columns)
    header = 36
    image = Image.new("RGB",(width*columns,(height+header)*rows),"#424d53")
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(FONT,font_size)
    for index,(cell,label) in enumerate(cells):
        x,y = index%columns*width,index//columns*(height+header)
        image.paste(cell,(x,y+header))
        draw.text((x+7,y+6),label,fill="#edf0eb",font=font)
    image.save(destination)


def encode_gif(frames: list[Image.Image], path: Path, seconds: float = 5.) -> dict:
    width,height = frames[0].size
    samples = frames[::max(1,len(frames)//15)]
    training = Image.new("RGB",(width,height*len(samples)))
    for index,image in enumerate(samples): training.paste(image,(0,index*height))
    palette = training.quantize(colors=256,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE)
    encoded = [image.quantize(palette=palette,dither=Image.Dither.NONE) for image in frames]
    total = seconds*1000
    durations = [round((i+1)*total/len(frames)/10)*10-round(i*total/len(frames)/10)*10 for i in range(len(frames))]
    encoded[0].save(path,save_all=True,append_images=encoded[1:],duration=durations,loop=0,
                    optimize=True,disposal=1)
    with Image.open(path) as result:
        duration = 0
        for index in range(result.n_frames):
            result.seek(index)
            duration += int(result.info["duration"])
        if duration != round(total): raise ValueError(f"GIF duration changed: {path.name}: {duration}")
        return {"encodedFrames":result.n_frames,"sourceFrames":len(frames),"durationMs":duration,
                "size":[width,height],"paletteColors":256,"dithering":False,"spatialResampling":False}


def expected_input(frame: int) -> tuple[list[int],int]:
    if frame<90: return [1,0],1
    if frame<150: return [0,0],1
    if frame<162: return [-1,0],1
    if frame<174: return [-1,0],-1
    return [0,0],-1


def validate(source: Path) -> tuple[list[dict],dict,dict]:
    rows = [json.loads(line) for line in (source/"frames.jsonl").read_text(encoding="utf-8").splitlines()]
    meta = json.loads((source/"capture_source.json").read_text(encoding="utf-8"))
    idle = json.loads((source/"idle_record.json").read_text(encoding="utf-8"))
    if len(rows)!=192 or [row["frame"] for row in rows]!=list(range(192)):
        raise ValueError("Expected the complete ordered 192-frame take")
    if meta.get("probe_only",True): raise ValueError("Probe capture cannot be packaged as the final take")
    paths = {
        "new_source_sha256":TOOLS/"guigu_model_copy_skin.gd",
        "old_source_sha256":TOOLS/"reference_walk_skin.gd",
        "model_data_sha256":MODEL/"model_frames_20261104.json",
        "hd_registration_sha256":MODEL/"hd_registration_20261104.json",
        "capture_script_sha256":TOOLS/"capture_model_hd.gd",
        "capture_scene_sha256":TOOLS/"capture_model_hd.tscn",
    }
    if meta.get("hd_manifest_present",False): paths["hd_manifest_sha256"] = HD/"manifest.json"
    for key,path in paths.items():
        if not meta.get(key) or digest(path)!=meta[key]: raise ValueError(f"Capture/current-source mismatch: {key}")
    for sprite_id,expected in meta["hd_textures_sha256"].items():
        if digest(HD/f"sprite_{sprite_id}.png")!=expected: raise ValueError(f"HD texture changed after capture: {sprite_id}")
    for row in rows:
        if row["base_speed"]!=114 or row["stamina"]!=6 or row["hp"]!=100:
            raise ValueError(f"Gameplay state changed at frame {row['frame']}")
        direction,facing = expected_input(row["frame"])
        if row.get("input_direction")!=direction or row.get("driver_facing")!=facing:
            raise ValueError(f"Replay input mismatch at frame {row['frame']}")
        moving = any(direction)
        speed = row["actual_speed"]
        if (moving and abs(speed-114)>.03) or (not moving and abs(speed)>.001):
            raise ValueError(f"Actual movement mismatch at frame {row['frame']}")
        if row.get("captured") and "native" not in row: raise ValueError("Native metadata missing")
    for frame in range(0,150,2):
        for mode in ("old","new","arena","compare"):
            if not (source/f"{mode}_{frame:04d}.png").is_file(): raise ValueError("Incomplete 5-second PNG take")
    return rows,meta,idle


def pick_stride(rows: list[dict],source: Path) -> list[dict]:
    candidates = [row for row in rows[54:91] if (source/f"new_{row['frame']:04d}.png").is_file()]
    def phase(row): return float(row["profiles"]["new"]["garment"]["source_phase"])
    wraps = [index for index in range(1,len(candidates)) if phase(candidates[index])<phase(candidates[index-1])-.5]
    if len(wraps)<2: raise ValueError("Complete consecutive source gait cycle was not captured")
    cycle = candidates[wraps[0]:wraps[1]]
    selected = []
    for index in range(6):
        target = index/6
        chosen = min(cycle,key=lambda row: abs((phase(row)-target+.5)%1-.5))
        selected.append({"target_phase":target,"actual_phase":phase(chosen),"frame":chosen["frame"]})
    if len({value["frame"] for value in selected})!=6: raise ValueError("Six gait phases are not distinct")
    return selected


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source",nargs="?",default="guigu_model_hd_final")
    parser.add_argument("--destination",type=Path,default=DEFAULT_DEST)
    parser.add_argument("--verify-only",action="store_true")
    args = parser.parse_args()
    source = Path(args.source)
    if not source.is_absolute(): source = TOOLS/"output"/source
    rows,meta,idle = validate(source)
    stride = pick_stride(rows,source)
    if args.verify_only:
        print(json.dumps({"status":"PASS","frames":len(rows),"source":str(source),"sixStridePhases":stride},ensure_ascii=False))
        return
    dest = args.destination
    dest.mkdir(parents=True,exist_ok=True)
    gifs = {}
    names = {"new":"高清注册角色_跑动3秒与停步2秒.gif",
             "compare":"真实改前与本轮实装_同输入5秒.gif",
             "arena":"实际战场原生尺寸_跑停5秒.gif"}
    for mode,name in names.items():
        gifs[name] = encode_gif([shot(source,mode,frame) for frame in range(0,150,2)],dest/name)
    native_crops, native_rects = [], {}
    for frame in range(0,150,2):
        record = rows[frame]
        foot = record["native"]["foot_origin"]
        crop,rect = fixed_crop(shot(source,"arena",frame),(foot[0],foot[1]-57),(180,150))
        native_crops.append(crop)
        native_rects[str(frame)] = rect
    gifs["角色实际尺寸未放大_跑停5秒.gif"] = encode_gif(native_crops,dest/"角色实际尺寸未放大_跑停5秒.gif")

    pose_cells, native_cells, crop_records = [], [], []
    for frame,label in RUN_STOP_PHASES:
        record = idle if frame is None else rows[frame]
        pose_cells.append((shot(source,"new",frame),label))
        foot = record["native"]["foot_origin"]
        crop,rect = fixed_crop(shot(source,"arena",frame),(foot[0],foot[1]-57),(180,150))
        native_cells.append((crop,label))
        crop_records.append({"frame":frame,"label":label,"native_rect":rect,"resampled":False})
    board(pose_cells,3,dest/"静站跑动停步_六相位.png")
    board(native_cells,3,dest/"实际尺寸未放大_站跑停六相位.png",16)
    board([(shot(source,"new",value["frame"]),f"源相位 {value['actual_phase']:.3f} · 第{value['frame']}帧") for value in stride],3,dest/"完整步态_六相位.png")

    shoulder_cells, shoulder_new, shoulder_records = [], [], []
    for frame,label in [(None,"静站"),(74,"跑动"),(148,"停稳")]:
        record = idle if frame is None else rows[frame]
        for mode in ("old","new"):
            points = record["profiles"][mode]["screen_points"]
            near,far = points["near_upper"],points["far_upper"]
            center = ((near[0]+far[0])/2,(near[1]+far[1])/2+26)
            crop,rect = fixed_crop(shot(source,mode,frame),center,(180,152))
            text = label+" · "+("真实改前" if mode=="old" else "本轮圆肩与接缝")
            shoulder_cells.append((crop,text))
            if mode=="new": shoulder_new.append((crop,label))
            shoulder_records.append({"frame":frame,"mode":mode,"source_rect":rect,"resampled":False})
    board(shoulder_cells,2,dest/"圆肩接缝_静站跑动停稳原生近景对照.png",15)
    board(shoulder_new,3,dest/"圆肩接缝_新版站跑停.png",16)

    selected = {value["frame"] for value in stride}
    selected.update(frame for frame,_ in RUN_STOP_PHASES if frame is not None)
    selected.update((150,160,162,170,174,190,191))
    selected_files = []
    for frame in sorted(selected):
        for mode in ("new","arena"):
            path = source/f"{mode}_{frame:04d}.png"
            if path.exists():
                shutil.copy2(path,dest/path.name)
                selected_files.append(path.name)
    shutil.copy2(source/"new_idle.png",dest/"原生静站.png")
    for name in ("capture_source.json","idle_record.json","frames.jsonl"):
        shutil.copy2(source/name,dest/name)
    shutil.copy2(MODEL/"hd_registration_20261104.json",dest/"本轮高清材质注册.json")
    report = {
        "capture":meta,"source_capture":source.relative_to(ROOT).as_posix(),
        "diagnostic_frames":192,"run_seconds":3,"release_seconds":2,
        "input_hp_stamina_checks":"PASS: recorded directions/facing, speed114 when moving, zero when released, HP100 and stamina6 throughout",
        "comparison":"Actual reference_walk_skin.gd at captured hash versus source-model registered-HD adapter; anatomical or pixel equality is not required",
        "native_pixels_repainted":False,"native_crops_resampled":False,
        "labels_outside_source_crops":True,"gif_encoding":gifs,
        "complete_stride_six_phases":stride,"standing_run_stop_crops":crop_records,
        "round_shoulder_crops":shoulder_records,"native_animation_crop_rects":native_rects,
        "selected_original_pngs":selected_files,
        "artifact_hashes":{path.name:{"sha256":digest(path),"bytes":path.stat().st_size} for path in sorted(dest.iterdir()) if path.suffix in (".png",".gif")},
    }
    (dest/"本轮原生捕捉与注册验收.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps({"status":"PASS","destination":str(dest),"frames":192,"gifCount":len(gifs),"stridePhases":stride},ensure_ascii=False))


if __name__=="__main__": main()
