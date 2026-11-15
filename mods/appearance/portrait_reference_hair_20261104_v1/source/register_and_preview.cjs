const fs = require('node:fs/promises');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const WIDTH = 1024, HEIGHT = 1536;
const settings = {
  hair_front_01: { scale: .85, x: 77, y: -28 },
  hair_front_02: { scale: .85, x: 77, y: -15 },
  hair_front_03: { scale: .85, x: 77, y: -28 },
  hair_back_01: { scale: .75, x: 129, y: 0 },
  hair_back_02: { scale: .75, x: 129, y: 0 },
  hair_back_03: { scale: .75, x: 129, y: 0 },
};
const backgrounds = { light: '#eae4d8', dark: '#252b30' };
const blank = (color = { r: 0, g: 0, b: 0, alpha: 0 }, width = WIDTH, height = HEIGHT) => sharp({ create: { width, height, channels: 4, background: color } });
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
const rgbaInfo = async file => {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let zero = 0, partial = 0, opaque = 0, skinCandidate = 0;
  let strongWarmCount = 0, strongWarmBounds = [info.width, info.height, -1, -1];
  let minX = info.width, minY = info.height, maxX = -1, maxY = -1;
  for (let y = 0; y < info.height; y++) for (let x = 0; x < info.width; x++) {
    const i = (y * info.width + x) * 4, a = data[i + 3];
    if (!a) zero++; else {
      if (a === 255) opaque++; else partial++;
      if (a >= 10) { minX = Math.min(minX, x); minY = Math.min(minY, y); maxX = Math.max(maxX, x); maxY = Math.max(maxY, y); }
      if (a > 180 && data[i] > 130 && data[i + 1] > 90 && data[i + 2] > 70 && data[i] - data[i + 1] > 10 && data[i + 1] - data[i + 2] > 5) skinCandidate++;
      if (a >= 20 && data[i] > 180 && data[i + 1] > 130 && data[i + 2] > 100 && data[i] - data[i + 1] > 10 && data[i + 1] - data[i + 2] > 5) {
        strongWarmCount++;
        strongWarmBounds = [Math.min(strongWarmBounds[0], x), Math.min(strongWarmBounds[1], y), Math.max(strongWarmBounds[2], x), Math.max(strongWarmBounds[3], y)];
      }
    }
  }
  const runs = x => {
    const out = []; let from = null;
    for (let y = 0; y <= Math.min(info.height - 1, 650); y++) {
      const active = data[(y * info.width + x) * 4 + 3] >= 180;
      if (active && from === null) from = y;
      if (!active && from !== null) { out.push([from, y - 1]); from = null; }
    }
    if (from !== null) out.push([from, 650]);
    return out;
  };
  return { width: info.width, height: info.height, channels: info.channels, alpha: { zero, partial, opaque }, bboxAlphaAtLeast10: [minX, minY, maxX, maxY], skinColorCandidatePixelsAlphaOver180: skinCandidate, strongWarmPixels: { count: strongWarmCount, bounds: strongWarmCount ? strongWarmBounds : null, note: 'Color-only candidate metric, not a semantic skin detector; central scalp part strokes can satisfy it.' }, verticalAlphaRunsAtLeast180: Object.fromEntries([442, 518, 596].map(x => [x, runs(x)])) };
};
const crop = { left: 196, top: 48, width: 640, height: 1000 };
const renderPortrait = async (front, back, color) => blank(color).composite([
  { input: path.join(root, 'female/hair_back', back + '.png') },
  { input: path.join(root, 'female/face_shape.png') },
  { input: path.join(root, 'female/clothing.png') },
  { input: path.join(root, 'female/hair_front', front + '.png') },
]).png().toBuffer();
async function renderFront085Comparison() {
  const items = [];
  for (const [id, dy] of [['01', -28], ['02', -15], ['03', -28]]) {
    const raw = path.join(__dirname, 'raw/hair_front', id + '.png');
    const resized = await sharp(raw).resize(870, 1306, { fit: 'fill', kernel: 'lanczos3' }).png().toBuffer();
    const cropped = await sharp(resized).extract({ left: 0, top: -dy, width: 870, height: 1306 + dy }).png().toBuffer();
    const front = await blank().composite([{ input: cropped, left: 77, top: 0 }]).png().toBuffer();
    const portrait = await blank(backgrounds.light).composite([
      { input: path.join(root, 'female/hair_back', id + '.png') },
      { input: path.join(root, 'female/face_shape.png') },
      { input: path.join(root, 'female/clothing.png') },
      { input: front },
    ]).png().toBuffer();
    items.push(await sharp(portrait).extract(crop).png().toBuffer());
  }
  await blank(backgrounds.light, 1920, 1000).composite(items.map((input, i) => ({ input, left: i * 640, top: 0 }))).png().toFile(path.join(root, 'preview/three_styles_front085_light.png'));
}
async function main() {
  await fs.mkdir(path.join(root, 'qa'), { recursive: true });
  await fs.mkdir(path.join(root, 'preview'), { recursive: true });
  const spec = JSON.parse(await fs.readFile(path.join(__dirname, 'generation_prompts.json'), 'utf8'));
  await fs.copyFile(spec.reference, path.join(__dirname, 'reference_user.png'));
  const records = [];
  for (const job of spec.jobs) {
    const m = /^(hair_front|hair_back)_(\d\d)$/.exec(job.key);
    const part = m[1], id = m[2], t = settings[job.key];
    const raw = path.join(__dirname, 'raw', part, id + '.png');
    const output = path.join(root, 'female', part, id + '.png');
    await fs.mkdir(path.dirname(raw), { recursive: true });
    await fs.mkdir(path.dirname(output), { recursive: true });
    const original = await fs.readFile(job.output_path);
    await fs.writeFile(raw, original);
    const md = await sharp(raw).metadata();
    const scaledWidth = Math.round(md.width * t.scale), scaledHeight = Math.round(md.height * t.scale);
    const resized = await sharp(raw).resize(scaledWidth, scaledHeight, { fit: 'fill', kernel: 'lanczos3' }).png().toBuffer();
    const clipX = Math.max(0, -t.x), clipY = Math.max(0, -t.y);
    const input = (clipX || clipY) ? await sharp(resized).extract({ left: clipX, top: clipY, width: scaledWidth - clipX, height: scaledHeight - clipY }).png().toBuffer() : resized;
    await blank().composite([{ input, left: Math.max(0, t.x), top: Math.max(0, t.y) }]).png().toFile(output);
    records.push({ key: job.key, generatedSource: job.output_path, rawCopy: raw, output, rawSha256: hash(original), copiedSha256: hash(await fs.readFile(raw)), uniformScale: t.scale, translation: [t.x, t.y], source: await rgbaInfo(raw), final: await rgbaInfo(output) });
  }
  for (const file of ['face_shape.png', 'clothing.png']) {
    await fs.copyFile(path.resolve(root, '../portrait_fresh_sets_20261103_v2/female', file), path.join(root, 'female', file));
  }
  for (const [tag, color] of Object.entries(backgrounds)) {
    const portraits = [];
    for (const id of ['01', '02', '03']) {
      const p = await renderPortrait(id, id, color);
      await fs.writeFile(path.join(root, 'preview', 'style_' + id + '_' + tag + '.png'), p);
      portraits.push(await sharp(p).extract(crop).png().toBuffer());
    }
    await blank(color, 1920, 1000).composite(portraits.map((input, i) => ({ input, left: i * 640, top: 0 }))).png().toFile(path.join(root, 'preview', 'three_styles_' + tag + '.png'));
    const layers = [];
    for (let row = 0; row < 2; row++) for (let col = 0; col < 3; col++) {
      const part = row ? 'hair_back' : 'hair_front', id = String(col + 1).padStart(2, '0');
      const composed = await blank(color).composite([{ input: path.join(root, 'female', part, id + '.png') }]).png().toBuffer();
      const input = await sharp(composed).extract({ left: 0, top: 0, width: 1024, height: 1200 }).resize(512, 600).png().toBuffer();
      layers.push({ input, left: col * 512, top: row * 600 });
    }
    await blank(color, 1536, 1200).composite(layers).png().toFile(path.join(root, 'preview', 'six_layers_' + tag + '.png'));
    const mixed = [];
    for (let fi = 1; fi <= 3; fi++) for (let bi = 1; bi <= 3; bi++) {
      const p = await renderPortrait(String(fi).padStart(2, '0'), String(bi).padStart(2, '0'), color);
      const input = await sharp(p).extract(crop).resize(448, 700).png().toBuffer();
      mixed.push({ input, left: (bi - 1) * 448, top: (fi - 1) * 700 });
    }
    await blank(color, 1344, 2100).composite(mixed).png().toFile(path.join(root, 'preview', 'nine_mixes_' + tag + '.png'));
  }
  const metadata = {
    status: 'Generated hair candidates with whole-layer uniform scaling/translation only; not installed in active game; full non-collinear three-point <=2px certification not asserted.',
    canvas: { width: WIDTH, height: HEIGHT, origin: 'top-left' },
    fixedFace: 'unchanged copy of portrait_fresh_sets_20261103_v2/female/face_shape.png',
    faceReference: 'User attachment is a close crop of near-identical fixed-face geometry; never enlarged to fill the production canvas.',
    operations: ['lossless raw source copy', 'whole-layer uniform resize', 'whole-layer translation', 'alpha composite for preview'],
    noPixelCleanup: true,
    layerOrder: ['hair_back', 'face_shape', 'clothing', 'hair_front'],
    pairedPreviewOrder: ['01 reference left', '02 reference middle', '03 reference right'],
    sixLayerSheet: 'row 1 fronts 01/02/03; row 2 backs 01/02/03',
    nineMixSheet: 'rows are front 01/02/03; columns are back 01/02/03',
    visualReview: {
      reviewed: ['three paired styles on light/dark backgrounds', 'six isolated hair layers on light/dark backgrounds', 'nine front/back mixes'],
      findings: [
        'Final front layers use scale 0.85 to match reference temple/ear coverage; back layers retain scale 0.75. Fixed face and clothing are unchanged.',
        'No visible ear skin patches or facial feature fragments were found in isolated alpha-composited hair layers. Color-candidate metrics include warm hair highlights and are not semantic skin findings.',
        'Narrow warm scalp-colored part strokes remain within the central hair part. They are not detached ear or face fragments.',
        'Front02 shows a continuous front hair cap and wisps, not a separate exposed rear bun. Its high rear crown/chignon is supplied by back02.',
        'Back03 includes outer rear braids; mixing it with other fronts exposes those rear braid edges. This style-specific silhouette is retained without pixel cleanup.',
        'Front central opaque hairline runs at x518 end near y257/y254/y254. Side geometry was visually fitted; non-collinear three-point <=2px certification was not performed.',
      ],
    },
    records,
  };
  await fs.writeFile(path.join(root, 'qa/metadata.json'), JSON.stringify(metadata, null, 2));
  await fs.writeFile(path.join(__dirname, 'registration.json'), JSON.stringify({ settings, note: 'Whole-layer fit only. Vertical alpha runs record actual pixels, not a three-point rig certification.' }, null, 2));
  await renderFront085Comparison();
  console.log(records.map(r => ({ key: r.key, transform: [r.uniformScale, ...r.translation], size: [r.final.width, r.final.height], alpha: r.final.alpha, bbox: r.final.bboxAlphaAtLeast10, skinCandidates: r.final.skinColorCandidatePixelsAlphaOver180, runs: r.final.verticalAlphaRunsAtLeast180 })));
}
main().catch(e => { console.error(e); process.exitCode = 1; });
