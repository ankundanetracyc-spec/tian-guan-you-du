// Hash-bound, measured PNG guard. Targets come only from the frozen snapshot rig.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const resolveRes=f=>f.startsWith('res://')?path.join(project,f.slice(6)):path.resolve(f);
function bounds(d,y,lo,hi,t){let a=-1,b=-1;for(let x=lo;x<=hi;x++)if(d[(y*1024+x)*4+3]>=t){if(a<0)a=x;b=x;}return a<0?null:[a,b]}
function measure(data,control){
 const left=control.side==='left',lo=left?350:518,hi=left?516:685,t=control.alpha_threshold;
 if(control.detector==='first_side_row_inner_pixel'){
  for(let y=490;y<620;y++){const b=bounds(data,y,lo,hi,t);if(b)return[left?b[1]:b[0],y];}
  return null;
 }
 const y=control.px[1],b=bounds(data,y,lo,hi,t);if(!b)return null;
 return[control.detector==='row_outer_pixel'?(left?b[0]:b[1]):(left?b[1]:b[0]),y];
}
async function validate(candidateDirectory=path.join(pack,'male/clothing'),options={}){
 const rigFile=path.join(__dirname,'clothing_strict_rig.json');
 const rig=JSON.parse(fs.readFileSync(rigFile,'utf8'));
 const indices=options.indices??[1,2,3,4,5,6,7];
 const report={pass:false,rig_id:rig.id,rig_sha256:hash(rigFile),scope:indices.length===7?'all_seven_required':'explicit_subset_review_only',indices,checks:0,failures:[],garments:[]};
 const check=(ok,message,detail)=>{report.checks++;if(!ok)report.failures.push({message,...(detail?{detail}: {})});};
 const activeFace=resolveRes(rig.face.active_resource),faceFile=resolveRes(rig.face.snapshot_source);
 check(fs.existsSync(activeFace)&&hash(activeFace)===rig.face.sha256,'Current face SHA equals frozen face');
 check(fs.existsSync(faceFile)&&hash(faceFile)===rig.face.sha256,'Snapshot face SHA equals frozen face');
 const face=await sharp(faceFile).ensureAlpha().raw().toBuffer();
 for(const id of indices){
  const record=rig.garments.find(g=>g.index===Number(id)),tag=String(id).padStart(2,'0');
  if(!record){check(false,'No frozen garment record '+tag);continue;}
  const candidate=path.join(candidateDirectory,tag+'.png'),originalFile=resolveRes(record.snapshot_source);
  check(fs.existsSync(candidate),'Completed candidate exists '+tag);
  if(!fs.existsSync(candidate)){report.garments.push({index:Number(id),pass:false,missing:true});continue;}
  check(hash(originalFile)===record.snapshot_sha256,'Snapshot garment SHA '+tag);
  const meta=await sharp(candidate).metadata();
  check(meta.width===1024&&meta.height===1536&&meta.channels===4&&meta.hasAlpha,'Candidate transparent RGBA canvas '+tag);
  if(meta.width!==1024||meta.height!==1536||meta.channels!==4)continue;
  const old=await sharp(originalFile).ensureAlpha().raw().toBuffer(),actual=await sharp(candidate).ensureAlpha().raw().toBuffer();
  const beforeFailures=report.failures.length,result={index:Number(id),candidate_sha256:hash(candidate),source_sha256:record.snapshot_sha256,controls:[],contacts:[]};
  const points=record.preserved_controls.map(c=>c.px);let noncollinear=false;
  for(let i=0;i<points.length&&!noncollinear;i++)for(let j=i+1;j<points.length&&!noncollinear;j++)for(let k=j+1;k<points.length;k++){
   const a=points[i],b=points[j],c=points[k];
   if(Math.abs((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))>1){noncollinear=true;break;}
  }
  check(noncollinear,'Snapshot provides >=3 actual noncollinear original frame controls '+tag);
  for(const c of record.preserved_controls){
   const measured=measure(actual,c),residual=measured?Math.hypot(measured[0]-c.px[0],measured[1]-c.px[1]):Infinity;
   result.controls.push({id:c.id,role:c.role,target_px:c.px,actual_px:measured,residual_px:Number.isFinite(residual)?residual:null});
   check(residual<=rig.maximum_control_residual_px,`Preserved control <=2px ${tag}/${c.id}`,{target:c.px,actual:measured,residual});
  }
  let changed=0,opaqueChanges=0,bodyChanges=0,outsideEnvelope=0;const bbox=[1024,1536,-1,-1];
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
   const k=(y*1024+x)*4;if(![0,1,2,3].some(c=>old[k+c]!==actual[k+c]))continue;
   changed++;bbox[0]=Math.min(bbox[0],x);bbox[1]=Math.min(bbox[1],y);bbox[2]=Math.max(bbox[2],x);bbox[3]=Math.max(bbox[3],y);
   if(old[k+3]>=230)opaqueChanges++;
   if(y>=620)bodyChanges++;
   const sr=record.frozen_collar_rows.find(r=>r.y===y),nr=rig.face.neck_outline.find(r=>r.y===y);
   const leftAllowed=sr?.left40&&nr&&x>=sr.left40[1]-2&&x<=Math.max(sr.left40[1]+2,nr.left+2);
   const rightAllowed=sr?.right40&&nr&&x>=Math.min(sr.right40[0]-2,nr.right-2)&&x<=sr.right40[0]+2;
   if(y<520||y>570||!(leftAllowed||rightAllowed))outsideEnvelope++;
  }
  result.changed_pixels=changed;result.changed_bbox_inclusive=bbox;result.original_opaque_changed_pixels=opaqueChanges;result.body_y_ge620_changed_pixels=bodyChanges;result.outside_tiny_connector_envelope_changes=outsideEnvelope;
  check(opaqueChanges===0,'All original opaque garment RGBA preserved '+tag,{opaqueChanges});
  check(bodyChanges===0,'Body y>=620 RGBA preserved '+tag,{bodyChanges});
  check(outsideEnvelope===0,'Changes confined to frozen tiny inner-connector envelope '+tag,{outsideEnvelope});
  for(const side of ['left','right'])for(const c of rig.shared_contact_controls[side]){
   if(c.role==='hidden_coverage_only'){
    const k=(c.px[1]*1024+c.px[0])*4;
    const a=255-(255-face[k+3])*(255-actual[k+3])/255;
    result.contacts.push({id:c.id,role:c.role,target_px:c.px,composed_alpha:a});
    check(a>=230,'Hidden neck support covered '+tag+'/'+c.id);continue;
   }
   const row=bounds(actual,c.px[1],side==='left'?350:518,side==='left'?516:685,230);
   const measured=row?[side==='left'?row[1]:row[0],c.px[1]]:null;
   const residual=measured?Math.hypot(measured[0]-c.px[0],measured[1]-c.px[1]):Infinity;
   result.contacts.push({id:c.id,role:c.role,target_px:c.px,actual_px:measured,residual_px:Number.isFinite(residual)?residual:null});
   check(residual<=2,'Measured solid neckline interface <=2px '+tag+'/'+c.id,{target:c.px,actual:measured,residual});
  }
  let worst=255,cracks=0;
  for(let y=530;y<=565;y++){
   const sr=record.frozen_collar_rows.find(r=>r.y===y),nr=rig.face.neck_outline.find(r=>r.y===y);if(!sr||!nr)continue;
   const segments=[];
   if(sr.left230)segments.push([sr.left230[1]+1,nr.left]);
   if(sr.right230)segments.push([nr.right,sr.right230[0]-1]);
   for(const [lo,hi]of segments)for(let x=lo;x<=hi;x++){
    const k=(y*1024+x)*4,a=255-(255-face[k+3])*(255-actual[k+3])/255;
    worst=Math.min(worst,a);if(a<230)cracks++;
   }
  }
  result.minimum_composed_join_alpha=worst;result.join_pixels_below230=cracks;
  check(cracks===0,'Whole required connection segment has solid composed support '+tag,{worst,cracks});
  const registration=path.join(__dirname,`clothing${tag}_strict_registration.json`);
  check(fs.existsSync(registration),'Measured identity-frame registration record exists '+tag);
  if(fs.existsSync(registration)){
   const r=JSON.parse(fs.readFileSync(registration,'utf8'));
   check(r.output_sha256===result.candidate_sha256&&r.snapshot_sha256===record.snapshot_sha256&&r.rig_sha256===report.rig_sha256,'Registration binds exact original, target rig and final PNG '+tag);
   check(r.original_garment_frame?.scale===1&&r.original_garment_frame?.rotation_degrees===0&&JSON.stringify(r.original_garment_frame?.translation)==='[0,0]','Original garment frame identity '+tag);
   check(r.ai_art_source&&fs.existsSync(resolveRes(r.ai_art_source))&&hash(resolveRes(r.ai_art_source))===r.ai_art_sha256,'New connector pigment source is retained AI raw '+tag);
  }
  result.maximum_preserved_control_residual_px=Math.max(...result.controls.map(c=>c.residual_px??Infinity));
  result.maximum_visible_interface_residual_px=Math.max(...result.contacts.filter(c=>c.role==='visible_join').map(c=>c.residual_px??Infinity));
  result.pass=report.failures.length===beforeFailures;report.garments.push(result);
 }
 report.pass=report.failures.length===0;
 const reportFile=options.reportFile??path.join(pack,'qa/clothing_strict_guard_report.json');
 fs.writeFileSync(reportFile,JSON.stringify(report,null,2)+'\n');
 return report;
}
module.exports={validate};
if(require.main===module){
 const arg=process.argv.indexOf('--indices'),indices=arg>=0?process.argv[arg+1].split(',').map(Number):undefined;
 validate(undefined,{indices}).then(r=>{console.log(JSON.stringify({pass:r.pass,scope:r.scope,checks:r.checks,failures:r.failures,garments:r.garments.map(g=>({index:g.index,pass:g.pass,max_preserved:g.maximum_preserved_control_residual_px,max_interface:g.maximum_visible_interface_residual_px}))}));process.exitCode=r.pass?0:1;}).catch(e=>{console.error(e);process.exitCode=1;});
}
