const fs=require('fs'),path=require('path'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
function bounds(data,y,x0,x1){let first=-1,last=-1;for(let x=x0;x<=x1;x++)if(data[(y*1024+x)*4+3]>=40){if(first<0)first=x;last=x;}return first<0?null:[first,last];}
async function main(){
 const face=await sharp(path.join(active,'male/face_shape/01.png')).ensureAlpha().raw().toBuffer();
 for(const id of [4,5,6,7]){
  const tag=String(id).padStart(2,'0');
  const cloth=await sharp(path.join(active,'male/clothing',tag+'.png')).ensureAlpha().raw().toBuffer();
  const {data,info}=await sharp(path.join(__dirname,'clothing'+tag+'_target_roi.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  for(let y=526;y<=575;y++){
   const skin=bounds(face,y,395,645),l=bounds(cloth,Math.max(y,530),395,516),r=bounds(cloth,Math.max(y,530),518,645);
   if(!skin||!l||!r)continue;
   for(const [start,end] of [[l[1]-1,skin[0]+5],[skin[1]-5,r[0]+1]])for(let x=start;x<=end;x++){
    if(x<395||x>=645)continue;
    for(let dy=0;dy<4;dy++)for(let dx=0;dx<4;dx++){
     const k=(((y-500)*4+dy)*1000+(x-395)*4+dx)*4;
     data[k]=244;data[k+1]=30;data[k+2]=62;data[k+3]=255;
    }
   }
  }
  await sharp(data,{raw:info}).png().toFile(path.join(__dirname,'clothing'+tag+'_addition_guide.png'));
  const prompt='This is a tiny garment-edge inpainting job. Input 1 is the exact original transparent clothing close-up to preserve. Input 2 shows RED PAINT MASKS marking the SPECIFIC MISSING FABRIC to add to BOTH collar tips. Input 3 is the fitting guide showing the fixed human neck. Replace EVERY red mask area in input 2 with believable matching IVORY BEIGE COLLAR FABRIC extending from the neighboring inner collar trim. The red areas are REQUIRED NEW OPAQUE FABRIC support: do not erase them to transparency, do not ignore or reduce their widths. The repaired upright INNER fabric edge must follow the INNER boundary of each red strip so the collar touches and slightly overlaps the neck edge in guide 3. Add the full marked cloth width, not just smoothing the old edge. Round the small upper corners gracefully and taper into the existing ivory edge below. Keep the rest of the large U-shaped center opening genuinely transparent and open to the top; absolutely no horizontal strip across the neck, no fabric band or bridge, no skin, person, face, head, neck, hair or background. Preserve the original OUTER collar silhouette, garment color, exact existing fabric pattern and framing of input1. Also clean ragged alpha specks along the inner collar into a smooth delicate antialiased painterly contour. Return only the two repaired cloth collar sides as native transparent RGBA, same landscape1000x520 crop framing without zooming, moving or rescaling the existing cloth. NO red remains, no text, markings, grid, background or shadow. Critical: add fabric into the full red support region on BOTH sides; earlier attempts only smoothed old edges and left side gaps.';
  fs.writeFileSync(path.join(__dirname,'clothing'+tag+'_prompt.json'),JSON.stringify({tool:'built-in image_gen',stage:'explicit_fabric_addition_mask',id:tag,roi:{left:395,top:500,width:250,height:130},scale:4,inputs:['source/clothing'+tag+'_target_roi.png','source/clothing'+tag+'_addition_guide.png','source/clothing'+tag+'_guide_roi.png'],prompt,status:'ready_for_native_edit',fitting_note:'Marked support intentionally extends beyond final needed support; final ROI selection limits neck overlap to 2 original px.'},null,2)+'\n');
 }
 console.log('CLOTHING04_07_ADDITION_GUIDES_READY');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
