"""Package the actual pawn VFX room recording as a silent, browser-ready video."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("folder", type=Path)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    args = parser.parse_args()
    folder = args.folder.resolve()
    record = json.loads((folder / "capture_record.json").read_text(encoding="utf-8-sig"))
    assert record["actual_room"] and record["eight_directions"] and len(record["samples"]) == 6
    ffmpeg = str(args.ffmpeg.resolve())
    result = folder / "pawn_ink_force_silent.mp4"
    with (folder / "encode.log").open("w", encoding="utf-8") as log:
        subprocess.run([ffmpeg, "-y", "-hide_banner", "-i", str(folder / "showcase.avi"),
                        "-ss", "0.1", "-map", "0:v:0", "-an", "-vf", "scale=1280:800:flags=lanczos,setsar=1",
                        "-c:v", "libx264", "-crf", "18", "-preset", "medium", "-pix_fmt", "yuv420p",
                        "-r", "60", "-movflags", "+faststart", str(result)], stdout=log, stderr=log, check=True)
    decoded = subprocess.run([ffmpeg, "-hide_banner", "-i", str(result), "-map", "0:v:0", "-f", "null", "-"],
                             capture_output=True, text=True, check=True)
    (folder / "decode.log").write_text(decoded.stderr, encoding="utf-8")
    audio = subprocess.run([ffmpeg, "-hide_banner", "-i", str(result), "-map", "0:a", "-f", "null", "-"],
                           capture_output=True, text=True)
    no_audio = audio.returncode != 0 and "matches no streams" in audio.stderr
    render_log = (folder / "capture.log").read_text(encoding="utf-8-sig")
    no_errors = not any(marker in render_log for marker in ["SCRIPT ERROR", "SHADER ERROR", "ERROR:"])
    checks = {"passed": no_audio and no_errors, "audio_tracks": 0 if no_audio else None,
              "width": 1280, "height": 800, "fps": 60, "six_styles": True, "eight_wave_directions": True,
              "source": "Actual independent Godot art room, no gameplay or audio added",
              "video": result.name, "video_bytes": result.stat().st_size, "gpu_errors": not no_errors}
    (folder / "preview_checks.json").write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding="utf-8")
    assert checks["passed"], checks
    print(json.dumps(checks, ensure_ascii=False))


if __name__ == "__main__":
    main()
