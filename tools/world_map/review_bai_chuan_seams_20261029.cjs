const fs=require('node:fs'),sharp=require('sharp');
const art='textures/world/bai_chuan/joint_paint_v4',doc='docs/美术规范/百川接边与色调修订_20261029';
async function main(){
 const file=art+'/master_6144.png',pixels=await sharp(file).removeAlpha().raw().toBuffer(),seams=JSON.parse(fs.readFileSync(art+'/seams.json','utf8')),reports=[];
 for(const axis of [0,1])for(let k=1;k<=5;k++){
  const center=k*1024,items=[];
  for(let section=0;section<4;section++){
   const box=axis===0?{left:center-192,top:section*1536,width:384,height:1536}:{left:section*1536,top:center-192,width:1536,height:384};
   items.push({input:await sharp(file).extract(box).png().toBuffer(),left:axis===0?section*384:0,top:axis===0?0:section*384});
  }
  const name=(axis===0?'纵向':'横向')+'接边_'+k+'.png';await sharp({create:{width:1536,height:1536,channels:3,background:'#eee'}}).composite(items).png().toFile(doc+'/'+name);
  const deltas=[];let maximum=0;
  const path=axis===0?seams.vertical[k-1]:seams.horizontal[k-1];
  for(let along=4;along<6140;along+=4){
   const across=path[along],x=axis===0?across:along,y=axis===0?along:across;
   let a=[0,0,0],b=[0,0,0],valid=true;
   for(let d=1;d<=4;d++)for(const sign of [-1,1]){
    const xx=x+(axis===0?d*sign:0),yy=y+(axis===1?d*sign:0),i=(yy*6144+xx)*3,r=pixels[i],g=pixels[i+1],blue=pixels[i+2];
    if(r*.299+g*.587+blue*.114<176||r-g<20||g-blue<30)valid=false;
    const dst=sign<0?a:b;for(let c=0;c<3;c++)dst[c]+=pixels[i+c]/4;
   }
   if(valid){const delta=Math.hypot(...a.map((v,i)=>v-b[i]));deltas.push(delta);maximum=Math.max(maximum,delta);}
  }
  deltas.sort((a,b)=>a-b);reports.push({axis:axis===0?'vertical':'horizontal',boundary:center,nativeContactSheet:name,plainSamples:deltas.length,medianRGBDelta:deltas[Math.floor(deltas.length*.5)],p95RGBDelta:deltas[Math.floor(deltas.length*.95)],maximumRGBDelta:maximum});
 }
 fs.writeFileSync(doc+'/接边平地色差测量.json',JSON.stringify({note:'Flat-paper color measurements supplement native visual inspection, not a semantic seam pass.',seams:reports},null,2));console.log(JSON.stringify(reports.map(r=>({axis:r.axis,boundary:r.boundary,p95:r.p95RGBDelta,max:r.maximumRGBDelta}))));
}
main().catch(e=>{console.error(e);process.exitCode=1});
