// Male skin calibration: ImageGen is a color study; only original-face RGB is adjusted.
// The original male01 alpha, facial outline, ear/neck geometry and texture positions are preserved.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const pack = path.resolve(__dirname, '..');
const project = path.resolve(__dirname, '../../../..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004');
const originalFile = path.join(__dirname, 'original_face.png');
const studyFile = path.join(__dirname, 'raw_skin_edit.png');
const fullReference = path.join(__dirname, 'skin_reference_user.png');
const attachment = 'C:/Users/34784/AppData/Local/Temp/codex-clipboard-46519b9f-6fb1-4fe6-8bb5-eba583337b0a.png';
const referenceSources = ['01', '02', '03'].map(id => path.join(project, 'mods/appearance/portrait_rebuild_20261103/source/male_final/reference_' + id + '.png'));
const referenceShifts = [[203, 49], [217, 49], [222, 49]];
const points = [
  {name: 'forehead_left', x: 465, y: 294}, {name: 'forehead_center', x: 517, y: 290}, {name: 'forehead_right', x: 565, y: 294},
  {name: 'cheek_left_outer', x: 450, y: 415}, {name: 'cheek_left_inner', x: 475, y: 420}, {name: 'cheek_right_inner', x: 555, y: 416}, {name: 'cheek_right_outer', x: 582, y: 414},
  {name: 'jaw_left', x: 468, y: 478}, {name: 'jaw_right', x: 565, y: 478}, {name: 'chin', x: 517, y: 529},
  {name: 'neck_left_shadow', x: 497, y: 574}, {name: 'neck_center', x: 517, y: 590}, {name: 'neck_right', x: 537, y: 574}, {name: 'neck_lower', x: 517, y: 634},
];
const smooth = (lo, hi, x) => {const t = Math.max(0, Math.min(1, (x - lo) / (hi - lo))); return t * t * (3 - 2 * t);};
// Anatomical color fields are smooth, broad and independent of individual texture pixels.
const region = y => [1, smooth(330, 405, y), smooth(440, 520, y), smooth(535, 575, y), smooth(595, 635, y), smooth(575, 588, y) * (1 - smooth(600, 615, y))];
const neckWeight = y => smooth(535, 575, y);
const load = file => sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject: true});
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
function medianPatch(image, x, y, radius = 6) {
  const values = [[], [], [], []];
  for (let yy = y - radius; yy <= y + radius; yy++) for (let xx = x - radius; xx <= x + radius; xx++) {
    const i = (yy * image.info.width + xx) * 4;
    for (let c = 0; c < 4; c++) values[c].push(image.data[i + c]);
  }
  return values.map(v => v.sort((a, b) => a - b)[Math.floor(v.length / 2)]);
}
const medianRgb = list => [0, 1, 2].map(c => list.map(v => v[c]).sort((a, b) => a - b)[Math.floor(list.length / 2)]);
function solve(matrix, vector) {
  const count = vector.length, a = matrix.map((row, i) => [...row, vector[i]]);
  for (let i = 0; i < count; i++) {
    let best = i; for (let j = i + 1; j < count; j++) if (Math.abs(a[j][i]) > Math.abs(a[best][i])) best = j;
    [a[i], a[best]] = [a[best], a[i]];
    const divisor = a[i][i]; if (Math.abs(divisor) < 1e-10) throw Error('Singular skin calibration');
    for (let k = i; k <= count; k++) a[i][k] /= divisor;
    for (let j = 0; j < count; j++) if (j !== i) {const m = a[j][i]; for (let k = i; k <= count; k++) a[j][k] -= m * a[i][k];}
  }
  return a.map(row => row[count]);
}
function fit(rows, target, ridge = 0.03) {
  const count = rows[0].length, matrix = Array.from({length: count}, () => Array(count).fill(0)), vector = Array(count).fill(0);
  for (let i = 0; i < rows.length; i++) for (let j = 0; j < count; j++) {vector[j] += rows[i][j] * target[i]; for (let k = 0; k < count; k++) matrix[j][k] += rows[i][j] * rows[i][k];}
  for (let i = 0; i < count; i++) matrix[i][i] += ridge;
  return solve(matrix, vector);
}
function lab(rgb) {
  const v = rgb.slice(0, 3).map(x => {x /= 255; return x <= .04045 ? x / 12.92 : ((x + .055) / 1.055) ** 2.4;});
  const q = [(v[0] * .4124564 + v[1] * .3575761 + v[2] * .1804375) / .95047, v[0] * .2126729 + v[1] * .7151522 + v[2] * .072175, (v[0] * .0193339 + v[1] * .119192 + v[2] * .9503041) / 1.08883].map(x => x > 216 / 24389 ? Math.cbrt(x) : (24389 / 27 * x + 16) / 116);
  return [116 * q[1] - 16, 500 * (q[0] - q[1]), 200 * (q[1] - q[2])];
}
const deltaE = (a, b) => Math.hypot(...lab(a).map((x, i) => x - lab(b)[i]));
const composite = (rgba, bg = [234, 228, 216]) => rgba.slice(0, 3).map((v, c) => Math.round(v * rgba[3] / 255 + bg[c] * (1 - rgba[3] / 255)));
function bbox(image) {
  let x0 = 1024, y0 = 1536, x1 = -1, y1 = -1;
  for (let y = 0; y < 1536; y++) for (let x = 0; x < 1024; x++) if (image.data[(y * 1024 + x) * 4 + 3] > 0) {x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);}
  return [x0, y0, x1 + 1, y1 + 1];
}
async function portrait(face, background) {
  const file = (part, id) => path.join(active, 'male', part, id + '.png');
  return sharp({create: {width: 1024, height: 1536, channels: 4, background}}).composite([
    file('hair_back', '01'), face, file('clothing', '01'), file('eyes', '01'), file('eyebrows', '01'), file('nose', '01'), file('mouth', '01'), file('hair_front', '01'),
  ].map(input => ({input}))).png().toBuffer();
}
(async () => {
  fs.mkdirSync(path.join(pack, 'male'), {recursive: true}); fs.mkdirSync(path.join(pack, 'qa'), {recursive: true});
  if (!fs.existsSync(originalFile)) fs.copyFileSync(path.join(active, 'male/face_shape/01.png'), originalFile);
  const providedStudy = process.argv[2];
  if (providedStudy && path.resolve(providedStudy) !== path.resolve(studyFile)) fs.copyFileSync(providedStudy, studyFile);
  if (!fs.existsSync(studyFile)) throw Error('Provide the male ImageGen color-study PNG as the first argument.');
  fs.copyFileSync(attachment, fullReference);
  const [original, generated, references] = await Promise.all([load(originalFile), load(studyFile), Promise.all(referenceSources.map(load))]);
  const sampled = points.map(p => {
    const samples = references.map((image, i) => medianPatch(image, p.x - referenceShifts[i][0], p.y - referenceShifts[i][1]));
    const valid = samples.map((rgba, i) => ({rgba, reference: i + 1})).filter(s => Math.min(...s.rgba.slice(0, 3)) > 145 && s.rgba[0] > s.rgba[1]);
    if (!valid.length) throw Error('No clean reference skin sample: ' + p.name);
    return {...p, original: medianPatch(original, p.x, p.y), reference: medianRgb(valid.map(s => s.rgba)), reference_samples: samples, reference_variants_used: valid.map(s => s.reference), generated: medianPatch(generated, p.x, p.y)};
  });
  const channels = [];
  for (let c = 0; c < 3; c++) {
    const target = sampled.map(p => p.reference[c] - composite(p.original)[c]);
    const rows = sampled.map(p => [...region(p.y), (p.original[c] - 210) / 30 * neckWeight(p.y)]);
    const unconstrained = fit(rows, target, .04);
    const slope = Math.max(-.65, Math.min(.15, unconstrained[6] / 30));
    const offset = fit(sampled.map(p => region(p.y)), target.map((value, i) => value - slope * (sampled[i].original[c] - 210) * neckWeight(sampled[i].y)), .02);
    channels.push({offset, neck_shadow_slope: slope, unconstrained});
  }
  const bytes = Buffer.from(original.data); let alphaDifference = 0, studyAlphaDifference = 0, changedRgb = 0;
  for (let y = 0; y < 1536; y++) for (let x = 0; x < 1024; x++) {
    const i = (y * 1024 + x) * 4;
    if (original.data[i + 3] !== generated.data[i + 3]) studyAlphaDifference++;
    if (!original.data[i + 3]) continue;
    const weights = region(y);
    for (let c = 0; c < 3; c++) {
      const channel = channels[c], delta = channel.offset.reduce((sum, v, j) => sum + v * weights[j], 0) + channel.neck_shadow_slope * (original.data[i + c] - 210) * neckWeight(y);
      bytes[i + c] = Math.max(0, Math.min(255, Math.round(original.data[i + c] + delta)));
      if (bytes[i + c] !== original.data[i + c]) changedRgb++;
    }
    if (bytes[i + 3] !== original.data[i + 3]) alphaDifference++;
  }
  const candidate = path.join(pack, 'male/face_shape.png');
  await sharp(bytes, {raw: {width: 1024, height: 1536, channels: 4}}).png().toFile(candidate);
  const final = await load(candidate);
  const comparisons = sampled.map(p => {const q = medianPatch(final, p.x, p.y); return {...p, final: q, deltaE_before: deltaE(composite(p.original), p.reference), deltaE_generated: deltaE(composite(p.generated), p.reference), deltaE_final: deltaE(composite(q), p.reference)};});
  const mean = (field, list = comparisons) => list.reduce((sum, p) => sum + p[field], 0) / list.length;
  const report = {
    status: 'male skin candidate; not installed in active game', mode: 'built-in image_gen male color study followed by measured smooth RGB calibration on original male01 geometry',
    source: {original: 'source/original_face.png', original_sha256: hash(originalFile), color_study: 'source/raw_skin_edit.png', study_sha256: hash(studyFile), user_reference: 'source/skin_reference_user.png', user_reference_sha256: hash(fullReference)},
    output: {file: 'male/face_shape.png', sha256: hash(candidate), width: 1024, height: 1536, channels: 4, alpha_different_pixels: alphaDifference, alpha_byte_exact: Buffer.from(original.data.filter((_, i) => i % 4 === 3)).equals(Buffer.from(final.data.filter((_, i) => i % 4 === 3))), original_alpha_bbox: bbox(original), final_alpha_bbox: bbox(final), changed_rgb_channels: changedRgb},
    generated_study_alpha_different_pixels: studyAlphaDifference,
    calibration: {model: 'smooth anatomical color fields plus monotone neck-only shadow response; original texture coordinates and alpha retained', channels, reference_alignment: {source_crop_full_image_x: [0, 593, 1187], source_to_project_translation: referenceShifts}, patch_radius: 6, target: 'Per-region median of clean skin from the three male reference portraits; hair-occluded samples excluded'},
    samples: comparisons, summary: {mean_deltaE_before: mean('deltaE_before'), mean_deltaE_final: mean('deltaE_final'), max_deltaE_final: Math.max(...comparisons.map(p => p.deltaE_final)), neck_mean_deltaE_before: mean('deltaE_before', comparisons.filter(p => p.name.startsWith('neck'))), neck_mean_deltaE_final: mean('deltaE_final', comparisons.filter(p => p.name.startsWith('neck')))},
    invariants: ['Original male01 alpha channel byte-for-byte identical', 'No geometric transform, outline movement or new feature/hair pixels', 'No female skin map reused', 'Original empty-canvas RGB retained where alpha=0', 'No active package or other candidates edited'],
    limitation: 'The triptych is a separate completed render; matching clean skin samples does not imply pixel-identical lighting or skin texture.',
  };
  fs.writeFileSync(path.join(pack, 'qa/skin_color_report.json'), JSON.stringify(report, null, 2) + '\n');
  for (const [theme, background] of [['light', '#eae4d8'], ['dark', '#252b30']]) {
    const assembled = [];
    for (const face of [originalFile, candidate]) assembled.push(await sharp(await portrait(face, background)).extract({left: 300, top: 150, width: 440, height: 560}).png().toBuffer());
    await sharp({create: {width: 880, height: 560, channels: 4, background}}).composite(assembled.map((input, i) => ({input, left: i * 440, top: 0}))).png().toFile(path.join(pack, 'qa/skin_before_after_' + theme + '.png'));
    const blank = [];
    for (const face of [originalFile, studyFile, candidate]) {const img = await sharp({create: {width: 1024, height: 1536, channels: 4, background}}).composite([{input: face}]).png().toBuffer(); blank.push(await sharp(img).extract({left: 340, top: 160, width: 360, height: 570}).png().toBuffer());}
    await sharp({create: {width: 1080, height: 570, channels: 4, background}}).composite(blank.map((input, i) => ({input, left: i * 360, top: 0}))).png().toFile(path.join(pack, 'qa/skin_original_study_final_' + theme + '.png'));
  }
  console.log(JSON.stringify({candidate, alphaByteExact: report.output.alpha_byte_exact, summary: report.summary}));
})().catch(error => {console.error(error); process.exitCode = 1;});
