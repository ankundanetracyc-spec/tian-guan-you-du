const fs=require('fs'),path=require('path'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..');
const project=path.resolve(pack,'../../..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
const roi={left:395,top:500,width:250,height:130};
const names={4:'玉虚杂役常服',5:'玉虚外门常服',6:'玉虚内门常服',7:'玉虚真传常服'};
async function main(){
 for(const id of [4,5,6,7]){
  const tag=String(id).padStart(2,'0');
  const original=path.join(active,'male/clothing',tag+'.png');
  const face=path.join(active,'male/face_shape/01.png');
  const guide=await sharp({create:{width:1024,height:1536,channels:4,background:'#e7e0d4'}}).composite([{input:face},{input:original}]).png().toBuffer();
  await sharp(original).extract(roi).resize(1000,520,{kernel:'lanczos3'}).png().toFile(path.join(__dirname,'clothing'+tag+'_target_roi.png'));
  await sharp(guide).extract(roi).resize(1000,520,{kernel:'lanczos3'}).png().toFile(path.join(__dirname,'clothing'+tag+'_guide_roi.png'));
  fs.writeFileSync(path.join(__dirname,'clothing'+tag+'_guide_full.png'),guide);
  const prompt=`Use case: precise-object-edit. Production game clothing-layer local collar repair, male ${names[id]}. FIRST image is the EDIT TARGET, a 4x enlarged 250x130-pixel crop of the existing clothing only, on transparent background. SECOND image is a placement guide containing the fixed neck behind the same collar: use it ONLY to understand the neck contour, NEVER copy its skin or background into the output. Output ONLY the edited standalone clothing crop, genuine transparency, landscape1000x520 composition, same framing and original exact scale. The collar must connect naturally to the neck side: extend the two existing narrow rear-collar tips inward by roughly 4 to 8 ORIGINAL pixels (16 to32 pixels in this enlarged crop), replacing ragged free edges with an elegant continuous fine fabric contour. Preserve identical fabric colors, weave and embroidery. At original y530 (120 in crop), the left INNER fabric edge should meet x442 global (188 in crop) and the right INNER fabric edge x589 global (776 in crop), overlap the guide neck by one or two original pixels only. Fill the tiny side gap between collar and neck; do not move the entire collar or scale the garment. The central U/V-shaped neck opening remains completely transparent, no fabric bridge or horizontal neck-spanning band, no neck/face/skin/hair. Do not invent another layer of collar and do not double the old rough edge. Keep garment construction, seam orientation and design; smooth only the ragged collar tips and finish the missing join. Preserve everything below original y620 (480 in this crop); all changes must be local to the collar tips above that line, with gentle transitions into existing fabric. Natural hand-painted fine ink/watercolor textile, not vector or plasticky. No text or guide markings.`;
  fs.writeFileSync(path.join(__dirname,'clothing'+tag+'_prompt.json'),JSON.stringify({tool:'built-in image_gen',id:tag,garment:names[id],roi,scale:4,inputs:['source/clothing'+tag+'_target_roi.png','source/clothing'+tag+'_guide_roi.png'],prompt,status:'prepared_waiting_for_style01_method_validation'},null,2)+'\n');
 }
 console.log('CLOTHING04_07_GUIDES_PREPARED');
}
main().catch(error=>{console.error(error);process.exitCode=1});
