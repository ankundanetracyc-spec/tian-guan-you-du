const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const {render}=require('./resample_rgba.cjs');
const pack=path.resolve(__dirname,'..'),project=path.resolve(pack,'../../..');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function fit(S,T){const mn=p=>p.reduce((a,p)=>[a[0]+p[0]/S.length,a[1]+p[1]/S.length],[0,0]);const s=mn(S),t=mn(T);let d=0,re=0,im=0;for(let i=0;i<S.length;i++){const x=S[i][0]-s[0],y=S[i][1]-s[1],u=T[i][0]-t[0],v=T[i][1]-t[1];d+=x*x+y*y;re+=x*u+y*v;im+=x*v-y*u;}const a=re/d,b=im/d,tx=t[0]-a*s[0]+b*s[1],ty=t[1]-b*s[0]-a*s[1];return{a,b,tx,ty,residuals:S.map((p,i)=>Math.hypot(a*p[0]-b*p[1]+tx-T[i][0],b*p[0]+a*p[1]+ty-T[i][1]))};}
function stats(data,W,H){let box=[W,H,-1,-1],border=0;for(let y=0;y<H;y++)for(let x=0;x<W;x++){const A=data[(y*W+x)*4+3];if((x===0||y===0||x===W-1||y===H-1)&&A)border++;if(A>=40){box[0]=Math.min(box[0],x);box[1]=Math.min(box[1],y);box[2]=Math.max(box[2],x);box[3]=Math.max(box[3],y);}}return{alpha40_bbox:box,nonzero_border_pixel_count:border};}
async function main(){for(const c of [
 {id:'01',source:[[515,145],[357,355],[669,355]],target:[[517,133],[365,341],[670,341]],names:['upper_left_bun_crest_supported_pixel','left_main_posterior_cap_rim_y355','right_main_posterior_cap_rim_y355'],placement:'The rear cap scaffold is intentionally nine pixels above the loose/high-bun cap scaffold, to keep the native compact bun and visible wood pin nearer the original reference. The posterior cap is hidden by the separately drawn front layer. These are design targets, not independently measured visible anatomy.'},
 {id:'02',source:[[531,80],[306,437],[721,437]],target:[[531,88],[365,350],[670,350]],names:['upper_right_bun_crest_supported_pixel','left_main_posterior_cap_rim_y437','right_main_posterior_cap_rim_y437'],placement:'The upper-right crest follows the right-hand high-bun lobe; it is not the center axis of the whole head. After native wrap-height revision, actual cap rim and crest were remeasured; the generator also changed small contour details, so old controls were not reused. Lateral posterior cap targets follow the new master envelope behind front hair. These are hidden-underpainting design targets, not independently measured visible anatomy.'}
 ]){
 const raw=path.join(__dirname,'back'+c.id+'_raw.png'),out=path.join(pack,'male/hair_back',c.id+'.png');
 const{data,info}=await sharp(raw).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 if(info.width!==1024||info.height!==1536)throw Error('Unexpected canvas '+c.id);
 const alphas=c.source.map(([x,y])=>data[(y*info.width+x)*4+3]);if(alphas.some(A=>A<230))throw Error('Unsupported source control '+c.id);
 for(const[x,y]of[[0,0],[100,200],[900,1000],[300,1500]])if(data[(y*info.width+x)*4+3])throw Error('Nontransparent background '+c.id);
 const t=fit(c.source,c.target);fs.mkdirSync(path.dirname(out),{recursive:true});await render(raw,out,t);
 const output=await sharp(out).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const rec={part:'hair_back',index:Number(c.id),native_new_generation:true,reused_rejected_v3_v4_shape:false,source:'source/back'+c.id+'_raw.png',source_sha256:hash(raw),source_canvas:[info.width,info.height],source_points:c.source,source_point_alpha:alphas,source_point_names:c.names,target_points:c.target,source_measurement:'Manually inspected actual native painted support and main posterior cap rim, alpha>=230; source coordinates were measured from the delivered PNG, not copied from a generation prompt.',target_scaffold:c.placement,transform:{a:t.a,b:t.b,tx:t.tx,ty:t.ty,scale:Math.hypot(t.a,t.b),rotation_degrees:Math.atan2(t.b,t.a)*180/Math.PI},residuals:t.residuals,maximum_residual_px:Math.max(...t.residuals),residual_scope:'Fit of three posterior-underpainting design controls only. This does not certify visible anatomical alignment or visual acceptance.',local_warp:false,colour_edit_by_script:false,antialiasing:'source/resample_rgba.cjs whole-layer area-integrated premultiplied reduction',source_stats:stats(data,info.width,info.height),output_stats:stats(output.data,1024,1536),output:'male/hair_back/'+c.id+'.png',output_sha256:hash(out),canvas:[1024,1536],transparent_background:'Native alpha retained through premultiplied antialiasing; no threshold extraction or background alpha removal.',visual_status:'Candidate for independent front-layer pairing and original-reference comparison; awaiting parent visual review.'};
 fs.writeFileSync(path.join(__dirname,'back'+c.id+'_registration.json'),JSON.stringify(rec,null,2)+'\n');
 const ref=path.join(project,'mods/appearance/portrait_rebuild_20261103/source/male_final/reference_'+c.id+'.png');
 const x=c.id==='01'?204:215,y=c.id==='01'?50:49;
 for(const[bg,tone]of[['#e8e1d6','light'],['#222a2b','dark']]){
  const original=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:ref,left:x,top:y}]).png().toBuffer();
  const tiles=await Promise.all([original,path.join(__dirname,'master03_registered.png'),out].map(async(input,i)=>({input:await sharp(input).flatten({background:bg}).resize(512,768).png().toBuffer(),left:i*512,top:0})));
  await sharp({create:{width:1536,height:768,channels:4,background:bg}}).composite(tiles).png().toFile(path.join(pack,'qa/back'+c.id+'_original_master_layer_'+tone+'.png'));
  await sharp(out).flatten({background:bg}).png().toFile(path.join(pack,'qa/back'+c.id+'_registered_'+tone+'.png'));
 }
 console.log(JSON.stringify({id:c.id,source:c.source,target:c.target,maximum_residual_px:rec.maximum_residual_px,transform:rec.transform,output_stats:rec.output_stats}));
}}
main().catch(e=>{console.error(e);process.exitCode=1;});
