const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');

const project = path.resolve(__dirname, '../../../..');
const pack = path.resolve(__dirname, '..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004');
const installation = path.join(pack, 'installation');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const resource = file => 'res://' + path.relative(project, file).replaceAll('\\', '/');
const files = dir => fs.readdirSync(dir, {withFileTypes: true}).flatMap(entry => entry.isDirectory() ? files(path.join(dir, entry.name)) : [path.join(dir, entry.name)]);
const specs = {eyebrows: 3, eyes: 2, nose: 1, mouth: 1, face_shape: 1};

(async () => {
  if (!fs.existsSync(path.join(installation, 'before_install.zip'))) throw new Error('Pre-install backup is required.');
  const reportFile = path.join(installation, 'install_report.json');
  const previous = fs.existsSync(reportFile) ? JSON.parse(fs.readFileSync(reportFile, 'utf8')) : null;
  const replaced = new Set(Object.entries(specs).flatMap(([part, count]) => Array.from({length: count}, (_, index) => 'male/' + part + '/' + String(index + 1).padStart(2, '0') + '.png')));
  const baseline = previous?.baseline_sha256 ?? Object.fromEntries(files(active).filter(file => {
    const relative = path.relative(active, file).replaceAll('\\', '/');
    return /^(male|female)\/(hair_front|hair_back|eyebrows|eyes|nose|mouth|face_shape|clothing|face_accessory)\/\d+\.png$/.test(relative) && !replaced.has(relative);
  }).map(file => [resource(file), hash(file)]));
  if (Object.keys(baseline).length !== 36) throw new Error('Expected 36 preserved appearance PNGs.');
  const configFile = path.join(active, 'appearance_parts.json');
  const config = JSON.parse(fs.readFileSync(configFile, 'utf8'));
  const configSha = hash(configFile);
  const total = config.components.reduce((sum, entry) => sum + entry.variant_count, 0);
  if (total !== 44) throw new Error('Expected 44 active appearance options.');
  for (const [file, expected] of Object.entries(baseline)) if (hash(path.join(project, file.slice(6))) !== expected) throw new Error('Preserved asset changed before install: ' + file);
  const pending = [];
  for (const [part, count] of Object.entries(specs)) for (let id = 1; id <= count; id++) {
    const filename = String(id).padStart(2, '0') + '.png';
    const candidate = path.join(pack, 'male', part, filename);
    const output = path.join(active, 'male', part, filename);
    const meta = await sharp(candidate).metadata();
    if (meta.width !== 1024 || meta.height !== 1536 || meta.channels !== 4 || !meta.hasAlpha) throw new Error('Invalid RGBA canvas: ' + candidate);
    const bytes = await sharp(candidate).ensureAlpha().raw().toBuffer();
    let visible = false;
    let transparent = false;
    for (let index = 3; index < bytes.length; index += 4) { visible ||= bytes[index] > 0; transparent ||= bytes[index] === 0; }
    if (!visible || !transparent) throw new Error('Expected visible isolated transparent component: ' + candidate);
    pending.push({part, index: id, candidate, output, before_sha256: hash(output)});
  }
  const records = [];
  for (const entry of pending) {
    fs.copyFileSync(entry.candidate, entry.output);
    records.push({part: entry.part, index: entry.index, resource_path: resource(entry.output), candidate_path: resource(entry.candidate), source_path: resource(entry.candidate), sha256: hash(entry.output), before_sha256: entry.before_sha256});
  }
  const manifestFile = path.join(active, 'portrait_manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestFile, 'utf8'));
  manifest.male_face_match_20261104_v2 = {
    source: resource(pack),
    skin_reference_column: 3,
    user_direction: 'Use the third reference face skin color and material; match all independent facial components to that skin.',
    face: 'male/face_shape/01.png',
    feature_counts: {brow: 3, eye: 2, nose: 1, mouth: 1},
    face_alpha_may_change: true,
    hair_and_clothes_unchanged: true,
    female_unchanged: true,
    full_three_point_2px_certification: false,
  };
  fs.writeFileSync(manifestFile, JSON.stringify(manifest, null, 2) + '\n');
  for (const [file, expected] of Object.entries(baseline)) if (hash(path.join(project, file.slice(6))) !== expected) throw new Error('Preserved asset changed: ' + file);
  if (hash(configFile) !== configSha) throw new Error('Appearance configuration changed.');
  const report = {
    installed: true,
    active_variant_count: total,
    male_counts: [3, 3, 3, 2, 1, 1, 1, 7, 1],
    records,
    baseline_sha256: baseline,
    config_sha256: configSha,
    skin_reference_column: 3,
    reference_resource_path: resource(path.join(pack, 'source/reference_user.png')),
    reference_sha256: hash(path.join(pack, 'source/reference_user.png')),
    face_alpha_may_change: true,
    feature_combination_count: 6,
    with_hair_combination_count: 54,
  };
  fs.writeFileSync(reportFile, JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({installedRecords: records.length, activeOptions: total, baselinePngs: Object.keys(baseline).length, skinReferenceColumn: 3}));
})().catch(error => { console.error(error); process.exitCode = 1; });
