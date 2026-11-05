"""Export genuine successful takes as a video with no audio track."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("folder", type=Path)
    parser.add_argument("--ffmpeg", type=Path, required=True)
    parser.add_argument("--existing-video", type=Path, help="Copy a previously cut video and remove its audio")
    args = parser.parse_args()
    folder = args.folder.resolve()
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8-sig"))
    if not manifest.get("passed"):
        raise RuntimeError("The production capture did not pass all requested cases")
    takes = [take for take in manifest["takes"] if take["passed"]]
    rate = float(manifest["fps"])
    filters: list[str] = []
    inputs: list[str] = []
    timeline: list[dict] = []
    output_time = 0.0
    for index, take in enumerate(takes):
        begin = take["start_frame"] / rate
        end = take["end_frame"] / rate
        filters.append(
            f"[0:v]trim=start={begin:.8f}:end={end:.8f},setpts=PTS-STARTPTS,"
            f"scale=1280:720:flags=lanczos,setsar=1[v{index}]"
        )
        inputs.append(f"[v{index}]")
        timeline.append({"case": take["case"], "attempt": take["attempt"],
                         "start": output_time, "end": output_time + end - begin,
                         "original_frames": [take["start_frame"], take["end_frame"]]})
        output_time += end - begin
    filters.append("".join(inputs) + f"concat=n={len(takes)}:v=1:a=0[v]")
    filter_file = folder / "preview_silent_filters.txt"
    filter_file.write_text(";\n".join(filters), encoding="utf-8")
    ffmpeg = str(args.ffmpeg.resolve())
    video = folder / "combat_impact_preview_silent.mp4"
    command = [ffmpeg, "-y", "-hide_banner", "-i", str(folder / "capture.avi"),
               "-filter_complex", ";".join(filters), "-map", "[v]", "-an",
               "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
               "-r", str(int(rate)), "-movflags", "+faststart", str(video)]
    if args.existing_video:
        if args.existing_video.resolve() == video:
            raise ValueError("Input and output video paths must differ")
        command = [ffmpeg, "-y", "-hide_banner", "-i", str(args.existing_video.resolve()),
                   "-map", "0:v:0", "-c:v", "copy", "-an", "-movflags", "+faststart", str(video)]
    with (folder / "encode_silent.log").open("w", encoding="utf-8") as log:
        subprocess.run(command, stdout=log, stderr=log, check=True)
    decoded = subprocess.run([ffmpeg, "-hide_banner", "-i", str(video), "-map", "0:v:0",
                              "-f", "null", "-"], capture_output=True, text=True, check=True)
    (folder / "silent_decode.log").write_text(decoded.stderr, encoding="utf-8")
    audio_probe = subprocess.run([ffmpeg, "-hide_banner", "-i", str(video), "-map", "0:a",
                                  "-f", "null", "-"], capture_output=True, text=True)
    no_audio = audio_probe.returncode != 0 and "matches no streams" in audio_probe.stderr
    checks = {"passed": no_audio,
              "source": "Genuine production takes; video only, no audio track",
              "case_count": len(takes), "duration": output_time, "audio_tracks": 0 if no_audio else None,
              "video_stream_copied": args.existing_video is not None,
              "timeline": timeline, "video": video.name}
    (folder / "preview_silent_checks.json").write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding="utf-8")
    if not checks["passed"]:
        raise RuntimeError(f"Silent preview verification failed: {checks}")
    print(json.dumps({key: value for key, value in checks.items() if key != "timeline"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
