const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const project = path.resolve(__dirname, '../../../..');
const pack = path.resolve(__dirname, '..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004');
const installation = path.join(pack, 'installation');
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const resource = file => 'res://' + path.relative(project, file).replaceAll('\\', '/');
const files = dir => fs.readdirSync(dir, {withFileTypes: true}).flatMap(e => e.isDirectory() ? files(path.join(dir, e.name)) : [path.join(dir, e.name)]);
(async () => {
  if (!fs.existsSync(path.join(installation, 'before_install.zip'))) throw Error('Create the backup before installing.');
  const reportPath = path.join(installation, 'install_report.json');
  const previous = fs.existsSync(reportPath) ? JSON.parse(fs.readFileSync(reportPath, 'utf8')) : null;
  const baseline = previous?.baseline_sha256 ?? Object.fromEntries(files(active).filter(file => {
    const rel = path.relative(active, file).replaceAll('\\', '/');
    return file.endsWith('.png') && /^(male|female)\//.test(rel) && !/^female\/(hair_front|hair_back|face_shape)\//.test(rel);
  }).map(file => [resource(file), sha(file)]));
  const records = [];
  for (const part of ['hair_front', 'hair_back', 'face_shape']) for (const id of (part === 'face_shape' ? [4] : [1, 2, 3])) {
    const candidate = part === 'face_shape' ? path.join(pack, 'female/face_shape.png') : path.join(pack, 'female', part, String(id).padStart(2, '0') + '.png');
    const output = path.join(active, 'female', part, String(id).padStart(2, '0') + '.png');
    const metadata = await sharp(candidate).metadata();
    if (metadata.width !== 1024 || metadata.height !== 1536 || metadata.channels !== 4 || !metadata.hasAlpha) throw Error('Invalid canvas: ' + candidate);
    if (part === 'face_shape') {
      const original = await sharp(path.join(__dirname, 'original_face.png')).ensureAlpha().raw().toBuffer();
      const updated = await sharp(candidate).ensureAlpha().raw().toBuffer();
      for (let i = 3; i < original.length; i += 4) if (original[i] !== updated[i]) throw Error('Face alpha changed at pixel ' + ((i - 3) / 4));
    }
    fs.copyFileSync(candidate, output);
    records.push({part, index: id, resource_path: resource(output), candidate_path: resource(candidate), source_path: resource(candidate), sha256: sha(output)});
  }
  for (const [file, expected] of Object.entries(baseline)) if (sha(path.join(project, file.slice(6))) !== expected) throw Error('Unrelated PNG changed: ' + file);
  const manifestPath = path.join(active, 'portrait_manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  manifest.hair_layer_skin_revision_20261104 = {
    source: resource(pack), head_boundary: 'Baihui anterior vs posterior; not a horizontal image cut',
    body_boundary: 'in front of clothes vs behind body',
    anterior_hair: 'Anterior scalp cap and temple sections/front braids/front hanging locks',
    posterior_hair: 'Posterior scalp, bun/chignon and body-rear loose hair; complete hidden underlay',
    face: 'Female face04 RGB calibrated to the reference, original Alpha preserved exactly',
    female_features_unchanged: {brow: 3, eye: 2, nose: 1, mouth: 1},
    full_three_point_2px_certification: false,
  };
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
  const config = JSON.parse(fs.readFileSync(path.join(active, 'appearance_parts.json'), 'utf8'));
  const report = {installed: true, active_variant_count: config.components.reduce((sum, e) => sum + e.variant_count, 0), records, baseline_sha256: baseline, preserved_skin_alpha: true, selected_hair_count: 6, recolored_face_count: 1, rule: manifest.hair_layer_skin_revision_20261104};
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({installedRecords: records.length, unchangedBaselinePngs: Object.keys(baseline).length, activeOptions: report.active_variant_count, originalFaceAlpha: 'exactly preserved'}));
})().catch(e => {console.error(e);process.exitCode=1;});
