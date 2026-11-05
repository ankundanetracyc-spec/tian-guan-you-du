// Transfer native material RGB onto the exact pure-band master alpha, not final rear01.
// Only edge colors are continued from adjacent native cloth when AI alpha drifted.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),masterFile=path.join(__dirname,'clothing01_back_v2_band_registered.png');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const requested=process.argv.slice(2),ids=requested.length?requested:['04','05','06','07'];
async function main(){
 const {data:master,info}=await sharp(masterFile).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 if(info.width!==1024||info.height!==1536)throw Error('Pure-band master canvas changed.');
 for(const id of ids){
  if(!['02','03','04','05','06','07'].includes(id))throw Error('Only assigned palette indices02..07 are authorized.');
  const rawFile=path.join(__dirname,'clothing'+id+'_back_v2_palette_raw.png');
  const {data:raw,info:rawInfo}=await sharp(rawFile).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(rawInfo.width!==1024||rawInfo.height!==1536)throw Error('Native material source must retain the1024x1536 canvas; no aspect warp or implicit fit.');
  const output=Buffer.from(master);let direct=0,continued=0,maxDistance=0,lowAlphaFringeContinuations=0;
  for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
   const k=(y*1024+x)*4;if(!master[k+3])continue;
   let source=k;
   if(raw[k+3]>=128)direct++;
   else{
    let best=null;
    // Native master has a few extremely faint alpha specks beyond its visible rim.
    // Preserve those alpha bytes while extending only neighboring AI pigment.
    const maxRadius=master[k+3]<40?64:8;
    for(let radius=1;radius<=maxRadius&&!best;radius++){
     for(let dy=-radius;dy<=radius;dy++)for(let dx=-radius;dx<=radius;dx++){
      if(Math.max(Math.abs(dx),Math.abs(dy))!==radius)continue;
      const u=x+dx,v=y+dy;if(u<0||u>=1024||v<0||v>=1536)continue;
      const q=(v*1024+u)*4;if(raw[q+3]<128)continue;
      const distance=Math.hypot(dx,dy);
      if(!best||distance<best.distance)best={index:q,distance};
     }
    }
    if(!best)throw Error('AI material has no adjacent supported edge color near '+id+' '+x+','+y);
    source=best.index;maxDistance=Math.max(maxDistance,best.distance);continued++;
    if(master[k+3]<40&&best.distance>8)lowAlphaFringeContinuations++;
   }
   for(let c=0;c<3;c++)output[k+c]=raw[source+c];
  }
  let alphaChanges=0;for(let k=3;k<master.length;k+=4)if(output[k]!==master[k])alphaChanges++;
  if(alphaChanges)throw Error('The shared master alpha must remain exactly identical.');
  const outputFile=path.join(__dirname,'clothing'+id+'_back_v2_band_registered.png');
  await sharp(output,{raw:info}).png().toFile(outputFile);
  const record={index:Number(id),part:'pure_back_band',tool:'built-in image_gen material variant',master_pure_band:'source/clothing01_back_v2_band_registered.png',master_pure_band_sha256:hash(masterFile),native_material_source:'source/clothing'+id+'_back_v2_palette_raw.png',native_material_sha256:hash(rawFile),material_reference:'source/clothing'+id+'_palette_reference.png',material_reference_sha256:hash(path.join(__dirname,'clothing'+id+'_palette_reference.png')),prompt:'source/clothing'+id+'_back_v2_palette_refine_prompt.json',output:'source/clothing'+id+'_back_v2_band_registered.png',output_sha256:hash(outputFile),canvas:[1024,1536],output_alpha_changed_pixels:alphaChanges,direct_native_rgb_pixels:direct,edge_native_rgb_continued_pixels:continued,maximum_edge_rgb_sample_distance_px:maxDistance,very_faint_alpha_fringe_continuations_beyond8px:lowAlphaFringeContinuations,edge_color_policy:'For master-supported edge pixels whose AI source had alpha<128, extend neighboring native cloth RGB only. Visible support uses search radius<=8; a few preexisting master alpha<40 specks may use radius<=64 to avoid transparent background RGB. All master alpha bytes remain unchanged; no pixels move and no fabric texture is synthesized.',color_calibration:'None; RGB comes directly from selected native AI material or its edge-color continuation.',shared_geometry:'source/clothing01_back_v2_registration.json',shared_geometry_maximum_residual_px:0.819902413467133,independent_variant_three_point_fit:false,local_warp:false,final_rear01_alpha_not_used:true};
  fs.writeFileSync(path.join(__dirname,'clothing'+id+'_back_v2_palette_registration.json'),JSON.stringify(record,null,2)+'\n');
  console.log(JSON.stringify({id,alphaChanges,direct,continued,maxDistance,output:record.output}));
 }
}
main().catch(error=>{console.error(error);process.exitCode=1;});
