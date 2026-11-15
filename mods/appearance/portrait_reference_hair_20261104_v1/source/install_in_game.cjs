// Install the reviewed PNGs unchanged; retire former female features into the backup ZIP.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../../..');
const source = path.resolve(__dirname, '..');
const active = path.join(root, 'mods/appearance/ink_three_variants_20261004');
const installation = path.join(source, 'installation');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const resourcePath = file => 'res://' + path.relative(root, file).replaceAll('\\', '/');
function files(dir) {
  return fs.readdirSync(dir, {withFileTypes: true}).flatMap(e => e.isDirectory() ? files(path.join(dir, e.name)) : [path.join(dir, e.name)]);
}
(async () => {
  if (!fs.existsSync(path.join(installation, 'before_install.zip'))) throw Error('Create the installation backup ZIP before modifying active assets.');
  const reportPath = path.join(installation, 'install_report.json');
  const prior = fs.existsSync(reportPath) ? JSON.parse(fs.readFileSync(reportPath, 'utf8')) : null;
  const baseline = prior?.baseline_sha256 ?? Object.fromEntries([
    ...files(path.join(active, 'male')),
    ...files(path.join(active, 'female/clothing')),
  ].filter(f => f.endsWith('.png')).map(f => [resourcePath(f), hash(f)]));
  for (const part of ['hair_front', 'hair_back']) for (const id of ['01', '02', '03']) {
    const m = await sharp(path.join(source, 'female', part, id + '.png')).metadata();
    if (m.width !== 1024 || m.height !== 1536 || m.channels !== 4 || !m.hasAlpha) throw Error('Invalid hair canvas: ' + part + id);
  }
  const configPath = path.join(active, 'appearance_parts.json');
  const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  const names = {
    hair_front: ['清簪垂鬓', '素挽分鬓', '编云垂鬓'],
    hair_back: ['清簪半束', '素挽低髻', '编云长披'],
    eyebrows: ['无眉'], eyes: ['无眼'], nose: ['无鼻'], mouth: ['无嘴'], face_accessory: ['素颜'],
  };
  const blank = await sharp({create: {width: 1024, height: 1536, channels: 4, background: {r: 0, g: 0, b: 0, alpha: 0}}}).png().toBuffer();
  const records = [];
  const retired = [];
  for (const entry of config.components) {
    if (entry.gender !== 'female') continue;
    const part = entry.file.split('/')[1];
    if (!(part in names)) continue;
    const hair = part === 'hair_front' || part === 'hair_back';
    const ids = hair ? [1, 2, 3] : [1];
    Object.assign(entry, {variant_count: ids.length, variant_names: names[part], variant_indices: ids, variant_style_ids: ids.map(i => i - 1)});
    const dir = path.join(active, 'female', part);
    for (const id of ids) {
      const filename = String(id).padStart(2, '0') + '.png';
      const output = path.join(dir, filename);
      if (hair) fs.copyFileSync(path.join(source, 'female', part, filename), output);
      else fs.writeFileSync(output, blank);
      records.push({part, index: id, resource_path: resourcePath(output), sha256: hash(output), blank: !hair});
    }
    const keep = new Set(ids.map(i => String(i).padStart(2, '0') + '.png'));
    for (const filename of fs.readdirSync(dir)) {
      const png = filename.endsWith('.png.import') ? filename.slice(0, -7) : filename;
      if (!png.endsWith('.png') || keep.has(png)) continue;
      const target = path.resolve(dir, filename);
      if (!target.startsWith(active + path.sep)) throw Error('Retirement path outside active pack');
      retired.push(resourcePath(target));
      fs.unlinkSync(target);
    }
  }
  const face = path.join(active, 'female/face_shape/04.png');
  fs.copyFileSync(path.join(source, 'female/face_shape.png'), face);
  fs.writeFileSync(configPath, JSON.stringify(config, null, 2) + '\n');
  const manifestPath = path.join(active, 'portrait_manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  for (const gender of ['male', 'female']) manifest.variants_per_slot[gender] = manifest.slots.map(slot => config.components.find(e => e.gender === gender && e.file.includes('/' + slot + '/')).variant_count);
  const total = config.components.reduce((sum, e) => sum + e.variant_count, 0);
  manifest.total_pngs = total;
  manifest.active_variant_count = total;
  manifest.reference_hair_installation_20261104 = {
    source: resourcePath(source), hair_variants: 3, hair_style_ids: [0, 1, 2],
    face: 'female/face_shape/04.png',
    empty_female_parts: ['eyebrows', 'eyes', 'nose', 'mouth', 'face_accessory'],
    retired_assets_backup: resourcePath(path.join(installation, 'before_install.zip')),
    legacy_style_fallback: 'Existing deterministic catalog fallback; retired features resolve to the sole transparent variant.',
  };
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
  for (const [resource, expected] of Object.entries(baseline)) if (hash(path.join(root, resource.slice(6))) !== expected) throw Error('Unrelated asset changed: ' + resource);
  const report = {
    installed: true, hair_png_count: 6, active_variant_count: total,
    records, retired_files: [...new Set([...(prior?.retired_files ?? []), ...retired])],
    face_resource: resourcePath(face), face_sha256: hash(face), baseline_sha256: baseline,
    operations: ['exact PNG file copy', 'transparent empty feature layers', 'retired files archived before removal'],
  };
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({installedHair: 6, blankFemaleParts: 5, activeVariants: total, unchangedBaselineAssets: Object.keys(baseline).length, report: reportPath}));
})().catch(error => {console.error(error);process.exitCode = 1;});
