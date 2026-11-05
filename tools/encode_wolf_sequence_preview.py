"""Package real Godot captures and the original whole-wolf sprite sheets."""
from pathlib import Path
import argparse
import json
import shutil
import subprocess
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "output/forest_wolf_sequences_20261104"
TARGET = ROOT / "docs/美术规范/局内凶狼序列帧正式改版_20261104"
ART = ROOT / "tools/action_battle_test/art/forest_wolf_frames_v2"


def encode_gif(paths, destination, fps, size=(960, 600)):
    # This is a review video of rendered frames, never a modification of art.
    frames = []
    for path in paths:
        with Image.open(path) as im:
            frames.append(im.convert("RGB").resize(size, Image.Resampling.LANCZOS))
    samples = Image.new("RGB", (size[0], size[1] * 8))
    for row in range(8):
        samples.paste(frames[round(row * (len(frames) - 1) / 7)], (0, row * size[1]))
    palette = samples.quantize(colors=256)
    indexed = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in frames]
    durations = [round((i + 1) * 100 / fps) * 10 - round(i * 100 / fps) * 10
                 for i in range(len(indexed))]
    indexed[0].save(destination, save_all=True, append_images=indexed[1:],
                    duration=durations, loop=0, disposal=1, optimize=True)
    with Image.open(destination) as result:
        stored_duration = 0
        for i in range(result.n_frames):
            result.seek(i)
            stored_duration += result.info["duration"]
        assert stored_duration == sum(durations)
    return {"input_capture_frames": len(paths), "duration_ms": sum(durations),
            "bytes": destination.stat().st_size, "fps": fps}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg", required=True)
    args = parser.parse_args()
    TARGET.mkdir(parents=True, exist_ok=True)
    report = {}
    for name, count, fps in [("motion", 240, 60), ("actions", 330, 30)]:
        paths = sorted((SOURCE / name / "frames").glob("frame_*.png"))
        assert len(paths) == count, (name, len(paths))
        capture = json.loads((SOURCE / name / "capture_report.json").read_text("utf-8"))
        assert capture["complete"] and capture["frames"] == count and capture["fps"] == fps
        video = TARGET / f"wolf_sequence_{name}.mp4"
        subprocess.run([args.ffmpeg, "-y", "-v", "error", "-framerate", str(fps),
                        "-i", str(SOURCE / name / "frames/frame_%04d.png"),
                        "-frames:v", str(count), "-c:v", "libx264", "-threads", "2",
                        "-preset", "fast", "-crf", "20", "-pix_fmt", "yuv420p",
                        "-movflags", "+faststart", "-an", str(video)], check=True)
        decoded = subprocess.run([args.ffmpeg, "-v", "error", "-i", str(video),
                                  "-f", "null", "-", "-progress", "pipe:1", "-nostats"],
                                 check=True, capture_output=True, text=True)
        decoded_count = [int(line.split("=", 1)[1]) for line in decoded.stdout.splitlines()
                         if line.startswith("frame=")][-1]
        assert decoded_count == count, (name, decoded_count)
        report[name] = {"video_frames": count, "decoded_frames": decoded_count,
                        "video_fps": fps, "video_bytes": video.stat().st_size}
        # GIF has centisecond timing; the 60fps original is retained in MP4.
        gif_paths = paths if fps == 30 else paths[::2]
        report[name]["gif"] = encode_gif(gif_paths, TARGET / f"wolf_sequence_{name}.gif", 30)
        print(name, report[name], flush=True)
    shutil.copyfile(SOURCE / "motion/preview.png", TARGET / "sequence_preview.png")
    shutil.copyfile(SOURCE / "actions/pose_0162.png", TARGET / "bite_preview.png")
    sheets = ["walk16", "trot16", "idle12", "bite24", "hurt_death18"]
    files = ["sequence.json", "sheet_qa.json", "forest_wolf_sequences.tres"]
    for sheet in sheets:
        files.extend([f"{sheet}.png", f"{sheet}_texture.res", f"{sheet}.prompt.txt"])
    archive = TARGET / "forest_wolf_sequences_86.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
        for name in files:
            package.write(ART / name, f"forest_wolf_frames_v2/{name}")
        package.write(ROOT / "tools/action_battle_test/forest_wolf_sequence.gd", "forest_wolf_sequence.gd")
        package.writestr("README.txt", """Whole-wolf sequence pack / 86 original cels

idle 12; walk 16; trot 16; windup 8; lunge 6; recover 10; hurt 6; death 12.
Source sheets are transparent original PNGs, read in row-major order.
SpriteFrames and five lossless texture resources are in forest_wolf_frames_v2/.
The SpriteFrames resource preserves raw cell pixels. For consistent body size,
ground registration and game timing, use sequence.json with the supplied player.
Each clip uses one fixed sheet density. Individual cels are never stretched.

Godot: instantiate forest_wolf_sequence.gd, set the parent scale to 0.55,
then call build() with the full path to forest_wolf_frames_v2.
Drive set_motion_context(delta, velocity, state, remaining_time, facing, alive).
windup=0.8s, lunge=0.2s, recover=1.5s; art does not own movement or damage.
Five complete image-generation prompts are included next to their PNG sheets.
""")
    with zipfile.ZipFile(archive) as package:
        assert package.testzip() is None
        assert sum(name.endswith(".png") for name in package.namelist()) == 5
    report["source_cels"] = 86
    report["archive_bytes"] = archive.stat().st_size
    (SOURCE / "preview_package_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("Archive", archive.name, archive.stat().st_size, flush=True)


if __name__ == "__main__":
    main()
