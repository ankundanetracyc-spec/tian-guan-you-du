const fs=require('node:fs'),sharp=require('sharp'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
async function main(){
 const dir=root+'/docs/美术规范/百川同画节点与悬停蒙版_20261029';
 fs.mkdirSync(dir,{recursive:true});
 const data=JSON.parse(fs.readFileSync(root+'/textures/world/bai_chuan/whole_paint_v3/presentation.json','utf8'));
 const plan=[];
 for(const name of ['玉虚仙宗','绛阙城']){
  const n=data.placements.find(p=>p.name===name);
  const origin=[Math.round(n.anchor[0]-627),Math.round(n.anchor[1]-627)];
  const file=dir+'/'+n.id+'_地形参照.png';
  await sharp(root+'/textures/world/bai_chuan/whole_paint_v3/master_6144.png').extract({left:origin[0],top:origin[1],width:1254,height:1254}).png().toFile(file);
  const w=name==='玉虚仙宗'?164:142,h=name==='玉虚仙宗'?110:104;
  const svg=Buffer.from(`<svg width="1254" height="1254"><rect x="${627-w/2}" y="${627-h}" width="${w}" height="${h}" fill="none" stroke="#e0529a" stroke-width="2"/><circle cx="627" cy="627" r="4" fill="#e0529a"/></svg>`);
  const guide=dir+'/'+n.id+'_位置引导.png';
  await sharp(file).composite([{input:svg}]).png().toFile(guide);
  plan.push({id:n.id,name,type:n.type,origin,reference:file,guide,anchorInFrame:[627,627],sourceLogicalPos:n.logicalPos});
 }
 fs.writeFileSync(dir+'/局部规划.json',JSON.stringify(plan,null,2));
 console.log(JSON.stringify(plan));
}
main().catch(e=>{console.error(e);process.exitCode=1});
