const fs=require('fs'),s=require('sharp');
(async()=>{const dir='E:/stacklands/mods/appearance/portrait_original_refinement_20261103_v1/work_female';fs.mkdirSync(dir,{recursive:true});
const src=['E:/tmp/CodexData/generated_images/01a1a1e3-6203-7a82-9158-df06f16fe249/exec-023caf3b-7f65-4793-a11e-257af8b70790.png','E:/tmp/CodexData/generated_images/01a1a1e3-6203-7a82-9158-df06f16fe249/exec-2475c1cd-d65a-40aa-b5e4-f093e907c535.png'];
for(let i=0;i<src.length;i++){fs.copyFileSync(src[i],dir+'/trial_'+i+'.png');await s(src[i]).flatten({background:'#eae4d7'}).png().toFile(dir+'/trial_'+i+'_light.png')}
const b='E:/stacklands/mods/appearance/portrait_original_refinement_20261103_v1/baseline/female/';
const list=[src[1],...['face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory'].map(p=>b+p+'.png'),src[0]];
await s({create:{width:1024,height:1536,channels:4,background:'#eae4d7'}}).composite(list.map(input=>({input}))).png().toFile(dir+'/trial_composite.png');
console.log(dir);
})();
