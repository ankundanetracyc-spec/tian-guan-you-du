// Read current installed assets, snapshot them under inactive names, and compose comparison QA.
// No active assets, application code or official documentation are written.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
const source=path.join(pack,'source'),installation=path.join(pack,'installation'),snapshot=path.join(installation,'current_active_snapshot'),qa=path.join(pack,'qa');
const reference='C:/Users/34784/AppData/Local/Temp/codex-clipboard-06d9557d-f435-4c84-9dd6-28df85a05f63.png';
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const resource=p=>'res://'+path.relative(project,p).replaceAll('\\','/');
const walk=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);
for(const dir of[source,installation,snapshot,qa])fs.mkdirSync(dir,{recursive:true});

async function main(){
  const baselineFile=path.join(installation,'current_active_baseline.json');
  if(fs.existsSync(baselineFile))throw Error('Frozen baseline already exists; do not replace it from a later active state.');
  const config=JSON.parse(fs.readFileSync(path.join(active,'appearance_parts.json'),'utf8'));
  const files=walk(active).filter(p=>p.toLowerCase().endsWith('.png')).sort();
  const baseline={schema_version:1,status:'read_only_current_state_snapshot_not_a_new_enabled_appearance_package',generated_at:new Date().toISOString(),active_package:resource(active),latest_user_reference:'source/reference_user.png',reference_sha256:hash(reference),all_package_png_count:files.length,all_package_png_sha256:{},configuration_sha256:{},configuration_snapshot_files:{},registered_png_sha256:{},female_registered_png_sha256:{},registered_option_count:config.components.reduce((n,c)=>n+c.variant_count,0),snapshot_root:'installation/current_active_snapshot',scope_note:'All package PNGs include historical source and preview files. Registered texture paths are listed separately; newly staging files do not enable an appearance package.'};
  for(const file of files){
    const relative=path.relative(active,file),target=path.join(snapshot,relative),digest=hash(file);
    fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(file,target);
    if(hash(target)!==digest)throw Error('Snapshot bytes differ: '+relative);
    baseline.all_package_png_sha256[resource(file)]=digest;
  }
  for(const name of['appearance_parts.json','portrait_manifest.json']){
    const original=path.join(active,name),copyName=name.replace(/\.json$/,'.snapshot.json'),target=path.join(snapshot,copyName);
    fs.copyFileSync(original,target);baseline.configuration_sha256[name]=hash(original);baseline.configuration_snapshot_files[name]='installation/current_active_snapshot/'+copyName;
    if(hash(target)!==baseline.configuration_sha256[name])throw Error('Configuration snapshot bytes differ: '+name);
  }
  for(const entry of config.components){
    for(const index of entry.variant_indices??Array.from({length:entry.variant_count},(_,i)=>i+(config.index_start??1))){
      const formatted=String(index).padStart(config.index_padding??2,'0');
      for(const filePattern of[entry.file,entry.rear_file].filter(Boolean)){
        const file=path.join(active,filePattern.replace('{index}',formatted)),key=resource(file);
        if(!fs.existsSync(file))throw Error('Registered PNG missing: '+key);
        baseline.registered_png_sha256[key]=hash(file);
        if(entry.gender==='female')baseline.female_registered_png_sha256[key]=hash(file);
      }
    }
  }
  baseline.registered_texture_count=Object.keys(baseline.registered_png_sha256).length;
  baseline.female_registered_texture_count=Object.keys(baseline.female_registered_png_sha256).length;
  fs.copyFileSync(reference,path.join(source,'reference_user.png'));
  const background='#e7e2d6',headCrop={left:290,top:60,width:460,height:680},headTiles=[],fullTiles=[];
  const selectedPath=(gender,part,rear=false)=>{
    const entry=config.components.find(c=>c.gender===gender&&c.part===part);if(!entry)return null;
    const pattern=rear?entry.rear_file:entry.file;if(!pattern)return null;
    const index=entry.variant_indices?.[0]??config.index_start??1;
    return path.join(snapshot,pattern.replace('{index}',String(index).padStart(config.index_padding??2,'0')));
  };
  for(const [i,gender]of['male','female'].entries()){
    const order=[selectedPath(gender,'hair_back'),selectedPath(gender,'clothes',true),selectedPath(gender,'face'),selectedPath(gender,'ear'),selectedPath(gender,'clothes'),selectedPath(gender,'eye'),selectedPath(gender,'brow'),selectedPath(gender,'nose'),selectedPath(gender,'mouth'),selectedPath(gender,gender==='male'?'beard':'makeup'),selectedPath(gender,'hair_front')].filter(Boolean);
    const full=await sharp({create:{width:1024,height:1536,channels:4,background}}).composite(order.map(input=>({input}))).png().toBuffer();
    fs.writeFileSync(path.join(qa,'current_'+gender+'_native.png'),full);
    headTiles.push({input:await sharp(full).extract(headCrop).png().toBuffer(),left:i*460,top:0});
    fullTiles.push({input:await sharp(full).resize(512,768).png().toBuffer(),left:i*512,top:0});
  }
  await sharp({create:{width:920,height:680,channels:4,background}}).composite(headTiles).png().toFile(path.join(qa,'current_male_female.png'));
  await sharp({create:{width:1024,height:768,channels:4,background}}).composite(fullTiles).png().toFile(path.join(qa,'current_male_female_full_canvas.png'));
  baseline.comparison={head:'qa/current_male_female.png',head_order:'Left current male, right current female. Same full-canvas ROI, no scaling or individual centering.',head_roi:headCrop,head_scale:1,full_canvas:'qa/current_male_female_full_canvas.png',full_canvas_each_size:[512,768],layer_order:'hair_back, selected clothes rear, face/ear, selected clothes front, eyes, brows, nose, mouth, accessory, hair_front',selection:'First configured choice of each independent part; male clothes rear follows the same clothes index.'};
  for(const [file,digest]of Object.entries(baseline.all_package_png_sha256))if(hash(path.join(project,file.slice(6)))!==digest)throw Error('Active PNG changed during snapshot: '+file);
  for(const[name,digest]of Object.entries(baseline.configuration_sha256))if(hash(path.join(active,name))!==digest)throw Error('Active configuration changed during snapshot: '+name);
  baseline.active_files_unchanged_during_preparation=true;
  baseline.enabled_appearance_config_created=false;
  fs.writeFileSync(baselineFile,JSON.stringify(baseline,null,2)+'\n');
  console.log(JSON.stringify({baseline:'installation/current_active_baseline.json',all_pngs:files.length,registered_textures:baseline.registered_texture_count,female_textures:baseline.female_registered_texture_count,options:baseline.registered_option_count,current_head_comparison:baseline.comparison.head,active_changed:false,enabled_config_created:false}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
