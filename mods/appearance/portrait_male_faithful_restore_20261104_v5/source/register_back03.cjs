// Source-supported contour points, one whole-layer similarity, area-integrated premultiplied reduction.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const {render}=require('./resample_rgba.cjs');
const pack=path.resolve(__dirname,'..'),project=path.resolve(pack,'../../..');
const raw=path.join(__dirname,'back03_raw.png'),out=path.join(pack,'male/hair_back/03.png');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
function fit(source,target){const mean=points=>points.reduce((r,p)=>[r[0]+p[0]/points.length,r[1]+p[1]/points.length],[0,0]),s=mean(source),t=mean(target);let d=0,re=0,im=0;for(let i=0;i<source.length;i++){const x=source[i][0]-s[0],y=source[i][1]-s[1],u=target[i][0]-t[0],v=target[i][1]-t[1];d+=x*x+y*y;re+=x*u+y*v;im+=x*v-y*u;}const a=re/d,b=im/d,tx=t[0]-a*s[0]+b*s[1],ty=t[1]-b*s[0]-a*s[1];return{a,b,tx,ty,residuals:source.map((p,i)=>Math.hypot(a*p[0]-b*p[1]+tx-target[i][0],b*p[0]+a*p[1]+ty-target[i][1]))};}
function rowBounds(data,width,y,lo,hi){let first=-1,last=-1;for(let x=lo;x<=hi;x++)if(data[(y*width+x)*4+3]>=230){if(first<0)first=x;last=x;}return first<0?null:[first,last];}
async function main(){
 const{data,info}=await sharp(raw).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let top;
 for(let y=0;y<400&&!top;y++){const span=rowBounds(data,info.width,y,250,800);if(span)top={y,span};}
 const sides=rowBounds(data,info.width,345,200,850);
 if(!top||!sides)throw Error('Missing actual source upper-head contour.');
 // Inspect the high left-of-center crest strand and lateral main-cap edge.
 // The first supported row is discontinuous: its bbox midpoint is transparent.
 const source=[[498,151],[sides[0],345],[sides[1],345]];
 const target=[[503,170],[365,350],[670,350]];
 for(const[x,y]of source)if(data[(y*info.width+x)*4+3]<230)throw Error('Measured control is not an actual core-alpha pixel.');
 const t=fit(source,target),max=Math.max(...t.residuals);
 if(max>2)throw Error('Source design scaffold cannot fit one similarity within2px: '+JSON.stringify(t));
 fs.mkdirSync(path.dirname(out),{recursive:true});await render(raw,out,t);
 const record={part:'hair_back',index:3,new_native_generation:true,source:'source/back03_raw.png',source_sha256:hash(raw),source_canvas:[info.width,info.height],source_points:source,target_points:target,point_names:['left_of_center_high_crest_supported_pixel','left_main_rear_skull_contour_y345','right_main_rear_skull_contour_y345'],source_measurement:'Manually inspected actual alpha>=230 crest pixel(498,151) and lateral main-cap contour row345. The first top row has separated strand clusters, so its bbox midpoint was not used. These are actual painted source pixels, not coordinates copied from prompt.',initial_automatic_scaffold_attempt:{source_points:[[508,151],[353,350],[683,350]],maximum_residual_px:2.9431349908929727,status:'rejected before export; control landmarks refined by inspecting actual crest and main cap rim'},target_scaffold:'Shared head underpainting design behind the new master03 front-hair envelope: rear top(503,170), sides(365,350)/(670,350). Hidden rear-scalp points are design scaffolds rather than claimed visible anatomy in the original.',transform:{a:t.a,b:t.b,tx:t.tx,ty:t.ty,scale:Math.hypot(t.a,t.b),rotation_degrees:Math.atan2(t.b,t.a)*180/Math.PI},residuals:t.residuals,maximum_residual_px:max,antialiasing:'source/resample_rgba.cjs: whole-layer area-integrated premultiplied reduction',output:'male/hair_back/03.png',output_sha256:hash(out),canvas:[1024,1536],local_warp:false,reused_v3_v4_shape:false,visual_status:'First v5 rear03 candidate, awaiting parent review and front-hair pairing; the three design controls alone are not full anatomical or visual certification.'};
 fs.writeFileSync(path.join(__dirname,'back03_registration.json'),JSON.stringify(record,null,2)+'\n');
 const ref=path.join(project,'mods/appearance/portrait_rebuild_20261103/source/male_final/reference_03.png');
 for(const[bg,tone]of[['#e8e1d6','light'],['#222a2b','dark']]){
  const original=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:ref,left:220,top:50}]).png().toBuffer();
  const panels=[original,path.join(__dirname,'master03_registered.png'),out];
  const tiles=await Promise.all(panels.map(async(input,i)=>({input:await sharp(input).flatten({background:bg}).resize(512,768).png().toBuffer(),left:i*512,top:0})));
  await sharp({create:{width:1536,height:768,channels:4,background:bg}}).composite(tiles).png().toFile(path.join(pack,'qa/back03_original_master_layer_'+tone+'.png'));
  await sharp(out).flatten({background:bg}).png().toFile(path.join(pack,'qa/back03_registered_'+tone+'.png'));
 }
 console.log(JSON.stringify({source,target,maximum_residual_px:max,transform:record.transform,output:record.output}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
