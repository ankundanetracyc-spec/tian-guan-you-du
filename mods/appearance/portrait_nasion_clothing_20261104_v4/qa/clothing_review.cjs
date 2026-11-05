// Read-only audit of active clothing PNGs and their composition with the new face.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const project=path.resolve(__dirname,'../../../..');
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
const qa=__dirname;
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const names=['01 碧岚云叶','02 玄青云竹','03 绛霞流云','04 杂役常服','05 外门常服','06 内门常服','07 真传常服'];
const face=path.join(active,'male/face_shape/01.png');
const blank=bg=>sharp({create:{width:1024,height:1536,channels:4,background:bg}});
const label=(text,w=320)=>Buffer.from(`<svg width="${w}" height="32"><rect width="100%" height="100%" fill="#d7d2c7"/><text x="10" y="23" font-size="18" font-family="Microsoft YaHei" fill="#202625">${text}</text></svg>`);
(async()=>{
  const report={read_only_active_assets:true,canvas:[1024,1536],hair_hidden:true,layer_order:['face_shape','clothing'],files:[]};
  for(const [bg,suffix] of [['#e7e0d4','light'],['#252b2e','dark']]){
    const full=[],neck=[];
    for(let id=1;id<=7;id++){
      const clothing=path.join(active,`male/clothing/${String(id).padStart(2,'0')}.png`);
      const raw=await blank(bg).composite([{input:clothing}]).png().toBuffer();
      const combined=await blank(bg).composite([{input:face},{input:clothing}]).png().toBuffer();
      for(const [row,input] of [[0,raw],[1,combined]]){
        full.push({input:await sharp(input).resize(320,480).png().toBuffer(),left:(id-1)*320,top:32+row*512});
        full.push({input:label(`${names[id-1]} ${row?'新脸颈+衣':'衣层单独'}`),left:(id-1)*320,top:row*512});
        neck.push({input:await sharp(input).extract({left:360,top:475,width:320,height:320}).png().toBuffer(),left:(id-1)*320,top:32+row*352});
        neck.push({input:label(`${names[id-1]} ${row?'新脸颈+衣':'衣层单独'}`),left:(id-1)*320,top:row*352});
      }
      if(suffix==='light'){
        const {data,info}=await sharp(clothing).ensureAlpha().raw().toBuffer({resolveWithObject:true});
        let visibleTop=1536,hiddenNonzeroRGB=0,visiblePixels=0;
        for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
          const k=(y*info.width+x)*4;
          if(data[k+3]>=40){visibleTop=Math.min(visibleTop,y);visiblePixels++;}
          if(!data[k+3]&&(data[k]||data[k+1]||data[k+2]))hiddenNonzeroRGB++;
        }
        report.files.push({index:id,name:names[id-1],file:path.relative(project,clothing).replaceAll('\\','/'),sha256:hash(clothing),visible_top_alpha40:visibleTop,visible_pixels_alpha40:visiblePixels,hidden_rgb_nonzero_pixels: hiddenNonzeroRGB,hidden_rgb_is_not_visible_contamination:true});
      }
    }
    await sharp({create:{width:2240,height:1024,channels:4,background:bg}}).composite(full).png().toFile(path.join(qa,`clothing_seven_full_${suffix}.png`));
    await sharp({create:{width:2240,height:704,channels:4,background:bg}}).composite(neck).png().toFile(path.join(qa,`clothing_seven_neck_${suffix}.png`));
  }
  const ff=path.join(active,'female/face_shape/04.png'),fc=path.join(active,'female/clothing/01.png');
  for(const [gender,skin,clothes] of [['male',face,path.join(active,'male/clothing/01.png')],['female',ff,fc]]){
    const composed=await blank('#e7e0d4').composite([{input:skin},{input:clothes}]).png().toBuffer();
    await sharp(composed).extract({left:350,top:475,width:340,height:340}).resize(680,680).png().toFile(path.join(qa,`clothing_${gender}_neck_reference.png`));
  }
  report.face_sha256=hash(face);
  fs.writeFileSync(path.join(qa,'clothing_review_sources.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify(report.files.map(({index,visible_top_alpha40})=>({index,visible_top_alpha40}))));
})().catch(e=>{console.error(e);process.exitCode=1;});
