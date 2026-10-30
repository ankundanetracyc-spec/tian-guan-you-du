// Package generated entrance art without changing world coordinates or source art.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const art = path.join(root, 'textures/world/bai_chuan/entrance_v1');
const doc = path.join(root, 'docs/美术规范/百川入口节点分层_20261028');
async function main() {
  const source = process.argv[2];
  if (!source) throw Error('Pass the generated transparent atlas path');
  fs.mkdirSync(art + '/icons', { recursive: true });
  fs.mkdirSync(doc, { recursive: true });
  fs.copyFileSync(source, doc + '/入口图集_原图.png');
  const meta = await sharp(source).metadata();
  if (meta.width !== 1536 || meta.height !== 1024 || !meta.hasAlpha) throw Error('Unexpected atlas format');
  // Cell gutters inspected on the native generated atlas, not equal thirds.
  const xs = [0, 600, 1070, 1536], ys = [0, 520, 1024];
  const jobs = [
    ['sect_main',0,0,[164]], ['sect_local',1,0,[118]],
    ['sect_austere',2,0,[118,164]], ['city_gate',0,1,[142]],
    ['town_gate',1,1,[100]], ['market_gate',2,1,[94]]
  ];
  const catalog = {};
  for (const [kind,c,r,widths] of jobs) {
    const left=xs[c], top=ys[r], width=xs[c+1]-left, height=ys[r+1]-top;
    const raw = await sharp(source).extract({left,top,width,height}).raw().toBuffer({resolveWithObject:true});
    let x0=width,y0=height,x1=0,y1=0;
    for(let y=0;y<height;y++) for(let x=0;x<width;x++) if(raw.data[(y*width+x)*4+3]>10){x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);}
    const crop={left:left+x0,top:top+y0,width:x1-x0+1,height:y1-y0+1};
    for(const w of widths){
      const key=kind+'_'+w, file=art+'/icons/'+key+'.png';
      await sharp(source).extract(crop).resize({width:w}).png().toFile(file);
      const out=await sharp(file).metadata();
      catalog[key]={file:'res://textures/world/bai_chuan/entrance_v1/icons/'+key+'.png',size:[out.width,out.height],sourceRect:crop};
    }
  }
  const data=JSON.parse(fs.readFileSync(root+'/docs/prototypes/百川宗门城镇资源规划数据_20261027.json','utf8'));
  const old=JSON.parse(fs.readFileSync(root+'/textures/world/bai_chuan/acceptance_v1/presentation.json','utf8'));
  const nodes=Object.fromEntries(data.nodes.map(n=>[n.id,n]));
  const placements=[];
  for(const row of old.placements){
    const n=nodes[row.id]; let key;
    if(n.type==='sect') key=(row.kind==='holy'?'sect_austere':n.level===1?'sect_main':'sect_local')+'_'+(n.level===1?164:118);
    if(n.type==='city') key='city_gate_142';
    if(n.type==='town') key='town_gate_100';
    if(n.type==='market') key='market_gate_94';
    if(!key) continue;
    const [w,h]=catalog[key].size;
    placements.push({id:n.id,asset:key,rect:[Math.round(row.anchor[0]-w*row.pivot[0]),Math.round(row.anchor[1]-h*row.pivot[1]),w,h],tier:n.type==='sect'?'sect_'+n.level:n.type});
  }
  const report={version:1,sourcePixels:[meta.width,meta.height],noStairs:true,noScenicCompounds:true,catalog,placements,nodeCount:data.nodes.length,counts:{}};
  for(const p of placements)report.counts[p.tier]=(report.counts[p.tier]||0)+1;
  fs.writeFileSync(art+'/nodes.json',JSON.stringify(report,null,2)+'\n');
  fs.writeFileSync(doc+'/素材分级清单.json',JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify({sourcePixels:report.sourcePixels,catalog:Object.keys(catalog).length,replaced:placements.length,counts:report.counts}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
