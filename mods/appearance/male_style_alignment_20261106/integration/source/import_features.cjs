// Deterministic import only: whole-pair similarity transform, source RGB selection,
// and alpha-only matte reduction. All painting originates in builtin image_gen.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),W=1024,H=1536,Q=path.join(R,'qa/features');
let frontOcclusion=null;
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const clamp=v=>Math.max(0,Math.min(1,v)),smooth=v=>{v=clamp(v);return v*v*(3-2*v)};
const defs={
 eyebrows:{native:'eyebrows_native_v3.png',preset:{a:.375,b:0,tx:325,ty:164,scale:.375,residuals:[]},controls:[],note:'Third native extraction supplies one paired natural-tapering mask. Actual approved dark brow pigment is recovered only where the generated alpha already supports the pixel; no rectangular opacity zone, no invented stroke, no change to transparent zero-alpha regions. Source-hair-occluded tips retain this same transformed native painting.'},
 eyes:{names:['left_pupil','right_pupil','left_inner_corner','right_inner_corner','right_outer_lash_tip','left_upper_lid_at_iris','left_lower_lid_at_iris','right_upper_lid_at_iris','right_lower_lid_at_iris'],source:[[447.82094436251305,365.42003013595377],[569.4110328954121,364.9736588331093],[478,377],[541,377],[609,363],[448,361],[448,377],[569,361],[569,377]],target:[[461.5689530030847,365.25258573761573],[566.4229137199434,365.08718933117797],[489,375],[542,375],[602,363],[462,361],[462,374],[566,361],[566,374]],note:'Actual painted pupil centroids plus manually read eye corners and upper/lower iris-level lid contours, about 1px reading uncertainty. Pair is transformed as one layer; no eye-local warp.'},
 nose:{names:['left_nostril_shadow','right_nostril_shadow','tip_highlight'],source:[[494.0104981453921,477.67019742483865],[529.9666577927014,477.5379544119144],[515,464]],target:[[504.7573311810601,443.9114877589453],[529.3398776134626,444.19428862825094],[516,434]],note:'Complete root and bridge retained with the same nose transform. Upper bridge matte is reduced only at the sides to avoid importing neighboring eyebrow pigment.'},
 mouth:{names:['left_corner','right_corner','cupid_bow'],source:[[469,640],[557,640],[512,633]],target:[[479,478],[555,478],[516,471]],note:'Natural lips retain approved source width and seam; one whole-layer similarity transform only.'}
};
async function read(p){const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Wrong full canvas: '+p);return data;}
async function save(data,p){fs.mkdirSync(path.dirname(p),{recursive:true});await sharp(data,{raw:{width:W,height:H,channels:4}}).png().toFile(p);}
function fit(d){if(d.preset)return d.preset;if(d.identity)return {a:1,b:0,tx:0,ty:0,scale:1,residuals:[]};let n=d.source.length,sx=0,sy=0,tx=0,ty=0;for(let i=0;i<n;i++){sx+=d.source[i][0]/n;sy+=d.source[i][1]/n;tx+=d.target[i][0]/n;ty+=d.target[i][1]/n;}let den=0,aa=0,bb=0;for(let i=0;i<n;i++){let x=d.source[i][0]-sx,y=d.source[i][1]-sy,u=d.target[i][0]-tx,v=d.target[i][1]-ty;den+=x*x+y*y;aa+=x*u+y*v;bb+=x*v-y*u;}const a=aa/den,b=bb/den,ox=tx-a*sx+b*sy,oy=ty-b*sx-a*sy;return{a,b,tx:ox,ty:oy,scale:Math.hypot(a,b),residuals:d.source.map(([x,y],i)=>Math.hypot(a*x-b*y+ox-d.target[i][0],b*x+a*y+oy-d.target[i][1]))};}
function sample(data,x,y){if(x<0||y<0||x>=W-1||y>=H-1)return[0,0,0,0];const x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,o=[0,0,0,0];for(const[dx,dy,w]of [[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){let k=((y0+dy)*W+x0+dx)*4,a=data[k+3]/255;o[3]+=w*a;for(let c=0;c<3;c++)o[c]+=data[k+c]*w*a;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o;}
function hidden(part,x,y){if(part!=='eyebrows'&&part!=='eyes')return false;return !!(frontOcclusion&&frontOcclusion[y*W+x])||(part==='eyebrows'&&(x<452||x>599))||(part==='eyes'&&(x<453||x>600));}
(async()=>{
 fs.mkdirSync(Q,{recursive:true});const masterPath=path.join(R,'source/approved_male.png'),master=await read(masterPath);if(sha(masterPath)!=='d97b79e3372df42db24b4dd5c3ec42f376d652616d9fa25d78a393b004da81a8')throw Error('Approved source differs');const records=[];
 const frontPath=path.join(R,'male/hair_front/01.png');let frontSha=null;if(fs.existsSync(frontPath)){frontSha=sha(frontPath);const front=await read(frontPath);frontOcclusion=new Uint8Array(W*H);for(let y=325;y<390;y++)for(let x=415;x<620;x++)if(front[(y*W+x)*4+3]>=64)for(let dy=-6;dy<=6;dy++)for(let dx=-6;dx<=6;dx++)frontOcclusion[(y+dy)*W+x+dx]=1;}
 for(const[part,d]of Object.entries(defs)){
  const rawRelative='raw/features/'+(d.native||part+'_native.png'),source=path.join(R,rawRelative),raw=await read(source),out=Buffer.alloc(W*H*4),t=fit(d),inv=t.a*t.a+t.b*t.b;if(t.residuals.length&&Math.max(...t.residuals)>2)throw Error(part+' exceeds 2px: '+t.residuals.join(','));
  const tipRaw=d.tip_native?await read(path.join(R,'raw/features',d.tip_native)):null;
  let preserved=0,generatedHidden=0,alphaReduced=0,sourceInkOpacityRecovered=0,recoveryBounds=[W,H,-1,-1];
  for(let y=0;y<H;y++)for(let x=0;x<W;x++){
   const dx=x-t.tx,dy=y-t.ty,s=sample(raw,(t.a*dx+t.b*dy)/inv,(-t.b*dx+t.a*dy)/inv),k=(y*W+x)*4;let a=s[3],tip=null;
   if(tipRaw){const tt=d.tip_transform;tip=sample(tipRaw,(x-tt.tx)/tt.scale,(y-tt.ty)/tt.scale);const blend=x<452?smooth((452-x)/4):smooth((x-599)/4);a=a*(1-blend)+tip[3]*blend;}
   if(a<=3)continue;
   if(part==='eyebrows'){
    // Remove only matte support; never add opaque rectangular support zones.
    if(!hidden(part,x,y)){const lum=master[k]*.2126+master[k+1]*.7152+master[k+2]*.0722;a*=smooth((210-lum)/45);const recovered=s[3]>=128?(lum<145?255:lum<175?(175-lum)/30*255:0):0;if(recovered>a+.5){a=recovered;sourceInkOpacityRecovered++;recoveryBounds=[Math.min(recoveryBounds[0],x),Math.min(recoveryBounds[1],y),Math.max(recoveryBounds[2],x+1),Math.max(recoveryBounds[3],y+1)];}}
   }
   if(part==='nose'&&y<390){const fade=smooth((y-345)/45),radius=18+9*fade;a*=smooth((radius-Math.abs(x-517))/8);}
   if(a<s[3]-.5)alphaReduced++;
   if(a<=3)continue;out[k+3]=Math.round(a);
   if(hidden(part,x,y)||(part==='eyebrows'&&s[3]<128)){for(let c=0;c<3;c++)out[k+c]=Math.round(tip?tip[c]:s[c]);generatedHidden++;}
   else{for(let c=0;c<3;c++)out[k+c]=master[k+c];preserved++;}
  }
  const dest=path.join(R,'male',part,'01.png');await save(out,dest);
  let core=0,wrong=0;for(let k=0;k<out.length;k+=4)if(out[k+3]>=230&&!hidden(part,(k/4)%W,Math.floor(k/(4*W)))){core++;for(let c=0;c<3;c++)if(out[k+c]!==master[k+c])wrong++;}if(wrong)throw Error('Approved core RGB drift');
  let sourceBrowInk=0,coveredBrowInk=0;if(part==='eyebrows')for(let y=333;y<358;y++)for(let x=425;x<610;x++){let k=(y*W+x)*4,l=master[k]*.2126+master[k+1]*.7152+master[k+2]*.0722;if(!hidden(part,x,y)&&l<145){sourceBrowInk++;if(out[k+3]>=128)coveredBrowInk++;}}
  records.push({part,file:'male/'+part+'/01.png',sha256:sha(dest),raw_source:rawRelative,raw_sha256:sha(source),tip_native:d.tip_native?('raw/features/'+d.tip_native):null,tip_transform:d.tip_transform||null,canvas:[W,H],transform:t,control_names:d.names||[],source_controls:d.source||[],target_controls:d.target||[],maximum_residual_px:t.residuals.length?Math.max(...t.residuals):null,approved_rgb_pixels:preserved,approved_opaque_core_pixels:core,approved_core_rgb_difference_count:wrong,generated_hair_occluded_tip_pixels:generatedHidden,alpha_reduced_pixels:alphaReduced,source_ink_opacity_recovered_pixels:sourceInkOpacityRecovered,source_ink_opacity_recovery_bbox:sourceInkOpacityRecovered?recoveryBounds:null,opacity_recovery_method:'Only approved ink pixels already inside transformed generated alpha>=128; never zero-alpha pixels or rectangular fills',source_unoccluded_dark_brow_pixels:sourceBrowInk,source_unoccluded_dark_brow_pixels_alpha128:coveredBrowInk,source_unoccluded_dark_brow_coverage:sourceBrowInk?coveredBrowInk/sourceBrowInk:null,note:d.note});
 }
 const report={schema:'male-style-feature-import/v1',mode:'builtin image_gen extraction plus deterministic whole-layer import',approved_source:'source/approved_male.png',approved_sha256:sha(masterPath),canvas:[W,H],runtime_offset:[0,0],local_geometry_warps:false,opaque_rectangular_alpha_fills:false,occluding_front_hair:{source:'male/hair_front/01.png',sha256:frontSha,usage:'Read-only current approved-source front alpha, dilated6px only as an occlusion guard; no hair RGB is ever copied into facial parts.'},records,visual_status:'Pending same-scale actual composite inspection; geometric controls and source core RGB are separate from visual fidelity.'};
 fs.writeFileSync(path.join(R,'source/feature_import.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
})().catch(e=>{console.error(e);process.exitCode=1});

