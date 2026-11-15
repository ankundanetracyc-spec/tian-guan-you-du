// Mechanical whole-layer hair registration only. No painting, color rebuild, or active writes.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const PACK = path.resolve(__dirname, '..');
const ROOT = path.resolve(__dirname, '../../../..');
const ACTIVE = path.join(ROOT, 'mods/appearance/ink_three_variants_20261004');
const W = 1024, H = 1536;
const clamp = (v, lo=0, hi=255) => Math.max(lo, Math.min(hi, v));
const sha = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const relative = p => path.relative(PACK,p).replaceAll('\\','/');
const resource = p => 'res://' + path.relative(ROOT,p).replaceAll('\\','/');
const json = p => JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
function resolve(p) {
  if (p.startsWith('res://')) return path.resolve(ROOT,p.slice(6));
  return path.isAbsolute(p) ? p : path.resolve(PACK,p);
}
function ownOutput(p) {
  const dest = resolve(p);
  const rel = path.relative(PACK,dest);
  if (rel.startsWith('..') || path.isAbsolute(rel) || dest.startsWith(ACTIVE+path.sep))
    throw Error('Output must stay in this staging package: '+p);
  return dest;
}
async function read(p) {
  const f=resolve(p);
  const r=await sharp(f).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  return {data:r.data,w:r.info.width,h:r.info.height,path:f};
}
async function save(image,p) {
  const f=ownOutput(p); fs.mkdirSync(path.dirname(f),{recursive:true});
  await sharp(image.data,{raw:{width:image.w,height:image.h,channels:4}}).png().toFile(f);
  return f;
}
function writeJson(p,data) {
  const f=ownOutput(p);fs.mkdirSync(path.dirname(f),{recursive:true});
  fs.writeFileSync(f,JSON.stringify(data,null,2)+'\n');
}
function map(t,x,y) {return [t.a*x-t.b*y+t.tx,t.b*x+t.a*y+t.ty];}
function inverse(t,x,y) {
  const dx=x-t.tx,dy=y-t.ty,k=t.a*t.a+t.b*t.b;
  return [(t.a*dx+t.b*dy)/k,(-t.b*dx+t.a*dy)/k];
}
function compose(o,i) {
  return {a:o.a*i.a-o.b*i.b,b:o.b*i.a+o.a*i.b,
    tx:o.a*i.tx-o.b*i.ty+o.tx,ty:o.b*i.tx+o.a*i.ty+o.ty};
}
function matrix(t) {return [[t.a,-t.b,t.tx],[t.b,t.a,t.ty]];}
function fit(source,target) {
  if (!Array.isArray(source)||source.length<2||source.length!==target.length)
    throw Error('At least two matched whole-layer controls are required');
  const n=source.length,s=[0,0],t=[0,0];
  for(let i=0;i<n;i++)for(let c=0;c<2;c++){s[c]+=source[i][c]/n;t[c]+=target[i][c]/n;}
  let aa=0,bb=0,den=0;
  for(let i=0;i<n;i++){
    const x=source[i][0]-s[0],y=source[i][1]-s[1],u=target[i][0]-t[0],v=target[i][1]-t[1];
    aa+=x*u+y*v;bb+=x*v-y*u;den+=x*x+y*y;
  }
  if(den<1e-6)throw Error('Degenerate registration controls');
  const a=aa/den,b=bb/den,tx=t[0]-a*s[0]+b*s[1],ty=t[1]-b*s[0]-a*s[1];
  const out={a,b,tx,ty};
  out.residuals=source.map((p,i)=>{const v=map(out,...p);return Math.hypot(v[0]-target[i][0],v[1]-target[i][1]);});
  out.maximum_residual_px=Math.max(...out.residuals);
  return out;
}
function transform(value,label) {
  if(!value)throw Error('Missing measured transform: '+label);
  let t;
  if(value.matrix_2x3) {
    const m=value.matrix_2x3;
    if(m.length!==2||m[0].length!==3||m[1].length!==3)throw Error('Invalid2x3 matrix: '+label);
    t={a:Number(m[0][0]),b:Number(m[1][0]),tx:Number(m[0][2]),ty:Number(m[1][2])};
    if(Math.abs(m[0][1]+t.b)>1e-6||Math.abs(m[1][1]-t.a)>1e-6)
      throw Error('Only whole-layer uniform scale/rotation/translation is allowed: '+label);
    t.residuals=value.residuals||[];
    t.maximum_residual_px=t.residuals.length?Math.max(...t.residuals):null;
  } else t=fit(value.source_controls,value.target_controls);
  if(![t.a,t.b,t.tx,t.ty].every(Number.isFinite)||Math.hypot(t.a,t.b)<0.01)
    throw Error('Invalid similarity: '+label);
  return t;
}
function cleanNoise(image,maximum) {
  const out={...image,data:Buffer.from(image.data)};let changed=0;
  for(let k=0;k<out.data.length;k+=4)if(out.data[k+3]<=maximum){
    if(out.data[k+3])changed++;
    out.data[k]=out.data[k+1]=out.data[k+2]=out.data[k+3]=0;
  }
  return {image:out,removed:changed};
}
function sample(image,x,y) {
  const x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,out=[0,0,0,0];
  for(const [dx,dy,weight] of [[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){
    const px=x0+dx,py=y0+dy;
    if(px<0||py<0||px>=image.w||py>=image.h)continue;
    const k=(py*image.w+px)*4,alpha=image.data[k+3]/255;
    out[3]+=alpha*weight;
    for(let c=0;c<3;c++)out[c]+=image.data[k+c]*alpha*weight;
  }
  if(out[3]>0)for(let c=0;c<3;c++)out[c]/=out[3];
  out[3]*=255;return out;
}
function resample(image,t) {
  const out={w:W,h:H,data:Buffer.alloc(W*H*4)};
  for(let y=0;y<H;y++)for(let x=0;x<W;x++){
    const p=inverse(t,x,y),color=sample(image,...p),k=(y*W+x)*4;
    const alpha=Math.round(clamp(color[3]));
    if(!alpha)continue;
    for(let c=0;c<3;c++)out.data[k+c]=Math.round(clamp(color[c]));
    out.data[k+3]=alpha;
  }
  return out;
}
function alphaStats(image) {
  let pixels=0,core=0,skinProbe=0;
  const bbox=[Infinity,Infinity,-Infinity,-Infinity],bbox8=[Infinity,Infinity,-Infinity,-Infinity];
  for(let y=0;y<image.h;y++)for(let x=0;x<image.w;x++){
    const k=(y*image.w+x)*4,a=image.data[k+3];if(!a)continue;pixels++;
    bbox[0]=Math.min(bbox[0],x);bbox[1]=Math.min(bbox[1],y);bbox[2]=Math.max(bbox[2],x+1);bbox[3]=Math.max(bbox[3],y+1);
    if(a>=8){bbox8[0]=Math.min(bbox8[0],x);bbox8[1]=Math.min(bbox8[1],y);bbox8[2]=Math.max(bbox8[2],x+1);bbox8[3]=Math.max(bbox8[3],y+1);}
    if(a>=230)core++;
    if(x>=400&&x<=640&&y>=220&&y<=530&&a>=128&&image.data[k]>150&&
      image.data[k]-image.data[k+1]>15&&image.data[k+1]-image.data[k+2]>8)skinProbe++;
  }
  let hidden=0,hiddenZero=0;
  if(image.w===W&&image.h===H)for(let y=225;y<435;y++)for(let x=435;x<595;x++){
    hidden++;if(image.data[(y*W+x)*4+3]===0)hiddenZero++;
  }
  return {nonzero_pixels:pixels,alpha_bbox:pixels?bbox:null,alpha8_bbox:bbox8[0]!==Infinity?bbox8:null,
    opaque_core_pixels:core,potential_skin_carrier_probe_pixels:skinProbe,
    posterior_cap_probe_zero_fraction:hidden?hiddenZero/hidden:null,
    probe_note:'Numerical probes only. Bright warm hair can trigger the skin probe; a back-layer cap may be hidden by the unchanged head. View the actual layered image.'};
}
function inventory() {
  const paths=[];
  function walk(p){for(const d of fs.readdirSync(p,{withFileTypes:true})){const f=path.join(p,d.name);if(d.isDirectory())walk(f);else if(d.name.endsWith('.png'))paths.push(f);}}
  for(const sex of ['male','female'])walk(path.join(ACTIVE,sex));
  return Object.fromEntries(paths.sort().map(f=>[resource(f),sha(f)]));
}
function unchanged(before) {if(JSON.stringify(inventory())!==JSON.stringify(before))throw Error('Existing active PNG inventory changed');}
function darkGuide(image,roi,threshold=100) {
  let sx=0,sy=0,sum=0;
  for(let y=roi[1];y<roi[3];y++)for(let x=roi[0];x<roi[2];x++){
    const k=(y*image.w+x)*4,g=(image.data[k]+image.data[k+1]+image.data[k+2])/3;
    const weight=Math.max(threshold-g,0)*image.data[k+3]/255;sx+=x*weight;sy+=y*weight;sum+=weight;
  }
  return sum?[sx/sum,sy/sum]:null;
}
function solve3(m,v) {
  const a=m.map((r,i)=>[...r,v[i]]);
  for(let c=0;c<3;c++){
    let pivot=c;for(let r=c+1;r<3;r++)if(Math.abs(a[r][c])>Math.abs(a[pivot][c]))pivot=r;
    [a[c],a[pivot]]=[a[pivot],a[c]];
    if(Math.abs(a[c][c])<1e-12)return null;
    const scale=a[c][c];for(let j=c;j<4;j++)a[c][j]/=scale;
    for(let r=0;r<3;r++)if(r!==c){const factor=a[r][c];for(let j=c;j<4;j++)a[r][j]-=factor*a[c][j];}
  }
  return a.map(r=>r[3]);
}
function chinGuide(image) {
  const points=[];
  for(let x=495;x<=540;x++){
    let best=Infinity,bestY=532;
    for(let y=532;y<=553;y++){
      const k=(y*image.w+x)*4,g=(image.data[k]+image.data[k+1]+image.data[k+2])/3;
      if(g<best){best=g;bestY=y;}
    }
    if(bestY>532&&bestY<553&&best<180)points.push([x,bestY]);
  }
  if(points.length<12)return {point:null,status:'manual_read_required',trace:points};
  const s=[0,0,0,0,0],z=[0,0,0];
  for(const [x,y] of points){const t=x-517.5;let p=1;for(let i=0;i<5;i++){s[i]+=p;p*=t;}z[0]+=y;z[1]+=t*y;z[2]+=t*t*y;}
  const coefficients=solve3([[s[4],s[3],s[2]],[s[3],s[2],s[1]],[s[2],s[1],s[0]]],[z[2],z[1],z[0]]);
  if(!coefficients||coefficients[0]>=0)return {point:null,status:'manual_read_required',trace:points};
  const [a,b,c]=coefficients,t=-b/(2*a),x=517.5+t,y=a*t*t+b*t+c;
  return {point:[x,y],status:'draft_dark_jaw_trace_manual_confirmation_required',trace:points,
    centered_parabola_coefficients:coefficients,uncertainty_px:'Painted contour and whole-image shading: approximately1–2px, not certified anatomy.'};
}
async function inspect() {
  const selected=json(path.join(PACK,'source/selected_sources.json'));
  const eye=await read(resource(path.join(ACTIVE,'male/eyes/01.png')));
  const face=await read(resource(path.join(ACTIVE,'male/face_shape/01.png')));
  const rois=[[453,358,475,374],[557,358,581,374]];
  const current={pupil_left:darkGuide(eye,rois[0]),pupil_right:darkGuide(eye,rois[1]),chin:chinGuide(face)};
  const reports=[];
  for(const spec of selected){
    const p=path.join(PACK,'source/approved',spec.key+'.png'),image=await read(resource(p));
    const source={pupil_left:darkGuide(image,rois[0]),pupil_right:darkGuide(image,rois[1]),chin:chinGuide(image)};
    const controls=[source.pupil_left,source.pupil_right,source.chin.point];
    const target=[current.pupil_left,current.pupil_right,current.chin.point];
    const estimate=controls.every(Boolean)&&target.every(Boolean)?fit(controls,target):null;
    reports.push({key:spec.key,approved:resource(p),approved_sha256:sha(p),source_measurements:source,
      draft_source_to_frame:estimate?{matrix_2x3:matrix(estimate),residuals:estimate.residuals,maximum_residual_px:estimate.maximum_residual_px}:null,
      decision:'Reuse current01 face/skin/neck, eyebrows, eyes, nose and mouth. Only hair is proposed as new.',
      reason:'Viewed source portraits retain the same face and very similar brow/eye/nose/lip design. Difference is predominantly hair; do not create near-duplicate features or alter old eye aperture.',
      caveat:'Reusing current01 intentionally keeps existing skin/feature rendering; it is not pixel-identical reconstruction of the approved whole portrait.'});
  }
  const out={schema:'male-four-hair-geometry-draft/v1',current_frame:current,pupil_rois:rois,pupil_threshold:100,
    method:'Read-only dark centroids in fixed native ROIs and draft jaw-trace parabola. Every frame must be visually confirmed before staging.',
    runtime_offsets:[0,0],existing_feature_pixels_changed:false,records:reports};
  writeJson('qa/geometry_draft.json',out);
  console.log(JSON.stringify(reports.map(r=>({key:r.key,source_pupils:[r.source_measurements.pupil_left,r.source_measurements.pupil_right],source_chin:r.source_measurements.chin.point,frame_max_residual:r.draft_source_to_frame?.maximum_residual_px}))));
}
function background(kind) {
  const data=Buffer.alloc(W*H*4),a=kind==='dark'?[37,49,54]:[232,225,214],b=[170,169,164];
  for(let y=0;y<H;y++)for(let x=0;x<W;x++){const color=kind==='checker'&&((Math.floor(x/24)+Math.floor(y/24))%2)?b:a,k=(y*W+x)*4;data[k]=color[0];data[k+1]=color[1];data[k+2]=color[2];data[k+3]=255;}
  return {w:W,h:H,data};
}
function over(dst,src) {
  for(let k=0;k<dst.data.length;k+=4){const a=src.data[k+3]/255;if(!a)continue;const old=dst.data[k+3]/255,out=a+old*(1-a);for(let c=0;c<3;c++)dst.data[k+c]=Math.round((src.data[k+c]*a+dst.data[k+c]*old*(1-a))/out);dst.data[k+3]=Math.round(out*255);}
}
async function renderQA(key,approved,frame,front,back) {
  const fixed={};for(const p of ['clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth'])fixed[p]=await read(resource(path.join(ACTIVE,'male',p,'01.png')));
  let light;
  for(const kind of ['light','dark','checker']){
    const result=background(kind);
    for(const layer of [back,fixed.clothing_back,fixed.face_shape,fixed.clothing,fixed.nose,fixed.eyes,fixed.eyebrows,fixed.mouth,front])over(result,layer);
    await save(result,'qa/'+key+'_assembled_'+kind+'.png');
    const dest=ownOutput('qa/'+key+'_head_'+kind+'.png');
    await sharp(result.data,{raw:{width:W,height:H,channels:4}}).extract({left:305,top:50,width:440,height:530}).resize(880,1060,{kernel:'nearest'}).png().toFile(dest);
    if(kind==='light')light=result;
  }
  const sourceFrame=resample(approved,frame);
  const left=await sharp(sourceFrame.data,{raw:{width:W,height:H,channels:4}}).extract({left:330,top:180,width:385,height:410}).png().toBuffer();
  const right=await sharp(light.data,{raw:{width:W,height:H,channels:4}}).extract({left:330,top:180,width:385,height:410}).png().toBuffer();
  await sharp({create:{width:770,height:410,channels:4,background:'#e8e1d6'}}).composite([{input:left,left:0,top:0},{input:right,left:385,top:0}]).png().toFile(ownOutput('qa/'+key+'_source_left_actual_right.png'));
}
async function stage(configPath) {
  const config=json(configPath);
  if(config.placeholder)throw Error('Replace all example registration values with measured native/source/frame controls first');
  if(!Array.isArray(config.styles)||config.styles.length!==4)throw Error('Expected four selected hair styles');
  const before=inventory(),records=[];
  const configBytes=fs.readFileSync(configPath);
  const slots=[];
  for(const spec of config.styles){
    const approvedPath=resolve(spec.approved),approved=await read(spec.approved);
    if(approved.w!==W||approved.h!==H)throw Error('Approved source canvas changed');
    if(spec.approved_sha256&&sha(approvedPath)!==spec.approved_sha256)throw Error('Approved source changed: '+spec.key);
    const frame=transform(spec.source_to_frame,spec.key+' common frame');
    const limit=Number(config.maximum_frame_residual_px??2.5);
    if(frame.maximum_residual_px!==null&&frame.maximum_residual_px>limit)throw Error('Common-frame residual requires review: '+spec.key);
    const parts={};
    for(const part of ['hair_front','hair_back']){
      const layer=spec.layers?.[part];
      if(!layer)throw Error('Missing independent layer: '+spec.key+'/'+part);
      const raw=await read(layer.native),nativeToSource=transform(layer.native_to_source,spec.key+'/'+part);
      const combined=compose(frame,nativeToSource),noise=Number(layer.noise_alpha_max??2);
      if(noise<0||noise>4)throw Error('Noise removal must be limited to alpha0..4');
      const clean=cleanNoise(raw,noise),output=resample(clean.image,combined);
      const out='male/'+part+'/'+String(spec.index).padStart(2,'0')+'.png';
      const dest=await save(output,out),stats=alphaStats(output);
      if(!stats.nonzero_pixels)throw Error('Empty staged hair: '+spec.key+'/'+part);
      parts[part]=output;
      records.push({key:spec.key,part,name:layer.name||'',physical_index:spec.index,style_id:spec.style_id,
        native:resource(raw.path),native_sha256:sha(raw.path),native_size:[raw.w,raw.h],
        native_to_approved:matrix(nativeToSource),native_control_residuals:nativeToSource.residuals,
        common_source_to_frame:matrix(frame),frame_control_residuals:frame.residuals,
        combined_native_to_frame:matrix(combined),one_pass_premultiplied_alpha_sampling:true,
        output:out,sha256:sha(dest),canvas:[W,H],runtime_offset:[0,0],
        removed_alpha_at_most:noise,removed_nonzero_noise_pixels:clean.removed,
        color_reconstruction:false,approved_rgb_copy:false,tint_applied:false,local_warps:false,alpha_stats:stats});
      slots.push({part,name:layer.name||'',source:resource(dest),index:spec.index,style_id:spec.style_id,approved_portrait:resource(approvedPath)});
    }
    await renderQA(spec.key,approved,frame,parts.hair_front,parts.hair_back);
  }
  unchanged(before);
  if(!fs.readFileSync(configPath).equals(configBytes))throw Error('Registration changed while processing');
  const result={schema:'male-four-hair-staged/v1',status:'staged_for_actual_composition_review_not_installed',
    registration:resource(configPath),records,additions:slots,
    unchanged_active_pngs:Object.keys(before).length,existing_skin_and_features_untouched:true,
    processing:'Native image_gen RGBA only; remove at most alpha1..4 noise, compose registration matrices, and resample once. No drawing, RGB recovery, matting, recoloring or independent feature movement.',
    deduplication:['face_shape01','eyebrows01','eyes01','nose01','mouth01'],active_config_modified:false};
  writeJson('source/hair_staging_report.json',result);
  console.log(JSON.stringify({staged:records.length,old_pngs_preserved:Object.keys(before).length,outputs:records.map(r=>({file:r.output,sha256:r.sha256}))}));
}
(async()=>{
  const args=process.argv.slice(2);
  if(args.includes('--inspect'))return inspect();
  const i=args.indexOf('--config'),configPath=i>=0?resolve(args[i+1]):path.join(PACK,'source/hair_registration.json');
  if(!args.includes('--stage')){
    console.log(JSON.stringify({mode:'read_only',commands:['node source/register_hair.cjs --inspect','node source/register_hair.cjs --stage --config source/hair_registration.json'],registration_exists:fs.existsSync(configPath),active_writes:false}));
    return;
  }
  await stage(configPath);
})().catch(error=>{console.error(error.stack||String(error));process.exitCode=1;});

