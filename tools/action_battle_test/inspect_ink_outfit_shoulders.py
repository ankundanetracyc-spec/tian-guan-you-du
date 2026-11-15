"""Arrange native shoulder snapshots for whole-cycle visual review."""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("capture",type=Path)
    args = parser.parse_args()
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",17)
    for clip in ["Idle","Run_New1","Run_New2"]:
        frames = sorted(path for path in args.capture.glob(clip+"_*.png") if path.stem.rsplit("_",1)[-1].isdigit())
        if len(frames)!=24:
            raise SystemExit(f"Expected 24 native {clip} snapshots; got {len(frames)}")
        page = Image.new("RGB",(1920,1104),"#dddcd1")
        painter = ImageDraw.Draw(page)
        painter.text((15,10),f"{clip} 完整周期肩根观察 · 24相位 · 原生帧缩略排列（不是修改素材）",font=font,fill="#424b48")
        for index,path in enumerate(frames):
            x,y = index%6*320,40+index//6*266
            with Image.open(path) as image:
                page.paste(image.convert("RGB").resize((320,240),Image.Resampling.LANCZOS),(x,y))
            painter.text((x+10,y+241),f"phase {index/24:.4f}",font=font,fill="#424b48")
        page.save(args.capture/(clip+"_shoulder_cycle_sheet.png"))
    print(str(args.capture.resolve()))


if __name__=="__main__":
    main()
