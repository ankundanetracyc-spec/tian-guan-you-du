"""Encode actual Godot captures; do not edit original character artwork."""
from pathlib import Path
from PIL import Image, ImageDraw
import sys

root=Path(__file__).resolve().parents[1]
variant="close" if "--close" in sys.argv else "battle"
revision5="--v5" in sys.argv
revision3="--v3" in sys.argv or revision5
revision2="--v2" in sys.argv or revision3
prefix="queue_small_v5_" if revision5 else "queue_small_v3_" if revision3 else "queue_small_v2_" if revision2 else "queue_small_"
count=180 if revision5 else 160 if revision2 else 200
source=root/f"tmp/{prefix}{variant}_capture"
output=root/"docs/美术规范/行动队列局内形象_20261103"
files=sorted(source.glob("motion_*.png"))
if revision2 and variant=="battle":
    assert 120 <= len(files) <= 1000, len(files)
    count=len(files)
else:
    assert len(files)==count, len(files)
size=(960,540) if variant=="close" else (1280,720)
frames=[]
palette=None
for f in files:
    with Image.open(f) as image:
        frame=image.convert("RGB").resize(size,Image.Resampling.LANCZOS)
        if palette is None:
            palette=frame.quantize(colors=256)
        frames.append(frame.quantize(palette=palette,dither=Image.Dither.NONE))
filename=f"{prefix}{variant}_motion.gif" if revision2 else f"{prefix}{variant}_{count//20}s.gif"
frames[0].save(output/filename,save_all=True,
               append_images=frames[1:],duration=50,loop=0,disposal=1,optimize=False)
if variant=="close":
    phases=([(25,"Approach: glide" if revision3 else "Approach: short steps"),(34,"Attack: contact"),(48,"Retreat: glide" if revision3 else "Retreat: backward steps"),
             (60,"Hurt: start"),(64,"Hurt: recoil"),(78,"Hurt: recover"),
             (104,"Dodge: early"),(111,"Dodge: middle"),(118,"Dodge: recover")]
            if revision2 else [(0,"Idle"),(23,"Attack: windup"),(26,"Attack: contact"),(32,"Return"),
            (54,"Hurt"),(86,"Guard"),(125,"Dodge"),(180,"Idle restored")])
    if revision5:
        phases=[(22,"Forward: push off"),(28,"Forward: airborne"),(34,"Forward: landing"),
                (53,"Backward: push off"),(59,"Backward: airborne"),(65,"Backward: landing"),
                (42,"Attack: contact"),(84,"Hurt: recoil"),(131,"Dodge")]
    columns=3 if revision2 else 4
    sheet=Image.new("RGB",(columns*380,(3 if revision2 else 2)*560),"#ddd9ca")
    draw=ImageDraw.Draw(sheet)
    for k,(index,label) in enumerate(phases):
        x,y=(k%columns)*380,(k//columns)*560
        with Image.open(files[index]) as image:
            left=340
            if revision2:
                t=index/20
                speed=38*420/((1474-75)*16/186)
                offset=speed*(t-1) if 1<=t<1.4 else speed*.4 if 1.4<=t<2.155 else speed*(2.555-t) if 2.155<=t<2.555 else 0
                if revision3:
                    p=(t-1)/.4 if t<1.4 else (t-2.155)/.4
                    eased=4*p**3 if p<.5 else 1-(-2*p+2)**3/2
                    offset=100*eased if 1<=t<1.4 else 100 if 1.4<=t<2.155 else 100*(1-eased) if 2.155<=t<2.555 else 0
                if revision5:
                    p=max(0,min(1,((t-1) if t<1.8 else (t-2.555))/.8))
                    h=max(0,min(1,(p-.15)/.67))
                    h=h*h*(3-2*h)
                    offset=140*h if 1<=t<1.8 else 140 if 1.8<=t<2.555 else 140*(1-h) if 2.555<=t<3.355 else 0
                left=round(320+offset-140)
            sheet.paste(image.convert("RGB").crop((left,165 if revision5 else 200,left+380,695 if revision5 else 730)),(x,y+30))
        draw.text((x+8,y+8),label,fill="#182b2a")
    sheet.save(output/f"{prefix}action_contact_sheet.png")
print(f"Encoded {variant}:{count}frames,{count*50}ms. Source frames preserved.")
