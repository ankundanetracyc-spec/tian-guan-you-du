// Register one coherent new anterior collar, then join its lower edge to the frozen robe.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),src=path.join(__dirname,'clothing01_front_v2_raw.png'),out=path.join(pack,'male/clothing/01.png'),oldFile=path.join(pack,'installation/backup_before/male/clothing/01.png');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
function fit(s,t){const mean=a=>a.reduce((v,p)=>[v[0]+p[0]/a.length,v[1]+p[1]/a.length],[0,0]),sm=mean(s),tm=mean(t);let den=0,re=0,im=0;for(let i=0;i<s.length;i++){const x=s[i][0]-sm[0],y=s[i][1]-sm[1],u=t[i][0]-tm[0],v=t[i][1]-tm[1];den+=x*x+y*y;re+=x*u+y*v;im+=x*v-y*u;}const a=re/den,b=im/den,tx=tm[0]-a*sm[0]+b*sm[1],ty=tm[1]-b*sm[0]-a*sm[1];const errors=s.map((p,i)=>Math.hypot(a*p[0]-b*p[1]+tx-t[i][0],b*p[0]+a*p[1]+ty-t[i][1]));return{a,b,tx,ty,errors};}
(async()=>{
 const sourceControls=[[373,240],[1304,240],[509,550],[1209,550]],targetControls=[[442,550],[589,550],[465,600],[578,600]];
 const transform=fit(sourceControls,targetControls);if(Math.max(...transform.errors)>2)throw Error('Actual source V controls do not fit one similarity');
 const {data,info}=await sharp(src).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 for(const [x,y]of sourceControls)if(data[(y*info.width+x)*4+3]<230)throw Error('A source control does not lie on native alpha230 artwork');
 const old=await sharp(oldFile).ensureAlpha().raw().toBuffer(),registered=Buffer.alloc(old.length),output=Buffer.from(old),{a,b,tx,ty}=transform,det=a*a+b*b;
 for(let y=490;y<650;y++)for(let x=350;x<685;x++){
  const u=(a*(x-tx)+b*(y-ty))/det,v=(-b*(x-tx)+a*(y-ty))/det,x0=Math.floor(u),y0=Math.floor(v),fx=u-x0,fy=v-y0;
  if(x0<0||x0+1>=info.width||y0<0||y0+1>=info.height)continue;
  let alpha=0,color=[0,0,0];for(const [dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){const k=((y0+dy)*info.width+x0+dx)*4,aa=data[k+3]/255*w;alpha+=aa;for(let c=0;c<3;c++)color[c]+=data[k+c]*aa;}
  if(alpha<=0)continue;const k=(y*1024+x)*4;for(let c=0;c<3;c++)registered[k+c]=Math.round(color[c]/alpha);registered[k+3]=Math.round(alpha*255);
 }
 // This deliberately REMOVES old posterior tips and their old dark strokes.
 // New art fully replaces the collar above600; a broad lower fabric transition ends at620.
 for(let y=490;y<620;y++)for(let x=370;x<675;x++){
  const k=(y*1024+x)*4,wx=smooth((x-370)/14)*smooth((675-x)/14),wy=y<600?1:smooth((620-y)/20),w=wx*wy;
  const aa=registered[k+3]/255,oa=old[k+3]/255,alpha=aa*w+oa*(1-w);
  for(let c=0;c<3;c++)output[k+c]=alpha?Math.round((registered[k+c]*aa*w+old[k+c]*oa*(1-w))/alpha):0;
  output[k+3]=Math.round(alpha*255);
 }
 let changed=0,bbox=[1024,1536,-1,-1],bodyChanges=0,oldTipRemnants=0;
 for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){const k=(y*1024+x)*4;if([0,1,2,3].some(c=>output[k+c]!==old[k+c])){changed++;bbox=[Math.min(bbox[0],x),Math.min(bbox[1],y),Math.max(bbox[2],x),Math.max(bbox[3],y)];if(y>=620)bodyChanges++;}if(y<540&&output[k+3]>=40)oldTipRemnants++;}
 if(bodyChanges||oldTipRemnants)throw Error('Frozen body changed or rear tips retained in anterior layer');
 fs.mkdirSync(path.dirname(out),{recursive:true});await sharp(output,{raw:{width:1024,height:1536,channels:4}}).png().toFile(out);
 const report={part:'clothing_front',index:1,source:'source/clothing01_front_v2_raw.png',source_sha256:hash(src),source_canvas:[info.width,info.height],source_controls:sourceControls,target_controls:targetControls,source_control_method:'Native alpha230 inner contour: upper inner corner turns at row240 and two lower V-contour cross-section pixels at row550, measured directly before fitting. No alpha bbox control.',transform:{a,b,tx,ty,uniform_scale:Math.sqrt(det),rotation_degrees:Math.atan2(b,a)*180/Math.PI},control_residuals_px:transform.errors,maximum_residual_px:Math.max(...transform.errors),canvas:[1024,1536],output:'male/clothing/01.png',output_sha256:hash(out),frozen_body_source:'installation/backup_before/male/clothing/01.png',frozen_body_sha256:hash(oldFile),body_frame:{scale:1,rotation_degrees:0,translation:[0,0]},changed_pixels:changed,changed_bbox_inclusive:bbox,y_ge620_changed_pixels:bodyChanges,front_y_lt540_visible_pixels:oldTipRemnants,material_transition:'Entire new collar above600; native new fabric blends into frozen original robe only from600 to620. No old posterior tip or deep posterior outline retained.',local_warp:false};
 fs.writeFileSync(path.join(__dirname,'clothing01_front_v2_registration.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
