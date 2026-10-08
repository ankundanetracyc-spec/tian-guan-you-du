// One female review set paired with the user-selected historic green male.
// All exported parts keep the 1024x1536 top-left origin used by the game.
const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const {PNG}=require('pngjs');

const root=path.resolve(__dirname,'..');
const pack=path.join(root,'mods/appearance/腰上女修拆件演示_v1');
const sourceDir=path.join(pack,'test_sources/green_master_single_set');
const out=path.join(pack,'test_assets_green_reference_v1');
const preview=path.join(pack,'preview/green_reference_single_set');
const old=path.join(pack,'test_assets_v5');
fs.mkdirSync(out,{recursive:true});fs.mkdirSync(preview,{recursive:true});
const W=1024,H=1536,N=W*H;
function png(file){const p=PNG.sync.read(fs.readFileSync(file));if(p.width!==W||p.height!==H)throw Error(`Wrong canvas ${file}`);return p}
function blank(){return new PNG({width:W,height:H})}
function clamp(n){return Math.max(0,Math.min(255,Math.round(n)))}
function smooth(t){t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)}
function ink(r,g,b){return smooth((r-g+5)/9)*smooth((23-r+g)/10)*smooth((30-r+b)/12)}
function skin(r,g,b){return smooth((r-g-8)/10)*smooth((g-b-1)/7)*smooth((r-75)/50)}
function ellipse(x,y,cx,cy,rx,ry){
 const distance=Math.sqrt(((x-cx)/rx)**2+((y-cy)/ry)**2);
 return smooth((1-distance)/.14);
}
function save(name,p){fs.writeFileSync(path.join(out,`${name}_0.png`),PNG.sync.write(p))}
async function transformed(file,width,height,left,top){
 const p=path.join(sourceDir,file);
 const b=await sharp(p).resize(width,height,{kernel:'lanczos3'}).extend({left,top,right:W-left-width,bottom:H-top-height,background:{r:0,g:0,b:0,alpha:0}}).png().toBuffer();
 return PNG.sync.read(b);
}
function clearSmallIslands(im,minimum){
 const seen=new Uint8Array(N),queue=new Int32Array(N);let removed=0;
 for(let seed=0;seed<N;seed++){
  if(seen[seed]||im.data[seed*4+3]<9)continue;
  seen[seed]=1;let count=0,head=0;queue[count++]=seed;
  while(head<count){
   const q=queue[head++],x=q%W,y=(q-x)/W;
   for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
    if(!dx&&!dy)continue;const nx=x+dx,ny=y+dy;
    if(nx<0||nx>=W||ny<0||ny>=H)continue;
    const ni=ny*W+nx;if(seen[ni]||im.data[ni*4+3]<9)continue;
    seen[ni]=1;queue[count++]=ni;
   }
  }
  if(count>=minimum)continue;
  removed+=count;for(let i=0;i<count;i++)im.data[queue[i]*4+3]=0;
 }
 return removed;
}
function drawMaster(master,layer,k,a){
 if(!a)return;layer.data[k]=master.data[k];layer.data[k+1]=master.data[k+1];layer.data[k+2]=master.data[k+2];layer.data[k+3]=a;
}
async function main(){
 const master=png(path.join(sourceDir,'female_master.png'));
 const robeSource=png(path.join(sourceDir,'female_clothes_clean_source.png'));
 const rearSource=await transformed('female_rear_complete_source.png',901,1490,61,35);
 const blankMaster=png(path.join(sourceDir,'female_featureless_master.png'));
 const frontGuide=png(path.join(sourceDir,'female_front_guide_source.png'));
 const oldFront=png(path.join(old,'hair_front_0.png'));

 // The clothing edit preserved the source silhouette but was placed 160 px
 // high. Its alpha agrees with the original outer garment after this shift.
 const clothes=blank();
 for(let y=160;y<H;y++)for(let x=0;x<W;x++){
  const dst=(y*W+x)*4,src=((y-160)*W+x)*4;
  for(let j=0;j<4;j++)clothes.data[dst+j]=robeSource.data[src+j];
 }

 // Full hidden rear hair: ordinary scalp and every tail remain painted below
 // the face/ears/clothes, without a face-shaped hole or shoulder cutoff.
 const rear=blank();
 for(let k=0;k<N*4;k+=4){
  const a=rearSource.data[k+3];if(!a)continue;
  rear.data[k]=clamp(rearSource.data[k]-5);
  rear.data[k+1]=clamp(rearSource.data[k+1]-4);
  rear.data[k+2]=clamp(rearSource.data[k+2]-4);
  rear.data[k+3]=a;
 }

 // The full-frame featureless edit kept the original jaw and neck geometry.
 // Extract only skin; split the ear pixels into their own selectable layer.
 const face=blank(),ear=blank();
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){
  if(y<260||y>696||x<368||x>656)continue;
  const k=(y*W+x)*4,r=blankMaster.data[k],g=blankMaster.data[k+1],b=blankMaster.data[k+2];
  const a=clamp(blankMaster.data[k+3]*skin(r,g,b));if(!a)continue;
  const earBand=y>=350&&y<=477;
  const left=429+Math.max(0,Math.min(1,(y-390)/80))*9;
  const right=1024-left;
  const earOnly=earBand&&(x<left+4||x>right-4);
  const faceOnly=!earBand||(x>=left-3&&x<=right+3);
  if(earOnly){for(let j=0;j<3;j++)ear.data[k+j]=blankMaster.data[k+j];ear.data[k+3]=a}
  if(faceOnly){for(let j=0;j<3;j++)face.data[k+j]=blankMaster.data[k+j];face.data[k+3]=a}
 }

 // The source master supplies every visible front strand. The old frontal
 // alpha gives only an initial crown envelope, while a separate clean front
 // extraction guides cheek-origin locks across the garment.
 const front=blank();let longPixels=0;
 for(let y=0;y<H;y++)for(let x=0;x<W;x++){
  const k=(y*W+x)*4,r=master.data[k],g=master.data[k+1],b=master.data[k+2];
  const hair=ink(r,g,b);
  const oldBrow=Math.max(ellipse(x,y,464,360,43,17),ellipse(x,y,560,360,43,17));
  const cap=y<340?master.data[k+3]*hair:(y<=505?oldFront.data[k+3]*hair*(1-oldBrow):0);
  const cheekCorridor=(x>=275&&x<=435)||(x>=589&&x<=775);
  let lock=0;
  if(y>=382&&y<=1280&&cheekCorridor){
   const dark=smooth((140-r)/40);
   const robeDelta=Math.abs(r-clothes.data[k])+Math.abs(g-clothes.data[k+1])+Math.abs(b-clothes.data[k+2]);
   const garmentGate=y<580?1:smooth((robeDelta-8)/45);
   lock=frontGuide.data[k+3]*hair*dark*garmentGate;
  }
  const a=clamp(Math.min(master.data[k+3],Math.max(cap,lock)));
  drawMaster(master,front,k,a);
  if(y>700&&lock>32)longPixels++;
 }
 if(longPixels<10000)throw Error('Long front locks disappeared');
 const cleared=clearSmallIslands(front,250);
 // Keep the original visible dark hair pigment beside the ears. The newly
 // generated complete rear painting supplies pixels hidden under the head.
 for(let y=300;y<=560;y++)for(let x=0;x<W;x++){
  if(x>422&&x<602)continue;
  const k=(y*W+x)*4,r=master.data[k],g=master.data[k+1],b=master.data[k+2];
  if(!master.data[k+3]||r>118||ink(r,g,b)<.85)continue;
  const t=y<480?1:smooth((560-y)/80);
  rear.data[k]=clamp(rear.data[k]*(1-t)+r*t);
  rear.data[k+1]=clamp(rear.data[k+1]*(1-t)+g*t);
  rear.data[k+2]=clamp(rear.data[k+2]*(1-t)+b*t);
  rear.data[k+3]=Math.max(rear.data[k+3],clamp(master.data[k+3]*t));
 }
 // Restrict the visible outer side silhouette to the selected master. The
 // complete hidden center stays untouched behind face, ears and clothing.
 for(let y=300;y<=560;y++)for(let x=0;x<W;x++){
  if(x>422&&x<602)continue;
  const k=(y*W+x)*4;
  rear.data[k+3]=Math.min(rear.data[k+3],master.data[k+3]);
 }
 let crownLeft=W,crownRight=-1;
 for(let x=0;x<W;x++)if(front.data[(335*W+x)*4+3]>64){crownLeft=Math.min(crownLeft,x);crownRight=Math.max(crownRight,x)}
 for(let y=125;y<600;y++){
  let left=crownLeft,right=crownRight;
  if(y<335){
   left=W;right=-1;
   for(let x=0;x<W;x++)if(front.data[(y*W+x)*4+3]>64){left=Math.min(left,x);right=Math.max(right,x)}
  }else{
   const growth=(y-335)*.62;left-=growth;right+=growth;
  }
  if(right<left)continue;
  for(let x=0;x<Math.floor(left);x++)rear.data[(y*W+x)*4+3]=0;
  for(let x=Math.ceil(right)+1;x<W;x++)rear.data[(y*W+x)*4+3]=0;
 }

 // Soft, tightly bounded source-pigment patches preserve complete eyes and
 // nose (including iris highlights and nasal-root shade) without old masks
 // that belong to a differently drawn face. Brows move 8 px upward only.
 const eye=blank(),brow=blank(),nose=blank(),mouth=blank();
 for(let y=330;y<=525;y++)for(let x=405;x<=618;x++){
  const k=(y*W+x)*4;
  const eyeA=Math.max(ellipse(x,y,463,387,43,23),ellipse(x,y,562,387,43,23));
  const noseA=ellipse(x,y,512,444,35,43);
  const mouthA=ellipse(x,y,513,490,52,25);
  drawMaster(master,eye,k,clamp(master.data[k+3]*eyeA));
  drawMaster(master,nose,k,clamp(master.data[k+3]*noseA));
  drawMaster(master,mouth,k,clamp(master.data[k+3]*mouthA));
  if(y>=345&&y<=375){
   const browA=Math.max(ellipse(x,y,464,360,38,12),ellipse(x,y,560,360,38,12));
   const dst=((y-8)*W+x)*4;
   drawMaster(master,brow,dst,clamp(master.data[k+3]*browA));
  }
 }
 save('eye',eye);save('brow',brow);save('nose',nose);save('mouth',mouth);

 const cleanFacePath=path.join(pack,'test_sources/original_skin_complete_v1/face_0.png');
 const cleanEarPath=path.join(pack,'test_sources/original_skin_complete_v1/ear_0.png');
 const cleanBrowPath=path.join(pack,'test_sources/original_skin_complete_v1/brow_0.png');
 save('hair_back',rear);save('face',fs.existsSync(cleanFacePath)?png(cleanFacePath):face);
 save('ear',fs.existsSync(cleanEarPath)?png(cleanEarPath):ear);save('clothes',clothes);
 if(fs.existsSync(cleanBrowPath))save('brow',png(cleanBrowPath));
 save('makeup',blank());save('hair_front',front);
 const tracedReview=path.join(pack,'test_sources/traced_head_v2');
 for(const part of ['face','ear','eye','brow','nose','mouth','hair_front','hair_back']){
  const repaired=path.join(tracedReview,`${part}_0.png`);
  if(fs.existsSync(repaired))fs.copyFileSync(repaired,path.join(out,`${part}_0.png`));
 }

 const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','makeup','hair_front'];
 async function compose(bg,name){
  await sharp({create:{width:W,height:H,channels:4,background:bg}})
   .composite(order.map(part=>({input:path.join(out,`${part}_0.png`)})))
   .png().toFile(path.join(preview,name));
 }
 await compose('#cbc8bd','light.png');await compose('#202526','dark.png');
 await sharp(path.join(sourceDir,'female_master.png')).png().toFile(path.join(preview,'source_master.png'));
 await sharp({create:{width:2048,height:H,channels:3,background:'#cbc8bd'}})
  .composite([{input:await sharp(path.join(preview,'source_master.png')).png().toBuffer(),left:0,top:0},
              {input:await sharp(path.join(preview,'light.png')).png().toBuffer(),left:W,top:0}])
  .png().toFile(path.join(preview,'source_vs_parts.png'));
 const meta={status:'single_female_green_reference_review_only',source:'test_sources/green_master_single_set/female_master.png',
  canvas:[W,H],layer_order:order,transforms:{clothes_y:160,rear_scale_x:901/1024,rear_scale_y:1490/1536,rear_y:35,brow_y:-8},
  rear:'complete painted underlay with no head/ear/garment cutout',front:'source-master pigment and separate guide masks, front over ear and garment',
  nose:'independent old-anchor mask with strengthened nasal-root alpha',visible_long_front_pixels:longPixels,cleared_front_island_pixels:cleared,
  latest_head_and_parts:'test_sources/traced_head_v2/placement.json',
  integration:'current start-menu appearance test reads this one-set review pack; general runtime portrait catalog remains separate'};
 fs.writeFileSync(path.join(out,'split_manifest.json'),JSON.stringify(meta,null,2)+'\n');
 console.log(JSON.stringify({result:'FEMALE_GREEN_REFERENCE_SINGLE_SET_READY',longPixels,cleared,preview:path.join(preview,'source_vs_parts.png')}));
}
main().catch(e=>{console.error(e);process.exitCode=1});
