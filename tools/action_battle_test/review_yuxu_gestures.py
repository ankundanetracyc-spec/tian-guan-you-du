"""Encode the 26-second in-game gesture capture at its actual elapsed time."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

root = Path(__file__).resolve().parent / "output" / "yuxu_gestures_v21"
records = [json.loads(line) for line in (root / "frames.jsonl").read_text().splitlines()]
assert len(records) == 520 and records[-1]["clip"] == "Run_New1"
assert records[-1]["idle_age"] == 0
for prefix in ["arena", "detail"]:
    paths = [root / f"{prefix}_{i:04d}.{'jpg' if prefix=='arena' else 'png'}" for i in range(520)]
    frames = [Image.open(path).convert("RGB") for path in paths[::2]]
    if prefix == "arena":
        frames[0].save(root / "gestures_26s.webp", save_all=True, append_images=frames[1:], duration=100, loop=0, quality=78, method=4)
        frames = [frame.resize((960,540),Image.Resampling.LANCZOS) for frame in frames]
    frames = [frame.quantize(colors=96,dither=Image.Dither.NONE) for frame in frames]
    frames[0].save(root / f"{prefix}_26s.gif", save_all=True, append_images=frames[1:], duration=100, loop=0, disposal=2)
for second in range(26):
    sheet = Image.new("RGB",(1100,1088),"#58646a")
    draw = ImageDraw.Draw(sheet)
    for offset in range(20):
        index = second*20+offset
        with Image.open(root / f"detail_{index:04d}.png") as source:
            x,y=(offset%5)*220,(offset//5)*272
            sheet.paste(source.convert("RGB").resize((220,250),Image.Resampling.LANCZOS),(x,y))
            draw.text((x+4,y+251),f"{index:03d} / {records[index]['seconds']:.2f}s",fill="white")
    sheet.save(root / f"continuous_{second:02d}.jpg",quality=94)
print("520 real frames checked; 26-second normal-speed loops and complete review sheets written.")
