const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const {PNG} = require('pngjs');
const crypto = require('crypto');
const pack = path.resolve(__dirname, '..');
const project = path.resolve(pack, '../../..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004/female');
const before = path.join(__dirname, 'female_nasion_before_nose.png');
const raw = path.join(__dirname, 'female_nasion_raw.png');
const overlayFile = path.join(__dirname, 'female_nasion_overlay_registered.png');
const final = path.join(pack, 'female/nose/01.png');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function smooth(t) { t=Math.max(0,Math.min(1,t)); return t*t*(3-2*t); }

(async () => {
  fs.mkdirSync(path.dirname(final), {recursive:true});
  const old = PNG.sync.read(fs.readFileSync(before));
  const resized = await sharp(raw).extract({left:460,top:370,width:110,height:90}).resize(77,63).png().toBuffer();
  const part = PNG.sync.read(resized);
  const overlay = new PNG({width:1024,height:1536});
  for (let py=0;py<63;py++) for (let px=0;px<77;px++) {
    const x=px+481,y=py+359,k=(y*1024+x)*4,p=(py*77+px)*4;
    if (x<490 || x>=548 || y>=415) continue;
    const edge=smooth((x-490)/5)*smooth((547-x)/5)*smooth((y-356)/5)*smooth((415-y)/10);
    for (let c=0;c<3;c++) overlay.data[k+c]=part.data[p+c];
    overlay.data[k+3]=Math.round(part.data[p+3]*.45*edge);
  }
  const overlayBytes=PNG.sync.write(overlay);
  fs.writeFileSync(overlayFile,overlayBytes);
  const merged=PNG.sync.read(await sharp(before).composite([{input:overlayBytes}]).png().toBuffer());
  let changed=0,lowerChanged=0,outsideChanged=0,bounds=[1024,1536,-1,-1];
  for (let y=0;y<1536;y++) for(let x=0;x<1024;x++) {
    const k=(y*1024+x)*4;
    // Keep every old channel outside the actual generated brush coverage,
    // including RGB hidden by alpha zero. Below y415 is strictly untouched.
    if (y>=415 || overlay.data[k+3]===0) for(let c=0;c<4;c++) merged.data[k+c]=old.data[k+c];
    let diff=false;for(let c=0;c<4;c++) diff ||= merged.data[k+c]!==old.data[k+c];
    if(diff){changed++;bounds=[Math.min(bounds[0],x),Math.min(bounds[1],y),Math.max(bounds[2],x),Math.max(bounds[3],y)];if(y>=415)lowerChanged++;if(!overlay.data[k+3])outsideChanged++;}
  }
  fs.writeFileSync(final,PNG.sync.write(merged));
  const report={
    gender:'female',part:'nose',tool:'built-in image_gen',
    raw:'source/female_nasion_raw.png',overlay:'source/female_nasion_overlay_registered.png',
    previous:'source/female_nasion_before_nose.png',final:'female/nose/01.png',
    raw_sha256:hash(raw),previous_sha256:hash(before),final_sha256:hash(final),
    canvas:[1024,1536],source_roi:[460,370,110,90],uniform_scale:.7,source_to_canvas_translation:[159,100],
    cropped_roi_placement:[481,359],opacity:.45,support_roi:[490,356,548,415],
    edge_fade:'Smooth alpha-only ROI falloff; no generated pigment is altered, no synthetic stroke, no local warp.',
    changed_pixels:changed,changed_bbox_inclusive:bounds,lower_nose_y415_and_below_changed_pixels:lowerChanged,
    outside_actual_overlay_changed_pixels:outsideChanged,
    lower_nose_pixel_identical:lowerChanged===0,original_nose_tip_alae_nostrils_preserved:true,
    visual_selection:'45% chosen over 65% and 85% to keep a subtle female eye-socket/nasion transition without a white bridge panel or strong frown.',
    full_three_point_2px_certification:false
  };
  fs.writeFileSync(path.join(__dirname,'female_nasion_registration.json'),JSON.stringify(report,null,2)+'\n');
  fs.writeFileSync(path.join(pack,'qa/female_nasion_change_report.json'),JSON.stringify(report,null,2)+'\n');
  const crop={left:390,top:315,width:260,height:220};
  const panels=[];
  for(let brow=1;brow<=3;brow++)for(let eye=1;eye<=2;eye++) {
    const layers=['face_shape/04.png','eyes/0'+eye+'.png','eyebrows/0'+brow+'.png'].map(p=>({input:path.join(active,p)}));
    layers.push({input:final},{input:path.join(active,'mouth/01.png')});
    const composite=await sharp({create:{width:1024,height:1536,channels:4,background:'#e7e2d6'}}).composite(layers).png().toBuffer();
    const tile=await sharp(composite).extract(crop).resize(520,440).png().toBuffer();
    panels.push({input:tile,left:(eye-1)*520,top:(brow-1)*440});
  }
  await sharp({create:{width:1040,height:1320,channels:4,background:'#e7e2d6'}}).composite(panels).png().toFile(path.join(pack,'qa/female_six_brow_eye_nasion_after.png'));
  console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
