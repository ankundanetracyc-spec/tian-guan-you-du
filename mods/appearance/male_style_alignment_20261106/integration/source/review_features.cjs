const fs=require('fs'),path=require('path'),sharp=require('sharp');
const R=path.resolve(__dirname,'..'),Q=path.join(R,'qa/features'),W=1024,H=1536;
const parts=['nose','eyes','eyebrows','mouth'];
const boxes={eyebrows:[425,328,185,35],eyes:[425,351,185,34],nose:[480,341,77,119],mouth:[471,461,91,38]};
async function background(kind){if(kind!=='checker')return sharp({create:{width:W,height:H,channels:4,background:kind==='dark'?'#253136':'#e8e1d6'}}).png().toBuffer();const data=Buffer.alloc(W*H*4);for(let y=0;y<H;y++)for(let x=0;x<W;x++){const c=((Math.floor(x/12)+Math.floor(y/12))%2)?175:217,k=(y*W+x)*4;data[k]=data[k+1]=data[k+2]=c;data[k+3]=255;}return sharp(data,{raw:{width:W,height:H,channels:4}}).png().toBuffer();}
(async()=>{
 fs.mkdirSync(Q,{recursive:true});const source=path.join(R,'source/approved_male.png'),head=path.join(R,'male/face_shape/01.png'),layers=[head,...parts.map(p=>path.join(R,'male',p,'01.png'))];if(!fs.existsSync(head))throw Error('New blank head not ready');
 for(const kind of ['light','dark','checker']){
  const bg=await background(kind),composite=await sharp(bg).composite(layers.map(input=>({input}))).png().toBuffer();await sharp(composite).toFile(path.join(Q,'head_with_features_'+kind+'.png'));
  const allParts=['hair_back','clothing_back','face_shape','clothing','nose','eyes','eyebrows','mouth','hair_front'].map(p=>path.join(R,'male',p,'01.png'));
  if(allParts.every(p=>fs.existsSync(p))){const full=await sharp(bg).composite(allParts.map(input=>({input}))).png().toBuffer();await sharp(full).toFile(path.join(Q,'full_actual_'+kind+'.png'));const srcHead=await sharp(source).extract({left:325,top:230,width:380,height:340}).resize(760,680,{kernel:'nearest'}).png().toBuffer(),actualHead=await sharp(full).extract({left:325,top:230,width:380,height:340}).resize(760,680,{kernel:'nearest'}).png().toBuffer();await sharp({create:{width:1520,height:680,channels:4,background:'#e8e1d6'}}).composite([{input:srcHead,left:0,top:0},{input:actualHead,left:760,top:0}]).png().toFile(path.join(Q,'source_left_actual_right_'+kind+'_2x.png'));}
  for(const part of ['eyebrows','eyes','nose','mouth']){
   const [left,top,width,height]=boxes[part],scale=6;
   const original=await sharp(source).extract({left,top,width,height}).resize(width*scale,height*scale,{kernel:'nearest'}).png().toBuffer();
   const actual=await sharp(composite).extract({left,top,width,height}).resize(width*scale,height*scale,{kernel:'nearest'}).png().toBuffer();
   await sharp({create:{width:width*scale,height:height*scale*2,channels:4,background:kind==='dark'?'#253136':'#e8e1d6'}}).composite([{input:original,left:0,top:0},{input:actual,left:0,top:height*scale}]).png().toFile(path.join(Q,part+'_source_top_actual_bottom_'+kind+'_6x.png'));
   const isolatedFull=await sharp(bg).composite([{input:path.join(R,'male',part,'01.png')}]).png().toBuffer();const isolated=await sharp(isolatedFull).extract({left,top,width,height}).resize(width*scale,height*scale,{kernel:'nearest'}).png().toBuffer();await sharp(isolated).toFile(path.join(Q,part+'_isolated_'+kind+'_6x.png'));
  }
 }
 const master=await sharp(source).ensureAlpha().raw().toBuffer(),eye=await sharp(path.join(R,'male/eyes/01.png')).ensureAlpha().raw().toBuffer(),records=[];
 for(const y of [365,367,370,372])for(const [side,x0,x1]of [['left',450,479],['right',554,583]]){let s=[],a=[];for(let x=x0;x<x1;x++){let k=(y*W+x)*4,lum=master[k]*.2126+master[k+1]*.7152+master[k+2]*.0722;if(lum<160)s.push(x);if(eye[k+3]>=128&&lum<160)a.push(x);}const span=v=>v.length?[v[0],v.at(-1),v.at(-1)-v[0]+1]:null;records.push({y,side,source_dark_iris_span:span(s),final_alpha128_covers_same_span:span(a)});}
 const report=JSON.parse(fs.readFileSync(path.join(R,'source/feature_import.json')));report.qa={new_head_source:'male/face_shape/01.png',backgrounds:['light','dark','checker'],same_crop_same_scale:true,comparison_order:'source top / actual new-head composite bottom',iris_scanlines:records};fs.writeFileSync(path.join(R,'source/feature_import.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(records));
})().catch(e=>{console.error(e);process.exitCode=1});
