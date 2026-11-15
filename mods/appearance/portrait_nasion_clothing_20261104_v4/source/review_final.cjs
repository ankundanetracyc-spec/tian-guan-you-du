// Read-only candidate/snapshot review. Never writes candidate or active PNGs.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const { PNG } = require('pngjs');
const crypto = require('crypto');
const pack = path.resolve(__dirname, '..');
const snapshot = path.join(pack, 'installation/backup_before');
const qa = path.join(pack, 'qa');
const preview = path.join(pack, 'preview');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const BG = '#e7e2d6';
const nativeCanvas = {width:1024,height:1536,channels:4,background:BG};
fs.mkdirSync(qa,{recursive:true});
fs.mkdirSync(preview,{recursive:true});

function choose(gender, part, index, after) {
  const relative = path.join(gender, part, String(index).padStart(2,'0')+'.png');
  const candidate = path.join(pack,relative);
  return after && fs.existsSync(candidate) ? candidate : path.join(snapshot,relative);
}

async function portrait(gender, after, clothing=1) {
  const face = gender==='female' ? 4 : 1;
  const ordered = [['hair_back',1],['clothing_back',clothing],['face_shape',face],['clothing',clothing],['eyes',1],['eyebrows',1],['nose',1],['mouth',1],['face_accessory',1],['hair_front',1]];
  // Rear collars are a second texture of the same clothing option, behind
  // the neck. Original snapshots have no rear layer, equivalent to empty.
  const layers=ordered.map(([part,id])=>choose(gender,part,id,after)).filter(fs.existsSync).map(input=>({input}));
  return await sharp({create:nativeCanvas}).composite(layers).png().toBuffer();
}

function compare(gender,part,index,preserveY) {
  const filename=String(index).padStart(2,'0')+'.png';
  const candidate=path.join(pack,gender,part,filename), before=path.join(snapshot,gender,part,filename);
  const present=fs.existsSync(candidate), selected=present?candidate:before;
  const old=PNG.sync.read(fs.readFileSync(before)), current=PNG.sync.read(fs.readFileSync(selected));
  if(old.width!==1024 || old.height!==1536 || current.width!==1024 || current.height!==1536) throw Error('Canvas changed: '+selected);
  let different=0,lowerDifferent=0,clothingChangesFrom550=0,bounds=[1024,1536,-1,-1];
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++) {
    const k=(y*1024+x)*4;let changed=false;
    for(let c=0;c<4;c++) changed ||= old.data[k+c]!==current.data[k+c];
    if(changed){different++;if(y>=preserveY)lowerDifferent++;if(part==='clothing'&&y>=550)clothingChangesFrom550++;bounds=[Math.min(bounds[0],x),Math.min(bounds[1],y),Math.max(bounds[2],x),Math.max(bounds[3],y)];}
  }
  return {gender,part,index,candidate_present:present,selected_source:path.relative(pack,selected).replaceAll('\\','/'),snapshot_source:path.relative(pack,before).replaceAll('\\','/'),selected_sha256:hash(selected),snapshot_sha256:hash(before),changed_rgba_pixels:different,changed_bbox_inclusive:different?bounds:null,preserve_from_y:preserveY,preserved_region_changed_rgba_pixels:lowerDifferent,preserved_region_byte_identical:lowerDifferent===0,...(part==='clothing'?{front_y_ge550_changed_rgba_pixels:clothingChangesFrom550}: {})};
}

async function reviewFirstPair() {
  const rear=path.join(pack,'male/clothing_back/01.png'),front=path.join(pack,'male/clothing/01.png');
  if(!fs.existsSync(rear)||!fs.existsSync(front))return {ready:false,user_visual_approval:false};
  const reviewedHashes={front:'61209e75322b68e6a4d4834e93ddbc4e493669842466b63df5b8955f31046ddb',rear:'dea92c22f2f205da5a6204f0dc373fc4dc16e4536e3e753508e3230d7f8afdaa'};
  const rootReviewed=hash(front)===reviewedHashes.front&&hash(rear)===reviewedHashes.rear;
  const face=path.join(snapshot,'male/face_shape/01.png'),old=path.join(snapshot,'male/clothing/01.png');
  const region={left:395,top:500,width:250,height:130};
  const views=[['original',[face,old]],['rear_layer_only',[rear]],['front_layer_only',[front]],['rear_face_front',[rear,face,front]]];
  const finalBuffers={};
  for(const [backgroundName,background] of [['light',BG],['dark','#202729']]) {
    const tiles=[];
    for(const [i,[name,inputs]]of views.entries()) {
      const full=await sharp({create:{width:1024,height:1536,channels:4,background}}).composite(inputs.map(input=>({input}))).png().toBuffer();
      const local=await sharp(full).extract(region).resize(1000,520).png().toBuffer();
      fs.writeFileSync(path.join(qa,`clothing01_stage_${name}_${backgroundName}.png`),local);
      tiles.push({input:local,left:(i%2)*1000,top:Math.floor(i/2)*520});
      if(name==='rear_face_front')finalBuffers[backgroundName]=local;
    }
    await sharp({create:{width:2000,height:1040,channels:4,background}}).composite(tiles).png().toFile(path.join(qa,`clothing01_four_stage_${backgroundName}.png`));
  }
  const rig=JSON.parse(fs.readFileSync(path.join(__dirname,'clothing_strict_rig.json'),'utf8'));
  const oldRecord=rig.garments.find(g=>g.index===1),rearImage=PNG.sync.read(fs.readFileSync(rear)),frontImage=PNG.sync.read(fs.readFileSync(front));
  const landmarks=oldRecord.preserved_controls.map(c=>({...c,category:'original_garment_reference'}));
  for(const side of ['left','right'])for(const c of rig.shared_contact_controls[side])landmarks.push({...c,category:c.role==='hidden_coverage_only'?'hidden_neck_support':'visible_neck_support'});
  const shape=[];
  for(const c of landmarks) {
    const[x,y]=c.px,[cx,cy]=[(x-region.left)*4,(y-region.top)*4],colour=c.category==='original_garment_reference'?'#f1b24c':c.category==='hidden_neck_support'?'#bc7fd4':'#50d4dc';
    shape.push(`<circle cx="${cx}" cy="${cy}" r="5" fill="none" stroke="${colour}" stroke-width="2"/><path d="M${cx-8},${cy}h16 M${cx},${cy-8}v16" stroke="${colour}" stroke-width="1"/>`);
    const k=(y*1024+x)*4;c.rear_alpha=rearImage.data[k+3];c.front_alpha=frontImage.data[k+3];
  }
  shape.push(`<text x="18" y="24" font-family="sans-serif" font-size="16" fill="#fff">Reference points only: original collar/frame/V = orange; neck = cyan; hidden support = purple.</text>`);
  const svg=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="520">${shape.join('')}</svg>`);
  await sharp(finalBuffers.dark).composite([{input:svg}]).png().toFile(path.join(qa,'clothing01_pair_reference_anchor_overlay.png'));
  const result={ready:true,root_reviewed_scheme:rootReviewed,reviewed_scheme_sha256:reviewedHashes,user_visual_approval:false,front_sha256:hash(front),rear_sha256:hash(rear),face_sha256:hash(face),original_sha256:hash(old),structure:'Original upper collar plus a newly painted complete rear band behind the face; original y>=550 front V/body after the face.',stage_crop:region,stage_scale:4,stage_order:'Top left original face+single clothing; top right rear texture alone; bottom left front texture alone; bottom right real rear+face+front composition.',landmarks,landmark_notice:'Reference overlay for independent visual assessment, not a new or reused single-layer guard certification.',review_findings:rootReviewed?'Inspected the selected_pair_v2 light/dark images and both actual PNGs. Thin collar tips and original V read naturally, without rejected rolled thickness or a cloth-painted vertical skin border. Deep background shows no obvious pale fringe, leak or detached green fragments. The fine green rim remains softly painted.':'Current files differ from the independently reviewed selected_pair_v2 hashes; this run only refreshed diagnostics and does not carry over its visual assessment.',guard_status:'Await the separately authored two-layer guard and runtime integration; this report does not approve the remaining six garments.'};
  fs.writeFileSync(path.join(qa,'clothing01_independent_pair_review.json'),JSON.stringify(result,null,2)+'\n');
  return result;
}

async function run() {
  const report={generated_at:new Date().toISOString(),mode:'candidate-first, snapshot fallback; active package is never read or changed',canvas:[1024,1536],nose:[],male_clothing:[],male_clothing_back:[],ready_for_final:false,pass:false,visual_art_approval:false,clothing_pair_rule:'Rear collar before face, front collar after face; one clothing option supplies both. Missing original rear layer is transparent. Legacy gap0 or old strict guard success is not visual acceptance.'};
  for(const gender of ['male','female'])report.nose.push(compare(gender,'nose',1,415));
  for(let index=1;index<=7;index++)report.male_clothing.push(compare('male','clothing',index,620));
  for(let index=1;index<=7;index++) {
    const relative=`male/clothing_back/${String(index).padStart(2,'0')}.png`,file=path.join(pack,relative),present=fs.existsSync(file);
    const entry={gender:'male',part:'clothing_back',index,candidate_present:present,selected_source:present?relative:null,snapshot_source:null,missing_snapshot_means:'transparent empty layer'};
    if(present){const im=PNG.sync.read(fs.readFileSync(file));let pixelsBelow620=0,bounds=[1024,1536,-1,-1];for(let y=0;y<im.height;y++)for(let x=0;x<im.width;x++){const a=im.data[(y*im.width+x)*4+3];if(a>0){bounds=[Math.min(bounds[0],x),Math.min(bounds[1],y),Math.max(bounds[2],x),Math.max(bounds[3],y)];if(y>=620)pixelsBelow620++;}}entry.canvas=[im.width,im.height];entry.selected_sha256=hash(file);entry.nonzero_alpha_bbox_inclusive=bounds[2]>=0?bounds:null;entry.nonzero_alpha_pixels_y_ge620=pixelsBelow620;}
    report.male_clothing_back.push(entry);
  }
  report.missing_candidates=[...report.nose,...report.male_clothing,...report.male_clothing_back].filter(r=>!r.candidate_present).map(r=>`${r.gender}/${r.part}/${String(r.index).padStart(2,'0')}.png`);
  report.ready_for_final=report.missing_candidates.length===0;
  report.all_preserved_regions_identical=[...report.nose,...report.male_clothing].every(r=>r.preserved_region_byte_identical);
  report.pass=report.ready_for_final&&report.all_preserved_regions_identical;
  const firstPairReview=await reviewFirstPair();
  report.first_pair_review={ready:firstPairReview.ready,root_reviewed_scheme:firstPairReview.root_reviewed_scheme??false,user_visual_approval:false,report:'qa/clothing01_independent_pair_review.json'};
  const portraits={};
  for(const gender of ['male','female'])for(const [state,after] of [['before',false],['after',true]]) {
    const bytes=await portrait(gender,after);
    portraits[gender+'_'+state]=bytes;
    fs.writeFileSync(path.join(preview,gender+'_'+state+'_native.png'),bytes);
  }
  for(const gender of ['male','female']) {
    const featureTiles=[];
    for(let brow=1;brow<=3;brow++)for(let eye=1;eye<=2;eye++) {
      const layers=[['face_shape',gender==='female'?4:1],['eyes',eye],['eyebrows',brow],['nose',1],['mouth',1]].map(([part,index])=>({input:choose(gender,part,index,true)}));
      const assembled=await sharp({create:nativeCanvas}).composite(layers).png().toBuffer();
      const tile=await sharp(assembled).extract({left:390,top:315,width:260,height:220}).resize(520,440).png().toBuffer();
      featureTiles.push({input:tile,left:(eye-1)*520,top:(brow-1)*440});
    }
    await sharp({create:{width:1040,height:1320,channels:4,background:BG}}).composite(featureTiles).png().toFile(path.join(qa,gender+'_six_brow_eye_nasion_after.png'));
  }
  const faceCrop={left:420,top:330,width:195,height:150};
  const nasionTiles=[];
  for(const [row,gender]of ['male','female'].entries())for(const [col,state]of ['before','after'].entries()) {
    const tile=await sharp(portraits[gender+'_'+state]).extract(faceCrop).resize(780,600).png().toBuffer();
    nasionTiles.push({input:tile,left:col*780,top:row*600});
  }
  await sharp({create:{width:1560,height:1200,channels:4,background:BG}}).composite(nasionTiles).png().toFile(path.join(qa,'nasion_both_genders_before_after.png'));
  const sameScale=[];
  for(const [i,gender]of ['male','female'].entries())sameScale.push({input:await sharp(portraits[gender+'_after']).resize(512,768).png().toBuffer(),left:i*512,top:0});
  await sharp({create:{width:1024,height:768,channels:4,background:BG}}).composite(sameScale).png().toFile(path.join(preview,'portraits_same_scale.png'));
  const neckCrop={left:340,top:460,width:355,height:240};
  const seven=[];
  for(let index=1;index<=7;index++) {
    const p=await portrait('male',true,index);
    const tile=await sharp(p).extract(neckCrop).resize(710,480).png().toBuffer();
    seven.push({input:tile,left:((index-1)%4)*710,top:Math.floor((index-1)/4)*480});
  }
  await sharp({create:{width:2840,height:960,channels:4,background:BG}}).composite(seven).png().toFile(path.join(qa,'clothing_seven_neck_after.png'));
  const fullBodies=[];
  for(let index=1;index<=7;index++) {
    const currentPair=fs.existsSync(path.join(pack,'male/clothing',String(index).padStart(2,'0')+'.png'))&&fs.existsSync(path.join(pack,'male/clothing_back',String(index).padStart(2,'0')+'.png'));
    const full=await portrait('male',true,index);
    const tag=String(index).padStart(2,'0');
    const note=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="384" height="576"><text x="10" y="22" font-family="sans-serif" font-size="14" fill="#333">${tag} ${currentPair?'current pair':'snapshot fallback'}</text></svg>`);
    const tile=await sharp(full).resize(384,576).composite([{input:note}]).png().toBuffer();
    fullBodies.push({input:tile,left:((index-1)%4)*384,top:Math.floor((index-1)/4)*576});
  }
  await sharp({create:{width:1536,height:1152,channels:4,background:BG}}).composite(fullBodies).png().toFile(path.join(preview,'male_seven_clothes_same_scale.png'));
  for(const [backgroundName,background]of [['light',BG],['dark','#202729']]) {
    const tiles=[];
    for(let index=1;index<=7;index++) {
      const tag=String(index).padStart(2,'0'),rear=choose('male','clothing_back',index,true),face=choose('male','face_shape',1,true),front=choose('male','clothing',index,true);
      const layers=[rear,face,front].filter(fs.existsSync).map(input=>({input}));
      const native=await sharp({create:{width:1024,height:1536,channels:4,background}}).composite(layers).png().toBuffer();
      const currentPair=fs.existsSync(path.join(pack,'male/clothing',tag+'.png'))&&fs.existsSync(path.join(pack,'male/clothing_back',tag+'.png'));
      const note=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="500" height="260"><text x="9" y="20" font-family="sans-serif" font-size="13" fill="${backgroundName==='light'?'#333':'#eee'}">${tag} ${currentPair?'current pair':'snapshot fallback'}</text></svg>`);
      const tile=await sharp(native).extract({left:395,top:500,width:250,height:130}).resize(500,260).composite([{input:note}]).png().toBuffer();
      tiles.push({input:tile,left:((index-1)%4)*500,top:Math.floor((index-1)/4)*260});
      if(currentPair)fs.writeFileSync(path.join(qa,`clothing${tag}_independent_neck_${backgroundName}.png`),tile);
    }
    await sharp({create:{width:2000,height:520,channels:4,background}}).composite(tiles).png().toFile(path.join(qa,`clothing_seven_neck_unobscured_${backgroundName}.png`));
  }
  const pair=[];
  for(const [i,state]of ['before','after'].entries())pair.push({input:await sharp(portraits['male_'+state]).extract(neckCrop).resize(710,480).png().toBuffer(),left:i*710,top:0});
  await sharp({create:{width:1420,height:480,channels:4,background:BG}}).composite(pair).png().toFile(path.join(qa,'clothing_first_before_side_by_side.png'));
  report.outputs={face_comparison:'qa/nasion_both_genders_before_after.png',face_comparison_order:'top male, bottom female; left before, right after; every crop x420,y330,w195,h150 enlarged 4x',same_scale:'preview/portraits_same_scale.png',same_scale_order:'left male/right female, each full 1024x1536 canvas uniformly resized to 512x768',seven_necks:'qa/clothing_seven_neck_after.png',seven_necks_order:'clothing01-04 top,05-07 bottom; fallback snapshots used for missing candidates',seven_necks_unobscured:['qa/clothing_seven_neck_unobscured_light.png','qa/clothing_seven_neck_unobscured_dark.png'],seven_full_bodies:'preview/male_seven_clothes_same_scale.png',first_clothes_comparison:'qa/clothing_first_before_side_by_side.png',native_portraits:['preview/male_before_native.png','preview/male_after_native.png','preview/female_before_native.png','preview/female_after_native.png']};
  fs.writeFileSync(path.join(qa,'unchanged_regions_report.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify({ready_for_final:report.ready_for_final,pass:report.pass,missing_candidates:report.missing_candidates,nose_preserved:report.nose.map(r=>r.preserved_region_changed_rgba_pixels),clothing_preserved:report.male_clothing.map(r=>r.preserved_region_changed_rgba_pixels)}));
}
run().catch(e=>{console.error(e);process.exitCode=1;});
