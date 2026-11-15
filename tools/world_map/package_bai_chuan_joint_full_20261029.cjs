const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),art=root+'/textures/world/bai_chuan/joint_paint_v4',doc=root+'/docs/美术规范/百川同画全图替换_20261029';
async function main(){
 const p=JSON.parse(fs.readFileSync(art+'/presentation.json','utf8')),data=JSON.parse(fs.readFileSync(root+'/docs/prototypes/百川宗门城镇资源规划数据_20261027.json','utf8'));
 const masks={};for(const row of p.placements.filter(r=>r.hoverOnly))masks[row.id]='data:image/png;base64,'+fs.readFileSync(art+'/icons/'+row.id+'.png').toString('base64');
 const roads=Object.values(data.roadCurveGeometry.edges).map(e=>e.points.map(v=>v.map(n=>n*61.44)));
 fs.writeFileSync(doc+'/map.js','window.BAI_CHUAN_PRESENTATION='+JSON.stringify(p)+';\nwindow.BAI_CHUAN_MASKS='+JSON.stringify(masks)+';\nwindow.BAI_CHUAN_ROADS='+JSON.stringify(roads)+';');
 const sites=p.placements.filter(r=>r.hoverOnly);
 for(let start=0;start<sites.length;start+=12){
  const batch=sites.slice(start,start+12),items=[];
  for(let i=0;i<batch.length;i++){
   const s=batch[i],[x,y,w,h]=s.rect,l=Math.max(0,Math.min(5760,Math.round(x+w/2-192))),t=Math.max(0,Math.min(5824,y-105));
   items.push({input:await sharp(art+'/master_6144.png').extract({left:l,top:t,width:384,height:320}).png().toBuffer(),left:i%4*384,top:Math.floor(i/4)*354});
   items.push({input:Buffer.from(`<svg width="384" height="34"><text x="8" y="23" font-size="17">${s.name} · ${s.visualTier} · ${w}×${h}</text></svg>`),left:i%4*384,top:Math.floor(i/4)*354+320});
  }
  await sharp({create:{width:1536,height:Math.ceil(batch.length/4)*354,channels:3,background:'#ead9b8'}}).composite(items).png().toFile(doc+'/最终入口_'+(start/12+1)+'.png');
 }
 console.log(JSON.stringify({gallery:5,roadEdges:roads.length,maskImages:Object.keys(masks).length}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
