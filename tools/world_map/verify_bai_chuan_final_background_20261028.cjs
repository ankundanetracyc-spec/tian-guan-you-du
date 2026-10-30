const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),art=root+'/textures/world/bai_chuan/whole_paint_v3',doc=root+'/docs/美术规范/百川入口节点分层_20261028';
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
async function main(){
 const plan=JSON.parse(fs.readFileSync(art+'/refinement_plan.json','utf8'));
 const failures=[],regions=[],tiles=[];
 for(const p of plan.patches){
  const file=art+'/refined/'+p.id+'.png',record=JSON.parse(fs.readFileSync(art+'/refined/'+p.id+'_generation.json','utf8').replace(/^\uFEFF/,''));
  const meta=await sharp(file).metadata();
  if(meta.width<1254||meta.height<1254||meta.width!==meta.height)failures.push(p.id+' too small');
  if(record.sourceMaster!==plan.sourceMaster)failures.push(p.id+' wrong composition');
  if(hash(file)!==hash(record.source))failures.push(p.id+' differs from generated original');
  regions.push({id:p.id,pixels:[meta.width,meta.height],sha256:hash(file),source:record.source});
 }
 const master=await sharp(art+'/master_6144.png').metadata();
 if(master.width!==6144||master.height!==6144)failures.push('master is not 6144');
 for(let r=0;r<3;r++)for(let c=0;c<3;c++){
  const name=`bc_${r+1}${c+1}.png`,file=art+'/tiles/'+name;
  const meta=await sharp(file).metadata();
  const actual=await sharp(file).removeAlpha().raw().toBuffer();
  const expected=await sharp(art+'/master_6144.png').extract({left:c*2048,top:r*2048,width:2048,height:2048}).removeAlpha().raw().toBuffer();
  const equal=actual.equals(expected),fresh=hash(file)!==hash(root+'/textures/world/bai_chuan/acceptance_v1/tiles/'+name);
  if(meta.width!==2048||meta.height!==2048||!equal||!fresh)failures.push('bad tile '+name);
  tiles.push({file:name,pixels:[meta.width,meta.height],matchesMaster:equal,differsFromOld:fresh,sha256:hash(file)});
 }
 const placement=JSON.parse(fs.readFileSync(doc+'/落位检查.json','utf8'));
 if(placement.nodes!==123||placement.wetBuildings.length||placement.iconOverlaps.length)failures.push('placement failures');
 const source=root+'/docs/prototypes/百川宗门城镇资源规划数据_20261027.json';
 const old=JSON.parse(fs.readFileSync(root+'/textures/world/bai_chuan/acceptance_v1/presentation.json','utf8'));
 if(hash(source)!==old.sourceHash)failures.push('planning source changed');
 const report={nativeRegions:regions.length,worldPixels:[master.width,master.height],deliveryTiles:tiles,regions,placement,failures,productionReady:!failures.length};
 fs.writeFileSync(doc+'/底图验收.json',JSON.stringify(report,null,2)+'\n');
 if(failures.length)throw Error(failures.join(', '));
 fs.writeFileSync(art+'/READY.json',JSON.stringify({version:3,nativeRegions:regions.length,worldPixels:6144,tiles:9,composition:plan.sourceMaster,validated:true},null,2)+'\n');
 console.log(JSON.stringify({nativeRegions:regions.length,tiles:tiles.length,failures}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
