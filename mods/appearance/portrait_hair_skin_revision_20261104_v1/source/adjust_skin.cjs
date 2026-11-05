// Calibrate only skin RGB from the existing ImageGen color-edit study to the user's exact reference.
// The original alpha, head/ear/neck outline and original texture positions are retained byte-for-byte.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const pack = path.resolve(__dirname, '..');
const project = path.resolve(__dirname, '../../../..');
const active = path.join(project, 'mods/appearance/ink_three_variants_20261004');
const originalFile = path.join(__dirname, 'original_face.png');
const referenceFile = path.join(project, 'mods/appearance/portrait_rebuild_20261103/source/female_reference.png');
const generatedSource = 'E:/tmp/CodexData/generated_images/01a1a4be-7b8b-76e1-af95-b099fa3c88dc/exec-15c95ae7-5418-4e30-9ba9-9cc8ddd820a7.png';
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const points = [
  {name:'forehead_left',x:465,y:295},{name:'forehead_center',x:519,y:290},{name:'forehead_right',x:565,y:295},
  {name:'cheek_left_outer',x:454,y:414},{name:'cheek_left_inner',x:474,y:421},{name:'cheek_right_inner',x:561,y:415},{name:'cheek_right_outer',x:580,y:413},
  {name:'jaw_left',x:463,y:478},{name:'jaw_right',x:574,y:478},{name:'chin',x:519,y:529},
  {name:'neck_left_shadow',x:497,y:570},{name:'neck_center',x:519,y:585},{name:'neck_right',x:539,y:570},{name:'neck_lower',x:518,y:630},
];
const smooth = (a,b,x) => {const t=Math.max(0,Math.min(1,(x-a)/(b-a)));return t*t*(3-2*t);};
const region = y => [1,smooth(330,405,y),smooth(440,520,y),smooth(535,575,y)];
const load = async file => sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
function medianPatch(img,x,y,r=6) {
  const channels=[[],[],[],[]];
  for(let yy=y-r;yy<=y+r;yy++)for(let xx=x-r;xx<=x+r;xx++){
    const i=(yy*img.info.width+xx)*4;
    for(let c=0;c<4;c++)channels[c].push(img.data[i+c]);
  }
  return channels.map(v=>v.sort((a,b)=>a-b)[Math.floor(v.length/2)]);
}
function solve(matrix, vector) {
  const n=vector.length, a=matrix.map((row,i)=>[...row,vector[i]]);
  for(let i=0;i<n;i++){
    let best=i;for(let j=i+1;j<n;j++)if(Math.abs(a[j][i])>Math.abs(a[best][i]))best=j;
    [a[i],a[best]]=[a[best],a[i]];
    const div=a[i][i];if(Math.abs(div)<1e-10)throw Error('Singular calibration');
    for(let k=i;k<=n;k++)a[i][k]/=div;
    for(let j=0;j<n;j++)if(j!==i){const mul=a[j][i];for(let k=i;k<=n;k++)a[j][k]-=mul*a[i][k];}
  }
  return a.map(row=>row[n]);
}
function fit(rows,targets,ridge=0.01){
  const n=rows[0].length,m=Array.from({length:n},()=>Array(n).fill(0)),v=Array(n).fill(0);
  for(let i=0;i<rows.length;i++)for(let j=0;j<n;j++){v[j]+=rows[i][j]*targets[i];for(let k=0;k<n;k++)m[j][k]+=rows[i][j]*rows[i][k];}
  for(let j=0;j<n;j++)m[j][j]+=ridge;
  return solve(m,v);
}
function lab(rgb){
  const l=rgb.map(v=>{v/=255;return v<=0.04045?v/12.92:((v+0.055)/1.055)**2.4;});
  const xyz=[(l[0]*.4124564+l[1]*.3575761+l[2]*.1804375)/.95047,l[0]*.2126729+l[1]*.7151522+l[2]*.072175,l[0]*.0193339/1.08883+l[1]*.119192/1.08883+l[2]*.9503041/1.08883].map(v=>v>216/24389?Math.cbrt(v):(24389/27*v+16)/116);
  return [116*xyz[1]-16,500*(xyz[0]-xyz[1]),200*(xyz[1]-xyz[2])];
}
const deltaE=(a,b)=>Math.hypot(...lab(a).map((v,i)=>v-lab(b)[i]));
const compositeSample = (rgba,bg=[234,228,216]) => rgba.slice(0,3).map((v,c)=>Math.round(v*rgba[3]/255+bg[c]*(1-rgba[3]/255)));
function bbox(img,threshold=1){let x0=1024,y0=1536,x1=-1,y1=-1;for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){if(img.data[(y*1024+x)*4+3]>=threshold){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}}return[x0,y0,x1+1,y1+1];}
async function portrait(face,background){
  const file=(part,id)=>path.join(active,'female',part,id+'.png');
  return sharp({create:{width:1024,height:1536,channels:4,background}}).composite([
    file('hair_back','01'),face,file('clothing','01'),file('eyes','01'),file('eyebrows','01'),file('nose','01'),file('mouth','01'),file('hair_front','01')
  ].map(input=>({input}))).png().toBuffer();
}
(async()=>{
  fs.mkdirSync(path.join(pack,'female'),{recursive:true});fs.mkdirSync(path.join(pack,'qa'),{recursive:true});
  if(!fs.existsSync(originalFile))fs.copyFileSync(path.join(active,'female/face_shape/04.png'),originalFile);
  fs.copyFileSync(generatedSource,path.join(__dirname,'raw_skin_edit.png'));
  const [original,reference,generated]=await Promise.all([load(originalFile),load(referenceFile),load(generatedSource)]);
  const sampled=points.map(p=>({...p,original:medianPatch(original,p.x,p.y),reference:medianPatch(reference,p.x-204,p.y-51),generated:medianPatch(generated,p.x,p.y)}));
  // Fit four broad anatomical color regions plus a monotone per-channel shadow correction.
  // Region fields vary smoothly with y; no new high-frequency texture or anatomical detail is painted.
  const channels=[];
  for(let c=0;c<3;c++){
    const target=sampled.map(p=>p.reference[c]-compositeSample(p.original)[c]);
    const rows=sampled.map(p=>[...region(p.y),(p.original[c]-210)/30]);
    const unconstrained=fit(rows,target,.05);
    const slope=Math.max([-.20,-.35,-.45][c],Math.min(.10,unconstrained[4]/30));
    const offset=fit(sampled.map(p=>region(p.y)),target.map((t,i)=>t-slope*(sampled[i].original[c]-210)),.01);
    channels.push({offset,shadow_slope:slope,unconstrained});
  }
  const output=Buffer.from(original.data);
  let alphaDifference=0,changedRgb=0,oldVsGeneratedAlpha=0;
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
    const i=(y*1024+x)*4,a=original.data[i+3];
    if(a!==generated.data[i+3])oldVsGeneratedAlpha++;
    if(a===0)continue;
    const r=region(y);
    for(let c=0;c<3;c++){
      const p=channels[c],delta=p.offset.reduce((s,v,j)=>s+v*r[j],0)+p.shadow_slope*(original.data[i+c]-210);
      output[i+c]=Math.max(0,Math.min(255,Math.round(original.data[i+c]+delta)));
      if(output[i+c]!==original.data[i+c])changedRgb++;
    }
    if(output[i+3]!==original.data[i+3])alphaDifference++;
  }
  const candidate=path.join(pack,'female/face_shape.png');
  await sharp(output,{raw:{width:1024,height:1536,channels:4}}).png().toFile(candidate);
  const final=await load(candidate);
  const comparisons=sampled.map(p=>{
    const q=medianPatch(final,p.x,p.y);return {...p,final:q,deltaE_before:deltaE(compositeSample(p.original),p.reference),deltaE_generated:deltaE(compositeSample(p.generated),p.reference),deltaE_final:deltaE(compositeSample(q),p.reference)};
  });
  const mean=(key,list=comparisons)=>list.reduce((s,v)=>s+v[key],0)/list.length;
  const report={
    status:'skin candidate only; not installed in active game',mode:'built-in image_gen skin-color edit followed by measured mechanical RGB calibration on the original geometry',
    source:{original:'source/original_face.png',original_sha256:hash(originalFile),imagegen_color_study:'source/raw_skin_edit.png',imagegen_sha256:hash(generatedSource),reference:referenceFile,reference_sha256:hash(referenceFile)},
    output:{file:'female/face_shape.png',sha256:hash(candidate),width:1024,height:1536,channels:4,alpha_different_pixels:alphaDifference,alpha_byte_exact:Buffer.from(original.data.filter((_,i)=>i%4===3)).equals(Buffer.from(final.data.filter((_,i)=>i%4===3))),original_alpha_bbox:bbox(original),final_alpha_bbox:bbox(final),changed_rgb_channels:changedRgb},
    original_vs_generated_alpha_different_pixels:oldVsGeneratedAlpha,
    calibration:{model:'smooth four-region RGB offset plus clamped monotone per-channel shadow response; original RGB texture positions and alpha retained',channels,reference_alignment:{column:'left',project_to_reference:[-204,-51]},patch_radius:6,region_boundaries:[[330,405],[440,520],[535,575]],render_background_for_comparison:[234,228,216]},
    samples:comparisons,summary:{mean_deltaE_before:mean('deltaE_before'),mean_deltaE_generated:mean('deltaE_generated'),mean_deltaE_final:mean('deltaE_final'),max_deltaE_final:Math.max(...comparisons.map(p=>p.deltaE_final)),neck_mean_deltaE_before:mean('deltaE_before',comparisons.filter(p=>p.name.startsWith('neck'))),neck_mean_deltaE_final:mean('deltaE_final',comparisons.filter(p=>p.name.startsWith('neck')))},
    invariants:['Original alpha channel byte-for-byte identical','No geometric transform, skin outline movement or new feature/hair pixels','Original empty-canvas RGB retained where alpha=0','Only low-frequency color calibration and monotone RGB response; original texture locations retained','Active pack, male sprites, clothes, hair and facial-feature PNGs not edited'],
    limitation:'The reference is a separate finished rendering; matching local skin color does not imply pixel-identical skin texture or lighting.'
  };
  fs.writeFileSync(path.join(pack,'qa/skin_color_report.json'),JSON.stringify(report,null,2)+'\n');
  for(const[theme,background]of[['light','#eae4d8'],['dark','#252b30']]){
    const inputs=[];for(const face of [originalFile,candidate])inputs.push(await sharp(await portrait(face,background)).extract({left:300,top:150,width:440,height:560}).png().toBuffer());
    await sharp({create:{width:880,height:560,channels:4,background}}).composite(inputs.map((input,i)=>({input,left:i*440,top:0}))).png().toFile(path.join(pack,'qa/skin_before_after_'+theme+'.png'));
    const blankInputs=[];for(const face of [originalFile,path.join(__dirname,'raw_skin_edit.png'),candidate]){const full=await sharp({create:{width:1024,height:1536,channels:4,background}}).composite([{input:face}]).png().toBuffer();blankInputs.push(await sharp(full).extract({left:320,top:190,width:400,height:555}).png().toBuffer());}
    await sharp({create:{width:1200,height:555,channels:4,background}}).composite(blankInputs.map((input,i)=>({input,left:i*400,top:0}))).png().toFile(path.join(pack,'qa/skin_original_generated_final_'+theme+'.png'));
  }
  console.log(JSON.stringify({candidate,alphaByteExact:report.output.alpha_byte_exact,summary:report.summary,channels}));
})().catch(error=>{console.error(error);process.exitCode=1;});
