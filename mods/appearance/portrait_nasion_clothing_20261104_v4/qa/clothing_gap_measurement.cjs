// Read-only collar/neck registration measurements. No active pixel is changed.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const project=path.resolve(__dirname,'../../../..'),qa=__dirname;
const active=path.join(project,'mods/appearance/ink_three_variants_20261004');
const alpha=async f=>sharp(f).ensureAlpha().raw().toBuffer();
function rowBounds(data,y,threshold,x0=350,x1=685){let a=-1,b=-1;for(let x=x0;x<x1;x++)if(data[(y*1024+x)*4+3]>=threshold){if(a<0)a=x;b=x;}return a<0?null:[a,b];}
function sideCloth(data,y,threshold,side){return rowBounds(data,y,threshold,side?518:350,side?685:517);}
(async()=>{
  const currentFile=path.join(active,'male/face_shape/01.png');
  const current=await alpha(currentFile);
  const previous=await alpha(path.join(qa,'clothing_previous_face_v2.png'));
  const original=await alpha(path.join(project,'mods/appearance/new_ink_hair_20261003_v1/male/face_shape.png'));
  const report={threshold:40,pixel_ranges_inclusive:true,rows_y510_to600:[],garments:[],old_documented_anchor_points:{left:[441,550],right:[594,550]},note:'A gap here means the requested rear collar does not meet the neck side, even when that gap remains connected to the external background.'};
  for(let y=510;y<=600;y++)report.rows_y510_to600.push({y,current_face:rowBounds(current,y,40),previous_face_v2:rowBounds(previous,y,40),original_ink_face:rowBounds(original,y,40)});
  for(let id=1;id<=7;id++){
    const file=path.join(active,`male/clothing/0${id}.png`),cloth=await alpha(file),rows=[];
    for(let y=510;y<=600;y++){
      const face=rowBounds(current,y,40),old=rowBounds(previous,y,40),left=sideCloth(cloth,y,40,0),right=sideCloth(cloth,y,40,1);
      const gaps=face?{left:left?Math.max(0,face[0]-left[1]-1):null,right:right?Math.max(0,right[0]-face[1]-1):null}:null;
      const oldGaps=old?{left:left?Math.max(0,old[0]-left[1]-1):null,right:right?Math.max(0,right[0]-old[1]-1):null}:null;
      rows.push({y,left_collar_outer_inner:left,right_collar_inner_outer:right,new_neck:face,gap_pixels:gaps,previous_face_gap_pixels:oldGaps});
    }
    report.garments.push({index:id,rows,max_new_gap_left:Math.max(...rows.map(r=>r.gap_pixels?.left??0)),max_new_gap_right:Math.max(...rows.map(r=>r.gap_pixels?.right??0)),last_gap_y_left:Math.max(...rows.filter(r=>r.gap_pixels?.left>0).map(r=>r.y),-1),last_gap_y_right:Math.max(...rows.filter(r=>r.gap_pixels?.right>0).map(r=>r.y),-1)});
    const bg=await sharp({create:{width:1024,height:1536,channels:4,background:'#202a30'}}).composite([{input:currentFile},{input:file}]).png().toBuffer();
    let marks='';
    for(const r of rows){if(r.gap_pixels?.left>0)marks+=`<rect x="${r.left_collar_outer_inner[1]+1}" y="${r.y}" width="${r.gap_pixels.left}" height="1" fill="#e45d5d"/>`;if(r.gap_pixels?.right>0)marks+=`<rect x="${r.new_neck[1]+1}" y="${r.y}" width="${r.gap_pixels.right}" height="1" fill="#e45d5d"/>`;}
    const annotated=await sharp(bg).composite([{input:Buffer.from(`<svg width="1024" height="1536">${marks}</svg>`)}]).png().toBuffer();
    await sharp(annotated).extract({left:395,top:500,width:250,height:130}).resize(1000,520,{kernel:'nearest'}).png().toFile(path.join(qa,`clothing_gap_0${id}_marked.png`));
  }
  fs.writeFileSync(path.join(qa,'clothing_gap_measurement.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify(report.rows_y510_to600.filter(r=>r.y%10===0)));
  for(const g of report.garments)console.log(JSON.stringify({index:g.index,max_left:g.max_new_gap_left,max_right:g.max_new_gap_right,last_left:g.last_gap_y_left,last_right:g.last_gap_y_right,rows:g.rows.filter(r=>[525,530,535,540,545,550,555,560,565,570].includes(r.y))}));
})().catch(e=>{console.error(e);process.exitCode=1;});
