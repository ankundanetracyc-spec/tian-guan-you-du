const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');

const pack = path.resolve(__dirname, '..');
const inputs = path.join(__dirname, 'hair_before_tone');
const baselineFile = path.join(pack, 'qa/hair_tone_baseline.json');
const WIDTH = 1024, HEIGHT = 1536;
const QUANTILES = [.01, .1, .5, .9, .99];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const round = n => n === null ? null : +n.toFixed(4);
const clamp = (n, lo, hi) => Math.min(hi, Math.max(lo, n));
const luma = rgb => .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2];
const smooth = (lo, hi, n) => { const t = clamp((n - lo) / (hi - lo), 0, 1); return t * t * (3 - 2 * t); };
const mean = a => a.length ? a.reduce((s, n) => s + n, 0) / a.length : null;
const percentile = (sorted, q) => sorted.length ? sorted[Math.floor((sorted.length - 1) * q)] : null;
const roi = {
  hair_front: [
    {name: 'crown_front', box: [330, 170, 710, 340]},
    {name: 'temple_front', box: [330, 340, 710, 500]},
    {name: 'chest_front', box: [300, 520, 740, 950]}
  ],
  hair_back: [
    {name: 'crown_back', box: [340, 190, 700, 330]},
    {name: 'nape_back', box: [300, 340, 740, 500]},
    {name: 'curtain_back', box: [260, 500, 780, 1000]}
  ]
};

function lab(rgb) {
  const lin = v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; };
  const [r, g, b] = rgb.map(lin);
  const f = t => t > .008856451679 ? t ** (1 / 3) : 7.787037 * t + 16 / 116;
  const x = f((.4124564 * r + .3575761 * g + .1804375 * b) / .95047);
  const y = f(.2126729 * r + .7151522 * g + .072175 * b);
  const z = f((.0193339 * r + .119192 * g + .9503041 * b) / 1.08883);
  return [116 * y - 16, 500 * (x - y), 200 * (y - z)];
}

function measure(data, box) {
  const ys = [], rgb = [[], [], []], labs = [[], [], []], gradients = [], highpass = [];
  const [x0, y0, x1, y1] = box;
  const atY = k => luma([data[k], data[k + 1], data[k + 2]]);
  for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
    const k = (y * WIDTH + x) * 4;
    if (data[k + 3] < 230) continue;
    const col = [data[k], data[k + 1], data[k + 2]], yy = luma(col), ll = lab(col);
    ys.push(yy);
    for (let c = 0; c < 3; c++) { rgb[c].push(col[c]); labs[c].push(ll[c]); }
    if (x + 1 < x1 && data[k + 7] >= 230) gradients.push(Math.abs(yy - atY(k + 4)));
    if (y + 1 < y1 && data[k + WIDTH * 4 + 3] >= 230) gradients.push(Math.abs(yy - atY(k + WIDTH * 4)));
    if (x > x0 && x + 1 < x1 && y > y0 && y + 1 < y1 &&
        [k - 4, k + 4, k - WIDTH * 4, k + WIDTH * 4].every(j => data[j + 3] >= 230)) {
      highpass.push(yy - (atY(k - 4) + atY(k + 4) + atY(k - WIDTH * 4) + atY(k + WIDTH * 4)) / 4);
    }
  }
  ys.sort((a, b) => a - b);
  const n = ys.length;
  if (!n) return {pixels: 0};
  return {
    pixels: n,
    mean_rgb: rgb.map(a => round(mean(a))),
    mean_lab: labs.map(a => round(mean(a))),
    luma_srgb_255: {
      mean: round(mean(ys)),
      ...Object.fromEntries(QUANTILES.map(q => ['p' + Math.round(q * 100), round(percentile(ys, q))]))
    },
    percent_luma_above: Object.fromEntries([100, 110, 125, 140].map(t => [t, round(ys.filter(n => n > t).length / n * 100)])),
    neighbor_absolute_luma_difference_mean: round(mean(gradients)),
    four_neighbor_highpass_rms: round(Math.sqrt(mean(highpass.map(n => n * n))))
  };
}

// Align both overly bright and overly dark material to the same active female
// distribution. The monotone curve preserves intensity ordering and has a soft
// 0..4 luma deadband, so already close material receives only a small adjustment.
function makeCurve(source, target, keepMatchingChest) {
  if (source.pixels < 100 || !target || target.pixels < 100) return {identity: true, reason: 'Insufficient comparable opaque core pixels'};
  const knots = [{x: 0, y: 0}];
  for (const q of QUANTILES) {
    const key = 'p' + Math.round(q * 100), x = source.luma_srgb_255[key], wanted = target.luma_srgb_255[key];
    const difference = wanted - x;
    // Front02 chest is protected only when a quantile already matches. A newly
    // too-dark or too-bright chest region must converge rather than retain a
    // separate palette. Smooth6..10 avoids a hard change at the protection edge.
    const strength = keepMatchingChest ? smooth(6, 10, Math.abs(difference)) : smooth(0, 4, Math.abs(difference));
    knots.push({x, y: x + difference * strength, female_target: wanted, strength});
  }
  const prev = knots.at(-2), last = knots.at(-1);
  const slope = clamp((last.y - prev.y) / Math.max(1e-6, last.x - prev.x), .1, 1);
  knots.push({x: 255, y: clamp(last.y + (255 - last.x) * slope, last.y, 255)});
  const unique = [];
  for (const knot of knots) {
    if (unique.length && knot.x <= unique.at(-1).x + 1e-6) continue;
    knot.y = Math.max(unique.at(-1)?.y ?? 0, knot.y);
    unique.push(knot);
  }
  const srcY = luma(source.mean_rgb), dstY = luma(target.mean_rgb);
  let chromaDelta = source.mean_rgb.map((v, i) => clamp((target.mean_rgb[i] - dstY) - (v - srcY), -3, 3));
  const correctionY = luma(chromaDelta);
  chromaDelta = chromaDelta.map(v => (v - correctionY) * .8);
  const matchingChestRegion = keepMatchingChest && ['p50', 'p90', 'p99'].every(key => Math.abs(source.luma_srgb_255[key] - target.luma_srgb_255[key]) <= 6);
  if (matchingChestRegion) chromaDelta = chromaDelta.map(v => v * .35);
  return {
    identity: false,
    knots,
    chroma_delta_rgb: chromaDelta.map(round),
    bidirectional_female_alignment: true,
    front02_chest_soft_protection_enabled: keepMatchingChest,
    front02_matching_chest_region: matchingChestRegion,
    monotone: unique.every((k, i) => !i || k.x > unique[i - 1].x && k.y >= unique[i - 1].y)
  };
}

function mapY(curve, y) {
  if (curve.identity) return y;
  const knots = curve.knots;
  for (let i = 1; i < knots.length; i++) {
    if (y <= knots[i].x) {
      const a = knots[i - 1], b = knots[i], t = clamp((y - a.x) / (b.x - a.x), 0, 1);
      return a.y + t * (b.y - a.y);
    }
  }
  return knots.at(-1).y;
}

function regionWeights(part, y) {
  const t1 = part === 'hair_front' ? smooth(300, 380, y) : smooth(290, 380, y);
  const t2 = part === 'hair_front' ? smooth(460, 560, y) : smooth(440, 560, y);
  return [1 - t1, t1 * (1 - t2), t2];
}

async function main() {
  const baseline = JSON.parse(fs.readFileSync(baselineFile, 'utf8'));
  const targets = baseline.pooled.female;
  // Targets are fixed to the measured ACTIVE female material, not a stale draft.
  for (const record of baseline.records.filter(r => r.sex === 'female')) {
    if (hash(record.file) !== record.sha256) throw Error('Female target changed since measurement: ' + record.file);
  }
  const jobs = ['hair_front', 'hair_back'].flatMap(part => ['01', '02', '03'].map(id => ({
    part, id, input: path.join(inputs, part, id + '.png'), output: path.join(pack, 'male', part, id + '.png')
  })));
  // Validate all six snapshots before writing any candidate output.
  for (const job of jobs) {
    const meta = await sharp(job.input).metadata();
    if (meta.width !== WIDTH || meta.height !== HEIGHT || meta.channels !== 4 || !meta.hasAlpha) throw Error('Invalid fixed input: ' + job.input);
  }
  const records = [], beforeTiles = [], afterTiles = [];
  for (const job of jobs) {
    const before = await sharp(job.input).ensureAlpha().raw().toBuffer();
    const after = Buffer.from(before);
    const beforeRegions = roi[job.part].map(reg => ({...reg, stats: measure(before, reg.box)}));
    const curves = beforeRegions.map(reg => makeCurve(reg.stats, targets[reg.name], job.part === 'hair_front' && job.id === '02' && reg.name === 'chest_front'));
    if (curves.some(c => !c.identity && !c.monotone)) throw Error('Non-monotone tone mapping for ' + job.part + '/' + job.id);
    for (let y = 0; y < HEIGHT; y++) {
      // Protect wood pins, cloth wraps and high knots exactly; fade smoothly into
      // the scalp correction below the protected zone.
      const protection = job.part === 'hair_back' ? smooth(190, 235, y) : 1;
      if (!protection) continue;
      const weights = regionWeights(job.part, y);
      for (let x = 0; x < WIDTH; x++) {
        const k = (y * WIDTH + x) * 4;
        if (!before[k + 3]) continue;
        const col = [before[k], before[k + 1], before[k + 2]], oldY = luma(col);
        let newY = 0;
        const dc = [0, 0, 0];
        for (let r = 0; r < 3; r++) {
          newY += weights[r] * mapY(curves[r], oldY);
          for (let c = 0; c < 3; c++) dc[c] += weights[r] * (curves[r].chroma_delta_rgb?.[c] ?? 0);
        }
        for (let c = 0; c < 3; c++) after[k + c] = Math.round(clamp(col[c] + protection * (newY - oldY + dc[c]), 0, 255));
      }
    }
    let alphaDifference = 0, emptyRgbDifference = 0, protectedDifference = 0, changedRgb = 0;
    for (let k = 0; k < before.length; k += 4) {
      if (after[k + 3] !== before[k + 3]) alphaDifference++;
      for (let c = 0; c < 3; c++) if (after[k + c] !== before[k + c]) {
        changedRgb++;
        if (!before[k + 3]) emptyRgbDifference++;
        if (job.part === 'hair_back' && Math.floor(k / 4 / WIDTH) <= 190) protectedDifference++;
      }
    }
    if (alphaDifference || emptyRgbDifference || protectedDifference) throw Error('An invariant changed: ' + job.part + '/' + job.id);
    const outputBuffer = await sharp(after, {raw: {width: WIDTH, height: HEIGHT, channels: 4}}).png().toBuffer();
    fs.mkdirSync(path.dirname(job.output), {recursive: true});
    fs.writeFileSync(job.output, outputBuffer);
    const afterRegions = roi[job.part].map(reg => ({...reg, stats: measure(after, reg.box), female_target: targets[reg.name]}));
    records.push({
      part: job.part, id: job.id, input: job.input, output: job.output,
      input_sha256: hash(job.input), output_sha256: hash(job.output),
      alpha_different_pixels: alphaDifference, transparent_rgb_different_channels: emptyRgbDifference,
      protected_y_0_190_different_channels: protectedDifference,
      changed_rgb_channels: changedRgb, dimensions: [WIDTH, HEIGHT],
      curves: beforeRegions.map((reg, i) => ({name: reg.name, ...curves[i]})),
      before: beforeRegions, after: afterRegions
    });
    beforeTiles.push(await sharp(job.input).resize(256, 384).png().toBuffer());
    afterTiles.push(await sharp(outputBuffer).resize(256, 384).png().toBuffer());
  }
  const preview = path.join(pack, 'preview');
  fs.mkdirSync(preview, {recursive: true});
  const previews = [];
  for (const [name, background] of [['light', '#e9e4d8'], ['dark', '#252b30']]) {
    const destination = path.join(preview, 'hair_tone_layers_' + name + '.png');
    await sharp({create: {width: 1536, height: 768, channels: 4, background}})
      .composite([...beforeTiles.map((input, i) => ({input, left: i * 256, top: 0})), ...afterTiles.map((input, i) => ({input, left: i * 256, top: 384}))])
      .png().toFile(destination);
    previews.push(destination);
  }
  const report = {
    generated_at: new Date().toISOString(),
    method: 'RGB-only region-wise bidirectional monotone multi-quantile luma alignment and mild mean-chroma correction toward one active-female baseline from fixed six-image snapshots; continuous smooth anatomical blending. Both overly bright and overly dark inputs converge. No image geometry, alpha or new texture is generated.',
    target_baseline: baselineFile, target_female_sha256: Object.fromEntries(baseline.records.filter(r => r.sex === 'female').map(r => [r.file, r.sha256])),
    alpha_core_statistics_threshold: 230,
    protections: ['Every alpha byte and canvas coordinate retained', 'Alpha0 RGB retained', 'Rear hair y<=190 RGB retained, transition190..235', 'Front02 near-matching chest quantiles retained within6 luma units', 'Decorations are not tone targets'],
    preview_layout: 'Six columns: front01,front02,front03,back01,back02,back03. Top before; bottom after.',
    previews, records,
    limitations: ['Targets are anatomy-region distributions, not pixel-identical shading certification.', 'Spatial blending and ornament protection can leave output quantiles away from the nominal target; both are measured explicitly.', 'Female02 low-bun nape/curtain are excluded from comparable long-hair target.', 'No synthetic fine strand texture is added; residual texture differences are recorded by highpass/gradient metrics.', 'High-frequency metrics vary with strand width and direction and are not a standalone art pass.']
  };
  fs.writeFileSync(path.join(pack, 'qa/hair_tone_report.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({outputs: records.length, alpha_different_pixels: records.reduce((s, r) => s + r.alpha_different_pixels, 0), changed_rgb_channels: records.reduce((s, r) => s + r.changed_rgb_channels, 0), report: path.join(pack, 'qa/hair_tone_report.json')}));
}

main().catch(e => {console.error(e); process.exitCode = 1;});

