const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const crypto = require('crypto');
const pack = path.resolve(__dirname, '..');
const project = path.resolve(__dirname, '../../../..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004/female');
const WIDTH = 1024, HEIGHT = 1536;
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
const canvas = (background = {r: 0, g: 0, b: 0, alpha: 0}, width = WIDTH, height = HEIGHT) => sharp({create: {width, height, channels: 4, background}});
async function metrics(file) {
  const {data, info} = await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject: true});
  let zero = 0, visible = 0, bbox = [info.width, info.height, -1, -1];
  for (let y = 0; y < info.height; y++) for (let x = 0; x < info.width; x++) {
    const alpha = data[(y * info.width + x) * 4 + 3];
    if (!alpha) zero++;
    if (alpha >= 180) {visible++;bbox = [Math.min(bbox[0], x), Math.min(bbox[1], y), Math.max(bbox[2], x), Math.max(bbox[3], y)];}
  }
  const rowRuns = y => {
    const ranges = []; let from = null;
    for (let x = 0; x <= info.width; x++) {
      const yes = x < info.width && data[(y * info.width + x) * 4 + 3] >= 180;
      if (yes && from === null) from = x;
      if (!yes && from !== null) {ranges.push([from, x - 1]);from = null;}
    }
    return ranges;
  };
  return {width: info.width, height: info.height, channels: info.channels, zeroAlphaPixels: zero, visibleCorePixels: visible, bboxAlpha180: bbox, horizontalRunsAlpha180: Object.fromEntries([103, 155, 256, 300, 350, 450, 515].map(y => [y, rowRuns(y)]))};
}
async function compose(front, back, brow, eye, background, before = false, clothing = 1) {
  const fixed = (part, id) => path.join(active, part, String(id).padStart(2, '0') + '.png');
  const hair = (part, id) => before ? fixed(part, id) : path.join(pack, 'female', part, String(id).padStart(2, '0') + '.png');
  const files = [hair('hair_back', back), before ? fixed('face_shape', 4) : path.join(pack, 'female/face_shape.png'), fixed('clothing', clothing), fixed('eyes', eye), fixed('eyebrows', brow), fixed('nose', 1), fixed('mouth', 1), fixed('face_accessory', 1), hair('hair_front', front)];
  return canvas(background).composite(files.map(input => ({input}))).png().toBuffer();
}
(async () => {
  const specFile = path.join(__dirname, 'hair_registration.json');
  const spec = JSON.parse(fs.readFileSync(specFile, 'utf8'));
  const records = [];
  for (const entry of spec.outputs) {
    const raw = path.join(__dirname, 'raw_hair', entry.part, entry.id + '.png');
    const output = path.join(pack, 'female', entry.part, entry.id + '.png');
    fs.mkdirSync(path.dirname(raw), {recursive: true});fs.mkdirSync(path.dirname(output), {recursive: true});
    if (path.resolve(entry.file) !== path.resolve(raw)) fs.copyFileSync(entry.file, raw);
    if (entry.unchanged) fs.copyFileSync(raw, output);
    else {
      const meta = await sharp(raw).metadata();
      const width = Math.round(meta.width * entry.scale), height = Math.round(meta.height * entry.scale);
      let input = await sharp(raw).resize(width, height, {fit: 'fill', kernel: 'lanczos3'}).png().toBuffer();
      const left = Math.max(0, -entry.x), top = Math.max(0, -entry.y);
      const clippedWidth = Math.min(width - left, WIDTH - Math.max(0, entry.x));
      const clippedHeight = Math.min(height - top, HEIGHT - Math.max(0, entry.y));
      if (left || top || clippedWidth !== width || clippedHeight !== height) input = await sharp(input).extract({left, top, width: clippedWidth, height: clippedHeight}).png().toBuffer();
      await canvas().composite([{input, left: Math.max(0, entry.x), top: Math.max(0, entry.y)}]).png().toFile(output);
    }
    records.push({...entry, rawCopy: raw, output, sha256: hash(fs.readFileSync(output)), metrics: await metrics(output)});
  }
  const preview = path.join(pack, 'preview');fs.mkdirSync(preview, {recursive: true});
  for (const [theme, bg] of [['light', '#ebe5d9'], ['dark', '#252b30']]) {
    const portraits = [];
    for (let i = 1; i <= 3; i++) {
      const img = await compose(i, i, i, i === 2 ? 2 : 1, bg);
      fs.writeFileSync(path.join(preview, 'style_' + String(i).padStart(2, '0') + '_' + theme + '.png'), img);
      portraits.push({input: await sharp(img).extract({left: 196, top: 48, width: 640, height: 1000}).png().toBuffer(), left: (i - 1) * 640, top: 0});
    }
    await canvas(bg, 1920, 1000).composite(portraits).png().toFile(path.join(preview, 'three_styles_' + theme + '.png'));
    const layers = [];
    for (let row = 0; row < 2; row++) for (let col = 0; col < 3; col++) {
      const fullLayer = await canvas(bg).composite([{input: path.join(pack, 'female', row ? 'hair_back' : 'hair_front', String(col + 1).padStart(2, '0') + '.png')}]).png().toBuffer();
      const input = await sharp(fullLayer).extract({left: 0, top: 0, width: 1024, height: 1200}).resize(512, 600).png().toBuffer();
      layers.push({input, left: col * 512, top: row * 600});
    }
    await canvas(bg, 1536, 1200).composite(layers).png().toFile(path.join(preview, 'six_layers_' + theme + '.png'));
    const mixes = [];
    for (let front = 1; front <= 3; front++) for (let back = 1; back <= 3; back++) {
      const input = await sharp(await compose(front, back, 2, 2, bg)).extract({left: 196, top: 48, width: 640, height: 1000}).resize(384, 600).png().toBuffer();
      mixes.push({input, left: (back - 1) * 384, top: (front - 1) * 600});
    }
    await canvas(bg, 1152, 1800).composite(mixes).png().toFile(path.join(preview, 'nine_mixes_' + theme + '.png'));
    const before = await sharp(await compose(2, 2, 2, 2, bg, true)).extract({left: 300, top: 65, width: 440, height: 580}).png().toBuffer();
    const after = await sharp(await compose(2, 2, 2, 2, bg)).extract({left: 300, top: 65, width: 440, height: 580}).png().toBuffer();
    const ref = await sharp(path.join(__dirname, 'guides/reference_02_in_canvas.png')).extract({left: 300, top: 65, width: 440, height: 580}).png().toBuffer();
    await canvas(bg, 1320, 580).composite([{input: before, left: 0, top: 0}, {input: after, left: 440, top: 0}, {input: ref, left: 880, top: 0}]).png().toFile(path.join(preview, 'style02_before_after_reference_' + theme + '.png'));
  }
  fs.mkdirSync(path.join(pack, 'qa'), {recursive: true});
  fs.writeFileSync(path.join(pack, 'qa/hair_registration_report.json'), JSON.stringify({full_three_point_2px_certification: false, headLayerRule: 'Baihui anterior/posterior plane, not image height', bodyRule: 'Front of clothing vs behind body', records}, null, 2) + '\n');
  console.log(JSON.stringify(records.map(r => ({key: r.key, transform: [r.scale, r.x, r.y], bbox: r.metrics.bboxAlpha180}))));
})().catch(e => {console.error(e);process.exitCode=1;});
