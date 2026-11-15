const fs=require('fs'),path=require('path'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(pack,'../../..');
async function composed(back,front,bg){
 const layers=[['hair_back',back],['clothing_back','01'],['face_shape','01'],['clothing','01'],['eyes',front==='03'?'02':'01'],['eyebrows','01'],['nose','01'],['mouth','01'],['hair_front',front]].map(([folder,id])=>({input:path.join(pack,'male',folder,id+'.png'),left:0,top:0}));
 for(const l of layers)if(!fs.existsSync(l.input))throw Error('Missing '+l.input);
 return sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite(layers).png().toBuffer();
}
async function main(){for(const[bg,tone]of[['#e8e1d6','light'],['#222a2b','dark']]){
 const comparisons=[],mixed=[];
 for(let i=1;i<=3;i++){
  const id=String(i).padStart(2,'0'),ref=path.join(project,'mods/appearance/portrait_rebuild_20261103/source/male_final/reference_'+id+'.png');
  const original=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:ref,left:[204,215,220][i-1],top:[50,49,50][i-1]}]).png().toBuffer();
  const combo=await composed(id,id,bg);
  await sharp(combo).png().toFile(path.join(pack,'qa/back'+id+'_full_pair_'+tone+'.png'));
  for(const[j,input]of [original,path.join(__dirname,'master03_registered.png'),combo].entries())comparisons.push({input:await sharp(input).flatten({background:bg}).resize(512,768).png().toBuffer(),left:j*512,top:(i-1)*768});
  for(let j=1;j<=3;j++)mixed.push({input:await sharp(await composed(id,String(j).padStart(2,'0'),bg)).resize(512,768).png().toBuffer(),left:(j-1)*512,top:(i-1)*768});
 }
 await sharp({create:{width:1536,height:2304,channels:4,background:bg}}).composite(comparisons).png().toFile(path.join(pack,'qa/back_full_original_master_pair_comparison_'+tone+'.png'));
 await sharp({create:{width:1536,height:2304,channels:4,background:bg}}).composite(mixed).png().toFile(path.join(pack,'qa/back_front_nine_pairs_'+tone+'.png'));
 }console.log('Three paired original/master/new comparisons and nine mixed front/rear pairs saved in qa.');}
main().catch(e=>{console.error(e);process.exitCode=1;});
