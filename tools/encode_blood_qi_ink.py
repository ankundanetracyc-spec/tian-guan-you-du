"""Encode contact, four-stage, or eight-direction blood-qi capture suites."""

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--stills", action="store_true")
    parser.add_argument("--max-width", type=int, default=1440)
    parser.add_argument("--prefix", default="")
    args = parser.parse_args()
    info = json.loads((args.source / "manifest.json").read_text(encoding="utf-8"))
    args.destination.mkdir(parents=True, exist_ok=True)
    width, height = info["crop"][2:]
    title = 42
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 24)
    columns = info.get("columns", 2)
    rows = info.get("rows", (len(info["cases"]) + columns - 1) // columns)
    suite = info.get("suite", "default")
    prefix = args.prefix or ("blood_qi_ink" if suite == "default" else f"blood_qi_ink_{suite}")

    def grid(frame=None):
        image = Image.new("RGB", (width * columns, (height + title) * rows), "#e9e0ce")
        draw = ImageDraw.Draw(image)
        for index, case in enumerate(info["cases"]):
            column, row = case.get("grid_position", [index % columns, index // columns])
            x, y = column * width, row * (height + title)
            path = (args.source / f'{case["id"]}_peak.png' if frame is None else
                    args.source / case["id"] / f"frame_{frame:03d}.png")
            with Image.open(path) as shot:
                image.paste(shot.convert("RGB"), (x, y + title))
            draw.text((x + 16, y + 5), case["label"], font=font, fill="#4b4840")
        if info.get("center_label"):
            for line, text in enumerate(info["center_label"].splitlines()):
                bounds = draw.textbbox((0, 0), text, font=font)
                x = width + (width - (bounds[2] - bounds[0])) // 2
                y = height + title + (height + title) // 2 - 30 + line * 38
                draw.text((x, y), text, font=font, fill="#4b4840")
        if image.width > args.max_width:
            image = image.resize((args.max_width, round(image.height * args.max_width / image.width)),
                                 Image.Resampling.LANCZOS)
        return image

    grid().save(args.destination / f"{prefix}_peak_grid.png")
    if args.stills or info.get("stills_only"):
        print(f"Saved {len(info['cases'])} real-combat {suite} peak crops; effects were not retouched.")
        return
    frames = [grid(frame) for frame in range(info["frames"])]
    sample = Image.new("RGB", (frames[0].width, frames[0].height * 5))
    for row, fraction in enumerate((0.0, 0.12, 0.27, 0.55, 0.70)):
        sample.paste(frames[min(len(frames) - 1, int(len(frames) * fraction))],
                     (0, row * frames[0].height))
    palette = sample.quantize(colors=256)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    fps = info["fps"]
    durations = [round((i + 1) * 100 / fps) * 10 - round(i * 100 / fps) * 10
                 for i in range(len(indexed))]
    indexed[0].save(args.destination / f"{prefix}_motion_grid.gif", save_all=True,
                    append_images=indexed[1:], duration=durations, loop=0,
                    disposal=1, optimize=True)
    print(f"Encoded {len(frames)} {suite} frames at {fps} fps from real combat.")


if __name__ == "__main__":
    main()
