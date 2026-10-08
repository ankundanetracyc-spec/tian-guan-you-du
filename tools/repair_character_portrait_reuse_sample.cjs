// User requested reuse of pre-proportion assets without image generation.
// One ten-layer sample only. Reuse original paint, repair foreign eye debris,
// and fit the complete ordinary scalp without cutting any hidden rear hair.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const { PNG } = require('pngjs');
const root = path.resolve(__dirname, '..');
const pack = path.join(root, 'mods/appearance/腰上女修拆件演示_v1');
const original = path.join(pack, 'source/anatomy_v11/before');
const out = path.join(pack, 'source/reuse_repair_sample_v13');
const review = path.join(pack, 'preview/reuse_repair_sample_v13');
fs.mkdirSync(out, { recursive: true });fs.mkdirSync(review, { recursive: true });
const choices = { hair_back:1, face:0, ear:0, clothes:0, eye:0, brow:0, nose:0, mouth:0, makeup:0, hair_front:0 };
// Approximate face distribution observed in the supplied small Guigu image:
// brow, eye, nose and mouth at ~.15/.30/.575/.75 from central hairline to chin.
// Reuse shapes and move each whole feature only a few pixels; no repainting.
const shifts={eye:-3,brow:-9,nose:-7,mouth:-3};
function shiftY(input,dy) {
 const output=new PNG({width:1024,height:1536});
 const first=Math.max(0,-dy),last=Math.min(1536,1536-dy);
 for(let y=0;y<1536;y++)if(y<first||y>=last)for(let x=0;x<1024;x++)if(input.data[(y*1024+x)*4+3])throw new Error('Feature shift would clip paint');
 input.data.copy(output.data,(first+dy)*4096,first*4096,last*4096);
 return output;
}
// This rear style has no top ornament. Ornament height is not part of scalp fit.
// Apply one continuous full-canvas registration to both complete hair layers.
// The cap moves down 45 px, the hairline 24 px, and ear/cheek locks and tails
// below y365 retain their exact original coordinates. No alpha holes or cuts.
function hairY(y) {
 const smooth=t=>t*t*(3-2*t);
 if(y<=140)return y+45;
 if(y<=279)return y+45-21*smooth((y-140)/139);
 if(y<=365)return y+24*(1-smooth((y-279)/86));
 return y;
}
function cubic(t) {
 t=Math.abs(t);
 return t<=1 ? 1.5*t*t*t-2.5*t*t+1 : t<2 ? -.5*t*t*t+2.5*t*t-4*t+2 : 0;
}
function fitHair(input) {
 const output=new PNG({width:1024,height:1536});
 for(let y=0;y<1536;y++) {
  if(y>=365) { input.data.copy(output.data,y*4096,y*4096,(y+1)*4096);continue; }
  let l=-46,r=365;
  for(let n=0;n<30;n++){const mid=(l+r)/2;if(hairY(mid)<y)l=mid;else r=mid;}
  const sy=(l+r)/2, base=Math.floor(sy);
  for(let x=0;x<1024;x++) {
   let a=0,red=0,green=0,blue=0;
   for(let row=base-1;row<=base+2;row++) {
    if(row<0||row>=1536)continue;
    const k=(row*1024+x)*4,w=cubic(sy-row),wa=w*input.data[k+3];
    a+=wa;red+=wa*input.data[k];green+=wa*input.data[k+1];blue+=wa*input.data[k+2];
   }
   const k=(y*1024+x)*4;
   if(a>0) {
    output.data[k]=Math.max(0,Math.min(255,Math.round(red/a)));
    output.data[k+1]=Math.max(0,Math.min(255,Math.round(green/a)));
    output.data[k+2]=Math.max(0,Math.min(255,Math.round(blue/a)));
    output.data[k+3]=Math.max(0,Math.min(255,Math.round(a)));
   }
  }
 }
 return output;
}
const eye = PNG.sync.read(fs.readFileSync(path.join(original, 'eye_0.png')));
// Hand-traced original eyelid silhouettes. Foreign temple stems and the
// surrounding skin plate are outside these shapes. No eye RGB is repainted.
const polygons = [
 [[423,373],[432,369],[450,368],[469,370],[485,377],[500,386],[501,388],[497,390],[487,392],[479,397],[458,399],[439,396],[430,391],[425,385],[423,379]]
];
polygons.push(polygons[0].map(([x,y])=>[1024-x,y]).reverse());
function distance(p, poly) {
 let inside = false, distance = Infinity;
 for(let i=0,j=poly.length-1;i<poly.length;j=i++) {
  const a=poly[j],b=poly[i];
  if((a[1]>p[1])!==(b[1]>p[1]) && p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]) inside=!inside;
  const vx=b[0]-a[0],vy=b[1]-a[1],t=Math.max(0,Math.min(1,((p[0]-a[0])*vx+(p[1]-a[1])*vy)/(vx*vx+vy*vy)));
  distance=Math.min(distance,Math.hypot(p[0]-a[0]-t*vx,p[1]-a[1]-t*vy));
 }
 return inside ? distance : -distance;
}
let removed=0, changed=0;
const before = Buffer.from(eye.data);
for(let y=0;y<eye.height;y++) for(let x=0;x<eye.width;x++) {
 const k=(y*eye.width+x)*4;
 if(!eye.data[k+3])continue;
 const d=Math.max(...polygons.map(p=>distance([x+.5,y+.5],p)));
 const coverage=Math.max(0,Math.min(1,(d+.6)/1.2));
 const alpha=Math.round(eye.data[k+3]*coverage);
 if(alpha!==eye.data[k+3])changed++;
 if(alpha===0)removed++;
 eye.data[k+3]=alpha;
}
// Core eye line, iris and sclera must retain exact source pixels.
for(const [l,t,r,b] of [[438,371,466,377],[432,376,480,390],[443,390,477,395],[558,371,586,377],[544,376,592,390],[547,390,581,395]]) {
 for(let y=t;y<b;y++)for(let x=l;x<r;x++) {
  const k=(y*eye.width+x)*4;
  if(eye.data[k+3]!==before[k+3])throw new Error(`Core eye changed at ${x},${y}`);
 }
}
for(let k=0;k<eye.data.length;k+=4)for(let c=0;c<3;c++)if(eye.data[k+c]!==before[k+c])throw new Error('Eye paint RGB changed');
fs.writeFileSync(path.join(out,'eye_0.png'),PNG.sync.write(shiftY(eye,shifts.eye)));
async function main() {
 for(const [part,id] of Object.entries(choices)) {
  const file=`${part}_${id}.png`,source=path.join(original,file),dest=path.join(out,file);
  if(part==='eye')continue;
  if(['hair_front','hair_back'].includes(part)) {
   const input=PNG.sync.read(fs.readFileSync(source)),output=fitHair(input);
   if(!output.data.subarray(365*4096).equals(input.data.subarray(365*4096)))throw new Error('Ear locks or hidden rear tails changed');
   fs.writeFileSync(dest,PNG.sync.write(output));
  } else if(shifts[part]) {
   const input=PNG.sync.read(fs.readFileSync(source));
   fs.writeFileSync(dest,PNG.sync.write(shiftY(input,shifts[part])));
  } else fs.copyFileSync(source,dest);
 }
 async function compose(dir,filename,background='#ccc8bd') {
  await sharp({create:{width:1024,height:1536,channels:4,background}}).composite(Object.entries(choices).map(([part,id])=>({input:path.join(dir,`${part}_${id}.png`)}))).png().toFile(path.join(review,filename));
 }
 await compose(original,'before_light.png');await compose(out,'after_light.png');await compose(out,'after_dark.png','#202526');
 const pieces=[];
 for(const [name,left] of [['before_light.png',0],['after_light.png',440]]) {
  const crop=await sharp(path.join(review,name)).extract({left:292,top:100,width:440,height:610}).png().toBuffer();pieces.push({input:crop,left,top:0});
 }
 await sharp({create:{width:880,height:610,channels:3,background:'#ccc8bd'}}).composite(pieces).png().toFile(path.join(review,'before_after.png'));
 const details=[];
 for(const [name,left] of [['before_light.png',0],['after_light.png',512]]) {
  const crop=await sharp(path.join(review,name)).extract({left:398,top:328,width:228,height:92}).resize(512,207).png().toBuffer();details.push({input:crop,left,top:0});
 }
 await sharp({create:{width:1024,height:207,channels:3,background:'#ccc8bd'}}).composite(details).png().toFile(path.join(review,'eye_before_after.png'));
 const metadata={status:'single_female_sample_pending_user_review',mode:'reuse_existing_assets_no_image_generation',baseline:'pre-face-proportion portrait_cleanup_v14',canvas:[1024,1536],choices,
  reference:'User supplied Guigu Bahuang female: approximate facial feature distribution; preserve project ink style. Previous two whole portraits are not proportion references.',
  unchanged:'face shape, eyebrow and eye shapes, nose and mouth shapes, ears, clothing; feature paint RGB and core eye RGBA after integer translation; all original hair below y365 including complete hidden rear and tails',
  eye_repair:{source:'source/anatomy_v11/before/eye_0.png',polygons,changed_alpha_pixels:changed,removed_alpha_pixels:removed,rgb:'exact source bytes',preserved_core:'exact source RGBA after translation'},
  hair_repair:{registration:'continuous full-canvas vertical fit shared by front and rear',control_points:[[140,185],[279,303],[365,365],[1535,1535]],front_crown_y:185,hairline_center_y:303,full_hidden_rear:true,no_alpha_cutouts:true,ornaments:'unadorned rear sample; other rear ornaments unchanged'},
  feature_registration:{translations_y:shifts,anchors:{hairline:303,brow:340,eye:377,nose:445,mouth:488,chin:550},normalized_from_hairline_to_chin:{brow:.15,eye:.30,nose:.575,mouth:.75},shape_and_paint:'exact source pixels; integer translation only'},
  integration:'Isolated ten-layer sample only; current runtime set restored to pre-proportion baseline. Other variants await user approval.'};
 fs.writeFileSync(path.join(out,'repair_manifest.json'),JSON.stringify(metadata,null,2)+'\n');
 console.log(JSON.stringify(metadata));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
