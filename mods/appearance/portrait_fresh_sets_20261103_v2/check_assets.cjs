const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const sharp=require('sharp');
const ROOT=__dirname, ACTIVE=path.resolve(ROOT,'../ink_three_variants_20261004');
const PARTS=['eyebrows','eyes','nose','mouth','hair_front','hair_back'];
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const median=a=>a.length?a.sort((x,y)=>x-y)[Math.floor(a.length/2)]:null;
async function main(){
 const before=JSON.parse(fs.readFileSync(path.join(ROOT,'qa/active_before.json'),'utf8'));
 const oldHashes=new Set(Object.entries(before).filter(([p])=>p.endsWith('.png')).map(([,h])=>h));
 const records=[],seen=new Set(),failures=[],warnings=[];
 for(const sex of ['male','female']){
  for(const part of [...PARTS,'face_shape','clothing','face_accessory']){
   for(let j=1;j<=(PARTS.includes(part)?3:1);j++){
    const rel=PARTS.includes(part)?`${sex}/${part}/${String(j).padStart(2,'0')}.png`:`${sex}/${part}.png`;
    const file=path.join(ROOT,rel);
    if(!fs.existsSync(file)){failures.push('Missing '+rel);continue;}
    const m=await sharp(file).metadata();
    if(m.width!==1024||m.height!==1536||m.channels!==4||!m.hasAlpha||m.format!=='png')failures.push('Dimensions/format '+rel);
    const digest=hash(file),isNew=PARTS.includes(part);
    if(isNew){if(seen.has(digest))failures.push('Duplicate new asset '+rel);if(oldHashes.has(digest))failures.push('Reused old asset '+rel);seen.add(digest);}
    else{const index=sex==='female'&&part==='face_shape'?'04':'01';if(digest!==hash(path.join(ACTIVE,sex,part,index+'.png')))failures.push('Fixed source changed '+rel);}
    const {data,info}=await sharp(file).raw().toBuffer({resolveWithObject:true});
    let visible=0,partial=0,strong=0,eyeOverlap=0;let box=[1024,1536,-1,-1];const rgb=[[],[],[]];
    for(let i=0;i<data.length;i+=4){const a=data[i+3],p=i/4,x=p%info.width,y=Math.floor(p/info.width);
     if(a>0){visible++;if(a<255)partial++;}
     if(a>180){strong++;box[0]=Math.min(box[0],x);box[1]=Math.min(box[1],y);box[2]=Math.max(box[2],x);box[3]=Math.max(box[3],y);for(let c=0;c<3;c++)rgb[c].push(data[i+c]);}
     const ey=sex==='female'?379:375,ex=sex==='female'?574:569;
     if(part==='hair_front'&&a>24&&Math.abs(y-ey)<=6&&(Math.abs(x-464)<=6||Math.abs(x-ex)<=6))eyeOverlap++;
    }
    const corners=[[0,0],[1023,0],[0,1535],[1023,1535]].map(([x,y])=>data[(y*1024+x)*4+3]);
    if(isNew&&corners.some(a=>a!==0))failures.push('Opaque canvas corner '+rel);
    if(isNew&&visible===0)failures.push('Empty new asset '+rel);
    if(eyeOverlap)failures.push('Hair covers fixed pupil region '+rel+': '+eyeOverlap);
    const skin=[[],[],[]];if(part==='face_shape')for(let y=300;y<325;y++)for(let x=490;x<540;x++){const i=(y*1024+x)*4;if(data[i+3]>180)for(let c=0;c<3;c++)skin[c].push(data[i+c]);}
    const axis=sex==='male'?517:519,center=[];if(part==='hair_front')for(let y=50;y<340;y++){let a=0;for(let x=axis-2;x<=axis+2;x++)a=Math.max(a,data[(y*1024+x)*4+3]);if(a>180)center.push(y);}
    records.push({gender:sex,part,variant:isNew?j:null,file:rel,new_asset:isNew,width:m.width,height:m.height,rgba:m.channels===4,sha256:digest,not_old_source:isNew?true:null,visible_pixels:visible,partial_alpha_pixels:partial,alpha180_pixels:strong,alpha180_bbox:strong?box:null,alpha180_rgb_median:rgb.map(median),forehead_median:part==='face_shape'?skin.map(median):null,pupil_region_hair_overlap:part==='hair_front'?eyeOverlap:null,central_hairline_scan_y:part==='hair_front'&&center.length?[Math.min(...center),Math.max(...center)]:null});
   }
  }
 }
 const changed=Object.entries(before).filter(([p,h])=>!fs.existsSync(path.join(ACTIVE,p))||hash(path.join(ACTIVE,p))!==h).map(([p])=>p);
 if(changed.length)failures.push('Active sources changed: '+changed.join(', '));
 const result={pass:failures.length===0,new_assets:records.filter(r=>r.new_asset).length,fixed_assets:records.filter(r=>!r.new_asset).length,active_files_unchanged:changed.length===0?Object.keys(before).length:null,failures,warnings,records,limits:['Source alpha and geometry checks are not artistic approval.','Native generation provenance is recorded separately; uniqueness alone does not prove semantic layer purity.','Control-point residual certification requires actual anatomical landmarks and is not inferred from alpha bounds.']};
 fs.writeFileSync(path.join(ROOT,'qa/assets.json'),JSON.stringify(result,null,2));
 console.log(JSON.stringify({pass:result.pass,new_assets:result.new_assets,fixed_assets:result.fixed_assets,failures,hair:records.filter(r=>r.part.startsWith('hair_')).map(r=>({file:r.file,median:r.alpha180_rgb_median,hairline:r.central_hairline_scan_y}))},null,2));
 if(failures.length)process.exitCode=1;
}
main().catch(e=>{console.error(e);process.exit(1)});
