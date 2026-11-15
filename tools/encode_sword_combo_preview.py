"""Arrange native Godot captures; no retouching or redrawing."""
import argparse
from bisect import bisect_right
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CAPTURE = ROOT / 'docs/美术规范/持剑普攻剑式四段_20261105'


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-dir', type=Path, default=DEFAULT_CAPTURE,
                        help='Directory containing capture_manifest.json and stage PNGs.')
    parser.add_argument('--frames-dir', type=Path,
                        help='Override the animation frame directory from the manifest.')
    parser.add_argument('--output-dir', type=Path,
                        help='Encoded assets destination; defaults to --capture-dir.')
    parser.add_argument('--stages-only', action='store_true', help='Encode the four stage grids only.')
    parser.add_argument('--crop', type=int, nargs=4, default=(430, 212, 1080, 664),
                        metavar=('LEFT', 'TOP', 'RIGHT', 'BOTTOM'))
    return parser.parse_args()


def main():
    args = arguments()
    source_dir = args.capture_dir.resolve()
    destination = (args.output_dir or source_dir).resolve()
    destination.mkdir(parents=True, exist_ok=True)
    record = json.loads((source_dir / 'capture_manifest.json').read_text(encoding='utf-8'))
    frames_dir = (args.frames_dir or Path(record['frames_dir'])).resolve()
    native_width, native_height = record['native_size']
    crop = list(args.crop)
    # Preserve the actual captured mesh bounds and the actor; never recreate VFX.
    for stage in [*record['stages'], *record.get('motion_samples', [])]:
        bounds = stage.get('visual_bounds', [])
        if len(bounds) == 4:
            crop = [min(crop[0], int(bounds[0]) - 12),
                    min(crop[1], int(bounds[1]) - 12),
                    max(crop[2], int(bounds[2]) + 13),
                    max(crop[3], int(bounds[3]) + 13)]
    crop = (max(0, crop[0]), max(0, crop[1]),
            min(native_width, crop[2]), min(native_height, crop[3]))
    if crop[2] <= crop[0] or crop[3] <= crop[1]:
        raise ValueError(f'Invalid crop: {crop}')
    fps = float(record.get('fps', 30))
    # GIF stores centiseconds. Round cumulative time rather than rounding each
    # 1/60-second frame to 20 ms, which would slow a 3.4-second take to 4.08 s.
    ticks = [round(i * 100 / fps) for i in range(len(record['frames']) + 1)]
    durations = [(ticks[i + 1] - ticks[i]) * 10 for i in range(len(record['frames']))]
    if durations and min(durations) <= 0:
        raise ValueError('GIF needs a positive centisecond duration for every source frame.')
    outputs = []
    gif_records = []

    def image_frame(filename, region=None, size=None):
        with Image.open(frames_dir / filename) as source:
            frame = source.convert('RGB')
        if region:
            cropped = frame.crop(region)
            frame.close()
            frame = cropped
        if size:
            resized = frame.resize(size, Image.Resampling.LANCZOS)
            frame.close()
            frame = resized
        return frame

    def encode(name, region=None, size=None):
        frames = []
        sample_indices = sorted({min(len(record['frames']) - 1, int(len(record['frames']) * phase))
                                 for phase in (0.0, 0.12, 0.27, 0.42, 0.55, 0.70)})
        samples = [image_frame(record['frames'][i], region, size) for i in sample_indices]
        sample = Image.new('RGB', (samples[0].width, samples[0].height * len(samples)))
        for index, frame in enumerate(samples):
            sample.paste(frame, (0, index * frame.height))
            frame.close()
        palette = sample.quantize(colors=224)
        sample.close()
        for filename in record['frames']:
            frame = image_frame(filename, region, size)
            frames.append(frame.quantize(palette=palette, dither=Image.Dither.NONE))
            frame.close()
        frames[0].save(destination / name, save_all=True, append_images=frames[1:],
                       duration=durations, loop=0, disposal=1, optimize=True)
        # Verify every composed decoded frame against its source palette frame.
        # This catches stale sword/ink pixels when only changed tiles are stored.
        elapsed = 0
        with Image.open(destination / name) as decoded:
            decoded_count = decoded.n_frames
            for index in range(decoded_count):
                decoded.seek(index)
                source_index = max(0, bisect_right(ticks, elapsed // 10) - 1)
                actual = decoded.convert('RGB')
                expected = frames[source_index].convert('RGB')
                difference = ImageChops.difference(actual, expected)
                if difference.getbbox() is not None:
                    raise AssertionError(f'{name}: composed frame {index} differs from source {source_index}')
                actual.close()
                expected.close()
                difference.close()
                elapsed += decoded.info['duration']
        if elapsed != sum(durations):
            raise AssertionError(f'{name}: duration changed from {sum(durations)} to {elapsed} ms')
        gif_records.append({'file': name, 'decoded_frames': decoded_count,
                            'total_milliseconds': elapsed, 'pixel_exact_after_palette': True,
                            'disposal': 1})
        for frame in frames:
            frame.close()
        palette.close()
        outputs.append(name)

    if record['frames'] and not args.stages_only:
        encode('combo_closeup.gif', region=crop)
        encode('combo_full.gif', size=(960, 540))

    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 20)
    cell_width, cell_height = crop[2] - crop[0], crop[3] - crop[1] + 40
    for suffix in ['', '_geometry']:
        sheet = Image.new('RGB', (cell_width * 2, cell_height * 2), (238, 233, 217))
        draw = ImageDraw.Draw(sheet)
        for index in range(4):
            x, y = (index % 2) * cell_width, (index // 2) * cell_height
            release = record['stages'][index]['release']
            title = '%d  %s · 基础伤害%d' % (index + 1, release['stage_name'], release['damage'])
            draw.text((x + 15, y + 7), title, font=font, fill=(41, 60, 57))
            with Image.open(source_dir / ('stage_%d%s.png' % (index + 1, suffix))) as source:
                sheet.paste(source.convert('RGB').crop(crop), (x, y + 40))
        name = 'stages%s_grid.png' % suffix
        sheet.save(destination / name)
        sheet.close()
        outputs.append(name)

    if record['frames'] and not args.stages_only:
        # A timing sheet consists only of actual source frames and labels.
        small_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 14)
        timing_font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 17)
        width = 300
        height = round((crop[3] - crop[1]) * width / (crop[2] - crop[0]))
        timing = Image.new('RGB', (width * 6, (height + 52) * 4), (238, 233, 217))
        draw = ImageDraw.Draw(timing)
        start = 0.0
        timing_frames = []
        for stage, sample in enumerate(record['stages']):
            release = sample['release']
            contact = start + float(release['duration']) * float(release['hit'])
            reveal, life = float(release['reveal']), float(release['life'])
            times = [contact - .075, contact - .012, contact + .025,
                     contact + reveal * .5, contact + reveal, contact + life - .025]
            labels = ['蓄势', '接触前', '穿过接触', '墨锋推进', '显锋末', '尾迹消散']
            selected = []
            for column, (time, label) in enumerate(zip(times, labels)):
                index = max(0, min(len(record['frames']) - 1, round(time * fps) - 1))
                actual_time = (index + 1) / fps
                x, y = column * width, stage * (height + 52)
                draw.text((x + 7, y + 4), f'{stage + 1} {release["stage_name"]} · {label}',
                          font=timing_font, fill=(41, 60, 57))
                draw.text((x + 7, y + 29), f'f{index:03d} t={actual_time:.3f}s 墨龄{actual_time-contact:+.3f}s',
                          font=small_font, fill=(41, 60, 57))
                shot = image_frame(record['frames'][index], crop, (width, height))
                timing.paste(shot, (x, y + 52))
                shot.close()
                selected.append(index)
            timing_frames.append(selected)
            start += float(release['duration'])
        timing.save(destination / 'timing_contact_sheet.png')
        timing.close()
        outputs.append('timing_contact_sheet.png')
    else:
        timing_frames = []

    (destination / 'encoding_manifest.json').write_text(json.dumps({
        'source': str(source_dir / 'capture_manifest.json'),
        'source_frames': len(record['frames']), 'frames_dir': str(frames_dir),
        'native_size': record['native_size'],
        'fps': fps, 'gif_verification': gif_records, 'timing_sample_frames': timing_frames,
        'stages_only': args.stages_only or not record['frames'],
        'total_milliseconds': sum(durations) if record['frames'] and not args.stages_only else 0,
        'requested_crop': args.crop, 'closeup_crop': crop, 'closeup_resize': False,
        'full_size': [960, 540], 'grid': 'native crops with labels, no redrawing',
        'palette': 'constant 224 colors sampled across the real sequence, dithering disabled',
        'outputs': outputs,
    }, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Encoded %d stage grids%s into %s' %
          (2, (' and %d native animation frames' % len(record['frames']))
           if record['frames'] and not args.stages_only else '', destination))


if __name__ == '__main__':
    main()
