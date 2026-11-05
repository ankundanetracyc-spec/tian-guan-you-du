// Use the image tool's new color painting on the image tool's correct native nose support.
// The color edit added an unwanted skin haze; never copy that new alpha into the game.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),source=path.join(__dirname,'raw/nose01_natural.png'),color=path.join(__dirname,'raw/nose01_natural_warm.png'),out=path.join(pack,'male/nose/01.png');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
(async()=>{
 const skin=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const paint=await sharp(color).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 if(skin.info.width!==1024||skin.info.height!==1536||paint.info.width!==1024||paint.info.height!==1536)throw Error('Expected unchanged common canvas');
 const output=Buffer.from(skin.data),blend=.65;
 for(let k=0;k<output.length;k+=4){
  if(!output[k+3]){output[k]=output[k+1]=output[k+2]=0;continue;}
  for(let c=0;c<3;c++)output[k+c]=Math.round(skin.data[k+c]*(1-blend)+paint.data[k+c]*blend);
 }
 const registered=Buffer.alloc(output.length),shift={left:2,top:-3};
 for(let y=3;y<1536;y++)for(let x=0;x<1022;x++){
  const src=(y*1024+x)*4,dst=((y-3)*1024+x+2)*4;
  output.copy(registered,dst,src,src+4);
 }
 await sharp(registered,{raw:skin.info}).png().toFile(out);
 const measured=await sharp(out).ensureAlpha().raw().toBuffer();
 let alphaChanges=0;for(let k=3;k<measured.length;k+=4)if(measured[k]!==registered[k])alphaChanges++;
 if(alphaChanges)throw Error('Nose alpha changed');
 fs.writeFileSync(path.join(__dirname,'nose_final_registration.json'),JSON.stringify({tool:'built-in image_gen',shape_source:'source/raw/nose01_natural.png',color_source:'source/raw/nose01_natural_warm.png',output:'male/nose/01.png',canvas:[1024,1536],transform:{scale:1,...shift},color_edit_blend:blend,alpha_changed_pixels_after_whole_layer_translation:alphaChanges,nostril_centers_before_translation:[[502.03,454.97],[524.11,454.61]],nostril_centers_after_translation:[[504.03,451.97],[526.11,451.61]],shape_sha256:hash(source),color_sha256:hash(color),sha256:hash(out),operation:'RGB from generated warm color edit applied only to generated natural nose alpha; reject skin haze in color-edit alpha; whole-layer integer translation only, no local warp',full_three_noncollinear_2px_certification:false},null,2)+'\n');
 console.log('Final nose: native geometry and alpha preserved; generated warm pigment integrated.');
})().catch(e=>{console.error(e);process.exitCode=1;});
