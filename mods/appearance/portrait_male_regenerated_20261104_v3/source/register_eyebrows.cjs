// Register each newly generated pair as ONE whole uniform transform.
// No independent eyebrow movement, masking, recoloring or local distortion.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const crypto = require('crypto');
const pack = path.resolve(__dirname, '..');
const out = path.join(pack, 'male/eyebrows');
const preview = path.join(pack, 'preview');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
fs.mkdirSync(out, {recursive: true});
fs.mkdirSync(preview, {recursive: true});

function bounds(data, width, height, threshold, half = -1) {
  let minx = width, miny = height, maxx = -1, maxy = -1;
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (half >= 0 && (x < width / 2 ? 0 : 1) !== half) continue;
    if (data[(y * width + x) * 4 + 3] < threshold) continue;
    minx = Math.min(minx, x); miny = Math.min(miny, y);
    maxx = Math.max(maxx, x); maxy = Math.max(maxy, y);
  }
  return [minx, miny, maxx, maxy];
}

(async () => {
  const records = [];
  const centersY = [351, 354, 352];
  const panels = [];
  for (let id = 1; id <= 3; id++) {
    const raw = path.join(__dirname, `brow_raw/eyebrows_${String(id).padStart(2, '0')}.png`);
    const {data, info} = await sharp(raw).ensureAlpha().raw().toBuffer({resolveWithObject: true});
    const left = bounds(data, info.width, info.height, 180, 0);
    const right = bounds(data, info.width, info.height, 180, 1);
    const centerLeft = (left[0] + left[2]) / 2;
    const centerRight = (right[0] + right[2]) / 2;
    const requestedScale = 106 / (centerRight - centerLeft);
    const size = Math.round(info.width * requestedScale);
    const scale = size / info.width;
    const sourceX = (centerLeft + centerRight) / 2;
    const sourceY = (left[1] + left[3] + right[1] + right[3]) / 4;
    const x = Math.round(517 - sourceX * scale);
    const y = Math.round(centersY[id - 1] - sourceY * scale);
    const resized = await sharp(raw).resize(size, size, {kernel: 'lanczos3'}).png().toBuffer();
    const output = path.join(out, `${String(id).padStart(2, '0')}.png`);
    await sharp({create: {width: 1024, height: 1536, channels: 4, background: '#00000000'}})
      .composite([{input: resized, left: x, top: y}]).png().toFile(output);
    const result = await sharp(output).ensureAlpha().raw().toBuffer({resolveWithObject: true});
    const targetL = bounds(result.data, 1024, 1536, 180, 0);
    const targetR = bounds(result.data, 1024, 1536, 180, 1);
    const bbox40 = bounds(result.data, 1024, 1536, 40);
    const coreSeparation = (targetR[0] + targetR[2] - targetL[0] - targetL[2]) / 2;
    records.push({index: id, raw: path.relative(pack, raw).replaceAll('\\', '/'), raw_sha256: hash(raw),
      output: path.relative(pack, output).replaceAll('\\', '/'), output_sha256: hash(output),
      raw_canvas: [info.width, info.height], target_canvas: [1024, 1536],
      transform: {uniform_scale: scale, translate: [x, y]},
      requested_pair_center_separation_px: 106, measured_core_pair_separation_px: coreSeparation,
      alpha40_bbox_inclusive: bbox40, alpha40_visible_size: [bbox40[2] - bbox40[0] + 1, bbox40[3] - bbox40[1] + 1],
      alpha180_left_bbox: targetL, alpha180_right_bbox: targetR,
      native_alpha_preserved_except_uniform_resampling: true, no_skin_foundation_added: true,
      no_local_distortion: true, no_independent_left_right_transform: true});
    const face = path.join(pack, 'male/face_shape/01.png');
    for (const background of ['#e7e0d4', '#24282b']) {
      const composed = await sharp({create: {width: 1024, height: 1536, channels: 4, background}})
        .composite([{input: face}, {input: output}]).png().toBuffer();
      const composition = await sharp(composed).extract({left: 390, top: 310, width: 254, height: 95})
        .resize(762, 285).png().toBuffer();
      panels.push(composition);
    }
  }
  await sharp({create: {width: 1524, height: 855, channels: 4, background: '#bcb8af'}})
    .composite(panels.map((input, index) => ({input, left: (index % 2) * 762, top: Math.floor(index / 2) * 285})))
    .png().toFile(path.join(preview, 'three_brows_light_dark.png'));
  fs.writeFileSync(path.join(__dirname, 'brow_registration.json'), JSON.stringify({
    tool: 'built-in image_gen', original_reference_only: true,
    female_canvas_reference: [1024, 1536], female_alpha40_brow_visible_width_px: [167, 179],
    records,
    limitation: 'Generated pair proportions are approximate. Whole-pair registration targets 106px core separation; faint tips may extend beyond the original 175px visible span. No local compression is used to conceal this difference.'
  }, null, 2) + '\n');
  console.log(JSON.stringify(records.map(({index, alpha40_visible_size, measured_core_pair_separation_px}) => ({index, alpha40_visible_size, measured_core_pair_separation_px}))));
})().catch(error => {console.error(error); process.exitCode = 1;});
