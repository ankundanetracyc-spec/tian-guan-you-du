// Repair the existing independent skin layer; do not extract skin from a portrait.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const { PNG } = require('pngjs');
const { completeBaldSkull } = require('./complete_review_bald_skull.cjs');
const root = path.resolve(__dirname, '..');
const pack = path.join(root, 'mods/appearance/腰上男修拆件演示_v2');
const sourcePath = path.join(pack, 'test_sources/anatomy_v15/before/face_0.png');
const masterPath = path.join(pack, 'test_sources/hair_clean_full_1.png');
const destination = path.join(pack, 'test_sources/original_skin_symmetry_v1');
const preview = path.join(pack, 'preview/original_skin_symmetry_v1');
fs.mkdirSync(destination, { recursive: true });
fs.mkdirSync(preview, { recursive: true });
const W = 1024, H = 1536, sourceCenter = 512.5, targetCenter = 512;
const source = PNG.sync.read(fs.readFileSync(sourcePath));
const master = PNG.sync.read(fs.readFileSync(masterPath));
if (source.width !== W || source.height !== H) throw Error('Source canvas changed');
const repaired = new PNG({ width: W, height: H });

// Broaden the pinched lower cheeks gradually, with no change in eye/chin height.
// The neck and upper head keep the existing registration and size.
const expansion = [[390,0],[420,4],[440,7],[460,10],[480,12],[495,10],[515,4],[540,0]];
function cheekExpansion(y) {
  for (let i = 1; i < expansion.length; i++) {
    const [y0,d0] = expansion[i-1], [y1,d1] = expansion[i];
    if (y < y0 || y > y1) continue;
    const t = (y-y0)/(y1-y0), s = t*t*(3-2*t);
    return d0+(d1-d0)*s;
  }
  return 0;
}
function sample(x,y) {
  const x0 = Math.floor(x), t = x-x0;
  if (x0 < 0 || x0+1 >= W) return [0,0,0,0];
  const k0 = (y*W+x0)*4, k1 = k0+4;
  const a0 = source.data[k0+3]/255, a1 = source.data[k1+3]/255;
  const a = a0*(1-t)+a1*t;
  if (!a) return [0,0,0,0];
  return [0,1,2].map(c => (source.data[k0+c]*a0*(1-t)+source.data[k1+c]*a1*t)/a).concat(a*255);
}
function median(a) { a.sort((x,y)=>x-y); return a[Math.floor(a.length/2)]; }
// Compare unmarked skin only. The master supplies a color target, never pixels
// or an alpha mask. A single offset preserves the original ink-paper texture.
const delta = [[],[],[]];
for (const [x0,x1,y0,y1] of [[470,550,306,330],[467,483,409,429],[543,559,409,429]]) {
  for (let y=y0;y<=y1;y++) for (let x=x0;x<=x1;x++) {
    const l=sample(sourceCenter-Math.abs(x-targetCenter),y);
    const r=sample(sourceCenter+Math.abs(x-targetCenter),y);
    const k=(y*W+x)*4;
    if (l[3]<240 || r[3]<240 || master.data[k+3]<240) continue;
    for (let c=0;c<3;c++) delta[c].push(master.data[k+c]-(l[c]+r[c])/2);
  }
}
const colorOffset = delta.map(median);
for (let y=0;y<H;y++) {
  let left=W,right=-1;
  for (let x=0;x<W;x++) if (source.data[(y*W+x)*4+3]>128) { left=Math.min(left,x);right=Math.max(right,x); }
  if (right<left) continue;
  const radius=(right-left)/2;
  const scale=radius>0?(radius+cheekExpansion(y))/radius:1;
  for (let x=0;x<W;x++) {
    const distance=Math.abs(x-targetCenter)/scale;
    const l=sample(sourceCenter-distance,y), r=sample(sourceCenter+distance,y);
    const a=Math.min(l[3],r[3]);
    if (a<4) continue;
    const k=(y*W+x)*4;
    for (let c=0;c<3;c++) repaired.data[k+c]=Math.max(0,Math.min(255,Math.round((l[c]+r[c])/2+colorOffset[c])));
    repaired.data[k+3]=a>=240?255:Math.round(a);
  }
}
// The historic independent head stopped at the hairline, leaving a flat
// forehead when both hair layers were hidden. Continue the bone/skull dome
// beyond that line, matching the original temples again by eye height.
const fullHead=completeBaldSkull(repaired,{
  top:235,sourceTop:250,crownY:330,maxRadius:99,join:380,joinRadius:96,
  lowerRadii:[[330,99],[380,96],[410,92],[440,88],[470,82],[500,76],[520,71],[540,66]]
});
const outputPath=path.join(destination,'face_0.png');
fs.writeFileSync(outputPath,PNG.sync.write(fullHead));
// Reuse the already clean independent ears and eyebrows. Sampling the portrait
// through their old masks had brought head hair back into those skin/brow layers.
for (const part of ['ear','brow']) {
  const original=PNG.sync.read(fs.readFileSync(path.join(pack,`test_sources/anatomy_v15/before/${part}_0.png`)));
  const layer=new PNG({width:W,height:H});
  for (let y=0;y<H;y++) for (let x=0;x<W;x++) {
    const mx=2*targetCenter-x;
    if (mx<0||mx>=W) continue;
    const k=(y*W+x)*4,m=(y*W+mx)*4;
    const a0=original.data[k+3],a1=original.data[m+3];
    const a=(a0+a1)/2;
    if (a<4) continue;
    for (let c=0;c<3;c++) {
      const color=(original.data[k+c]*a0+original.data[m+c]*a1)/(a0+a1);
      layer.data[k+c]=Math.max(0,Math.min(255,Math.round(color+(part==='ear'?colorOffset[c]:0))));
    }
    layer.data[k+3]=Math.round(a);
  }
  fs.writeFileSync(path.join(destination,`${part}_0.png`),PNG.sync.write(layer));
}
fs.writeFileSync(path.join(destination,'repair_manifest.json'),JSON.stringify({
  status:'single_male_review',source:path.relative(root,sourcePath).replaceAll('\\','/'),
  canvas:[W,H],center_axis_x:targetCenter,source_center_x:sourceCenter,
  operation:'symmetrize independent skin and neck, complete the rounded bald cranium beyond the hairline, gently widen pinched cheeks, retain original skin texture',
  bald_skull_top_y:235,bald_skull_join_y:380,bald_skull_max_radius_px:99,
  cheek_expansion_px:expansion,color_offset_rgb:colorOffset,
  color_reference:path.relative(root,masterPath).replaceAll('\\','/'),
  reused_clean_parts:['face','ear','brow'],
  no_portrait_extraction:true,no_generated_replacement:true
},null,2)+'\n');
async function run() {
  const active=path.join(pack,'test_assets_green_reference_v1');
  const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','beard','hair_front'];
  for (const [bg,name] of [['#cbc8bd','light.png'],['#202526','dark.png']]) {
    await sharp({create:{width:W,height:H,channels:4,background:bg}})
      .composite(order.map(part=>({input:path.join(['face','ear','brow'].includes(part)?destination:active,`${part}_0.png`)})))
      .png().toFile(path.join(preview,name));
  }
  await sharp(path.join(preview,'light.png')).extract({left:365,top:260,width:294,height:320})
    .resize(588,640).png().toFile(path.join(preview,'face_zoom.png'));
  await sharp({create:{width:W,height:H,channels:4,background:'#cbc8bd'}})
    .composite([{input:outputPath}]).png().toFile(path.join(preview,'skin_only.png'));
  console.log(JSON.stringify({outputPath,colorOffset,center:targetCenter}));
}
run().catch(e=>{console.error(e);process.exitCode=1;});
