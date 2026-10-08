// Repair one female review set using existing full frontal and rear paintings.
const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const {PNG}=require('pngjs');
const root=path.resolve(__dirname,'..');
const pack=path.join(root,'mods/appearance/腰上女修拆件演示_v1');
const active=path.join(pack,'test_assets_green_reference_v1');
const source=path.join(pack,'test_sources/green_master_single_set');
const output=path.join(pack,'test_sources/hair_integrity_v1');
const preview=path.join(pack,'preview/hair_integrity_v1');
fs.mkdirSync(output,{recursive:true});fs.mkdirSync(preview,{recursive:true});
const W=1024,H=1536;
function read(file){const im=PNG.sync.read(fs.readFileSync(file));if(im.width!==W||im.height!==H)throw Error(file);return im}
function blank(){return new PNG({width:W,height:H})}
function save(name,im){fs.writeFileSync(path.join(output,`${name}_0.png`),PNG.sync.write(im))}
function clamp(x){return Math.max(0,Math.min(255,Math.round(x)))}
function smooth(x){x=Math.max(0,Math.min(1,x));return x*x*(3-2*x)}
async function main(){
 const completeFront=read(path.join(pack,'source/full_front_crown_v8/hair_front_0.png'));
 const frontLength=read(path.join(source,'female_front_guide_source.png'));
 const rearBytes=await sharp(path.join(source,'female_rear_complete_source.png'))
  .resize(901,1490,{kernel:'lanczos3'})
  .extend({left:61,top:35,right:62,bottom:11,background:{r:0,g:0,b:0,alpha:0}})
  .png().toBuffer();
 const completeRear=PNG.sync.read(rearBytes);
 const front=blank(),rear=blank();
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){
  const k=(y*W+x)*4;
  // Restore the ordinary head silhouette and temples from the complete
  // female front. Its own crown is aligned to the female y145 head anchor.
  const fx=Math.round(512+(x-512)/.90),fy=y+30;
  if(fx>=0&&fx<W&&fy>=0&&fy<H){
   const s=(fy*W+fx)*4,a=completeFront.data[s+3];
   if(a){
    front.data[k]=clamp(completeFront.data[s]*.85+36);
    front.data[k+1]=clamp(completeFront.data[s+1]*.85+28);
    front.data[k+2]=clamp(completeFront.data[s+2]*.85+24);
    front.data[k+3]=a;
   }
  }
  // Preserve the independently painted cheek-origin front lengths.
  const lx=Math.round(515+(x-512)/.78);
  if(y>=440&&lx>=0&&lx<W&&(x<441||x>583)){
   const s=(y*W+lx)*4,a=frontLength.data[s+3];
   const coverage=a/255*smooth((y-440)/150);
   if(coverage){
    const baseA=front.data[k+3]/255,resultA=coverage+baseA*(1-coverage);
    for(let c=0;c<3;c++)front.data[k+c]=clamp((frontLength.data[s+c]*coverage+front.data[k+c]*baseA*(1-coverage))/resultA);
    front.data[k+3]=clamp(resultA*255);
   }
  }
  // Complete rear remains painted behind face, ears and clothing, including
  // every hidden strand. No master-shaped alpha subtraction is applied.
  const a=completeRear.data[k+3];
  if(a){
   rear.data[k]=clamp(completeRear.data[k]-5);
   rear.data[k+1]=clamp(completeRear.data[k+1]-4);
   rear.data[k+2]=clamp(completeRear.data[k+2]-4);
   rear.data[k+3]=a;
  }
 }
 save('hair_front',front);save('hair_back',rear);
 fs.writeFileSync(path.join(output,'repair_manifest.json'),JSON.stringify({
  status:'one_female_review_candidate',canvas:[W,H],center_axis_x:512,
  front_source:'source/full_front_crown_v8/hair_front_0.png',
  front_cheek_lock_source:'test_sources/green_master_single_set/female_front_guide_source.png',
  rear_source:'test_sources/green_master_single_set/female_rear_complete_source.png',
  front_scale_x:.90,front_shift_y:-30,front_lock_scale_x:.78,rear_alpha_untouched:true
 },null,2)+'\n');
 async function compose(name,visible){
  const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','makeup','hair_front'];
  await sharp({create:{width:W,height:H,channels:4,background:'#cbc8bd'}})
   .composite(order.filter(part=>visible[part]!==false).map(part=>({input:path.join(part.startsWith('hair_')?output:active,`${part}_0.png`)})))
   .png().toFile(path.join(preview,name));
 }
 await compose('both.png',{});
 await compose('front_only.png',{hair_back:false});
 await compose('back_only.png',{hair_front:false});
 console.log('FEMALE_HAIR_INTEGRITY_REVIEW_READY');
}
main().catch(e=>{console.error(e);process.exitCode=1});
