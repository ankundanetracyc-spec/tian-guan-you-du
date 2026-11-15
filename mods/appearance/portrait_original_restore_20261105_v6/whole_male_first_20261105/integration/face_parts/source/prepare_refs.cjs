const fs=require('fs'),path=require('path'),sharp=require('sharp');
const R=path.resolve(__dirname,'..'),M=path.resolve(R,'../../male_whole_01.png');
const defs={eyebrows:{box:[430,328,183,27],factor:4},eyes:{box:[434,350,173,38],factor:4},nose:{box:[487,348,65,104],factor:6},mouth:{box:[477,459,84,34],factor:6}};
(async()=>{fs.mkdirSync(path.join(R,'source'),{recursive:true});fs.mkdirSync(path.join(R,'qa'),{recursive:true});
for(const[part,d]of Object.entries(defs)){const[x,y,width,height]=d.box;const buffer=await sharp(M).extract({left:x,top:y,width,height}).resize(width*d.factor,height*d.factor,{kernel:'lanczos3'}).png().toBuffer();const left=Math.round((1024-width*d.factor)/2),top=Math.round((1024-height*d.factor)/2);await sharp({create:{width:1024,height:1024,channels:3,background:'#dcbba5'}}).composite([{input:buffer,left,top}]).png().toFile(path.join(R,'source',part+'_reference.png'));d.reference_insert={left,top,width:width*d.factor,height:height*d.factor};}
fs.writeFileSync(path.join(R,'source/reference_geometry.json'),JSON.stringify(defs,null,2));console.log('four local reference crops prepared');
})().catch(e=>{console.error(e);process.exitCode=1});
