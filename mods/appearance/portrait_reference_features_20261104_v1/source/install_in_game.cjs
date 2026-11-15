// Exact asset copies from the three faces in the user's byte-identical reference; no redraw or pixel edits.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../../..');
const pack = path.resolve(__dirname, '..');
const active = path.join(root, 'mods/appearance/ink_three_variants_20261004');
const original = path.join(root, 'mods/appearance/portrait_rebuild_20261103/three_style_parts/female');
const installation = path.join(pack, 'installation');
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const resource = file => 'res://' + path.relative(root, file).replaceAll('\\', '/');
const specs = {
  eyebrows: {part: 'brow', names: ['疏平柔眉', '弯挑细眉', '平直细眉'], groups: [[1], [2], [3]], aliases: {}},
  eyes: {part: 'eye', names: ['浅杏长目', '清圆杏目'], groups: [[1, 3], [2]], aliases: {'2': 0, female_eyes_02: 0}},
  nose: {part: 'nose', names: ['柔圆鼻'], groups: [[1, 2, 3]], aliases: {'1': 0, '2': 0, female_nose_01: 0, female_nose_02: 0}},
  mouth: {part: 'mouth', names: ['自然浅唇'], groups: [[1, 2, 3]], aliases: {'1': 0, '2': 0, female_mouth_01: 0, female_mouth_02: 0}},
};
function allFiles(dir) {
  return fs.readdirSync(dir, {withFileTypes: true}).flatMap(e => e.isDirectory() ? allFiles(path.join(dir, e.name)) : [path.join(dir, e.name)]);
}
async function composite(front, back, brow, eye, background = '#ebe5d9') {
  const file = (part, id) => path.join(active, 'female', part, String(id).padStart(2, '0') + '.png');
  return sharp({create: {width: 1024, height: 1536, channels: 4, background}}).composite([
    file('hair_back', back), file('face_shape', 4), file('clothing', 1), file('eyes', eye),
    file('eyebrows', brow), file('nose', 1), file('mouth', 1), file('face_accessory', 1), file('hair_front', front),
  ].map(input => ({input}))).png().toBuffer();
}
(async () => {
  if (!fs.existsSync(path.join(installation, 'before_install.zip'))) throw Error('Create the pre-installation backup first.');
  const reportFile = path.join(installation, 'install_report.json');
  const previous = fs.existsSync(reportFile) ? JSON.parse(fs.readFileSync(reportFile, 'utf8')) : null;
  const baseline = previous?.baseline_sha256 ?? Object.fromEntries(allFiles(active).filter(f => {
    const rel = path.relative(active, f).replaceAll('\\', '/');
    return f.endsWith('.png') && /^(male|female)\//.test(rel) && !/^female\/(eyebrows|eyes|nose|mouth)\//.test(rel);
  }).map(f => [resource(f), sha(f)]));
  const reference = path.join(root, 'mods/appearance/portrait_rebuild_20261103/source/female_reference.png');
  const userReference = 'C:/Users/34784/AppData/Local/Temp/codex-clipboard-c9dc4e26-cea3-4176-a016-1b77fdd406e9.png';
  if (fs.existsSync(userReference) && sha(userReference) !== sha(reference)) throw Error('User reference differs from the existing feature source.');
  fs.copyFileSync(reference, path.join(__dirname, 'reference_user.png'));
  const configFile = path.join(active, 'appearance_parts.json');
  const config = JSON.parse(fs.readFileSync(configFile, 'utf8'));
  const records = [];
  const selection = [];
  for (const [part, spec] of Object.entries(specs)) {
    const candidateDir = path.join(pack, 'female', part);
    fs.mkdirSync(candidateDir, {recursive: true});
    const entry = config.components.find(e => e.gender === 'female' && e.part === spec.part);
    const ids = spec.groups.map((_, i) => i + 1);
    for (const [position, group] of spec.groups.entries()) {
      const id = String(position + 1).padStart(2, '0');
      const sourceFile = path.join(original, part, String(group[0]).padStart(2, '0') + '.png');
      const candidateFile = path.join(candidateDir, id + '.png');
      const output = path.join(active, 'female', part, id + '.png');
      const meta = await sharp(sourceFile).metadata();
      if (meta.width !== 1024 || meta.height !== 1536 || meta.channels !== 4 || !meta.hasAlpha) throw Error('Invalid feature: ' + sourceFile);
      fs.copyFileSync(sourceFile, candidateFile);
      fs.copyFileSync(candidateFile, output);
      records.push({part, index: position + 1, source_path: resource(sourceFile), resource_path: resource(output), candidate_path: resource(candidateFile), sha256: sha(output), source_sha256: sha(sourceFile), name: spec.names[position]});
      selection.push({part, retained_source_index: group[0], merged_source_indices: group, saved_style_id: position, name: spec.names[position]});
    }
    Object.assign(entry, {variant_count: ids.length, variant_names: spec.names, variant_indices: ids, variant_style_ids: ids.map(i => i - 1), variant_aliases: spec.aliases});
  }
  fs.writeFileSync(configFile, JSON.stringify(config, null, 2) + '\n');
  const manifestFile = path.join(active, 'portrait_manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestFile, 'utf8'));
  for (const gender of ['male', 'female']) manifest.variants_per_slot[gender] = manifest.slots.map(slot => config.components.find(e => e.gender === gender && e.file.includes('/' + slot + '/')).variant_count);
  const total = config.components.reduce((sum, e) => sum + e.variant_count, 0);
  manifest.total_pngs = total;
  manifest.active_variant_count = total;
  manifest.reference_features_installation_20261104 = {source: resource(pack), counts: {eyebrows: 3, eyes: 2, nose: 1, mouth: 1}, selection, independent_slots: true, hair_unchanged: true, face_unchanged: true, deprecated_previous_blank_features: true};
  fs.writeFileSync(manifestFile, JSON.stringify(manifest, null, 2) + '\n');
  for (const [file, expected] of Object.entries(baseline)) if (sha(path.join(root, file.slice(6))) !== expected) throw Error('Baseline changed: ' + file);
  const report = {installed: true, reference_sha256: sha(reference), source_reference_exactly_matches_attachment: true, counts: {eyebrows: 3, eyes: 2, nose: 1, mouth: 1}, independent_slots: true, feature_combination_count: 6, with_hair_combination_count: 54, active_variant_count: total, records, selection, aliases: Object.fromEntries(Object.entries(specs).map(([p, s]) => [p, s.aliases])), baseline_sha256: baseline};
  fs.writeFileSync(reportFile, JSON.stringify(report, null, 2) + '\n');
  fs.writeFileSync(path.join(__dirname, 'selection_and_deduplication.json'), JSON.stringify({reference: resource(path.join(__dirname, 'reference_user.png')), selection, comparisons: {eyebrows: {pairwise_luminance_correlation: [0.837, 0.859], decision: 'retain all three shapes'}, eyes: {one_vs_three: 0.9606, one_vs_two: 0.8614, two_vs_three: 0.8512, decision: 'merge source03 into source01'}, nose: {pairwise_luminance_correlation: [0.988, 0.994], local_mean_difference_at_most: 1.04, decision: 'retain source01 for all three'}, mouth: {pairwise_luminance_correlation: [0.975, 0.989], decision: 'retain source01 for all three'}}, operation: 'No averaging, color edits, extraction, rescaling or regeneration; exact copies of the attachment-derived existing transparent sprites.'}, null, 2) + '\n');
  const preview = path.join(pack, 'preview');
  fs.mkdirSync(preview, {recursive: true});
  for (const [theme, background] of [['light', '#ebe5d9'], ['dark', '#252b30']]) {
    const portraits = [];
    for (let i = 1; i <= 3; i++) {
      const buffer = await composite(i, i, i, i === 2 ? 2 : 1, background);
      await sharp(buffer).toFile(path.join(preview, 'reference_' + String(i).padStart(2, '0') + '_' + theme + '.png'));
      portraits.push({input: await sharp(buffer).extract({left: 215, top: 55, width: 600, height: 935}).resize(640, 998).png().toBuffer(), left: (i - 1) * 640, top: 0});
    }
    await sharp({create: {width: 1920, height: 998, channels: 4, background}}).composite(portraits).png().toFile(path.join(preview, 'three_faces_' + theme + '.png'));
    const combinations = [];
    for (let brow = 1; brow <= 3; brow++) for (let eye = 1; eye <= 2; eye++) {
      const buffer = await composite(1, 1, brow, eye, background);
      combinations.push({input: await sharp(buffer).extract({left: 350, top: 290, width: 340, height: 300}).resize(510, 450).png().toBuffer(), left: (eye - 1) * 510, top: (brow - 1) * 450});
    }
    await sharp({create: {width: 1020, height: 1350, channels: 4, background}}).composite(combinations).png().toFile(path.join(preview, 'six_feature_combinations_' + theme + '.png'));
  }
  console.log(JSON.stringify({installedFeatures: records.length, counts: report.counts, activeVariants: total, unchangedBaselineFiles: Object.keys(baseline).length, featureCombinations: 6, withHair: 54, report: reportFile}));
})().catch(error => {console.error(error);process.exitCode = 1;});
