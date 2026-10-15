// Package actual Godot-rendered frames; this script does not redraw character art.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const OUT=__dirname,PRE=path.join(OUT,'preview');
async function main(){
 const selected=[['idle','防守待机','50'],['cast','施法','50'],['attack','横击','50'],['attack_rising','上挑','50'],['attack_leap','跃击','50'],['hurt','受击','25'],['knockback_return','倒飞回位','50'],['death_kneel','跪地终姿','100']];
 const cells=[];
 for(const [clip,label,fraction]of selected){
   const character=await sharp(path.join(PRE,`audit_${clip}_${fraction}.png`)).extract({left:610,top:120,width:720,height:850}).resize(360,425).png().toBuffer();
   const title=Buffer.from(`<svg width="360" height="45"><rect width="360" height="45" fill="#e9e1d3"/><text x="180" y="30" text-anchor="middle" font-family="Microsoft YaHei" font-size="21" fill="#293e38">${label}</text></svg>`);
   cells.push(await sharp({create:{width:360,height:470,channels:4,background:'#e9e1d3'}}).composite([{input:character,top:45,left:0},{input:title,top:0,left:0}]).png().toBuffer());
 }
 await sharp(cells,{join:{across:4,shim:4,background:'#b8b0a3'}}).png().toFile(path.join(PRE,'动作实机对照.png'));
 const frames=JSON.parse(fs.readFileSync(path.join(PRE,'motion/frames.json')));
 await sharp(frames.map(f=>path.join(PRE,'motion',f.file)),{join:{animated:true}}).gif({loop:0,delay:frames.map(f=>f.delay),colours:160,effort:5}).toFile(path.join(PRE,'动作实机预览.gif'));
 const rig=JSON.parse(fs.readFileSync(path.join(OUT,'runtime/male_sample.json')));
 const checks={same_canvas:true,transparent_corners:true,weights_normalized:true,slots_resolve:true,atlas_regions_resolve:true,grounded_collapse:true};
 for(const file of fs.readdirSync(path.join(OUT,'layers')).filter(f=>f.endsWith('.png'))){
   const {data,info}=await sharp(path.join(OUT,'layers',file)).raw().toBuffer({resolveWithObject:true});
   checks.same_canvas &&= info.width===1024&&info.height===1536&&info.channels===4;
   checks.transparent_corners &&= [3,(1023)*4+3,(1535*1024)*4+3,(1536*1024-1)*4+3].every(i=>data[i]===0);
 }
 const atlasNames=new Set(fs.readFileSync(path.join(OUT,'runtime/male_sample.atlas'),'utf8').split('\n').filter(x=>x&&!x.includes(':')&&!x.endsWith('.res')));
 for(const [slot,atts]of Object.entries(rig.skins[0].attachments))for(const a of Object.values(atts)){
   checks.atlas_regions_resolve &&= atlasNames.has(a.path);
   if(a.type==='mesh')for(let i=0;i<a.vertices.length;){const count=a.vertices[i++];let sum=0;for(let n=0;n<count;n++){const b=a.vertices[i];checks.weights_normalized &&= Number.isInteger(b)&&b>=0&&b<rig.bones.length;sum+=a.vertices[i+3];i+=4;}checks.weights_normalized &&=Math.abs(sum-1)<.00001;}
 }
 for(const a of Object.values(rig.animations))for(const [slot,ts]of Object.entries(a.slots))for(const t of ts.attachment)checks.slots_resolve &&= t.name===null||!!rig.skins[0].attachments[slot][t.name];
 const audit=JSON.parse(fs.readFileSync(path.join(PRE,'runtime_audit.json')));
 for(const f of audit.grounded_collapse.filter(f=>f.time<.82))for(const [side,x,y]of [['near',-172,220],['far',203,245]])checks.grounded_collapse &&=Math.hypot(f[side].worldX-x,f[side].worldY-y)<3;
 const report={...checks,pause_step:audit.pause_step,death_hold:audit.death_hold,interrupt_return:audit.interrupt_return,rendered_motion_frames:frames.length,engine:audit.engine};
 fs.writeFileSync(path.join(PRE,'verification.json'),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify(report));
 if(Object.values(report).some(v=>v===false))process.exitCode=1;
}
main().catch(e=>{console.error(e);process.exitCode=1});
