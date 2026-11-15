const fs=require('fs'),path=require('path'),sharp=require('sharp');
const R=path.resolve(__dirname,'..'),Q=path.join(R,'qa/features');
fs.mkdirSync(Q,{recursive:true});
const specs={eyebrows:{source:[420,329,195,31],raw:[392,328,240,34]},eyes:{source:[420,352,195,31],raw:[397,350,225,42]},nose:{source:[480,339,77,119],raw:[466,333,94,164]},mouth:{source:[471,463,91,35],raw:[459,622,107,42]}};
async function grid(file,box,name){
 const [left,top,width,height]=box,scale=6,sw=width*scale,sh=height*scale,pad=30;
 const crop=await sharp(file).extract({left,top,width,height}).resize(sw,sh,{kernel:'nearest'}).flatten({background:'#e8ded2'}).png().toBuffer();
 let svg=`<svg width="${sw+pad}" height="${sh+pad}"><style>text{font:11px sans-serif;fill:#445566}line{stroke:#5685a4;stroke-opacity:.45;stroke-width:1}</style>`;
 for(let x=Math.ceil(left/5)*5;x<left+width;x+=5){let p=pad+(x-left)*scale;svg+=`<line x1="${p}" y1="${pad}" x2="${p}" y2="${sh+pad}"/><text x="${p+2}" y="${x%10===0?12:25}">${x}</text>`;}
 for(let y=Math.ceil(top/5)*5;y<top+height;y+=5){let p=pad+(y-top)*scale;svg+=`<line x1="${pad}" y1="${p}" x2="${sw+pad}" y2="${p}"/><text x="1" y="${p+4}">${y}</text>`;}
 svg+='</svg>';
 await sharp({create:{width:sw+pad,height:sh+pad,channels:4,background:'#e8ded2'}}).composite([{input:crop,left:pad,top:pad},{input:Buffer.from(svg)}]).png().toFile(path.join(Q,name+'_grid_6x.png'));
 await sharp(crop).toFile(path.join(Q,name+'_close_6x.png'));
}
(async()=>{const stats=[];for(const [part,s]of Object.entries(specs)){
 const p=path.join(R,'raw/features',part+'_native.png');
 await grid(path.join(R,'source/approved_male.png'),s.source,'source_'+part);
 await grid(p,s.raw,'native_'+part);
 const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});const levels={};
 for(const t of [0,3,16,128,230]){let minx=info.width,miny=info.height,maxx=-1,maxy=-1,count=0;for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>t){minx=Math.min(minx,x);maxx=Math.max(maxx,x);miny=Math.min(miny,y);maxy=Math.max(maxy,y);count++;}levels[t]={count,bbox:[minx,miny,maxx+1,maxy+1]};}
 stats.push({part,size:[info.width,info.height],alpha:levels});
 }const actual=path.join(Q,'head_with_features_light.png');if(fs.existsSync(actual))await grid(actual,[425,351,185,34],'actual_eyes');fs.writeFileSync(path.join(Q,'native_alpha_stats.json'),JSON.stringify(stats,null,2));console.log(JSON.stringify(stats));})().catch(e=>{console.error(e);process.exitCode=1});
