const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const crypto=require('crypto');
const ROOT=__dirname, A=path.join(ROOT,'assets');
fs.mkdirSync(A,{recursive:true});
const V4=path.resolve(ROOT,'../../textures/characters/ruina_ink_modular_candidate_20261115/revision_v4');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
async function geometry(p){
 const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let x0=info.width,y0=info.height,x1=-1,y1=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>16){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
 let footL=info.width,footR=0;
 for(let y=Math.max(y0,y1-24);y<=y1;y++)for(let x=x0;x<=x1;x++)if(data[(y*info.width+x)*4+3]>80){footL=Math.min(footL,x);footR=Math.max(footR,x);}
 return {width:info.width,height:info.height,bbox:[x0,y0,x1+1,y1+1],foot:[(footL+footR)/2,y1+1],visibleHeight:y1-y0+1};
}
(async()=>{
 const manifest={kind:'standalone_choreographed_visual_preview',production_runtime_changed:false,rule_source:'docs/plans/战斗与构筑/灵台选行战斗规则_20261111.md',character_reference:'textures/characters/ruina_ink_modular_candidate_20261115/revision_v4',generation_mode:'built-in image_gen',actors:{},effects:{},source_hashes:{}};
 const bundle={images:{},meta:{actors:{},effects:{}}};
 async function register(key,p){bundle.images[key]='data:image/png;base64,'+fs.readFileSync(p).toString('base64');}
 await sharp(path.join(ROOT,'source/background.png')).resize(1600,900).png().toFile(path.join(A,'background.png'));
 await register('background',path.join(A,'background.png'));
 for(const who of ['inner','outer']){
  const atlas=path.join(ROOT,`source/poses_${who}.png`),m=await sharp(atlas).metadata();
  const cw=Math.floor(m.width/2),ch=Math.floor(m.height/2);
  manifest.actors[who]={};bundle.meta.actors[who]={};
  for(const [i,pose] of ['move','strike','guard','hurt'].entries()){
   const p=path.join(A,`${who}_${pose}.png`);
   await sharp(atlas).extract({left:(i%2)*cw,top:Math.floor(i/2)*ch,width:cw,height:ch}).png().toFile(p);
   const g=await geometry(p);manifest.actors[who][pose]={file:`assets/${who}_${pose}.png`,...g,sha256:sha(p)};bundle.meta.actors[who][pose]=g;await register(`${who}_${pose}`,p);
  }
  const source=path.join(V4,who==='inner'?'assembled_inner_woodpin_woodpin.png':'assembled_outer_sidepart_sidepart.png');
  const g=await geometry(source),b=g.bbox;
  const png=await sharp(source).extract({left:b[0],top:b[1],width:b[2]-b[0],height:b[3]-b[1]}).resize({height:560}).png().toBuffer();
  const mm=await sharp(png).metadata();
  const p=path.join(A,`${who}_idle.png`);
  await sharp({create:{width:cw,height:ch,channels:4,background:'#00000000'}}).composite([{input:png,left:Math.round((cw-mm.width)/2),top:ch-32-mm.height}]).png().toFile(p);
  const geom=await geometry(p);manifest.actors[who].idle={file:`assets/${who}_idle.png`,...geom,source_sha256:sha(source),sha256:sha(p)};bundle.meta.actors[who].idle=geom;await register(`${who}_idle`,p);
 }
 const atlas=path.join(ROOT,'source/vfx_atlas.png'),m=await sharp(atlas).metadata(),cw=Math.floor(m.width/2),ch=Math.floor(m.height/2);
 for(const [i,name] of ['sweep','deflect','impact','wake'].entries()){
  const p=path.join(A,`fx_${name}.png`);
  await sharp(atlas).extract({left:i%2*cw,top:Math.floor(i/2)*ch,width:cw,height:ch}).png().toFile(p);
  const g=await geometry(p);manifest.effects[name]={file:`assets/fx_${name}.png`,...g,sha256:sha(p)};bundle.meta.effects[name]=g;await register(`fx_${name}`,p);
 }
 for(const name of ['background','vfx_atlas','poses_inner','poses_outer'])manifest.source_hashes[name]=sha(path.join(ROOT,`source/${name}.png`));
 fs.writeFileSync(path.join(ROOT,'asset_manifest.json'),JSON.stringify(manifest,null,2));
 fs.writeFileSync(path.join(ROOT,'assets.js'),'window.BATTLE_ASSETS='+JSON.stringify(bundle)+';');
 console.log(JSON.stringify({actors:manifest.actors,effects:Object.keys(manifest.effects),bundleBytes:fs.statSync(path.join(ROOT,'assets.js')).size},null,2));
})();
