const fs=require('fs'),path=require('path'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
function range(data,y,x0,x1){let l=-1,r=-1;for(let x=x0;x<=x1;x++)if(data[(y*1024+x)*4+3]>=40){if(l<0)l=x;r=x;}return l<0?null:[l,r]}
(async()=>{const faceFile=path.join(active,'male/face_shape/01.png'),face=await sharp(faceFile).ensureAlpha().raw().toBuffer();
for(const a of process.argv.slice(2)){
 const id=String(Number(a)).padStart(2,'0'),file=path.join(active,`male/clothing/${id}.png`);
 const data=await sharp(file).ensureAlpha().raw().toBuffer();
 const target=await sharp(file).extract({left:395,top:500,width:250,height:130}).resize({width:1000}).png().toBuffer();
 fs.writeFileSync(path.join(__dirname,`clothing${id}_collar_target.png`),target);
 const assembled=await sharp({create:{width:1024,height:1536,channels:4,background:'#252b2e'}}).composite([{input:faceFile},{input:file}]).png().toBuffer();
 await sharp(assembled).extract({left:395,top:500,width:250,height:130}).resize({width:1000}).png().toFile(path.join(__dirname,`clothing${id}_collar_guide.png`));
 const marks=Buffer.alloc(1000*520*4);
 for(let y=518;y<=570;y++){
  const skin=range(face,y,395,645),left=range(data,y,395,516),right=range(data,y,518,645);if(!skin)continue;
  const spans=[];if(left)spans.push([left[1]-2,Math.max(left[1],skin[0]+5)]);if(right)spans.push([Math.min(right[0],skin[1]-6),right[0]+2]);
  for(const [lo,hi]of spans)for(let x=lo;x<=hi;x++)for(let dy=0;dy<4;dy++)for(let dx=0;dx<4;dx++){const k=(((y-500)*4+dy)*1000+(x-395)*4+dx)*4;marks[k]=240;marks[k+1]=35;marks[k+2]=50;marks[k+3]=235;}
 }
 await sharp(target).composite([{input:marks,raw:{width:1000,height:520,channels:4}}]).png().toFile(path.join(__dirname,`clothing${id}_collar_addition_guide.png`));
 console.log(id+' ROI target, mask addition guide and neck fitting guide ready.');
}
})().catch(e=>{console.error(e);process.exitCode=1;});
