const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..');
const art=root+'/textures/world/bai_chuan/whole_paint_v3';
const doc=root+'/docs/美术规范/百川入口节点分层_20261028';
async function main(){
 for(const p of [art,art+'/reference',art+'/refined',art+'/tiles',doc+'/底图提示词'])fs.mkdirSync(p,{recursive:true});
 const master=root+'/docs/美术规范/百川整幅山水底图重绘_20261028/开阔舆图母图_无桥_v4_增岭来河.png';
 const data=JSON.parse(fs.readFileSync(root+'/docs/prototypes/百川宗门城镇资源规划数据_20261027.json','utf8'));
 // Enlargement here supplies framing references only, never production pixels.
 const expanded=await sharp(master).resize(6144,6144).extend({top:20,bottom:20,left:20,right:20,extendWith:'mirror'}).png().toBuffer();
 const patches=[];
 for(let r=0;r<5;r++)for(let c=0;c<5;c++){
  const x0=Math.round(c*6144/5),x1=Math.round((c+1)*6144/5),y0=Math.round(r*6144/5),y1=Math.round((r+1)*6144/5);
  const ox=Math.round((x0+x1)/2-627),oy=Math.round((y0+y1)/2-627),id=`r${r+1}c${c+1}`;
  const file=art+'/reference/'+id+'_scene.png';
  await sharp(expanded).extract({left:ox+20,top:oy+20,width:1254,height:1254}).png().toFile(file);
  const places=data.nodes.filter(n=>n.type!=='wild').map(n=>({name:n.name,type:n.type,x:Math.round(n.pos[0]*61.44-ox),y:Math.round(n.pos[1]*61.44-oy)})).filter(n=>n.x>12&&n.x<1242&&n.y>12&&n.y<1242);
  const positions=places.map(n=>`(${n.x},${n.y}) ${n.type==='sect'?'a quiet dry low mountain shoulder or shallow hillside saddle supporting a separate tiny mountain-gate icon':'flat dry land for a separate small '+n.type+' entrance'}`).join('; ')||'No special building clearings are required here.';
  const prompt=`Refine IMAGE 1, a SINGLE LOCAL CROP of our existing full world map. Output EXACTLY this local area at the same crop, scale and orientation, square 1254 pixels or larger, not a new complete map. IMAGE 2 is the full world master ONLY to explain context and coordinate continuity. IMAGE 3 is the approved fine small-conifer stroke reference ONLY. Do not copy a different composition from any reference. This local region is ${id}, row ${r+1} column ${c+1} of a 5x5 refinement layout; the final game map will be a single continuous whole.
Keep ALL hill groups, riverbanks, lakes, shorelines and woodland footprints in their present places. Rebuild their enlarged soft marks into clean native fine gray-brown ink contours and delicate restrained gray-green washes. Keep existing density of separated low hills and usable valley/plain gaps. Do not add mountains to open ground or recompose existing groups. Low horizontally spread shoulders and shallow saddles, no tall pyramid peaks, no wall of edge mountains or dense engraving. Thin 1-2 pixel gray-brown lines at native scale.
Warm muted earth-yellow parchment, around the current reference palette. Very faint uniform paper grain, otherwise CLEAN QUIET EMPTY GROUND between landforms. NO grass strokes, soil blotches, smudges, squiggles, dot stippling, ground cracks, coarse fibers, dirt, paper stains or scratchy filler. Plains must not look dirty. Forests stay as small compact irregular groups of tiny individually hinted slender conifers, around 6-15 pixels tall in this local frame, mixed heights and open gaps, varied natural clumps. Only on flats and low valleys, NEVER on mountain slopes or ridges. No broadleaf crowns, tall trees, repeated stamps or continuous forest border.
When water is present: retain exact shape and connectivity, muted gray-green water, subtly irregular banks and only few thin current hints. No pure blue, heavy dark double outlines, flooding a new area or inventing branches. A river crossing an image edge MUST remain open through that same edge; no tapering it to an inland source. East sea when present must continue along the original long coast. If no water in the crop, do not add any.
Dry terrain accommodations in this frame: ${positions}. Make each a natural tiny shoulder or level lowland, NOT a circle, a geometric platform, visible reserve box, white hole or a building. Leave the exact coordinate dry and feasible for an independent entrance icon. Do NOT draw those entrance icons. Do not fill these openings with water, trees or sharp rock lines. Sect mountains can have a very subtle nearby low shoulder at that coordinate if needed, preserving the surrounding map structure. NO mountain stairway, NO steps, NO architectural retaining walls and NO roads leading to it.
EDGE MATCHING IS CRITICAL: preserve the outermost 60-pixel band's existing geometry, bank positions, broad color and feature scale very closely so this region can meet its neighbors. No frame, fade-out, vignette, empty border, changed lighting or edge mountain addition. Refine clarity in place, not a separate decorative landscape.
ABSOLUTELY NO BRIDGES, stairs, steps, mountain paths, roads, buildings, courtyard, gates, walls, icons, labels, text, map markers, UI, compass, grid, border or guide marks. Terrain, forests and water are integrated in one painting. Not a sprite sheet, not a collage. Deliver only this same local map region redrawn cleanly.`;
  fs.writeFileSync(doc+'/底图提示词/'+id+'.txt',prompt);
  patches.push({id,row:r,col:c,worldOrigin:[ox,oy],coreRect:[x0,y0,x1-x0,y1-y0],reference:file,places,bridges:[],prompt});
 }
 const plan={worldSize:6144,grid:[5,5],patchFrame:1254,sourceMaster:'开阔舆图母图_无桥_v4_增岭来河.png',sourceCompositionPixels:[1254,1254],productionReady:false,patches};
 fs.writeFileSync(art+'/refinement_plan.json',JSON.stringify(plan,null,2)+'\n');
 console.log(JSON.stringify({regions:patches.length,sourcePixels:plan.sourceCompositionPixels,targetPixels:[6144,6144]}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
