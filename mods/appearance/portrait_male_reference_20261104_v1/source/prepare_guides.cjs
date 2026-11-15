const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const project = path.resolve(__dirname, '../../../..');
const pack = path.resolve(__dirname, '..');
const reference = 'C:/Users/34784/AppData/Local/Temp/codex-clipboard-46519b9f-6fb1-4fe6-8bb5-eba583337b0a.png';
(async () => {
  fs.mkdirSync(path.join(__dirname, 'guides'), {recursive: true});
  fs.copyFileSync(reference, path.join(__dirname, 'reference_user.png'));
  const metadata = await sharp(reference).metadata();
  const placements = [{id:'01', left:0, width:590, x:204, y:50}, {id:'02', left:593, width:589, x:215, y:49}, {id:'03', left:1187, width:587, x:220, y:50}];
  for (const p of placements) {
    const crop = await sharp(reference).extract({left:p.left,top:0,width:p.width,height:metadata.height}).png().toBuffer();
    fs.writeFileSync(path.join(__dirname, 'guides/reference_' + p.id + '.png'), crop);
    await sharp({create:{width:1024,height:1536,channels:4,background:'#ebe6de'}}).composite([{input:crop,left:p.x,top:p.y}]).png().toFile(path.join(__dirname,'guides/reference_' + p.id + '_in_canvas.png'));
  }
  fs.writeFileSync(path.join(__dirname,'reference_placements.json'),JSON.stringify({canvas:[1024,1536],source:reference,placements,status:'Native-size portrait layout guides; measurements to be checked against fixed male face01.'},null,2)+'\n');
  console.log('Prepared three native-size male reference layouts.');
})().catch(e=>{console.error(e);process.exitCode=1;});
