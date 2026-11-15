const fs=require('fs'),path=require('path'),sharp=require('sharp');
const pack='E:/stacklands/mods/appearance/portrait_male_reference_20261104_v1',src=path.join(pack,'source'),files={"hair_front":{"01":"exec-99e31243-cc64-4bb1-8729-ef4244a71a83.png","02":"exec-0548b915-0cee-4313-bfc4-9e9702180004.png","03":"exec-d8f55beb-0759-4229-910d-654837ad6050.png"},"hair_back":{"01":"exec-ff5a0ec4-4799-4a16-b31d-5bf627e50a89.png","02":"exec-74a172e1-9e69-411d-b029-c8f621c8c6c4.png","03":"exec-20048137-8c75-46ef-848d-6923511e8c8c.png"}};
const specFile=path.join(src,'hair_registration.json'),spec=JSON.parse(fs.readFileSync(specFile,'utf8')),jobs=require('./ear_depth_generation_specs.cjs'),records=[];
(async()=>{for(const [part,images] of Object.entries(files))for(const [id,name] of Object.entries(images)){
const original='E:/tmp/CodexData/generated_images/01a1a4be-7b8b-76e1-af95-b099fa3c88dc/'+name,raw=path.join(src,'raw_hair',part,id+'.png');
fs.copyFileSync(original,raw);
const entry=spec.outputs.find(x=>x.part===part&&x.id===id);entry.before_ear_depth_registration={...entry};entry.file=raw;entry.generated_original=original;entry.scale=part==='hair_front'&&id==='03'?.85:1;entry.x=part==='hair_front'&&id==='03'?78:0;entry.y=part==='hair_front'&&id==='03'?22:0;entry.unchanged=entry.scale===1;
const {data,info}=await sharp(raw).ensureAlpha().raw().toBuffer({resolveWithObject:true});let bb=[1024,1536,-1,-1],runs=[],start=null;
for(let y=0;y<info.height;y++){for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>=180)bb=[Math.min(bb[0],x),Math.min(bb[1],y),Math.max(bb[2],x),Math.max(bb[3],y)];
const a=data[(y*info.width+517)*4+3];if(a>=180&&start===null)start=y;if((a<180||y===info.height-1)&&start!==null){runs.push([start,y-1]);start=null;}}
console.log(part,id,bb,runs);
records.push({...jobs.find(j=>j.part===part&&j.id===id),generated_original:original,raw_copy:raw});
}fs.writeFileSync(specFile,JSON.stringify(spec,null,2)+'\n');fs.writeFileSync(path.join(src,'ear_depth_generation_records.json'),JSON.stringify({tool:'built-in image_gen',purpose:'Ear-over-rear-hair depth as user reference; wide posterior side hair behind fixed face and ears',records},null,2)+'\n');})().catch(e=>{console.error(e);process.exitCode=1});

