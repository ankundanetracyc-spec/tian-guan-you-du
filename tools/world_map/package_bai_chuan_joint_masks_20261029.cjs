const fs=require('node:fs'),path=require('node:path'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),dir=root+'/docs/美术规范/百川同画节点与悬停蒙版_20261029';
const shapes={
 bc_place_001:[[575,523],[582,519],[583,514],[587,514],[588,520],[603,520],[604,512],[596,508],[596,503],[604,502],[618,489],[621,488],[622,492],[670,491],[674,488],[676,493],[683,500],[692,502],[695,506],[690,509],[682,512],[683,520],[698,520],[699,514],[702,514],[704,520],[709,523],[709,552],[576,552]],
 bc_place_015:[[537,609],[542,574],[552,574],[553,579],[558,579],[559,574],[570,574],[571,579],[577,579],[579,574],[589,574],[591,570],[589,567],[587,564],[595,560],[600,553],[604,542],[604,534],[609,534],[609,538],[655,538],[655,534],[660,534],[661,542],[664,550],[668,556],[674,560],[672,564],[666,567],[666,574],[677,574],[679,580],[686,580],[686,575],[696,575],[697,580],[704,580],[704,575],[714,575],[722,609],[727,612],[727,615],[539,615]]
};
async function main(){
 const plan=JSON.parse(fs.readFileSync(dir+'/局部规划.json','utf8')),manifest=[];
 for(const site of plan){
  const polygon=shapes[site.id],xs=polygon.map(p=>p[0]),ys=polygon.map(p=>p[1]);
  const x=Math.min(...xs),y=Math.min(...ys),w=Math.max(...xs)-x+1,h=Math.max(...ys)-y+1;
  const svg=Buffer.from(`<svg width="${w}" height="${h}"><polygon points="${polygon.map(p=>`${p[0]-x},${p[1]-y}`).join(' ')}" fill="white"/></svg>`);
  const scene=dir+'/'+site.id+'_同画.png',file=site.id+'_建筑蒙版.png';
  const meta=await sharp(scene).metadata();
  if(meta.width!==1254||meta.height!==1254)throw Error('Unexpected scene dimensions');
  await sharp(scene).extract({left:x,top:y,width:w,height:h}).ensureAlpha().composite([{input:svg,blend:'dest-in'}]).png().toFile(dir+'/'+file);
  const sample=await sharp(scene).extract({left:x,top:y,width:w,height:h}).ensureAlpha().raw().toBuffer();
  const mask=await sharp(dir+'/'+file).raw().toBuffer();
  let opaque=0,different=0,transparent=0;
  for(let i=0;i<mask.length;i+=4){if(mask[i+3]===255){opaque++;for(let c=0;c<3;c++)if(mask[i+c]!==sample[i+c])different++;}if(mask[i+3]===0)transparent++;}
  if(!opaque||!transparent||different)throw Error(site.id+' extraction mismatch');
  manifest.push({...site,scene:site.id+'_同画.png',mask:file,scenePixels:[1254,1254],maskRect:[x,y,w,h],polygon,visibleFoot:[Math.round((x+x+w)/2),y+h-1],artOffsetFromRequested:[Math.round(x+w/2-627),y+h-1-627],opaquePixels:opaque,transparentPixels:transparent,opaquePixelDifferences:different,source:'built_in_imagegen',status:'two_site_proof_not_whole_map'});
 }
 fs.writeFileSync(dir+'/蒙版与像素验收.json',JSON.stringify({sites:manifest,pixelMatch:true,normalOverlayVisible:false,wholeMapReplaced:false},null,2));
 fs.writeFileSync(dir+'/sites.js','window.JOINT_SITES='+JSON.stringify(manifest)+';\n');
 console.log(JSON.stringify(manifest.map(s=>({id:s.id,maskRect:s.maskRect,opaque:s.opaquePixels,differences:s.opaquePixelDifferences,visualOffset:s.artOffsetFromRequested}))));
}
main().catch(e=>{console.error(e);process.exitCode=1});
