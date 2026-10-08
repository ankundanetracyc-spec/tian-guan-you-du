const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const root=path.resolve(__dirname,'..');
const out=path.join(root,'mods/appearance/head_contour_reference_20261001/review');
fs.mkdirSync(out,{recursive:true});
const packs={male:['腰上男修拆件演示_v2','green_master_single_set'],female:['腰上女修拆件演示_v1','green_reference_single_set']};
async function main(){
 const full=[],bald=[],heads=[];
 for(const [sex,[name,preview]] of Object.entries(packs)){
  const pack=path.join(root,'mods/appearance',name);
  const render=path.join(pack,'preview',preview,`godot_composite_${sex}.png`);
  const bare=path.join(pack,'preview',preview,`godot_no_hair_${sex}.png`);
  const bg={r:203,g:200,b:189,alpha:1};
  const colored=await sharp(render).flatten({background:bg}).png().toBuffer();
  full.push(await sharp(colored).resize(600,900).png().toBuffer());
  bald.push(await sharp(bare).flatten({background:bg}).extract({left:330,top:160,width:364,height:445}).resize(450,550).png().toBuffer());
  heads.push(await sharp(colored).extract({left:300,top:105,width:424,height:540}).resize(424,540).png().toBuffer());
  const manifestPath=path.join(pack,'test_assets_green_reference_v1/split_manifest.json');
  const manifest=JSON.parse(fs.readFileSync(manifestPath));
  manifest.current_revision='traced_head_v2';manifest.latest_head_and_parts='test_sources/traced_head_v2/placement.json';
  manifest.face=sex==='female'?'original independent female skin reshaped to user red trace':'new independently generated male bald skin, fitted to supplied front anatomy';
  manifest.symmetry='one clear original feature side mirrored, with eye-side hair removed; head and neck bilaterally matched';
  fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');
 }
 for(const [name,width,height,tiles] of [['game_pair.png',1200,900,full],['bald_pair.png',900,550,bald],['game_heads.png',848,540,heads]])
  await sharp({create:{width,height,channels:4,background:'#cbc8bd'}}).composite(tiles.map((input,i)=>({input,left:i*width/2,top:0}))).png().toFile(path.join(out,name));
 console.log(out);
}
main().catch(e=>{console.error(e);process.exitCode=1});
