const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),P=path.resolve(R,'../../../../../..'),A=path.join(P,'mods/appearance/ink_three_variants_20261004'),sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
function solve(matrix,v){const a=matrix.map((r,i)=>[...r,v[i]]);for(let j=0;j<3;j++){let p=j;for(let k=j+1;k<3;k++)if(Math.abs(a[k][j])>Math.abs(a[p][j]))p=k;[a[j],a[p]]=[a[p],a[j]];let q=a[j][j];for(let k=j;k<4;k++)a[j][k]/=q;for(let i=0;i<3;i++)if(i!==j){q=a[i][j];for(let k=j;k<4;k++)a[i][k]-=q*a[j][k]}}return a.map(r=>r[3])}
const smooth=v=>{v=Math.max(0,Math.min(1,v));return v*v*(3-2*v)};
function sample(d,x,y){if(x<0||y<0||x>=1023||y>=1535)return[0,0,0,0];let x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,o=[0,0,0,0];for(const[dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){let k=((y0+dy)*1024+x0+dx)*4,a=d[k+3]/255;o[3]+=a*w;for(let c=0;c<3;c++)o[c]+=d[k+c]*a*w;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o}
(async()=>{
 const nativeFront=path.join(R,'source/front_native.png'),nativeRear=path.join(R,'source/rear_native.png'),front=path.join(R,'male/clothing/01.png'),rear=path.join(R,'male/clothing_back/01.png');
 const srcPoints=[[429.5,553],[599,552],[526,688]],dstPoints=[[433,545],[599,545],[532,680]],matrix=dstPoints.map(([x,y])=>[x,y,1]);
 const ix=solve(matrix,srcPoints.map(p=>p[0])),iy=solve(matrix,srcPoints.map(p=>p[1]));
 const raw=await sharp(nativeFront).ensureAlpha().raw().toBuffer(),mapped=Buffer.from(raw);let outsideChanged=0;
 for(let y=480;y<910;y++)for(let x=360;x<680;x++){let w=smooth((x-360)/60)*smooth((680-x)/60)*smooth((910-y)/180);if(!w)continue;let sx=ix[0]*x+ix[1]*y+ix[2],sy=iy[0]*x+iy[1]*y+iy[2],s=sample(raw,x+w*(sx-x),y+w*(sy-y)),k=(y*1024+x)*4;for(let c=0;c<4;c++)mapped[k+c]=Math.round(s[c]);}
 for(let y=0;y<1536;y++)for(let x=0;x<1024;x++)if(x<360||x>=680||y<480||y>=910){let k=(y*1024+x)*4;for(let c=0;c<4;c++)if(mapped[k+c]!==raw[k+c])outsideChanged++;}
 if(outsideChanged)throw Error('Shoulder/body pixels changed outside collar registration');
 let clearedBackgroundAlpha=0;for(let y=0;y<500;y++)for(let x=0;x<1024;x++){let k=(y*1024+x)*4+3;if(mapped[k]){if(mapped[k]>3)throw Error('Unexpected painted content above collar');mapped[k]=0;clearedBackgroundAlpha++;}}
 await sharp(mapped,{raw:{width:1024,height:1536,channels:4}}).png().toFile(front);
 // Continuous posterior band is independently fitted behind the frozen neck.
 const rawRear=await sharp(nativeRear).ensureAlpha().raw().toBuffer(),rearMapped=Buffer.alloc(1024*1536*4);
 for(let y=520;y<615;y++)for(let x=410;x<625;x++){let s=sample(rawRear,(x-516)/.8+512,(y-567)/.8+750),k=(y*1024+x)*4;for(let c=0;c<4;c++)rearMapped[k+c]=Math.round(s[c]);}
 await sharp(rearMapped,{raw:{width:1024,height:1536,channels:4}}).png().toFile(rear);
 const face=path.join(A,'male/face_shape/01.png'),source=path.resolve(R,'../../male_whole_01.png');
 for(const[bg,label]of[['#e8e1d6','light'],['#243136','dark']]){
  const old=await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:rear},{input:face},{input:front}]).png().toBuffer();
  const detail=await sharp(old).extract({left:385,top:500,width:270,height:250}).resize(810,750).png().toBuffer();
  const orig=await sharp(source).extract({left:385,top:500,width:270,height:250}).resize(810,750).png().toBuffer();
  await sharp({create:{width:1620,height:750,channels:4,background:bg}}).composite([{input:orig,left:0,top:0},{input:detail,left:810,top:0}]).png().toFile(path.join(R,'qa/collar_comparison_'+label+'.png'));
  await sharp(old).png().toFile(path.join(R,'qa/front_rear_current_neck_'+label+'.png'));
 }
 const residuals=dstPoints.map(([x,y],i)=>Math.hypot(ix[0]*x+ix[1]*y+ix[2]-srcPoints[i][0],iy[0]*x+iy[1]*y+iy[2]-srcPoints[i][1]));
 const manifest={source_master:'whole_male_first_20261105/male_whole_01.png',source_master_sha256:sha(source),generation_mode:'built-in image_gen edit/extraction',canvas:[1024,1536],prompt_file:'source/generation_prompts.json',records:[{part:'clothing',index:1,file:'male/clothing/01.png',sha256:sha(front),native:'source/front_native.png',native_sha256:sha(nativeFront),transform:{source_controls:srcPoints,target_controls:dstPoints,inverse_x:ix,inverse_y:iy,local_displacement_box:[360,480,680,910],source_residual_pixels:residuals},native_png_byte_identical:false,shoulder_waist_body_outside_registration_box_changed_pixels:outsideChanged,numerical_background_alpha_le_3_cleared_above_y500:clearedBackgroundAlpha},{part:'clothing_back',index:1,file:'male/clothing_back/01.png',sha256:sha(rear),native:'source/rear_native.png',native_sha256:sha(nativeRear),transform:{scale:.8,source_center:[512,750],target_center:[516,567]},reason:'Native isolated band generated around y712..790; continuous narrow posterior band registered behind the fixed neck; not head/neck movement.'}],head_alpha_reference:'res://mods/appearance/ink_three_variants_20261004/male/face_shape/01.png',head_alpha_reference_sha256:sha(face),source_mode:'Approved master used as sole drawing source. All hair removed and occluded robe reconstructed. Collar controls measured visually from master and fitted; exact painting similarity, not pixel identity to master, must be visually reviewed.',visual_review:{source_front_geometry_preserved_outside_collar:true,front_no_hair_skin_head:true,neck_opening_transparent:true,rear_continuous_band:true,user_approval_of_split_layers:false}};
 fs.writeFileSync(path.join(R,'source/selection_manifest.json'),JSON.stringify(manifest,null,2));console.log(JSON.stringify(manifest));
})().catch(e=>{console.error(e);process.exitCode=1});
