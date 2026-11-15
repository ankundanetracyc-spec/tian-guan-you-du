"""Package the locked running/swinging pose from native short captures."""
from pathlib import Path
import hashlib
import json
import math
import shutil
import statistics
import sys
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
SOURCE = TOOLS / "output" / (sys.argv[1] if len(sys.argv)>1 else "running_swing_short_final_v3")
DEST = PROJECT / "docs/美术规范/斗法跑动倾身与交替摆臂_20261104"
DEST.mkdir(parents=True, exist_ok=True)
ROWS = [json.loads(line) for line in (SOURCE / "frames.jsonl").read_text(encoding="utf-8").splitlines()]
IDLE = json.loads((SOURCE / "idle_record.json").read_text(encoding="utf-8"))
assert len(ROWS)==165
assert all(row["stamina"]==6 and row["hp"]==100 and row["base_speed"]==114 for row in ROWS)
assert all(abs(row["actual_speed"]-114)<0.02 for row in ROWS[:120])
assert all(row["actual_speed"]==0 for row in ROWS[120:])
final_hash = hashlib.sha256((TOOLS / "reference_walk_skin.gd").read_bytes()).hexdigest()
assert final_hash == "1daba5359212c43277861c2c8e5eccce2a068e3ae5a8949d9c2d716ebb0d735a"
baseline = TOOLS / "reference_walk_upper_open_baseline_20261104.gd"
baseline_raw = TOOLS / "output/reference_walk_before_running_20261104.gd.txt"
assert baseline.read_bytes()==baseline_raw.read_bytes()

def point(data, name):
    return data["arm_bones"][name]["position"] if name in data["arm_bones"] else data["bone_points"][name]

def corr(a,b):
    aa,bb=statistics.mean(a),statistics.mean(b)
    denominator=math.sqrt(sum((v-aa)**2 for v in a)*sum((v-bb)**2 for v in b))
    return sum((x-aa)*(y-bb) for x,y in zip(a,b))/denominator if denominator>1e-10 else None

def span(values):
    return {"min":min(values),"mean":statistics.mean(values),"max":max(values)}

def observations(mode, start=8, stop=60):
    data=[row["profiles"][mode] for row in ROWS[start:stop]]
    arms,legs={},{}
    result={}
    for side in ("near","far"):
        arms[side]=[point(d,side+"_hand")[0]-point(d,side+"_upper")[0] for d in data]
        legs[side]=[point(d,side+"_foot")[0]-point(d,side+"_thigh")[0] for d in data]
        result[side]={"hand_forward_of_shoulder":span(arms[side]),"same_side_hand_foot_correlation":corr(arms[side],legs[side])}
    order=[a-b for a,b in zip(arms["near"],arms["far"])]
    result["hand_order_changes"]=sum(order[i]*order[i-1]<0 for i in range(1,len(order)))
    result["opposite_arm_correlation"]=corr(arms["near"],arms["far"])
    tilts=[]
    neck_errors=[]
    for d in data:
        ns,fs,hip=point(d,"near_upper"),point(d,"far_upper"),point(d,"pelvis")
        tilts.append(math.degrees(math.atan2((ns[0]+fs[0])/2-hip[0],hip[1]-(ns[1]+fs[1])/2)))
        neck_errors.append(abs(d["neck_chain_distance"]-d["rest_neck_chain_distance"]))
    result["actual_shoulder_center_to_hip_lean_degrees"]=span(tilts)
    result["maximum_neck_bone_length_error"]=max(neck_errors)
    return result

def shot(mode,number):
    tag="idle" if number is None else f"{number:04d}"
    return Image.open(SOURCE / f"{mode}_{tag}.png").convert("RGB")

def native_crop(number):
    r=IDLE if number is None else ROWS[number]
    x,y=r["native"]["foot_origin"]
    return shot("arena",number).crop((round(x)-75,round(y)-130,round(x)+85,round(y)+25))

def encode(mode,start,name):
    images=[shot(mode,n) for n in range(start,start+60,2)]
    w,h=images[0].size
    train=Image.new("RGB",(w,h*10))
    for i,im in enumerate(images[::3]): train.paste(im,(0,i*h))
    palette=train.quantize(colors=128,dither=Image.Dither.NONE)
    images=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in images]
    durations=[round((i+1)*100/15)*10-round(i*100/15)*10 for i in range(30)]
    images[0].save(DEST/name,save_all=True,append_images=images[1:],duration=durations,loop=0,disposal=1,optimize=True)

encode("new",0,"新版倾身跑动_单独近景2秒.gif")
encode("arena",0,"新版倾身跑动_实际局内2秒.gif")
encode("compare",0,"上一版与新版_同输入对照2秒.gif")
encode("new",60,"新版后退_单独近景2秒.gif")
encode("arena",60,"新版后退_实际局内2秒.gif")
font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",18)
small=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",12)

def sheet(samples,mode,name):
    w,h=(160,155) if mode=="native" else (440,500)
    pad=28 if mode=="native" else 32
    output=Image.new("RGB",(w*3,(h+pad)*2),"#424d53")
    for i,(number,label) in enumerate(samples):
        x,y=(i%3)*w,(i//3)*(h+pad)
        output.paste(native_crop(number) if mode=="native" else shot("new",number),(x,y+pad))
        ImageDraw.Draw(output).text((x+8,y+4),label,font=small if mode=="native" else font,fill="#e7e9e4")
    output.save(DEST/name)

def leg_metrics(rows):
    samples=[]
    for row in rows[8:60]:
        d=row["profiles"]["new"]
        hip=d["bone_points"]["near_thigh"]
        ankle=d["bone_points"]["near_foot"]
        knee=d["bone_points"]["near_calf"]
        samples.append({"frame":row["frame"],"phase":d["gait"]["phase"],"ankle_x_from_hip":ankle[0]-hip[0],
                        "knee_x_from_hip":knee[0]-hip[0],"foot_lift":d["contacts"]["near"]["lift"],
                        "pelvis_drop":d["garment"]["reference_pelvis_drop"]})
    values=[r["ankle_x_from_hip"] for r in samples]
    return {"ankle_x_from_hip":span(values),"ankle_total_span":max(values)-min(values),
            "foot_lift":span([r["foot_lift"] for r in samples]),"pelvis_drop":span([r["pelvis_drop"] for r in samples]),"samples":samples}

legs_v3=leg_metrics(ROWS)
native_forward=[r for r in legs_v3["samples"] if r["frame"]%2==0]
phase_choices=[(max(native_forward,key=lambda r:r["ankle_x_from_hip"]),"前伸落步"),
               (min(native_forward,key=lambda r:r["ankle_x_from_hip"]),"后推"),
               (max(native_forward,key=lambda r:r["foot_lift"]),"最高回收采样"),
               (min(native_forward,key=lambda r:abs(r["phase"]-.92)),"回收前送")]
phases=[(r["frame"],label) for r,label in phase_choices]+[(74,"后退一"),(84,"后退二")]
sheet(phases,"new","正常交替摆臂_六相位.png")
sheet(phases,"native","实际游戏尺寸_未缩放六相位.png")
stops=[(120,"停步 0.03秒"),(124,"停步 0.17秒"),(132,"停步 0.43秒"),(144,"停步 0.83秒"),(156,"停步 1.23秒"),(164,"停步 1.50秒")]
sheet(stops,"new","衣摆停步延续与回落_六相位.png")
for number,label in [(18,"换臂一"),(28,"换臂二"),(74,"后退"),(164,"回落")]:
    shutil.copy2(SOURCE/f"arena_{number:04d}.png",DEST/f"实际局内_{label}.png")
    shutil.copy2(SOURCE/f"compare_{number:04d}.png",DEST/f"上轮reference与新版_{label}.png")
shutil.copy2(SOURCE/"new_idle.png",DEST/"新版静止.png")
shutil.copy2(baseline_raw,DEST/"上一版reference源码快照.gd.txt")
reports={}
for source_name,dest_name in [("reference_walk_pose_smoke.json","专用姿态与惯性检查.json"),("reference_walk_actual_contact.json","真实腿长与鞋底接触检查.json"),("reference_arm_phase_smoke.json","双臂腿相位检查.json"),("reference_anchor_drive_smoke.json","髋挂点驱动与衣摆物理检查.json")]:
    p=TOOLS/"output"/source_name
    if p.exists():
        reports[source_name]=json.loads(p.read_text(encoding="utf-8"))
        shutil.copy2(p,DEST/dest_name)

steady=[r["profiles"]["new"] for r in ROWS[30:60]]
tip_x=[d["garment"]["rear_tip"][0]-d["bone_points"]["pelvis"][0] for d in steady]
report={"source_capture":str(SOURCE.relative_to(PROJECT)),"single_speed":114,"ordinary_movement_stamina_cost":0,"diagnostic_frames":len(ROWS),
        "forward_seconds":2,"backward_seconds":2,"stop_review_seconds":1.5,"fixture_initial_distance":650,"ai_and_camera_rules":"unchanged",
        "old_scheme":"exact previous reference snapshot, not cape", "old_source_sha256":hashlib.sha256(baseline.read_bytes()).hexdigest(),
        "new_source_sha256":final_hash,"motion_old":observations("old"),"motion_new":observations("new"),"motion_backward":observations("new",72,120),
        "legs_final_v3":legs_v3,"native_leg_phase_choices":[{"label":label,**r} for r,label in phase_choices],
        "steady_tail_tip_relative_to_hip_x":span(tip_x),
        "stop_samples":[{"frame":n,"angles":ROWS[n]["profiles"]["new"]["garment"]["angles"],"rear_tip":ROWS[n]["profiles"]["new"]["garment"]["rear_tip"]} for n,_ in stops],
        "native_character_pixels_repainted":False,"native_crops_resampled":False,"reports":reports,
        "artifacts":{p.name:p.stat().st_size for p in DEST.iterdir() if p.suffix in (".gif",".png")}}
v2_path=TOOLS/"output/running_swing_short_final_v2/frames.jsonl"
if v2_path.exists():
    v2_rows=[json.loads(line) for line in v2_path.read_text(encoding="utf-8").splitlines()]
    report["legs_previous_v2"]=leg_metrics(v2_rows)
    report["v2_leg_data_source"]=str(v2_path.relative_to(PROJECT))
(DEST/"实际骨向量与动作记录.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
shutil.copy2(SOURCE/"frames.jsonl",DEST/"同输入短录逐帧记录.jsonl")
shutil.copy2(SOURCE/"idle_record.json",DEST/"静止姿态记录.json")
print(json.dumps({"source_hash":final_hash,"old":report["motion_old"],"new":report["motion_new"],"tail_relative_x":report["steady_tail_tip_relative_to_hip_x"],"artifacts":report["artifacts"]},ensure_ascii=False,indent=2))
