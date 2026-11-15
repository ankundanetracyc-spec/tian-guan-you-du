const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const project=path.resolve(__dirname,'../../../..'),pack=path.resolve(__dirname,'..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004'),installation=path.join(pack,'installation');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const resource=file=>'res://'+path.relative(project,file).replaceAll('\\','/');
const files=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);
const specs={
 hair_front:{part:'hair_front',names:['木簪半束前发','高束拢发','疏风偏分']},
 hair_back:{part:'hair_back',names:['木簪半束长披','玄巾高束长披','散发长披']},
 eyebrows:{part:'brow',names:['平直浓眉','挑锋剑眉','细直柔眉']},
 eyes:{part:'eye',names:['窄长凤目','清朗杏目']},
 nose:{part:'nose',names:['柔直鼻']},mouth:{part:'mouth',names:['自然浅唇']},
 face_shape:{part:'face',names:['清俊长脸']},face_accessory:{part:'beard',names:['无须']}
};
(async()=>{
 if(!fs.existsSync(path.join(installation,'before_install.zip')))throw Error('Pre-install backup is required.');
 const reportFile=path.join(installation,'install_report.json');
 const previous=fs.existsSync(reportFile)?JSON.parse(fs.readFileSync(reportFile,'utf8')):null;
 const baseline=previous?.baseline_sha256??Object.fromEntries(files(active).filter(f=>{
  const r=path.relative(active,f).replaceAll('\\','/');
  return f.endsWith('.png')&&(r.startsWith('female/')||r.startsWith('male/clothing/')||r==='male/face_accessory/01.png');
 }).map(f=>[resource(f),hash(f)]));
 const configFile=path.join(active,'appearance_parts.json'),config=JSON.parse(fs.readFileSync(configFile,'utf8'));
 const records=[],retired=[];
 for(const [folder,spec] of Object.entries(specs)){
  const entry=config.components.find(e=>e.gender==='male'&&e.part===spec.part);
  const count=spec.names.length,ids=Array.from({length:count},(_,i)=>i+1),dir=path.join(active,'male',folder);
  for(const id of ids){
   if(folder==='face_accessory')continue;
   const file=String(id).padStart(2,'0')+'.png';
   const candidate=folder==='face_shape'?path.join(pack,'male/face_shape.png'):path.join(pack,'male',folder,file);
   const output=path.join(dir,file),meta=await sharp(candidate).metadata();
   if(meta.width!==1024||meta.height!==1536||meta.channels!==4||!meta.hasAlpha)throw Error('Invalid canvas: '+candidate);
   if(folder==='face_shape'){
    const a=await sharp(path.join(__dirname,'original_face.png')).ensureAlpha().raw().toBuffer(),b=await sharp(candidate).ensureAlpha().raw().toBuffer();
    for(let i=3;i<a.length;i+=4)if(a[i]!==b[i])throw Error('Male face Alpha changed.');
   }
   fs.copyFileSync(candidate,output);
   records.push({part:folder,index:id,resource_path:resource(output),candidate_path:resource(candidate),source_path:resource(candidate),sha256:hash(output)});
  }
  Object.assign(entry,{variant_count:count,variant_names:spec.names,variant_indices:ids,variant_style_ids:ids.map(i=>i-1)});
  delete entry.variant_aliases;
  const keep=new Set(ids.map(i=>String(i).padStart(2,'0')+'.png'));
  for(const filename of fs.readdirSync(dir)){
   const png=filename.endsWith('.png.import')?filename.slice(0,-7):filename;
   if(!png.endsWith('.png')||keep.has(png))continue;
   const target=path.resolve(dir,filename);
   if(!target.startsWith(active+path.sep))throw Error('Retirement path outside active package.');
   retired.push(resource(target));fs.unlinkSync(target);
  }
 }
 fs.writeFileSync(configFile,JSON.stringify(config,null,2)+'\n');
 const manifestFile=path.join(active,'portrait_manifest.json'),manifest=JSON.parse(fs.readFileSync(manifestFile,'utf8'));
 for(const sex of ['male','female'])manifest.variants_per_slot[sex]=manifest.slots.map(slot=>config.components.find(e=>e.gender===sex&&e.file.includes('/'+slot+'/')).variant_count);
 const total=config.components.reduce((s,e)=>s+e.variant_count,0);manifest.total_pngs=total;manifest.active_variant_count=total;
 manifest.male_reference_installation_20261104={source:resource(pack),head_boundary:'Baihui anterior/posterior plane',body_boundary:'in front of clothes versus behind body',ear_hair_rule:'User final choice: restore the initial ear-space version with continuous clean ear edges; no ear-exposure ratio requirement.',hair_selection:'source/hair_registration.json',hair_color:'RGB-only regional calibration to active female material; original selected alpha and geometry unchanged.',feature_counts:{brow:3,eye:2,nose:1,mouth:1},face:'male/face_shape/01.png',face_alpha_preserved:true,female_unchanged:true,full_three_point_2px_certification:false,legacy_styles:'Retired IDs use the existing deterministic catalog fallback.'};
 fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2)+'\n');
 for(const [file,expected]of Object.entries(baseline))if(hash(path.join(project,file.slice(6)))!==expected)throw Error('Unrelated asset changed: '+file);
 const report={installed:true,active_variant_count:total,male_counts:[3,3,3,2,1,1,1,7,1],records,baseline_sha256:baseline,retired_files:[...new Set([...(previous?.retired_files??[]),...retired])],feature_combination_count:6,with_hair_combination_count:54,preserved_skin_alpha:true};
 fs.writeFileSync(reportFile,JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify({installedRecords:records.length,activeOptions:total,baselinePngs:Object.keys(baseline).length,maleCounts:report.male_counts}));
})().catch(e=>{console.error(e);process.exitCode=1;});
