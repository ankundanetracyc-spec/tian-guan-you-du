// Rebuild ONE modular review set from the exact user-selected green portrait.
// Reuse independent skin/ear/brow layers; selected master detail stays registered.
// The complete historical rear painting fills only the regions hidden by
// the head/clothes; it is never cropped to a face or ear-shaped hole.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const {PNG} = require('pngjs');

const root = path.resolve(__dirname, '..');
const pack = path.join(root, 'mods/appearance/腰上男修拆件演示_v2');
const masterPath = path.join(pack, 'test_sources/hair_clean_full_1.png');
const old = path.join(pack, 'test_assets_v6');
const backup = path.join(pack, 'test_sources/anatomy_v15/before');
const out = path.join(pack, 'test_sources/green_master_single_set');
const preview = path.join(pack, 'preview/green_master_single_set');
fs.mkdirSync(out,{recursive:true});fs.mkdirSync(preview,{recursive:true});

const W=1024,H=1536,N=W*H;
function png(file){const p=PNG.sync.read(fs.readFileSync(file));if(p.width!==W||p.height!==H)throw Error(`Bad canvas: ${file}`);return p}
function empty(){return new PNG({width:W,height:H})}
function save(name,im){fs.writeFileSync(path.join(out,`${name}_0.png`),PNG.sync.write(im))}
function clamp(n){return Math.max(0,Math.min(255,Math.round(n)))}
function smooth(t){t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)}
function hairInk(r,g,b){return smooth((r-g+4)/7)*smooth((22-r+g)/8)*smooth((28-r+b)/10)}
const master=png(masterPath);
const clothes=png(path.join(old,'clothes_4.png'));

// Restore the fully painted and separately inspectable hair layers.
// The rear is never erased to follow the front silhouette.
const hairRepair=path.join(pack,'test_sources/hair_integrity_v1');
const rear=png(path.join(hairRepair,'hair_back_0.png'));
const front=png(path.join(hairRepair,'hair_front_0.png'));

// Keep the existing clean garment drawing. It was registered to the master
// and has the green robe pattern without hair baked into its pixels.
// Reuse the actual independent skin/neck sprite and clean small parts.
// Repairs keep their full canvas and original watercolor pigment.
const repairSource=path.join(pack,'test_sources/original_skin_symmetry_v1');
const face=png(path.join(repairSource,'face_0.png'));
const ear=png(path.join(repairSource,'ear_0.png'));
const cleanBrow=png(path.join(repairSource,'brow_0.png'));

// Feature masks come from the old anchor version. They remain separate,
// selectable parts, while their visible pigment is taken from the exact
// selected master instead of an approximated later redraw.
for(const part of ['eye','nose','mouth']){
 const mask=png(path.join(old,`${part}_0.png`)),layer=empty();
 for(let k=0;k<N*4;k+=4){
  const a=part==='nose'?clamp(mask.data[k+3]*3):mask.data[k+3];if(!a)continue;
  layer.data[k]=master.data[k];layer.data[k+1]=master.data[k+1];layer.data[k+2]=master.data[k+2];layer.data[k+3]=a;
 }
 save(part,layer);
}
save('hair_back',rear);save('face',face);save('ear',ear);save('brow',cleanBrow);
save('clothes',clothes);save('beard',png(path.join(old,'beard_0.png')));save('hair_front',front);
const tracedReview=path.join(pack,'test_sources/traced_head_v2');
for(const part of ['face','ear','eye','brow','nose','mouth','hair_front','hair_back']){
 const repaired=path.join(tracedReview,`${part}_0.png`);
 if(fs.existsSync(repaired))fs.copyFileSync(repaired,path.join(out,`${part}_0.png`));
}

const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','beard','hair_front'];
async function compose(bg,file){
 await sharp({create:{width:W,height:H,channels:4,background:bg}})
  .composite(order.map(part=>({input:path.join(out,`${part}_0.png`)}))).png().toFile(path.join(preview,file));
}
async function run(){
 await compose('#cbc8bd','light.png');await compose('#202526','dark.png');
 await sharp(masterPath).png().toFile(path.join(preview,'source_master.png'));
 const pieces=[];
 for(const [name,left] of [['source_master.png',0],['light.png',1024]]){
  pieces.push({input:await sharp(path.join(preview,name)).png().toBuffer(),left,top:0});
 }
 await sharp({create:{width:2048,height:1536,channels:3,background:'#cbc8bd'}}).composite(pieces).png().toFile(path.join(preview,'source_vs_parts.png'));
 const detail=[];
 for(const [name,left] of [['source_master.png',0],['light.png',600]]){
  detail.push({input:await sharp(path.join(preview,name))
   .extract({left:412,top:302,width:200,height:225})
   .resize(600,675,{kernel:'lanczos3'}).png().toBuffer(),left,top:0});
 }
 await sharp({create:{width:1200,height:675,channels:3,background:'#cbc8bd'}})
  .composite(detail).png().toFile(path.join(preview,'face_nose_detail.png'));
 const meta={status:'single_male_green_master_review_only',source:'test_sources/hair_clean_full_1.png',source_is_pixel_identical_to_user_attachment:true,canvas:[W,H],layer_order:order,
  anchor_source:'original green master and tools/prepare_character_appearance_test_v3.py; existing half_body_portrait_rig_v2.json',
  anchors_px:{shared_scalp_crown_y:140,hairline_y:277,brow_y:352,eye_y:378,nose_tip_y:446,mouth_center_y:477,chin_y:547},
  rear:'complete independent painting from anatomy_v15/before/hair_back_0.png; no front-silhouette alpha cutting',
  face:'reused independent skin/neck from anatomy_v15/before/face_0.png; symmetric continuous cheeks, original texture color-matched without sampling portrait pixels',
  face_repair_manifest:'test_sources/original_skin_symmetry_v1/repair_manifest.json',
  ear_and_brow:'reused clean independent original sprites; no master RGB sampling',
  front:'complete front crown, ear-overlapping locks and face-side strands reused from full_front_crown_v11/hair_front_1.png; reviewed crown pigment blended at the top',
  hair_repair_manifest:'test_sources/hair_integrity_v1/repair_manifest.json',
  latest_head_and_parts:'test_sources/traced_head_v2/placement.json',
  editable_parts:order,integration:'current start-menu appearance test reads this one-set review pack; other variants await user approval'};
 fs.writeFileSync(path.join(out,'split_manifest.json'),JSON.stringify(meta,null,2)+'\n');
 console.log(JSON.stringify({result:'GREEN_MASTER_SINGLE_SET_READY',parts:order.length,preview:path.join(preview,'source_vs_parts.png')}));
}
run().catch(e=>{console.error(e);process.exitCode=1});
