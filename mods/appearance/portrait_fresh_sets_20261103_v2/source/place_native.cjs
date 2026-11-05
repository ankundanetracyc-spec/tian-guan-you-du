// Mechanical whole-canvas registration only. Never changes pigments or alpha masks.
const fs=require('node:fs/promises'),path=require('node:path'),sharp=require('sharp');
async function place(input,output,requestedScale,sourcePoint,targetPoint){
 const m=await sharp(input).metadata();if(m.width!==1024||m.height!==1536||m.channels!==4)throw Error('Expected full RGBA source');
 const w=Math.max(2,Math.round(1024*requestedScale/2)*2),h=w*3/2,s=w/1024;
 const tx=Math.round(targetPoint[0]-sourcePoint[0]*s),ty=Math.round(targetPoint[1]-sourcePoint[1]*s);
 const native=await sharp(input).resize(w,h,{kernel:'lanczos3'}).png().toBuffer();
 const {data}=await sharp(native).raw().toBuffer({resolveWithObject:true});let clipped=0;
 for(let y=0;y<h;y++)for(let x=0;x<w;x++)if((x+tx<0||x+tx>=1024||y+ty<0||y+ty>=1536)&&data[(y*w+x)*4+3]!==0)clipped++;
 if(clipped)throw Error('Whole-layer placement would clip visible alpha: '+clipped);
 const l=Math.max(0,-tx),t=Math.max(0,-ty),rw=Math.min(w-l,1024-Math.max(0,tx)),rh=Math.min(h-t,1536-Math.max(0,ty));
 const canvasInput=await sharp(native).extract({left:l,top:t,width:rw,height:rh}).png().toBuffer();
 await fs.mkdir(path.dirname(output),{recursive:true});
 await sharp({create:{width:1024,height:1536,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:canvasInput,left:Math.max(0,tx),top:Math.max(0,ty)}]).png().toFile(output);
 return {source:input,output,uniform_scale:s,translate:[tx,ty],source_point:sourcePoint,target_point:targetPoint,mapped_point:[sourcePoint[0]*s+tx,sourcePoint[1]*s+ty],visible_alpha_clipped:clipped,method:'whole-canvas uniform scaling and integer translation; source alpha preserved; no mask extraction, color editing, or local warping'};
}
module.exports={place};
