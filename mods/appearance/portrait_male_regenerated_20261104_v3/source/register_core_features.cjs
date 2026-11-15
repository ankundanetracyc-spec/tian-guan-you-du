const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const records=[];
async function data(file){return sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});}
function luma(d,k){return .2126*d[k]+.7152*d[k+1]+.0722*d[k+2];}
async function put(name,part,id,scale,left,top,landmarks){
 const raw=path.join(__dirname,'raw',name+'.png'),out=path.join(pack,'male',part,id+'.png');
 const meta=await sharp(raw).metadata();
 const w=Math.round(meta.width*scale),h=Math.round(meta.height*scale);
 const resized=await sharp(raw).resize(w,h).toBuffer();
 await sharp({create:{width:1024,height:1536,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:resized,left,top}]).png().toFile(out);
 const {data:bytes,info}=await data(out);
 for(let i=0;i<bytes.length;i+=4)if(bytes[i+3]===0)bytes[i]=bytes[i+1]=bytes[i+2]=0;
 await sharp(bytes,{raw:info}).png().toFile(out);
 records.push({part,id,raw:path.relative(pack,raw),output:path.relative(pack,out),raw_canvas:[meta.width,meta.height],scale,left,top,landmarks,raw_sha256:hash(raw),sha256:hash(out),alpha_policy:'native alpha retained through whole-layer uniform resize; hidden RGB cleared only at alpha0'});
}
async function eyes(name,id,targetDistance,targetMidX=517,targetY=376){
 const raw=path.join(__dirname,'raw',name+'.png'),{data:d,info}=await data(raw);
 const points=[];
 for(const [a,b]of [[250,430],[800,980]]){let sx=0,sy=0,sw=0;for(let y=600;y<650;y++)for(let x=a;x<b;x++){const k=(y*info.width+x)*4;const w=d[k+3]>180?Math.max(0,75-luma(d,k)):0;sx+=x*w;sy+=y*w;sw+=w;}if(!sw)throw Error('Missing pupil core '+name);points.push([sx/sw,sy/sw]);}
 const scale=targetDistance/(points[1][0]-points[0][0]);
 const left=Math.round(targetMidX-(points[0][0]+points[1][0])/2*scale),top=Math.round(targetY-(points[0][1]+points[1][1])/2*scale);
 await put(name,'eyes',id,scale,left,top,{source_pupil_pigment_centroids:points,target_pupil_distance:targetDistance,target_midpoint:[targetMidX,targetY]});
}
async function mouth(){
 const {data:d,info}=await data(path.join(__dirname,'raw','mouth01.png'));
 let x0=info.width,x1=-1,sx=0,sy=0,sw=0;
 for(let y=Math.round(info.height*.40);y<Math.round(info.height*.55);y++)for(let x=0;x<info.width;x++){const k=(y*info.width+x)*4,l=luma(d,k);if(d[k+3]<180||l>130)continue;const w=130-l;x0=Math.min(x0,x);x1=Math.max(x1,x);sx+=x*w;sy+=y*w;sw+=w;}
 if(!sw)throw Error('No lip seam');
 const scale=73/(x1-x0+1),left=Math.round(515.5-(x0+x1)/2*scale),top=Math.round(483.5-sy/sw*scale);
 await put('mouth01','mouth','01',scale,left,top,{source_seam:[x0,x1,sy/sw],target_seam_width:73,target_axis:[515.5,483.5]});
}
async function nose(){
 if(fs.existsSync(path.join(__dirname,'raw','nose01_natural_warm.png'))){
  require('child_process').execFileSync(process.execPath,[path.join(__dirname,'register_final_nose.cjs')],{stdio:'inherit'});
  records.push({part:'nose',id:'01',...JSON.parse(fs.readFileSync(path.join(__dirname,'nose_final_registration.json'),'utf8'))});
  return;
 }
 const name=fs.existsSync(path.join(__dirname,'raw','nose01_ala_final.png'))?'nose01_ala_final':fs.existsSync(path.join(__dirname,'raw','nose01_ala.png'))?'nose01_ala':fs.existsSync(path.join(__dirname,'raw','nose01_tip.png'))?'nose01_tip':fs.existsSync(path.join(__dirname,'raw','nose01_refined.png'))?'nose01_refined':'nose01';
 const {data:d,info}=await data(path.join(__dirname,'raw',name+'.png'));
 const points=[];
 const yrange=['nose01_tip','nose01_ala','nose01_ala_final'].includes(name)?[.52,.67]:[.70,.86];
 for(const [a,b]of [[400,600],[650,850]]){let sx=0,sy=0,sw=0;for(let y=Math.round(info.height*yrange[0]);y<Math.round(info.height*yrange[1]);y++)for(let x=a;x<b;x++){const k=(y*info.width+x)*4,l=luma(d,k);const w=d[k+3]>140?Math.max(0,150-l):0;sx+=x*w;sy+=y*w;sw+=w;}if(!sw)throw Error('No nostril '+name);points.push([sx/sw,sy/sw]);}
 const scale=22.1/(points[1][0]-points[0][0]);
 const left=Math.round(515-(points[0][0]+points[1][0])/2*scale),top=Math.round(451.5-(points[0][1]+points[1][1])/2*scale);
 await put(name,'nose','01',scale,left,top,{source_nostril_pigment_centroids:points,target_nostril_distance:22.1,target_midpoint:[515,451.5]});
}
(async()=>{await eyes('eyes01','01',101.5);await eyes('eyes02','02',101.7,515.5,376.5);await mouth();await nose();fs.writeFileSync(path.join(__dirname,'core_registration.json'),JSON.stringify({canvas:[1024,1536],method:'whole-layer uniform scale plus integer translation; no local feature warping',full_three_noncollinear_2px_certification:false,records},null,2)+'\n');console.log(records.map(r=>({part:r.part,id:r.id,scale:r.scale,left:r.left,top:r.top})));})().catch(e=>{console.error(e);process.exitCode=1;});
