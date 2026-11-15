"""Encode unmodified Godot blood-wave captures for visual review."""

import argparse
from pathlib import Path

from PIL import Image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--fps", type=int, default=30)
    args = parser.parse_args()
    files = sorted(args.source.glob("frame_*.png"))
    if not files:
        raise SystemExit(f"No rendered frames in {args.source}")
    args.destination.mkdir(parents=True, exist_ok=True)
    frames = []
    for path in files:
        with Image.open(path) as image:
            frame = image.convert("RGB")
            frame.thumbnail((1100, 760), Image.Resampling.LANCZOS)
            frames.append(frame)
    # A palette sampled across the entire motion preserves the dark red ink.
    sample = Image.new("RGB", (frames[0].width, frames[0].height * 5))
    for row, fraction in enumerate((0.0, 0.2, 0.4, 0.6, 0.8)):
        sample.paste(frames[min(len(frames) - 1, int(len(frames) * fraction))],
                     (0, row * frames[0].height))
    palette = sample.quantize(colors=256)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE)
               for frame in frames]
    # GIF duration uses 10 ms units; alternate rounded frame boundaries.
    durations = [round((i + 1) * 100 / args.fps) * 10
                 - round(i * 100 / args.fps) * 10 for i in range(len(indexed))]
    indexed[0].save(args.destination / "blood_wave_ink.gif", save_all=True,
                    append_images=indexed[1:], duration=durations,
                    loop=0, disposal=1, optimize=True)
    print(f"Encoded {len(files)} actual rendered frames at {args.fps} fps; "
          "no effect pixels retouched.")


if __name__ == "__main__":
    main()
