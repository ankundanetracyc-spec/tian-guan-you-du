const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../../..');
const pack = path.resolve(__dirname, '..');
(async () => {
  fs.mkdirSync(path.join(__dirname, 'guides'), {recursive: true});
  for (const [id, x] of [['01', 204], ['02', 217], ['03', 219]]) {
    const input = path.join(root, 'mods/appearance/portrait_rebuild_20261103/source/female_hair_final/reference_' + id + '.png');
    await sharp({create: {width: 1024, height: 1536, channels: 4, background: '#ebe6de'}})
      .composite([{input, left: x, top: 51}]).png().toFile(path.join(__dirname, 'guides/reference_' + id + '_in_canvas.png'));
  }
  console.log('Prepared native-size reference layouts on the production canvas.');
})().catch(e => {console.error(e);process.exitCode=1;});
