const fs=require('fs'),path=require('path');
const pack='E:/stacklands/mods/appearance/portrait_male_reference_20261104_v1',src=path.join(pack,'source'),map={"hair_front":{"02":"exec-4b532b89-3dba-4d53-aa94-a05f34b87c79.png","03":"exec-b46f7e6d-5d33-4f00-9232-adb6e58a9111.png"},"hair_back":{"02":"exec-b235fc13-8147-463d-ac6a-46ed41c69ab6.png"}};
const specs=require('./tone_layer_generation_specs.cjs'),file=path.join(src,'hair_registration.json'),registry=JSON.parse(fs.readFileSync(file,'utf8')),records=[];
for(const[part,items]of Object.entries(map))for(const[id,name]of Object.entries(items)){
const original='E:/tmp/CodexData/generated_images/01a1a4be-7b8b-76e1-af95-b099fa3c88dc/'+name,raw=path.join(src,'raw_hair',part,id+'.png'),entry=registry.outputs.find(e=>e.part===part&&e.id===id);
fs.copyFileSync(original,raw);entry.before_tone_layer_registration={...entry};Object.assign(entry,{file:raw,generated_original:original,scale:1,x:0,y:0,unchanged:true});records.push({...specs.find(j=>j.part===part&&j.id===id),generated_original:original,raw_copy:raw});
}fs.writeFileSync(file,JSON.stringify(registry,null,2)+'\n');fs.writeFileSync(path.join(src,'tone_layer_generation_records.json'),JSON.stringify({tool:'built-in image_gen',records},null,2)+'\n');

