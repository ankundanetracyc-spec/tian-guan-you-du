const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),art=root+'/textures/world/bai_chuan/joint_paint_v4',doc=root+'/docs/美术规范/百川同画全图替换_20261029';
async function main(){
 const p=JSON.parse(fs.readFileSync(art+'/presentation.json','utf8')),plan=JSON.parse(fs.readFileSync(art+'/plan.json','utf8')),issues=[];
 const hash=crypto.createHash('sha256').update(fs.readFileSync(root+'/docs/prototypes/百川宗门城镇资源规划数据_20261027.json')).digest('hex');
 if(hash!==p.sourceHash)issues.push('Original node/route data changed');
 let tileDifferences=0,maskDifferences=0,maskOpaque=0;const dimensions=await sharp(art+'/master_6144.png').metadata();if(dimensions.width!==6144||dimensions.height!==6144)issues.push('Master size');
 for(let y=0;y<3;y++)for(let x=0;x<3;x++){
  const a=await sharp(art+'/master_6144.png').extract({left:x*2048,top:y*2048,width:2048,height:2048}).removeAlpha().raw().toBuffer(),b=await sharp(art+'/tiles/bc_'+(y+1)+(x+1)+'.png').removeAlpha().raw().toBuffer();
  if(a.length!==b.length)issues.push('Tile dimensions');for(let i=0;i<a.length;i++)if(a[i]!==b[i])tileDifferences++;
 }
 const built=p.placements.filter(r=>r.hoverOnly);if(built.length!==53||p.placements.length!==123)issues.push('Entrance or place count');
 for(const r of built){const [x,y,w,h]=r.rect,a=await sharp(art+'/master_6144.png').extract({left:x,top:y,width:w,height:h}).ensureAlpha().raw().toBuffer(),b=await sharp(art+'/icons/'+r.id+'.png').ensureAlpha().raw().toBuffer();let clear=0,solid=0;for(let i=0;i<b.length;i+=4){if(b[i+3]===0)clear++;if(b[i+3]===255){solid++;for(let c=0;c<3;c++)if(a[i+c]!==b[i+c])maskDifferences++;}}if(clear<10||solid<200)issues.push('Mask empty or opaque '+r.id);maskOpaque+=solid;}
 for(const patch of plan.patches)if(!fs.existsSync(art+'/painted/'+patch.id+'_generation.json'))issues.push('Missing generation provenance '+patch.id);
 if(tileDifferences||maskDifferences)issues.push('Pixel mismatch');
 const result={passed:!issues.length,issues,master:[6144,6144],tiles:9,tilePixelsDifferent:tileDifferences,sourceScenes:plan.patches.length,builtEntrances:built.length,naturalSites:p.placements.length-built.length,maskPixelsCompared:maskOpaque,maskPixelsDifferent:maskDifferences,originalGraphHash:hash,graphCounts:p.sourceGraphCounts};fs.writeFileSync(doc+'/文件与像素验收.json',JSON.stringify(result,null,2));console.log(JSON.stringify(result));if(issues.length)process.exitCode=1;
}
main().catch(e=>{console.error(e);process.exitCode=1});
