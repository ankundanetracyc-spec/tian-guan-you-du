const fs=require('node:fs/promises'),path=require('node:path'),sharp=require('sharp');
const {place}=require('./place_native.cjs');
const R=path.resolve(__dirname,'..');
(async()=>{
 const settings={
  eyes:{s:110/(565.3924-458.8921),source:[(458.8921+565.3924)/2,(410.3785+410.4619)/2],target:[519,379],landmarks:{left_pupil:[458.8921,410.3785],right_pupil:[565.3924,410.4619]}},
  eyebrows:{s:125/(582.5155-442.9259),source:[(582.5155+442.9259)/2,(386.9073+386.8542)/2],target:[519.5,353.5],landmarks:{left_brow_pigment_center:[442.9259,386.9073],right_brow_pigment_center:[582.5155,386.8542]}},
  nose:{s:.8,source:[514,493],target:[519,446],landmarks:{nose_tip:[514,493],nostril_left:[502,499],nostril_right:[525,499]}},
  mouth:{s:.9,source:[514,548],target:[519,483],landmarks:{lip_seam_center:[514,548],lip_left:[479,548],lip_right:[549,548]}},
  hair_front:{s:1,source:[519,296],target:[519,277],landmarks:{central_hairline:[519,296],crown_center:[519,137]}}
 };
 const results=[];
 for(const [part,a]of Object.entries(settings)){
  const item=await place(path.join(R,'source/female/raw',part,'01.png'),path.join(R,'female',part,'01.png'),a.s,a.source,a.target);
  item.measured_landmarks=a.landmarks;item.mapped_landmarks=Object.fromEntries(Object.entries(a.landmarks).map(([k,[x,y]])=>[k,[x*item.uniform_scale+item.translate[0],y*item.uniform_scale+item.translate[1]]]));results.push(item);
 }
 await fs.writeFile(path.join(R,'source/female/registration_01.json'),JSON.stringify({status:'actual point placement; full three-point rig certification not asserted',records:results},null,2));
 const order=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front'];
 for(const [tag,color]of [['warm','#eae4d8'],['dark','#252b30']]){
  const list=order.map(p=>path.join(R,'female',settings[p]||p==='hair_back'?p+'/01.png':p+'.png'));
  await sharp({create:{width:1024,height:1536,channels:4,background:color}}).composite(list.map(input=>({input}))).png().toFile(path.join(R,'qa/female01_'+tag+'.png'));
 }
 console.log(results.map(x=>({part:path.basename(path.dirname(x.output)),scale:x.uniform_scale,translate:x.translate,points:x.mapped_landmarks})));
})();
