// Two-depth garment gate; this intentionally does not apply the rejected single-layer contract.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const resolveRes=f=>f.startsWith('res://')?path.join(project,f.slice(6)):path.join(pack,f);
const decode=async file=>sharp(file).ensureAlpha().raw().toBuffer();
function bounds(d,y,lo,hi,t){let l=-1,r=-1;for(let x=lo;x<=hi;x++)if(d[(y*1024+x)*4+3]>=t){if(l<0)l=x;r=x;}return l<0?null:[l,r]}
function localTip(visible,c){const x=c.px[0];for(let y=c.px[1]-4;y<=c.px[1]+4;y++)if(visible[(y*1024+x)*4+3]>=c.alpha_threshold)return[x,y];return null;}
function noncollinear(points){for(let a=0;a<points.length;a++)for(let b=a+1;b<points.length;b++)for(let c=b+1;c<points.length;c++)if(Math.abs((points[b][0]-points[a][0])*(points[c][1]-points[a][1])-(points[b][1]-points[a][1])*(points[c][0]-points[a][0]))>1)return true;return false;}
async function validate(candidatePack=pack,options={}){
 const rigFile=path.join(__dirname,'clothing_strict_v2_rig.json'),rig=JSON.parse(fs.readFileSync(rigFile,'utf8'));
 const indices=options.indices??[1,2,3,4,5,6,7];
 const report={pass:false,rig_id:rig.id,rig_sha256:hash(rigFile),scope:indices.length===7?'all_seven_pairs_required':'explicit_subset_review_only',checks:0,failures:[],garments:[],numeric_pass_is_not_visual_approval:true};
 const check=(ok,message,detail)=>{report.checks++;if(!ok)report.failures.push({message,...(detail?{detail}: {})});};
 const activeFace=resolveRes(rig.face.active_resource),faceFile=resolveRes(rig.face.snapshot_source);
 check(fs.existsSync(activeFace)&&hash(activeFace)===rig.face.sha256,'Current fixed face matches frozen SHA');
 check(hash(faceFile)===rig.face.sha256,'Snapshot face matches frozen SHA');
 const face=await decode(faceFile),masterFile=resolveRes(rig.pure_band_master.file),geometryFile=resolveRes(rig.pure_band_master.geometry_record);
 check(hash(masterFile)===rig.pure_band_master.sha256,'Pure band master matches frozen SHA');
 check(hash(geometryFile)===rig.pure_band_master.geometry_record_sha256,'Pure band geometry report matches frozen SHA');
 const master=await decode(masterFile),nativeFile=resolveRes(rig.pure_band_master.native_source),native=await sharp(nativeFile).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 check(hash(nativeFile)===rig.pure_band_master.native_source_sha256,'Retained native AI rear band matches provenance SHA');
 check(noncollinear(rig.pure_band_master.source_controls),'Measured rear source controls are noncollinear');
 let masterMaximumResidual=0;const tf=rig.pure_band_master.transform;
 for(let i=0;i<rig.pure_band_master.source_controls.length;i++){
  const s=rig.pure_band_master.source_controls[i],t=rig.pure_band_master.target_controls[i];
  const sourceAlpha=native.data[(s[1]*native.info.width+s[0])*4+3];
  check(sourceAlpha>=230,'Actual source rim control is supported native pigment '+i);
  check(s[1]===0||native.data[((s[1]-1)*native.info.width+s[0])*4+3]<230,'Actual native rim control is the measured first solid pixel in its column '+i);
  const predicted=[tf.a*s[0]-tf.b*s[1]+tf.tx,tf.b*s[0]+tf.a*s[1]+tf.ty],residual=Math.hypot(predicted[0]-t[0],predicted[1]-t[1]);
  masterMaximumResidual=Math.max(masterMaximumResidual,residual);check(residual<=2,'Native rear source-to-target residual <=2px '+i,{source:s,target:t,predicted,residual});
  let actualY=null;for(let y=t[1]-4;y<=t[1]+4;y++)if(master[(y*1024+t[0])*4+3]>=230){actualY=y;break;}
  check(actualY!==null&&Math.abs(actualY-t[1])<=2,'Registered pure rear PNG actual rim <=2px '+i,{target:t,actual:actualY===null?null:[t[0],actualY]});
 }
 report.shared_geometry_maximum_residual_px=masterMaximumResidual;
 for(const id of indices){
  const g=rig.garments.find(g=>g.index===Number(id)),tag=String(id).padStart(2,'0'),start=report.failures.length;
  if(!g){check(false,'Frozen pair record exists '+tag);continue;}
  const files={front:path.join(candidatePack,g.front_file),rear:path.join(candidatePack,g.rear_file),band:path.join(candidatePack,g.pure_band_file),record:path.join(candidatePack,g.pair_record)};
  const missing=Object.entries(files).filter(([,file])=>!fs.existsSync(file)).map(([name])=>name);
  check(missing.length===0,'Completed front/rear/pure-band/registration exist '+tag,{missing});
  if(missing.length){report.garments.push({index:Number(id),pass:false,missing});continue;}
  const originalFile=resolveRes(g.snapshot_source);check(hash(originalFile)===g.snapshot_sha256,'Original garment snapshot SHA '+tag);
  for(const [name,file]of Object.entries(files).filter(([n])=>n!=='record')){const m=await sharp(file).metadata();check(m.width===1024&&m.height===1536&&m.channels===4&&m.hasAlpha,`Common RGBA canvas ${tag}/${name}`);}
  const original=await decode(originalFile),front=await decode(files.front),rear=await decode(files.rear),band=await decode(files.band),record=JSON.parse(fs.readFileSync(files.record,'utf8'));
  check(record.face_sha256===rig.face.sha256&&record.snapshot_sha256===g.snapshot_sha256,'Pair record binds frozen face and original '+tag);
  check(record.front.sha256===hash(files.front)&&record.rear.sha256===hash(files.rear)&&record.pure_band_sha256===hash(files.band),'Pair record binds exact front/rear/pure band PNGs '+tag);
  check(record.shared_geometry_record_sha256===rig.pure_band_master.geometry_record_sha256,'Pair refers to selected pure-band geometry '+tag);
  check(record.part_ownership_split_y===550&&record.local_warp===false,'Pair records physical split and no local warp '+tag);
  check(record.front.identity_frame.scale===1&&record.front.identity_frame.rotation_degrees===0&&JSON.stringify(record.front.identity_frame.translation)==='[0,0]','Original front/body identity frame '+tag);
  let frontUpperPixels=0,frontLowerChanges=0,bodyChanges=0,bandAlphaChanges=0;
  const cloth=Buffer.alloc(front.length),visible=Buffer.alloc(front.length);
  for(let k=0;k<front.length;k+=4){const y=Math.floor(k/4/1024);
   if(y<550){if(front[k]||front[k+1]||front[k+2]||front[k+3])frontUpperPixels++;}
   else if([0,1,2,3].some(c=>front[k+c]!==original[k+c])){frontLowerChanges++;if(y>=620)bodyChanges++;}
   if(band[k+3]!==master[k+3])bandAlphaChanges++;
   const ra=rear[k+3]/255,fa=front[k+3]/255,sa=face[k+3]/255;
   cloth[k+3]=Math.round((fa+ra*(1-fa))*255);
   visible[k+3]=Math.round((fa+ra*(1-sa)*(1-fa))*255);
  }
  check(frontUpperPixels===0,'Anterior layer has no original rear tips before550 '+tag,{frontUpperPixels});
  check(frontLowerChanges===0,'Front V/body RGBA exactly original from550 '+tag,{frontLowerChanges});
  check(bodyChanges===0,'Frozen body RGBA exactly original from620 '+tag,{bodyChanges});
  check(bandAlphaChanges===0,'Variant pure-band alpha equals selected pure01 master '+tag,{bandAlphaChanges});
  // Recreate only the declared layer assembly, not an alternative artwork method.
  const upper=Buffer.from(original);upper.fill(0,550*1024*4);
  const upperPng=await sharp(upper,{raw:{width:1024,height:1536,channels:4}}).png().toBuffer();
  const expectedRear=await sharp(files.band).composite([{input:upperPng}]).ensureAlpha().raw().toBuffer();
  check(expectedRear.equals(rear),'Final rear is continuous new band + this original upper surface '+tag);
  check(noncollinear(g.preserved_controls.map(c=>c.px)),'Original garment frame provides noncollinear actual controls '+tag);
  const controls=[];
  for(const c of g.preserved_controls){let actual=null;
   if(c.role==='preserved_original_tip')actual=localTip(visible,c);
   else {const left=c.side==='left',d=c.role==='preserved_front_v'?front:cloth,b=bounds(d,c.px[1],left?350:518,left?516:685,c.alpha_threshold);if(b)actual=[c.role==='preserved_front_v'?(left?b[1]:b[0]):(left?b[0]:b[1]),c.px[1]];}
   const residual=actual?Math.hypot(actual[0]-c.px[0],actual[1]-c.px[1]):Infinity;controls.push({id:c.id,role:c.role,target:c.px,actual,residual_px:Number.isFinite(residual)?residual:null});
   check(residual<=2,'Actual selected-pair frame/V control <=2px '+tag+'/'+c.id,{target:c.px,actual,residual});
  }
  let centerMinimum=255,solidBandMissing=0;
  for(let y=530;y<=570;y++){const n=rig.face.neck_outline.find(n=>n.y===y);for(let x=n.left;x<=n.right;x++){const a=band[(y*1024+x)*4+3];centerMinimum=Math.min(centerMinimum,a);if(a<230)solidBandMissing++;}}
  check(solidBandMissing===0,'Pure rear band retains continuous solid hidden center '+tag,{centerMinimum,solidBandMissing});
  let closureMinimum=255,closureCracks=0;
  for(let y=530;y<=565;y++){const s=g.frozen_collar_rows.find(r=>r.y===y),n=rig.face.neck_outline.find(n=>n.y===y);const spans=[];if(s?.left230)spans.push([s.left230[1]+1,n.left]);if(s?.right230)spans.push([n.right,s.right230[0]-1]);for(const [lo,hi]of spans)for(let x=lo;x<=hi;x++){const k=(y*1024+x)*4,ra=rear[k+3]/255,sa=face[k+3]/255,fa=front[k+3]/255,alpha=(fa+(sa+ra*(1-sa))*(1-fa))*255;closureMinimum=Math.min(closureMinimum,alpha);if(alpha<230)closureCracks++;}}
  check(closureCracks===0,'Actual rear-face-front order closes neck-side connection '+tag,{closureMinimum,closureCracks});
  if(g.palette_record){const pr=resolveRes(g.palette_record);check(fs.existsSync(pr),'Native material variant provenance exists '+tag);if(fs.existsSync(pr)){const p=JSON.parse(fs.readFileSync(pr,'utf8'));check(p.master_pure_band_sha256===rig.pure_band_master.sha256&&p.output_sha256===hash(files.band)&&p.output_alpha_changed_pixels===0,'Palette maps native material onto exact pure-band geometry '+tag);const art=resolveRes(p.native_material_source);check(fs.existsSync(art)&&hash(art)===p.native_material_sha256,'Selected native material raw SHA '+tag);}}
  report.garments.push({index:Number(id),pass:report.failures.length===start,front_sha256:hash(files.front),rear_sha256:hash(files.rear),pure_band_sha256:hash(files.band),controls,maximum_frame_control_residual_px:Math.max(...controls.map(c=>c.residual_px??Infinity)),front_upper_nonzero_pixels:frontUpperPixels,front_y_ge550_rgba_changed_pixels:frontLowerChanges,body_y_ge620_rgba_changed_pixels:bodyChanges,pure_band_alpha_changed_pixels:bandAlphaChanges,minimum_hidden_band_alpha:centerMinimum,minimum_actual_layer_closure_alpha:closureMinimum,closure_cracks:closureCracks});
 }
 report.pass=report.failures.length===0;fs.writeFileSync(options.reportFile??path.join(pack,'qa/clothing_strict_v2_guard_report.json'),JSON.stringify(report,null,2)+'\n');return report;
}
module.exports={validate};
if(require.main===module){const a=process.argv.indexOf('--indices'),indices=a<0?undefined:process.argv[a+1].split(',').map(Number);validate(undefined,{indices}).then(r=>{console.log(JSON.stringify({pass:r.pass,scope:r.scope,checks:r.checks,shared_geometry_maximum_residual_px:r.shared_geometry_maximum_residual_px,failures:r.failures,pairs:r.garments.map(g=>({index:g.index,pass:g.pass,max_frame:g.maximum_frame_control_residual_px,missing:g.missing}))}));process.exitCode=r.pass?0:1;}).catch(e=>{console.error(e);process.exitCode=1;});}
