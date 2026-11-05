// Install the wholly regenerated male portrait round. Old rounds are archive only.
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
const specs = {hair_front: 3, hair_back: 3, eyebrows: 3, eyes: 2, nose: 1, mouth: 1, face_shape: 1};

(async () => {
  if (!fs.existsSync(path.join(installation, 'before_install.zip'))) throw new Error('Pre-install archive is required.');
  const baselineReport = JSON.parse(fs.readFileSync(path.join(installation, 'preserved_baseline.json'), 'utf8'));
  const backupReport = JSON.parse(fs.readFileSync(path.join(installation, 'backup_report.json'), 'utf8'));
  const baseline = baselineReport.baseline_sha256;
  if (Object.keys(baseline).length !== 30) throw new Error('Expected 22 female PNGs, seven male clothes and one empty accessory.');
  const configFile = path.join(active, 'appearance_parts.json');
  if (hash(configFile) !== baselineReport.configuration_sha256) throw new Error('Appearance configuration changed since backup.');
  const config = JSON.parse(fs.readFileSync(configFile, 'utf8'));
  const total = config.components.reduce((sum, entry) => sum + entry.variant_count, 0);
  if (total !== 44) throw new Error('Expected 44 active appearance options.');
  const verifyBaseline = () => {
    for (const [file, expected] of Object.entries(baseline)) {
      if (hash(path.join(project, file.slice(6))) !== expected) throw new Error('Preserved asset changed: ' + file);
    }
  };
  verifyBaseline();
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
    const oldHash = backupReport.sha256[resource(output)];
    if (!oldHash) throw new Error('Missing rejected-round baseline: ' + output);
    if (hash(candidate) === oldHash) throw new Error('Rejected old artwork cannot be reused: ' + candidate);
    pending.push({part, index: id, candidate, output, before_sha256: oldHash});
  }
  if (pending.length !== 14) throw new Error('All 14 new components must be ready before installation.');
  const records = [];
  for (const entry of pending) {
    fs.copyFileSync(entry.candidate, entry.output);
    records.push({part: entry.part, index: entry.index, resource_path: resource(entry.output), candidate_path: resource(entry.candidate), source_path: resource(entry.candidate), sha256: hash(entry.output), before_sha256: entry.before_sha256});
  }
  const manifestFile = path.join(active, 'portrait_manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestFile, 'utf8'));
  for (const key of ['male_reference_installation_20261104', 'male_reference_20261104_v1', 'male_face_match_20261104_v2']) {
    if (manifest[key]) {
      manifest[key].status = 'rejected_by_user_superseded';
      manifest[key].do_not_restore = true;
    }
  }
  manifest.male_regenerated_20261104_v3 = {
    source: resource(pack), status: 'installed_awaiting_visual_review',
    user_direction: 'Discard both previous male portrait rounds and generate all male hair, facial features and face again from the supplied reference.',
    rejected_previous_sources: ['portrait_male_reference_20261104_v1', 'portrait_male_face_match_20261104_v2'],
    restore_rejected_artwork: false, regenerated_part_count: 14,
    hair_counts: {front: 3, back: 3}, feature_counts: {brow: 3, eye: 2, nose: 1, mouth: 1},
    independent_selections: true, hair_tone_material_reference: 'active_female_parts',
    face_alpha_may_change: true, female_and_clothes_unchanged: true,
    full_three_point_2px_certification: false,
  };
  fs.writeFileSync(manifestFile, JSON.stringify(manifest, null, 2) + '\n');
  verifyBaseline();
  if (hash(configFile) !== baselineReport.configuration_sha256) throw new Error('Appearance configuration changed during installation.');
  const reference = path.join(pack, 'source/reference_user.png');
  const report = {
    installed: true, active_variant_count: total, male_counts: [3, 3, 3, 2, 1, 1, 1, 7, 1],
    records, baseline_sha256: baseline, config_sha256: hash(configFile),
    reference_resource_path: resource(reference), reference_sha256: hash(reference),
    all_male_artwork_regenerated: true, rejected_previous_rounds_must_not_be_restored: true,
    face_alpha_may_change: true, feature_combination_count: 6, with_hair_combination_count: 54,
  };
  fs.writeFileSync(path.join(installation, 'install_report.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({installedRecords: records.length, activeOptions: total, baselinePngs: Object.keys(baseline).length}));
})().catch(error => { console.error(error); process.exitCode = 1; });
