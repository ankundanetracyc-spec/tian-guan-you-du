const fs=require('fs'),s=require('sharp');
(async()=>{const d='E:/stacklands/mods/appearance/portrait_original_refinement_20261103_v1/work_female/';
const f='E:/tmp/CodexData/generated_images/01a1a1e3-6203-7a82-9158-df06f16fe249/exec-738e76f5-c8d5-44f4-8f92-487def8aa886.png';const k=d+'trial_1.png';fs.copyFileSync(f,d+'front_source.png');fs.copyFileSync(k,d+'back_source.png');
await s({create:{width:1024,height:1536,channels:4,background:'#00000000'}}).composite([{input:await s(f).extract({left:0,top:22,width:1024,height:1514}).toBuffer(),left:0,top:0}]).png().toFile(d+'hair_front.png');
await s({create:{width:1024,height:1536,channels:4,background:'#00000000'}}).composite([{input:await s(k).extract({left:0,top:0,width:1024,height:1518}).toBuffer(),left:0,top:18}]).png().toFile(d+'hair_back.png');
const b='E:/stacklands/mods/appearance/portrait_original_refinement_20261103_v1/baseline/female/';const list=[d+'hair_back.png',...['face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory'].map(p=>b+p+'.png'),d+'hair_front.png'];
for(const [tag,c] of [['light','#eae4d7'],['dark','#252528']]){await s({create:{width:1024,height:1536,channels:4,background:c}}).composite(list.map(input=>({input}))).png().toFile(d+'composite_'+tag+'.png')}
console.log('saved translated candidates');})();
