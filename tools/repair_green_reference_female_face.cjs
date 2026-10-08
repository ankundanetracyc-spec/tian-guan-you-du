// Reuse the independent, featureless female skin source. The current test
// face was sampled from a complete painting and contains hair/robe fragments.
const fs=require('fs');
const path=require('path');
const {PNG}=require('pngjs');
const {completeBaldSkull}=require('./complete_review_bald_skull.cjs');

const root=path.resolve(__dirname,'..');
const pack=path.join(root,'mods/appearance/腰上女修拆件演示_v1');
const sourcePath=path.join(pack,'source/anatomy_v11/before/face_0.png');
const currentPath=path.join(pack,'test_sources/green_master_single_set/female_featureless_master.png');
const destination=path.join(pack,'test_sources/original_skin_complete_v1');
const outputPath=path.join(destination,'face_0.png');
const W=1024,H=1536,CX=512;
const source=PNG.sync.read(fs.readFileSync(sourcePath));
const current=PNG.sync.read(fs.readFileSync(currentPath));
if(source.width!==W||source.height!==H)throw Error('Wrong female face canvas');
fs.mkdirSync(destination,{recursive:true});
function at(image,x,y) {
 const x0=Math.max(0,Math.min(W-2,Math.floor(x))),t=x-x0;
 const k=(y*W+x0)*4,m=k+4,a0=image.data[k+3]/255,a1=image.data[m+3]/255;
 const alpha=a0*(1-t)+a1*t;
 return alpha?[0,1,2].map(c=>(image.data[k+c]*a0*(1-t)+image.data[m+c]*a1*t)/alpha).concat(alpha*255):[0,0,0,0];
}
function median(values){values.sort((a,b)=>a-b);return values[Math.floor(values.length/2)]}
const diffs=[[],[],[]];
for(let y=305;y<=450;y+=4)for(let x of [480,496,512,528,544]) {
 const k=(y*W+x)*4,px=at(source,x,y);
 if(px[3]<240||current.data[k+3]<240||current.data[k]<170)continue;
 for(let c=0;c<3;c++)diffs[c].push(current.data[k+c]-px[c]);
}
const offset=diffs.map(median);
const symmetric=new PNG({width:W,height:H});
for(let y=0;y<H;y++)for(let x=350;x<=674;x++) {
 const d=Math.abs(x-CX)/.96;
 const left=at(source,CX-d,y),right=at(source,CX+d,y);
 const alpha=Math.min(left[3],right[3]);
 if(alpha<4)continue;
 const k=(y*W+x)*4;
 for(let c=0;c<3;c++)symmetric.data[k+c]=Math.max(0,Math.min(255,Math.round((left[c]+right[c])/2+offset[c])));
 symmetric.data[k+3]=alpha>=240?255:Math.round(alpha);
}
const fullHead=completeBaldSkull(symmetric,{
 top:240,sourceTop:255,crownY:330,maxRadius:93,join:380,joinRadius:91,
 lowerRadii:[[330,93],[380,90],[410,85],[440,81],[470,75],[500,66],[520,60],[540,57]]
});
fs.writeFileSync(outputPath,PNG.sync.write(fullHead));
// These two paint-extracted layers hide contamination while hair is visible.
// Use the original independent ears and brows so the bald-head preview is
// genuinely free of leftover side hair and beige rectangular patches.
for(const part of ['ear','brow']) {
 const original=PNG.sync.read(fs.readFileSync(path.join(pack,`source/anatomy_v11/before/${part}_0.png`)));
 const clean=new PNG({width:W,height:H});
 for(let y=0;y<H;y++)for(let x=0;x<W;x++) {
  const sx=part==='brow'?CX+(x-CX)/.9:x;
  const pixel=at(original,sx,y);
  if(pixel[3]<4)continue;
  const k=(y*W+x)*4;
  for(let c=0;c<3;c++)clean.data[k+c]=Math.max(0,Math.min(255,Math.round(pixel[c]+(part==='ear'?offset[c]:0))));
  clean.data[k+3]=Math.round(pixel[3]);
 }
 fs.writeFileSync(path.join(destination,`${part}_0.png`),PNG.sync.write(clean));
}
fs.writeFileSync(path.join(destination,'repair_manifest.json'),JSON.stringify({
 status:'single_female_review',source:path.relative(root,sourcePath).replaceAll('\\','/'),
 canvas:[W,H],center_axis_x:CX,horizontal_scale:.96,color_offset_rgb:offset,
 bald_skull_top_y:240,bald_skull_join_y:380,bald_skull_max_radius_px:93,
 operation:'symmetrize and complete the independent bald skin/neck layer beyond the hairline; reuse clean independent ear and brow layers; remove painting-extraction hair and clothing fragments',
 no_portrait_extraction:true,no_generated_replacement:true
},null,2)+'\n');
console.log(JSON.stringify({outputPath,offset}));
