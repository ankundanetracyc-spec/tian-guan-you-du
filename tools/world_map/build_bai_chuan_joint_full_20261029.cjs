const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),art=root+'/textures/world/bai_chuan/joint_paint_v4',doc=root+'/docs/美术规范/百川同画全图替换_20261029';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'')),save=(p,x)=>fs.writeFileSync(p,JSON.stringify(x,null,2));
const clamp=v=>Math.max(0,Math.min(1,v));
function outside(x,y,r){return Math.max(r[0]-x,0,x-r[0]-r[2],r[1]-y,y-r[1]-r[3]);}
async function main(){
 const plan=read(art+'/plan.json'),boxes=read(art+'/architecture_boxes.json');
 if(Object.keys(boxes).length!==53)throw Error('Need all53 reviewed entrance bounds');
 const sites=plan.patches.flatMap(p=>p.sites.map(s=>({...s,patch:p.id,localRect:boxes[s.id],rect:[boxes[s.id][0]+p.worldOrigin[0],boxes[s.id][1]+p.worldOrigin[1],...boxes[s.id].slice(2)]})));
 const scenes=[];
 for(const p of plan.patches){const source=art+'/painted/'+p.id+'.png';const m=await sharp(source).metadata();if(m.width!==1254||m.height!==1254)throw Error('Unexpected native dimensions '+p.id);scenes.push({...p,pixels:await sharp(source).removeAlpha().raw().toBuffer()});}
 const measurements=read(art+'/registration/measurements.json');
 for(const scene of scenes)require('./register_bai_chuan_joint_pixels_20261029.cjs')(scene,art,measurements.find(m=>m.id===scene.id));
 const base=await sharp(plan.sourceMaster).removeAlpha().raw().toBuffer(),master=Buffer.alloc(6144*6144*3);
 const stitch=require('./joint_map_seams_20261029.cjs')(scenes,sites,base);save(art+'/seams.json',{vertical:stitch.vertical,horizontal:stitch.horizontal});
 for(let ty=0;ty<3;ty++)for(let tx=0;tx<3;tx++){
  const sx=tx*2048,sy=ty*2048,n=2048*2048,sum=new Float32Array(n*3),weights=new Float32Array(n),bestWeights=new Float32Array(n),bestPixels=Buffer.alloc(n*3),inkPixels=new Uint8Array(n);
  for(let sceneIndex=0;sceneIndex<scenes.length;sceneIndex++){
   const p=scenes[sceneIndex];
   const [ox,oy]=p.worldOrigin,l=Math.max(sx,ox),t=Math.max(sy,oy),r=Math.min(sx+2048,ox+1254),b=Math.min(sy+2048,oy+1254);
   if(l>=r||t>=b)continue;
   const protections=sites.filter(s=>outside(Math.max(l,Math.min(r,s.rect[0])),Math.max(t,Math.min(b,s.rect[1])),s.rect)<180);
   for(let y=t;y<b;y++)for(let x=l;x<r;x++){
    let w=clamp(Math.min(x-ox,ox+1253-x)/230)*clamp(Math.min(y-oy,oy+1253-y)/230);
    for(const s of protections){const d=outside(x,y,s.rect);if(s.patch===p.id){if(d<45)w=1;else if(d<110)w=Math.max(w,(110-d)/65);}else if(d<100)w*=clamp((d-45)/55);}
    if(w<=0)continue;
    const i=(y-sy)*2048+x-sx,j=((y-oy)*1254+x-ox)*3;weights[i]+=w;
    if(p.pixels[j]*.299+p.pixels[j+1]*.587+p.pixels[j+2]*.114<150)inkPixels[i]=1;
    if(stitch.owners[y*6144+x]===sceneIndex){bestWeights[i]=w;for(let c=0;c<3;c++)bestPixels[i*3+c]=p.pixels[j+c];}
    for(let c=0;c<3;c++)sum[i*3+c]+=p.pixels[j+c]*w;
   }
  }
  const tile=Buffer.alloc(n*3);
  for(let y=0;y<2048;y++)for(let x=0;x<2048;x++){
   const i=y*2048+x,j=((sy+y)*6144+sx+x)*3;
   for(let c=0;c<3;c++){const v=bestWeights[i]>0?bestPixels[i*3+c]:base[j+c];tile[i*3+c]=v;master[j+c]=v;}
  }
  await sharp(tile,{raw:{width:2048,height:2048,channels:3}}).png().toFile(art+'/tiles/bc_'+(ty+1)+(tx+1)+'.png');
  console.log('composed tile '+(ty+1)+(tx+1));
 }
 await sharp(master,{raw:{width:6144,height:6144,channels:3}}).png().toFile(art+'/master_6144.png');
 await sharp(art+'/master_6144.png').resize(1024).png().toFile(art+'/overview.png');
 const presentation=read(root+'/textures/world/bai_chuan/whole_paint_v3/presentation.json');presentation.nodesIntegrated=true;presentation.version=4;presentation.artMethod='co-painted terrain and entrances; original-pixel hover masks';
 const reports=[];
 for(const s of sites){
  const [x,y,w,h]=s.rect,source=await sharp(art+'/master_6144.png').extract({left:x,top:y,width:w,height:h}).ensureAlpha().raw().toBuffer();
  const rgba=Buffer.from(source);let opaque=0,transparent=0;
  // Trace each row between its outermost architecture ink strokes. Review bounds exclude terrain and trees.
  for(let yy=0;yy<h;yy++){
   let l=w,r=-1;
   const limit=s.type==='dock'?0.98:0.98;
   for(let xx=0;xx<w;xx++){
    const i=(yy*w+xx)*4;
    if(source[i]*.299+source[i+1]*.587+source[i+2]*.114<145&&yy<h*limit){l=Math.min(l,xx);r=Math.max(r,xx);}
   }
   for(let xx=0;xx<w;xx++){const i=(yy*w+xx)*4;if(xx<l||xx>r){rgba[i+3]=0;transparent++;}else opaque++;}
  }
  if(opaque<200||transparent<10)throw Error('Invalid silhouette '+s.id);
  require('./refine_bai_chuan_joint_masks_20261029.cjs')(s,rgba);opaque=0;transparent=0;for(let i=3;i<rgba.length;i+=4){if(rgba[i]===255)opaque++;else if(rgba[i]===0)transparent++;}
  const file=art+'/icons/'+s.id+'.png';await sharp(rgba,{raw:{width:w,height:h,channels:4}}).png().toFile(file);
  let differences=0;for(let i=0;i<rgba.length;i+=4)if(rgba[i+3]===255)for(let c=0;c<3;c++)if(rgba[i+c]!==source[i+c])differences++;
  const row=presentation.placements.find(p=>p.id===s.id),foot=[x+w/2,y+h];
  row.asset=s.id;row.drawIcon=true;row.hoverOnly=true;row.rect=s.rect;row.artOffset=[foot[0]-row.anchor[0],foot[1]-row.anchor[1]];row.pivot=[0.5,1];row.visualTier=s.type==='sect'?'sect_'+s.level:s.type;row.footing=s.type==='sect'?'continuous mountain shoulder':s.type==='dock'?'dry bank and small landing':'connected level plain';row.approach=[row.anchor,foot];
  row.labelSize=s.type==='sect'&&s.level===1?21:s.type==='city'?19:17;
  const labelW=Math.max(76,s.name.length*row.labelSize+16);row.labelRect=[Math.round(foot[0]-labelW/2),y+h+7,labelW,30];
  presentation.catalog[s.id]={file:'res://textures/world/bai_chuan/joint_paint_v4/icons/'+s.id+'.png',size:[w,h],originalPixels:true,hoverOnly:true};
  reports.push({id:s.id,name:s.name,type:s.type,level:s.level,patch:s.patch,localRect:s.localRect,rect:s.rect,logicalPos:s.logicalPos,artOffset:row.artOffset,opaquePixels:opaque,transparentPixels:transparent,opaquePixelDifferences:differences,footing:row.footing});
 }
 // Keep information readable when a natural site's old label falls inside a newly painted gate.
 const overlaps=(a,b)=>a[0]<b[0]+b[2]&&a[0]+a[2]>b[0]&&a[1]<b[1]+b[3]&&a[1]+a[3]>b[1];
 const occupied=sites.flatMap(s=>{const row=presentation.placements.find(p=>p.id===s.id);return [s.rect,row.labelRect];});
 for(const row of presentation.placements.filter(p=>!p.hoverOnly)){
  const label=row.labelRect.slice();for(const dy of [0,35,-35,70,-70,105,-105]){const q=[label[0],label[1]+dy,label[2],label[3]];if(!occupied.some(b=>overlaps(q,b))){row.labelRect=q;break;}}occupied.push(row.labelRect);
 }
 save(art+'/presentation.json',presentation);save(art+'/architecture_audit.json',reports);
 save(doc+'/原像素与落点验收.json',{scenes:31,builtEntrances:53,naturalSites:70,namedNodes:123,maskPixelDifferences:reports.reduce((a,s)=>a+s.opaquePixelDifferences,0),sites:reports,originalGraphHash:presentation.sourceHash,status:'assembled_pending_runtime_and_visual_review'});
 console.log(JSON.stringify({scenes:31,entrances:reports.length,pixelDifferences:reports.reduce((a,s)=>a+s.opaquePixelDifferences,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
