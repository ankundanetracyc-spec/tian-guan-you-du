const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),art=root+'/textures/world/bai_chuan/joint_paint_v4',doc=root+'/docs/美术规范/百川同画全图替换_20261029';
async function main(){
 const plan=JSON.parse(fs.readFileSync(art+'/plan.json','utf8'));
 const patches=plan.patches.filter(p=>fs.existsSync(art+'/painted/'+p.id+'.png')),sites=[];
 const overview=[];
 for(let i=0;i<patches.length;i++){
  const p=patches[i],file=art+'/painted/'+p.id+'.png';
  overview.push({input:await sharp(file).resize(250,250).png().toBuffer(),left:i%6*250,top:Math.floor(i/6)*278});
  overview.push({input:Buffer.from(`<svg width="250" height="28"><text x="8" y="20" font-size="17">${p.id}</text></svg>`),left:i%6*250,top:Math.floor(i/6)*278+250});
  for(const s of p.sites){
   const x=Math.max(0,Math.min(894,Math.round(s.x-180))),y=Math.max(0,Math.min(894,Math.round(s.y-220)));
   const crop=art+'/reference/'+s.id+'_review.png';
   await sharp(file).extract({left:x,top:y,width:360,height:360}).png().toFile(crop);
   sites.push({...s,patch:p.id,cropOrigin:[x,y],crop});
  }
 }
 if(overview.length)await sharp({create:{width:1500,height:Math.ceil(patches.length/6)*278,channels:3,background:'#ead7af'}}).composite(overview).png().toFile(doc+'/同画区域检查.png');
 for(let start=0;start<sites.length;start+=12){
  const group=sites.slice(start,start+12),items=[];
  for(let i=0;i<group.length;i++){
   const s=group[i];items.push({input:s.crop,left:i%4*360,top:Math.floor(i/4)*396});
   items.push({input:Buffer.from(`<svg width="360" height="36"><text x="5" y="16" font-size="14">${s.id} ${s.name}</text><text x="5" y="31" font-size="12">${s.patch} origin ${s.cropOrigin} foot ${s.x-s.cropOrigin[0]},${s.y-s.cropOrigin[1]}</text></svg>`),left:i%4*360,top:Math.floor(i/4)*396+360});
  }
  await sharp({create:{width:1440,height:Math.ceil(group.length/4)*396,channels:3,background:'#ead7af'}}).composite(items).png().toFile(doc+'/入口检查_'+String(start/12+1).padStart(2,'0')+'.png');
 }
 fs.writeFileSync(art+'/review.json',JSON.stringify(sites,null,2));console.log(JSON.stringify({painted:patches.length,entrances:sites.length}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
