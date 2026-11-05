// Install seven bound front/rear clothing pairs and two upper-nose corrections.
// All source files and the strict-v2 guard must pass before any active file changes.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const {isDeepStrictEqual}=require('util');
const project=path.resolve(__dirname,'../../../..'),pack=path.resolve(__dirname,'..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
const installation=path.join(pack,'installation'),snapshot=path.join(installation,'backup_before');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const hashText=text=>crypto.createHash('sha256').update(text,'utf8').digest('hex');
const resource=file=>'res://'+path.relative(project,file).replaceAll('\\','/');
const NOSE_PRESERVE_FROM_Y=415,REAR_FILE='male/clothing_back/{index}.png';
const MALE_OVERLAY='res://mods/appearance/portrait_nasion_clothing_20261104_v4/rig/male_overlay.png';
const walk=dir=>!fs.existsSync(dir)?[]:fs.readdirSync(dir,{withFileTypes:true}).flatMap(entry=>entry.isDirectory()?walk(path.join(dir,entry.name)):[path.join(dir,entry.name)]);

function configWithRearBinding(originalText){
 const original=JSON.parse(originalText);
 const clothes=original.components.filter(entry=>entry.id==='male_clothes'&&entry.gender==='male'&&entry.part==='clothes');
 if(clothes.length!==1||Object.hasOwn(clothes[0],'rear_file'))throw Error('Snapshot must have one male_clothes entry without rear_file.');
 const pattern=/^([ \t]*)("file"\s*:\s*"male\/clothing\/\{index\}\.png"\s*,)(\r?\n)/gm;
 const matches=[...originalText.matchAll(pattern)];
 if(matches.length!==1)throw Error('Expected one male clothing file line for byte-preserving insertion.');
 const insertedLine=matches[0][1]+'"rear_file": "'+REAR_FILE+'",'+matches[0][3];
 let text=originalText.replace(pattern,match=>match+insertedLine);
 if(Object.hasOwn(original,'anchor_overlays'))throw Error('Original snapshot must not already override anchor overlays.');
 const anchorPattern=/^([ \t]*)("enabled"\s*:\s*true\s*,)(\r?\n)/gm,anchorMatches=[...originalText.matchAll(anchorPattern)];
 if(anchorMatches.length!==1)throw Error('Expected one enabled line for byte-preserving anchor_overlays insertion.');
 const anchorInsertedLine=anchorMatches[0][1]+'"anchor_overlays": {"male": "'+MALE_OVERLAY+'"},'+anchorMatches[0][3];
 text=text.replace(anchorPattern,match=>match+anchorInsertedLine);
 if(text.replace(insertedLine,'').replace(anchorInsertedLine,'')!==originalText)throw Error('Configuration changed bytes outside the two authorized binding lines.');
 const edited=JSON.parse(text),updated=edited.components.find(entry=>entry.id==='male_clothes');
 if(updated.rear_file!==REAR_FILE)throw Error('Rear binding was not inserted into male_clothes.');
 delete updated.rear_file;
 if(!isDeepStrictEqual(edited.anchor_overlays,{male:MALE_OVERLAY}))throw Error('Only the male anchor overlay may be overridden.');
 delete edited.anchor_overlays;
 if(!isDeepStrictEqual(edited,original))throw Error('Configuration semantics changed beyond rear_file and male anchor_overlays.');
 return{text,insertedLine,insertedLines:[insertedLine,anchorInsertedLine]};
}

async function main(){
 if(!fs.existsSync(path.join(installation,'before_install.zip')))throw Error('Original pre-install archive required.');
 const original=JSON.parse(fs.readFileSync(path.join(installation,'preserved_baseline.json'),'utf8'));
 const baselineAll=original.baseline_sha256;
 if(Object.keys(baselineAll).length!==44)throw Error('Expected 44 original option PNG hashes.');
 const originalConfigFile=path.join(snapshot,'appearance_parts.snapshot.json');
 if(hash(originalConfigFile)!==original.configuration_sha256['appearance_parts.json'])throw Error('Original configuration snapshot changed.');
 const originalConfigText=fs.readFileSync(originalConfigFile,'utf8'),patch=configWithRearBinding(originalConfigText);
 const configuration=path.join(active,'appearance_parts.json'),currentConfig=fs.readFileSync(configuration,'utf8');
 if(currentConfig!==originalConfigText&&currentConfig!==patch.text)throw Error('Active config changed outside authorized rear_file/anchor_overlays bindings.');
 const config=JSON.parse(patch.text),total=config.components.reduce((sum,entry)=>sum+entry.variant_count,0);
 if(total!==44)throw Error('Rear garments must not add independent appearance options.');
 const overlayFile=path.join(project,MALE_OVERLAY.slice(6));
 if(!fs.existsSync(overlayFile))throw Error('Updated male anchor overlay must exist before installation: '+MALE_OVERLAY);
 const overlayMeta=await sharp(overlayFile).metadata();
 if(overlayMeta.width!==1024||overlayMeta.height!==1536||!overlayMeta.hasAlpha)throw Error('Male anchor overlay must be1024x1536 with alpha.');
 const required=['male/nose/01.png','female/nose/01.png',...Array.from({length:7},(_,i)=>'male/clothing/'+String(i+1).padStart(2,'0')+'.png'),...Array.from({length:7},(_,i)=>'male/clothing_back/'+String(i+1).padStart(2,'0')+'.png')];
 const candidates=['male','female'].flatMap(gender=>['nose','clothing','clothing_back'].flatMap(part=>walk(path.join(pack,gender,part)))).filter(file=>/^(male|female)\/(nose|clothing|clothing_back)\/\d{2}\.png$/.test(path.relative(pack,file).replaceAll('\\','/'))).sort();
 const present=new Set(candidates.map(file=>path.relative(pack,file).replaceAll('\\','/')));
 const missing=required.filter(file=>!present.has(file)),extra=[...present].filter(file=>!required.includes(file));
 if(missing.length||extra.length||present.size!==16)throw Error('All sixteen scoped PNGs (seven front/rear pairs and two noses) are required. Missing: '+missing.join(', ')+'; extra: '+extra.join(', '));
 const strictGuard=path.join(__dirname,'clothing_strict_v2_guard.cjs');
 if(!fs.existsSync(strictGuard))throw Error('Strict-v2 paired clothing/anchor guard required before installation.');
 require('child_process').execFileSync(process.execPath,[strictGuard],{stdio:'inherit'});
 const pending=[];
 for(const candidate of candidates){
  const relative=path.relative(pack,candidate).replaceAll('\\','/'),[gender,part,filename]=relative.split('/');
  const output=path.join(active,relative),before=path.join(snapshot,relative),key=resource(output);
  const isNew=gender==='male'&&part==='clothing_back';
  if(!isNew&&(!baselineAll[key]||!fs.existsSync(before)||hash(before)!==baselineAll[key]))throw Error('Candidate must replace its registered immutable baseline: '+relative);
  if(isNew&&fs.existsSync(before))throw Error('Rear layers must be absent from the original snapshot: '+relative);
  const metadata=await sharp(candidate).metadata();
  if(metadata.width!==1024||metadata.height!==1536||metadata.channels!==4||!metadata.hasAlpha)throw Error('Expected 1024x1536 transparent RGBA: '+candidate);
  const bytes=await sharp(candidate).ensureAlpha().raw().toBuffer();
  let visible=false,transparent=false;
  for(let k=3;k<bytes.length;k+=4){visible||=bytes[k]>0;transparent||=bytes[k]===0;}
  if(!visible||!transparent)throw Error('Candidate needs visible content and transparent space: '+relative);
  if(part==='nose'){
   const old=await sharp(before).ensureAlpha().raw().toBuffer();
   if(!bytes.subarray(NOSE_PRESERVE_FROM_Y*1024*4).equals(old.subarray(NOSE_PRESERVE_FROM_Y*1024*4)))throw Error('Nose at y>=415 must remain byte-identical: '+relative);
  }
  const candidateHash=hash(candidate),beforeHash=isNew?null:baselineAll[key];
  if(fs.existsSync(output)){
   const currentHash=hash(output);
   if(currentHash!==candidateHash&&currentHash!==beforeHash)throw Error('Target changed outside this installation: '+key);
  }else if(!isNew)throw Error('Original target disappeared: '+key);
  pending.push({gender,part,index:Number(filename.slice(0,2)),candidate,output,before,isNew,sha256:candidateHash,before_sha256:beforeHash,changed_from_snapshot:isNew||candidateHash!==beforeHash});
 }
 const replaced=new Set(pending.filter(entry=>!entry.isNew).map(entry=>resource(entry.output)));
 const baseline=Object.fromEntries(Object.entries(baselineAll).filter(([file])=>!replaced.has(file)));
 if(Object.keys(baseline).length!==35)throw Error('Expected 35 preserved original PNGs.');
 const verifyBaseline=()=>{for(const[file,expected]of Object.entries(baseline))if(hash(path.join(project,file.slice(6)))!==expected)throw Error('Preserved PNG differs: '+file);};
 verifyBaseline();
 const manifestFile=path.join(active,'portrait_manifest.json'),manifestBeforeHash=hash(manifestFile),records=[];
 for(const entry of pending){
  fs.mkdirSync(path.dirname(entry.output),{recursive:true});fs.copyFileSync(entry.candidate,entry.output);
  records.push({gender:entry.gender,part:entry.part,index:entry.index,resource_path:resource(entry.output),candidate_path:resource(entry.candidate),source_path:resource(entry.candidate),before_resource_path:entry.isNew?null:resource(entry.before),new_file:entry.isNew,changed_from_snapshot:entry.changed_from_snapshot,sha256:entry.sha256,before_sha256:entry.before_sha256});
 }
 fs.writeFileSync(configuration,patch.text,'utf8');
 const manifest=JSON.parse(fs.readFileSync(manifestFile,'utf8'));
 manifest.total_pngs=51;
 manifest.blend_order=['hair_back','clothing_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front'];
 manifest.private_sublayers={clothing_back:{parent_slot:'clothing',file:REAR_FILE,gender:'male',independent_option:false}};
 manifest.nasion_clothing_20261104_v4={source:resource(pack),status:'installed_awaiting_visual_review',changed_parts:records.map(record=>({gender:record.gender,part:record.part,index:record.index})),independent_selections:true,paired_rear_clothing_count:7,option_count_unchanged:44,nose_lower_region_preserved_from_y:NOSE_PRESERVE_FROM_Y,strict_guard:resource(strictGuard),strict_guard_sha256:hash(strictGuard)};
 fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2)+'\n');verifyBaseline();
 if(fs.readFileSync(configuration,'utf8')!==patch.text)throw Error('Appearance configuration changed during installation.');
 const changedCount=records.filter(record=>record.changed_from_snapshot).length;
 const restoredConfig=patch.insertedLines.reduce((text,line)=>text.replace(line,''),patch.text);
 const report={installed:true,generated_at:new Date().toISOString(),active_variant_count:total,physical_png_count:51,original_png_count:44,new_rear_png_count:7,replaced_existing_png_count:9,male_counts:[3,3,3,2,1,1,1,7,1],female_counts:[3,3,3,2,1,1,1,7,1],candidate_count:16,expected_installed_count:16,expected_changed_count:changedCount,actual_changed_count:changedCount,records,baseline_sha256:baseline,all_before_png_sha256:baselineAll,config_sha256:hash(configuration),config_before_sha256:original.configuration_sha256['appearance_parts.json'],config_inserted_lines:patch.insertedLines,config_only_authorized_bindings:true,authorized_configuration_fields:['components.male_clothes.rear_file','anchor_overlays.male'],config_removed_added_lines_sha256:hashText(restoredConfig),anchor_overlay_resource_path:MALE_OVERLAY,anchor_overlay_sha256:hash(overlayFile),manifest_before_sha256:manifestBeforeHash,manifest_after_sha256:hash(manifestFile),original_configuration_sha256:original.configuration_sha256,nose_lower_region_preserved_from_y:NOSE_PRESERVE_FROM_Y,all_other_parts_preserved:true,strict_guard_resource_path:resource(strictGuard),strict_guard_sha256:hash(strictGuard)};
 fs.writeFileSync(path.join(installation,'install_report.json'),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({installedRecords:records.length,newRearPngs:7,preservedOriginalPngs:35,activeOptions:total,physicalPngs:51}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
