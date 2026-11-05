// Read-only candidate assembly for independent visual review. Writes QA only.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(pack,'../../..'),qa=path.join(pack,'qa');
const inventory=JSON.parse(fs.readFileSync(path.join(qa,'candidate_inventory.json')));
const snapshot=path.join(pack,'installation/current_active_snapshot');
const config=JSON.parse(fs.readFileSync(path.join(snapshot,'appearance_parts.snapshot.json')));
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const id=n=>String(n).padStart(2,'0');
async function male(front,back,brow,eye,clothes,bg){
 const layers=[['hair_back',back],['clothing_back',clothes],['face_shape',1],['clothing',clothes],['eyes',eye],['eyebrows',brow],['nose',1],['mouth',1],['hair_front',front]].filter(([p,n])=>n>0).map(([p,n])=>({input:path.join(pack,'male',p,id(n)+'.png')}));
 return sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite(layers).png().toBuffer();
}
async function female(bg){
 const selected=(part,rear=false)=>{const c=config.components.find(c=>c.gender==='female'&&c.part===part);if(!c)return null;const pattern=rear?c.rear_file:c.file;if(!pattern)return null;const n=c.variant_indices?.[0]??config.index_start??1;return path.join(snapshot,pattern.replace('{index}',id(n)));};
 const layers=[selected('hair_back'),selected('clothes',true),selected('face'),selected('ear'),selected('clothes'),selected('eye'),selected('brow'),selected('nose'),selected('mouth'),selected('makeup'),selected('hair_front')].filter(Boolean).map(input=>({input}));
 return sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite(layers).png().toBuffer();
}
(async()=>{
 if(!inventory.ready||Object.keys(inventory.selected_sha256).length!==28)throw Error('The final set must contain 28 candidates');
 const metadata=[];
 for(const [resource,digest]of Object.entries(inventory.selected_sha256)){const f=path.join(project,resource.slice(6));if(sha(f)!==digest)throw Error('Inventory hash differs '+resource);const m=await sharp(f).metadata();if(m.width!==1024||m.height!==1536||m.channels!==4||!m.hasAlpha)throw Error('Invalid canvas '+resource);metadata.push({file:resource,width:m.width,height:m.height,channels:m.channels,hasAlpha:m.hasAlpha});}
 for(const [label,bg]of [['light','#e8e1d6'],['dark','#202a2e']]){
  const parts=[];for(let brow=1;brow<=3;brow++)for(let eye=1;eye<=2;eye++){const rendered=await male(0,0,brow,eye,1,bg);parts.push({input:await sharp(rendered).extract({left:385,top:320,width:270,height:180}).resize(810,540).png().toBuffer(),left:(eye-1)*810,top:(brow-1)*540});}
  await sharp({create:{width:1620,height:1620,channels:4,background:bg}}).composite(parts).png().toFile(path.join(qa,'independent_brow_eye_six_'+label+'.png'));
  const m=await male(3,3,3,2,1,bg),f=await female(bg);
  await sharp({create:{width:1024,height:768,channels:4,background:bg}}).composite([{input:await sharp(m).resize(512,768).png().toBuffer(),left:0,top:0},{input:await sharp(f).resize(512,768).png().toBuffer(),left:512,top:0}]).png().toFile(path.join(qa,'independent_male_female_full_'+label+'.png'));
 }
 fs.writeFileSync(path.join(qa,'independent_metadata_check.json'),JSON.stringify({selected_sha256:inventory.selected_sha256,metadata,passed:true},null,2)+'\n');
 console.log(JSON.stringify({qa:'independent_brow_eye_six_light/dark.png; independent_male_female_full_light/dark.png',verified:metadata.length}));
})().catch(e=>{console.error(e);process.exitCode=1;});
