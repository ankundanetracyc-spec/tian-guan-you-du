// Prepare material references/prompts only. Never calls image generation or writes final pairs.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),snapshot=path.join(pack,'installation/backup_before');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const median=a=>{a.sort((x,y)=>x-y);return a[Math.floor(a.length/2)]??null;};
async function main(){
 const config=JSON.parse(fs.readFileSync(path.join(snapshot,'appearance_parts.snapshot.json'),'utf8'));
 const clothes=config.components.find(entry=>entry.id==='male_clothes');
 const all=[];
 for(let id=2;id<=7;id++){
  const tag=String(id).padStart(2,'0'),original=path.join(snapshot,'male/clothing',tag+'.png');
  const reference=path.join(__dirname,'clothing'+tag+'_palette_reference.png');
  await sharp(original).extract({left:395,top:500,width:250,height:130}).resize(1000,520,{kernel:'lanczos3'}).png().toFile(reference);
  const {data}=await sharp(original).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const palettes=[];
  for(const [name,x0,y0,width,height]of[['left_inner_lining',424,550,12,20],['right_inner_lining',601,550,12,20],['outer_collar_fabric',403,575,8,8]]){
   const rgb=[[],[],[]];let count=0;
   for(let y=y0;y<y0+height;y++)for(let x=x0;x<x0+width;x++){
    const k=(y*1024+x)*4;if(data[k+3]<230)continue;
    count++;for(let c=0;c<3;c++)rgb[c].push(data[k+c]);
   }
   const value=rgb.map(median);
   palettes.push({name,source_roi:[x0,y0,width,height],alpha_minimum:230,sample_pixels:count,median_rgb:value,median_hex:value.every(c=>c!==null)?'#'+value.map(c=>c.toString(16).padStart(2,'0')).join(''):null,not_a_global_flat_fill:true});
  }
  const prompt='Use case: precise-object-edit. FIRST image is the production master REAR COLLAR on an exact1024x1536 transparent canvas. SECOND image is only the material/pigment reference from '+clothes.variant_names[id-1]+' existing garment. Make a COLOR AND FABRIC-MATERIAL VARIANT OF IMAGE1 ONLY. Preserve the first image exact canvas, occupied position, shape, folded rim, continuous filled hidden center, proportions, shadows, highlights directions, outer contour and opacity. Do not enlarge, reposition, rescale, recrop, rotate, sharpen, thicken, redraw geometry or cut a neck hole. Match image2 robe outer textile hue and its inner collar lining/piping hue and subtle woven surface texture. Ivory facing remains fabric, not skin; preserve realistic painterly fold shading rather than replacing with flat color. No redrawing of a full garment, no front lapels, shoulders, head, neck, face, skin or hair. The rear collar is one continuous solid fabric support behind an unchanged neck and must keep its entire center painted. Everything outside the precise existing cloth remains actually transparent. No background glow, text, label, guide marks, extra pattern, bright stripe or plastic sheen. The result will contribute RGB only within the fixed alpha of master1; keep palette aligned in the exact same common canvas.';
  const record={id:tag,name:clothes.variant_names[id-1],status:'prepared_waiting_for_master01_pair_selection_no_generation',tool:'built-in image_gen',material_reference:'source/clothing'+tag+'_palette_reference.png',material_snapshot:'installation/backup_before/male/clothing/'+tag+'.png',material_snapshot_sha256:hash(original),material_reference_sha256:hash(reference),palette_samples:palettes,master_geometry:'male/clothing_back/01.png once explicitly selected',master_registration:'source/clothing01_back_v2_registration.json',geometry_policy:'Share only explicitly selected master01 geometry/alpha and its existing measured similarity registration. Do not claim a new 3-point fit for a color variant. Freeze all alpha bytes and canvas coordinates to that master.',color_policy:'Use native AI color/material RGB; map RGB back to the unchanged registered master alpha only after examining native alignment. Never generate alpha or replace it with palette output.',front_split_policy:'Pending root/audit master01 pairing decision. Proposed original upper y<550 moves to rear, original y>=550 front/body remains identity. Not yet applied.',prompt};
  fs.writeFileSync(path.join(__dirname,'clothing'+tag+'_palette_edit_prepared.json'),JSON.stringify(record,null,2)+'\n');
  all.push(record);
 }
 fs.writeFileSync(path.join(pack,'qa/clothing02_07_palette_preparation.json'),JSON.stringify({status:'references_and_prompts_only',native_calls_made:0,final_candidates_written:0,records:all},null,2)+'\n');
 console.log(JSON.stringify(all.map(({id,name,palette_samples})=>({id,name,palette_samples})),null,2));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
