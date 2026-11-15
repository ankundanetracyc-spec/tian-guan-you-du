const fs=require('fs'),path=require('path');
const pack='E:/stacklands/mods/appearance/portrait_male_reference_20261104_v1', src=path.join(pack,'source');
const generated=['exec-2c7711f5-8232-4ff8-a1ce-b02d40810840.png','exec-d27ee605-438c-4d6b-8388-de8dc3cd5bd6.png','exec-962a4e43-7640-485d-82df-4ab6909bb739.png'];
const specFile=path.join(src,'hair_registration.json'),spec=JSON.parse(fs.readFileSync(specFile,'utf8')),records=[];
fs.mkdirSync(path.join(src,'raw_hair_before_ear_revision'),{recursive:true});
for(let i=0;i<3;i++){
const id=String(i+1).padStart(2,'0'),raw=path.join(src,'raw_hair/hair_front',id+'.png'),file='E:/tmp/CodexData/generated_images/01a1a4be-7b8b-76e1-af95-b099fa3c88dc/'+generated[i];
fs.copyFileSync(raw,path.join(src,'raw_hair_before_ear_revision',id+'.png'));
fs.copyFileSync(file,raw);
const entry=spec.outputs.find(e=>e.key==='hair_front_'+id);
entry.before_ear_registration={...entry};entry.file=raw;entry.generated_original=file;entry.scale=1;entry.x=0;entry.y=0;entry.unchanged=true;
records.push({id,input:'source/ear_revision_inputs/'+id+'.png',generated_original:file,raw:'source/raw_hair/hair_front/'+id+'.png'});
}
for(const entry of spec.outputs.filter(e=>e.part==='hair_back')){entry.generated_original=entry.file;entry.file=path.join(src,'raw_hair',entry.part,entry.id+'.png');}
fs.writeFileSync(specFile,JSON.stringify(spec,null,2)+'\n');
fs.writeFileSync(path.join(src,'ear_revision_records.json'),JSON.stringify({tool:'built-in image_gen',prompt:"Use case: precise-object-edit. Edit this ONE existing transparent male front-hair sprite only. Preserve its 1024x1536 canvas and its current placement, warm charcoal grey-brown color, fine gongbi strands, hairline, crown silhouette, side part/central part and long front locks. Change ONLY the ear-height side hair on both sides: it must stream naturally downward as continuous temple hair, overlapping the ear zone without any specially carved indentation, little opening, curved ear-shaped contour, scallop or cutout intended to expose an ear. Let the hair mass flow on its own with its natural strand rhythm. Cover the small unnatural notches beside the temples with matching continuous flowing hair, and keep the outer side silhouette continuous into the lower side locks. Hair can cover the ears; do not draw ears or skin. Retain the empty face region in the center. Do not add a head, face, eyebrows, eyes, ears, neck, clothing, rear hair curtain, bun, hairpin or ornament. Retain crown/Baihui anterior-layer design. No resizing, no recentering, no top margin change. True transparent background, soft natural strand alpha at the edge, no black background or text. Preserve all other areas of the existing sprite.",records},null,2)+'\n');
