// Technical candidate assembly only: no eye warping, pixel painting, or installation.
const fs=require('fs');const path=require('path');const crypto=require('crypto');const sharp=require('sharp');
const P=path.resolve(__dirname,'..');
const OLD=path.join(P,'reference/current_male/eyes/04.png');
const RAW=path.join(P,'raw/eyes_balance_v6.png');
const OUT=path.join(P,'candidate/new_male_eyes04.png');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
(async()=>{
 if(sha(OLD)!=='fd3f04b779e0849bbe9c9cf46fcfa2b5bd6b8fcb8839b2aac5c1d26819acba89')throw Error('Frozen original04 baseline changed');
 const old=await sharp(OLD).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const raw=await sharp(RAW).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const softOldAlpha=await sharp(old.data,{raw:{width:1024,height:1536,channels:4}}).extractChannel(3).blur(.65).raw().toBuffer();
 if(old.info.width!==1024||old.info.height!==1536)throw Error('Canvas mismatch');
 const nativePoints=[[540,438],[1275,440]],targetPoints=[[462.695,365.417],[567.799,364.916]];
 const dx=nativePoints[1][0]-nativePoints[0][0],dy=nativePoints[1][1]-nativePoints[0][1],ex=targetPoints[1][0]-targetPoints[0][0],ey=targetPoints[1][1]-targetPoints[0][1],den=dx*dx+dy*dy;
 const aa=(ex*dx+ey*dy)/den,bb=(ey*dx-ex*dy)/den;
 const tx=targetPoints[0][0]-aa*nativePoints[0][0]+bb*nativePoints[0][1],ty=targetPoints[0][1]-bb*nativePoints[0][0]-aa*nativePoints[0][1];
 const matrix=[[aa,-bb,tx],[bb,aa,ty]],scale=Math.hypot(aa,bb),ss=Math.ceil(1/scale);
 let removedNoise=0,preservedLeft=0;for(let k=0;k<raw.data.length;k+=4)if(raw.data[k+3]<=3){if(raw.data[k+3])removedNoise++;raw.data.fill(0,k,k+4);}
 const result=Buffer.alloc(1024*1536*4);
 function sample(x,y){let ri=0,gi=0,bi=0,al=0;const xi=Math.floor(x),yi=Math.floor(y),fx=x-xi,fy=y-yi;for(let j=0;j<2;j++)for(let i=0;i<2;i++){let xx=xi+i,yy=yi+j;if(xx<0||yy<0||xx>=raw.info.width||yy>=raw.info.height)continue;const w=(i?fx:1-fx)*(j?fy:1-fy),k=(yy*raw.info.width+xx)*4,a=raw.data[k+3]/255;al+=w*a;ri+=w*a*raw.data[k];gi+=w*a*raw.data[k+1];bi+=w*a*raw.data[k+2];}return[ri,gi,bi,al];}
 for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
  const k=(y*1024+x)*4;
  // The old pair is empty throughout the interocular joining interval.
  if(x>=510&&x<525&&old.data[k+3])throw Error('Joining interval is not empty');
  if(x<515){old.data.copy(result,k,k,k+4);if(old.data[k+3])preservedLeft++;}
  else if(x<620&&y>338&&y<395){
   let r=0,g=0,b=0,a=0;for(let j=0;j<ss;j++)for(let i=0;i<ss;i++){
    const ox=x+(i+.5)/ss-.5-tx,oy=y+(j+.5)/ss-.5-ty,d=aa*aa+bb*bb;
    const p=sample((aa*ox+bb*oy)/d,(-bb*ox+aa*oy)/d);r+=p[0];g+=p[1];b+=p[2];a+=p[3];
   }
   if(a>0){result[k]=Math.round(r/a);result[k+1]=Math.round(g/a);result[k+2]=Math.round(b/a);result[k+3]=Math.round(a/(ss*ss)*255);}
  }
 }
 await sharp(result,{raw:{width:1024,height:1536,channels:4}}).png().toFile(path.join(P,'candidate/new_male_eyes04_before_matte_v6.png'));
 let matteReducedPixels=0,alphaReduction=0;
 for(let y=0;y<1536;y++)for(let x=515;x<1024;x++){
  const p=y*1024+x,k=p*4,previous=result[k+3],cap=Math.max(old.data[k+3],Math.round(softOldAlpha[p]*.85));
  result[k+3]=Math.min(previous,cap);if(result[k+3]<previous){matteReducedPixels++;alphaReduction+=previous-result[k+3];}
  if(!result[k+3])result.fill(0,k,k+4);
 }
 await sharp(result,{raw:{width:1024,height:1536,channels:4}}).png().toFile(OUT);
 for(const [theme,bg] of [['light','#e8e1d6'],['dark','#202b31']]){
  const flat=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:OUT}]).png().toBuffer();
  await sharp(flat).extract({left:400,top:335,width:225,height:60}).resize(1350,360,{kernel:'nearest'}).png().toFile(path.join(P,`qa/candidate_isolated_${theme}_6x.png`));
 }
 const report={schema:'male-eye-balance-correction-import/v1',status:'candidate_for_visual_review_not_installed',mode:'built-in image_gen edit plus technical RGBA assembly',input:{old:OLD,old_sha256:sha(OLD),native:RAW,native_sha256:sha(RAW)},output:{path:OUT,sha256:sha(OUT),width:1024,height:1536,runtime_offset:[0,0]},geometry:{native_to_current:matrix,native_pupil_centers:nativePoints,reference_current_pupil_centers:targetPoints,native_pupil_reading_uncertainty:'approximately3 native pixels =0.43 output pixels',scale,resampling:`single premultiplied Alpha area-supersampled similarity (${ss}x${ss} subpixel samples)`,whole_pair_transform_only:true,local_warp:false,mirror_clone:false,right_upper_lid:'Intentionally newly painted lower to balance left; old upper lid is not a rigid registration target'},assembly:{left:'Current original left RGBA retained, pending isolated baked-hair Alpha review',right:'New built-in painting; no old source RGB restoration',join_x:515,join_empty_alpha_interval:[510,525],original_left_nonzero_pixels:preservedLeft,native_alpha_1_to_3_removed_pixels:removedNoise},art_intent:'User-requested natural eye-balance correction, not pixel-faithful extraction of the unequal old source',known_differences:['New right pupil/highlight texture and upper lid paint replace baked hair and the unequal old opening','Left current eye drawing retained'],active_files_edited:false};
 report.assembly.left='Current original left RGBA retained unchanged; independent16x audit found its small outer-upper marks attached to fold/lash, so no speculative Alpha cut';
 report.assembly.right_alpha_matte={method:'min(new Alpha, max(original04 Alpha,0.85*GaussianBlur(original04 Alpha,sigma0.65)))',reason:'Remove new pointed tail, extra inner extension and skin support; existing Alpha is a matte upper bound only, never a source of RGB or new visible pixels',reduced_pixels:matteReducedPixels,total_alpha_reduction:alphaReduction,can_increase_native_alpha:false};
 const darkKernel=([x0,y0,x1,y1])=>{let sx=0,sy=0,sw=0;for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++){const k=(y*raw.info.width+x)*4,w=Math.max(0,80-(raw.data[k]+raw.data[k+1]+raw.data[k+2])/3)*raw.data[k+3]/255;sx+=x*w;sy+=y*w;sw+=w;}return[sx/sw,sy/sw];};
 report.geometry.native_dark_kernel_verification_centers=[[500,420,580,465],[1235,423,1315,468]].map(darkKernel);
 report.geometry.native_dark_kernel_measurement='Reuse independent audit ROI/method on final native: meanRGB; max(80-gray,0)*Alpha; L ROI x500..579 y420..464, R x1235..1314 y423..467';
 report.geometry.native_dark_kernel_position_errors_px=report.geometry.native_dark_kernel_verification_centers.map(([x,y],i)=>Math.hypot(aa*x-bb*y+tx-targetPoints[i][0],bb*x+aa*y+ty-targetPoints[i][1]));
 report.geometry.native_dark_kernel_max_pupil_error_px=Math.max(...report.geometry.native_dark_kernel_position_errors_px);
 report.known_differences.push('Right fold, lash/outer contour and iris highlight are newly painted; no source-pixel-equality claim');
 fs.writeFileSync(path.join(P,'source/eye_balance_import.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report.output));
})();
