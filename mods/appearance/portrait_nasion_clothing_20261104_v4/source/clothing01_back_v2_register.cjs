// One source-native similarity transform, using measured top-rim pixels.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),src=path.join(__dirname,'clothing01_back_v2_raw.png'),out=path.join(__dirname,'clothing01_back_v2_band_registered.png');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
function fit(s,t){const mean=a=>a.reduce((v,p)=>[v[0]+p[0]/a.length,v[1]+p[1]/a.length],[0,0]),sm=mean(s),tm=mean(t);let den=0,re=0,im=0;for(let i=0;i<s.length;i++){const x=s[i][0]-sm[0],y=s[i][1]-sm[1],u=t[i][0]-tm[0],v=t[i][1]-tm[1];den+=x*x+y*y;re+=x*u+y*v;im+=x*v-y*u;}const a=re/den,b=im/den,tx=tm[0]-a*sm[0]+b*sm[1],ty=tm[1]-b*sm[0]-a*sm[1];const errors=s.map((p,i)=>Math.hypot(a*p[0]-b*p[1]+tx-t[i][0],b*p[0]+a*p[1]+ty-t[i][1]));return{a,b,tx,ty,errors};}
(async()=>{
 const sourceControls=[[300,208],[850,106],[1400,203]],targetControls=[[433,528],[516,514],[600,528]];
 const transform=fit(sourceControls,targetControls);if(Math.max(...transform.errors)>2)throw Error('Measured rim controls do not fit one transform');
 const {data,info}=await sharp(src).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 for(const [x,y]of sourceControls)if(data[(y*info.width+x)*4+3]<230)throw Error('Source control is not a real core-alpha rim pixel');
 const output=Buffer.alloc(1024*1536*4),{a,b,tx,ty}=transform,det=a*a+b*b;
 for(let y=490;y<650;y++)for(let x=360;x<675;x++){
  const u=(a*(x-tx)+b*(y-ty))/det,v=(-b*(x-tx)+a*(y-ty))/det,x0=Math.floor(u),y0=Math.floor(v),fx=u-x0,fy=v-y0;
  if(x0<0||x0+1>=info.width||y0<0||y0+1>=info.height)continue;
  let alpha=0,color=[0,0,0];for(const [dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){const k=((y0+dy)*info.width+x0+dx)*4,aa=data[k+3]/255*w;alpha+=aa;for(let c=0;c<3;c++)color[c]+=data[k+c]*aa;}
  if(alpha<=0)continue;const k=(y*1024+x)*4;for(let c=0;c<3;c++)output[k+c]=Math.round(color[c]/alpha);output[k+3]=Math.round(alpha*255);
 }
 fs.mkdirSync(path.dirname(out),{recursive:true});await sharp(output,{raw:{width:1024,height:1536,channels:4}}).png().toFile(out);
 const report={part:'clothing_back_band',index:1,source:'source/clothing01_back_v2_raw.png',source_sha256:hash(src),source_canvas:[info.width,info.height],source_controls:sourceControls,target_controls:targetControls,source_control_method:'Actual first alpha>=230 supported pixel at measured native columns 300,850,1400; do not infer source points from target.',transform:{a,b,tx,ty,uniform_scale:Math.sqrt(det),rotation_degrees:Math.atan2(b,a)*180/Math.PI},control_residuals_px:transform.errors,maximum_residual_px:Math.max(...transform.errors),canvas:[1024,1536],output:'source/clothing01_back_v2_band_registered.png',output_sha256:hash(out),alpha_policy:'Native opacity preserved through one premultiplied global similarity resample. Continuous center is retained; face occludes it at runtime.',local_warp:false};
 fs.writeFileSync(path.join(__dirname,'clothing01_back_v2_registration.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1;});
