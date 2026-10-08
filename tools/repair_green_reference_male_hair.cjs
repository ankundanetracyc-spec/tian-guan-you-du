// Reuse the complete existing frontal and rear hair paintings for one review set.
const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const {PNG}=require('pngjs');
const root=path.resolve(__dirname,'..');
const pack=path.join(root,'mods/appearance/腰上男修拆件演示_v2');
const active=path.join(pack,'test_assets_green_reference_v1');
const output=path.join(pack,'test_sources/hair_integrity_v1');
const preview=path.join(pack,'preview/hair_integrity_v1');
fs.mkdirSync(output,{recursive:true});fs.mkdirSync(preview,{recursive:true});
const W=1024,H=1536,N=W*H;
function read(file){const im=PNG.sync.read(fs.readFileSync(file));if(im.width!==W||im.height!==H)throw Error(file);return im}
function blank(){return new PNG({width:W,height:H})}
function save(name,image){fs.writeFileSync(path.join(output,`${name}_0.png`),PNG.sync.write(image))}
function clamp(x){return Math.max(0,Math.min(255,Math.round(x)))}
function smooth(x){x=Math.max(0,Math.min(1,x));return x*x*(3-2*x)}
const completeFront=read(path.join(pack,'test_sources/full_front_crown_v11/hair_front_1.png'));
const completeRear=read(path.join(pack,'test_sources/anatomy_v15/before/hair_back_0.png'));
const oldFront=read(path.join(output,'reviewed_crown_source.png'));
const front=blank(),rear=blank();
for(let y=0;y<H;y++)for(let x=0;x<W;x++){
 const k=(y*W+x)*4;
 // The complete front already contains a natural scalp, temple and ear locks.
 // Its x registration is fitted to the reviewed 1024px face without clipping.
 const frontX=Math.round(512+(x-512)/.80);
 if(frontX>=0&&frontX<W){
  const s=(y*W+frontX)*4,a=completeFront.data[s+3];
  if(a){
   front.data[k]=clamp(completeFront.data[s]*.85+36);
   front.data[k+1]=clamp(completeFront.data[s+1]*.85+28);
   front.data[k+2]=clamp(completeFront.data[s+2]*.85+24);
   front.data[k+3]=a;
  }
 }
 // The reviewed green painting supplies its crown brushwork. The complete
 // frontal source supplies intact face-side locks without ragged masking.
 const oldA=oldFront.data[k+3];
 const crownWeight=y<210?1:y<280?1-smooth((y-210)/70):0;
 if(oldA&&crownWeight){
  const coverage=oldA/255*crownWeight;
  const baseA=front.data[k+3]/255;
  const resultA=coverage+baseA*(1-coverage);
  if(resultA){
   for(let c=0;c<3;c++)front.data[k+c]=clamp((oldFront.data[k+c]*coverage+front.data[k+c]*baseA*(1-coverage))/resultA);
   front.data[k+3]=clamp(resultA*255);
  }
 }
 const backX=Math.round(512+(x-512)/.90),backY=y-5;
 if(backX>=0&&backX<W&&backY>=0&&backY<H){
  const s=(backY*W+backX)*4,a=completeRear.data[s+3];
  if(a){
   rear.data[k]=clamp(completeRear.data[s]*.85+35);
   rear.data[k+1]=clamp(completeRear.data[s+1]*.85+28);
   rear.data[k+2]=clamp(completeRear.data[s+2]*.85+24);
   rear.data[k+3]=a;
  }
 }
}
save('hair_front',front);save('hair_back',rear);
fs.writeFileSync(path.join(output,'repair_manifest.json'),JSON.stringify({
 status:'one_male_review_candidate',canvas:[W,H],
 front_source:'test_sources/full_front_crown_v11/hair_front_1.png',
 rear_source:'test_sources/anatomy_v15/before/hair_back_0.png',
 scalp_center_x:512,front_width_scale:.80,rear_width_scale:.90,
 rear_alpha_untouched:true,front_has_complete_temple_strands:true,
 pigment_reference:'reviewed_crown_source.png copied from the previous one-set review'
},null,2)+'\n');
async function compose(name,visible){
 const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','beard','hair_front'];
 await sharp({create:{width:W,height:H,channels:4,background:'#cbc8bd'}})
  .composite(order.filter(part=>visible[part]!==false).map(part=>({input:path.join(part.startsWith('hair_')?output:active,`${part}_0.png`)})))
  .png().toFile(path.join(preview,name));
}
async function run(){
 await compose('both.png',{});
 await compose('front_only.png',{hair_back:false});
 await compose('back_only.png',{hair_front:false});
 console.log('HAIR_INTEGRITY_REVIEW_READY');
}
run().catch(e=>{console.error(e);process.exitCode=1});
