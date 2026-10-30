const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),art=root+'/textures/world/bai_chuan/joint_paint_v4',doc=root+'/docs/美术规范/百川同画全图替换_20261029';
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
async function main(){
 for(const d of [art,art+'/reference',art+'/painted',art+'/icons',art+'/tiles',doc,doc+'/提示词'])fs.mkdirSync(d,{recursive:true});
 const data=JSON.parse(fs.readFileSync(root+'/docs/prototypes/百川宗门城镇资源规划数据_20261027.json','utf8'));
 const old=JSON.parse(fs.readFileSync(root+'/textures/world/bai_chuan/whole_paint_v3/presentation.json','utf8'));
 const byId=Object.fromEntries(data.nodes.map(n=>[n.id,n]));
 const groups={};
 const specs={city:[142,104],town:[100,66],market:[94,65],village:[78,52],dock:[92,62]};
 const sites=old.placements.filter(p=>p.type!=='wild').map(p=>{
  const n=byId[p.id],size=n.type==='sect'?(n.level===1?[164,110]:[118,82]):specs[n.type];
  const center=[p.anchor[0],p.anchor[1]-size[1]/2],c=clamp(Math.floor(center[0]/1024),0,5),r=clamp(Math.floor(center[1]/1024),0,5);
  const s={id:n.id,name:n.name,type:n.type,level:n.level||0,logicalPos:n.pos,anchor:p.anchor,size,owner:`r${r+1}c${c+1}`};
  (groups[s.owner]||=[]).push(s);return s;
 });
 const source=root+'/textures/world/bai_chuan/whole_paint_v3/master_6144.png';
 const padded=await sharp(source).extend({top:256,bottom:256,left:256,right:256,extendWith:'mirror'}).png().toBuffer();
 const patches=[];
 for(const id of Object.keys(groups).sort()){
  const owners=groups[id],r=Number(id[1])-1,c=Number(id[3])-1;
  const bounds=owners.map(s=>[s.anchor[0]-s.size[0]/2-28,s.anchor[1]-s.size[1]-55,s.anchor[0]+s.size[0]/2+28,s.anchor[1]+28]);
  const minX=Math.min(...bounds.map(q=>q[0])),minY=Math.min(...bounds.map(q=>q[1])),maxX=Math.max(...bounds.map(q=>q[2])),maxY=Math.max(...bounds.map(q=>q[3]));
  if(maxX-minX>1214||maxY-minY>1214)throw Error('Split oversized owner group '+id);
  const ox=Math.round(clamp(c*1024-115,maxX-1234,minX-20)),oy=Math.round(clamp(r*1024-115,maxY-1234,minY-20));
  const file=art+'/reference/'+id+'.png',guide=art+'/reference/'+id+'_guide.png';
  await sharp(padded).extract({left:ox+256,top:oy+256,width:1254,height:1254}).png().toFile(file);
  const local=owners.map(s=>({...s,x:Math.round(s.anchor[0]-ox),y:Math.round(s.anchor[1]-oy)}));
  const svg=Buffer.from(`<svg width="1254" height="1254">${local.map((s,i)=>`<rect x="${s.x-s.size[0]/2}" y="${s.y-s.size[1]}" width="${s.size[0]}" height="${s.size[1]}" fill="none" stroke="${s.type==='sect'?'#d24c9d':'#3489bd'}" stroke-width="2"/><circle cx="${s.x}" cy="${s.y}" r="3" fill="#d24c9d"/><text x="${s.x-s.size[0]/2}" y="${s.y-s.size[1]-9}" fill="#a24573" font-size="17">${i+1}</text>`).join('')}</svg>`);
  await sharp(file).composite([{input:svg}]).png().toFile(guide);
  const items=local.map((s,i)=>{
   const kind=s.type==='sect'?'small roofed sect MOUNTAIN GATE, with pale piers and gray slate canopy; no whole compound':s.type==='city'?'compact fortified CITY GATE with one small roof and very short wall stubs; no city panorama':s.type==='town'?'simple small timber TOWN ENTRANCE canopy; no streetscape':s.type==='market'?'small market entrance with ONE tiny adjacent muted cloth awning; no market panorama':s.type==='village'?'a tiny rustic village entrance, modest timber posts and one thatched roof hint; no whole village':'one modest low dock landing at the existing bank, a small shed and short water-edge deck; NOT a bridge';
   const ground=s.type==='sect'?'MOUNTAIN FOOTING REQUIRED: extend or reshape the nearby low mountain shoulder so the gate sits on continuous mountainside or a dry mountain saddle, with visible sloping mountain ground directly BELOW the gate. A hill standing separately behind an icon on flat plain is NOT enough. No tall plinth, cliff, wall terrace, mountain path or stairs.':s.type==='dock'?'SHORE FOOTING REQUIRED: shed feet on a dry connected bank, only the small deck reaches water. Preserve river continuity; do not bridge or dam it.':'PLAIN FOOTING REQUIRED: remove any ridge and mountain wash beneath the marker and leave a BROAD naturally connected flat plain around this entrance, approximately 130 pixels or more to either side. Shift adjacent local hills aside if needed. Do not put the entrance on a peak or slope or cut an isolated circular clearing.';
   return `${i+1}. ${s.id} (${s.name}, instruction identity only, NEVER write it on the map): ${kind}. Approximate architecture width ${s.size[0]} pixels, height ${s.size[1]}, centered horizontally at x=${s.x}, feet at y=${s.y}. ${ground}`;
  }).join('\n');
  const prompt=`Edit IMAGE 1, one original local map crop, same square frame and fixed native scale. IMAGE 2 is the SAME crop with numbered rectangles and dots locating independent entrance sites; erase every guide and number in the final. IMAGE 3 shows the approved terrain-and-entrance CO-PAINTING treatment, not a layout to copy. Generate ONE integrated local terrain scene with EXACTLY ${local.length} small entrance building${local.length===1?'':'s'}, at the supplied positions. Do not return a sprite sheet or whole new world map.
ENTRANCES TO PAINT TOGETHER WITH TERRAIN (coordinates from top-left of this1254-square frame):\n${items}
Critical: all entrances must look like parts of this SAME ink drawing, NOT fine-detail cutout icons pasted over softer terrain. Fine muted gray-brown outlines, simple flat shading, slate-gray roofs and pale earth walls. Architecture line density and sharpness must match mountains and tiny trees. No dense brick pattern, tile engraving, photographic detail, glossy 3D or separate dark drop shadow. Main sect gates slightly broader than local sect gates; city gates compact; town/market/village icons smaller and simpler. Keep the indicated footprint and especially the feet position; don't move a node to a prettier empty location. Never enlarge an entrance into a dominant hero building.
Vegetation avoidance is mandatory: clear any tree crowns, trunks and bushes within EACH architecture footprint and at least30pixels around it. Thin or move the local tree cluster naturally. No tree behind a doorway, no gate over a tree, no trees on mountain slopes. Forests are compact irregular groups of SMALL fine conifer strokes with varied heights and breathing gaps, no big round crowns, repeated stamps, or dark dense tree walls.
Keep the source's geographic arrangement except the explicitly required local mountain/plain repair at entrance sites. Rivers, lakes and long coast remain connected and near existing banks; preserve through-frame river entries. Do not flood land, invent a new river, or place buildings in water. Same warm subdued ochre paper, very pale gray-green slope wash and muted gray-green water. Low separated hill groups with open valley corridors, no enclosing mountain wall. Clean quiet even ground with only extremely faint paper grain: no dirt dots, grass ticks, smears, wandering dry streams or filler scratches.
Preserve the outermost60pixels' existing broad tone, contour geometry and bank positions, so this crop can meet neighboring map regions. Near frame boundaries don't invent new terrain or buildings. ONLY the listed entrances, no other architecture, hidden duplicate, entire compound, cityscape, people, roads, path dots, mountain paths, steps, stairs, bridges, UI, caption, writing, Chinese labels, compass, grid, border or visible technical markers. No gate on top of trees. Final output exactly this local scene at square1254pixels or larger.`;
  fs.writeFileSync(doc+'/提示词/'+id+'.txt',prompt);
  patches.push({id,row:r,col:c,worldOrigin:[ox,oy],framePixels:1254,coreRect:[c*1024,r*1024,1024,1024],reference:file,guide,sites:local,prompt});
 }
 const result={version:4,worldPixels:6144,framePixels:1254,sourceMaster:source,sourceHash:crypto.createHash('sha256').update(fs.readFileSync(source)).digest('hex'),sites,patches,status:'preparing_joint_replacement_not_ready'};
 fs.writeFileSync(art+'/plan.json',JSON.stringify(result,null,2));
 fs.writeFileSync(doc+'/节点制作清单.json',JSON.stringify(sites,null,2));
 const cols=6,thumb=240,rows=Math.ceil(patches.length/cols),composites=[];
 for(let i=0;i<patches.length;i++)composites.push({input:await sharp(patches[i].guide).resize(thumb,thumb).png().toBuffer(),left:(i%cols)*thumb,top:Math.floor(i/cols)*thumb});
 await sharp({create:{width:cols*thumb,height:rows*thumb,channels:3,background:'#e6d1a5'}}).composite(composites).png().toFile(doc+'/分区制作引导.png');
 console.log(JSON.stringify({regions:patches.length,entrances:sites.length,types:sites.reduce((a,s)=>(a[s.type]=(a[s.type]||0)+1,a),{})}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
