// Geometry and layer-integrity checks for the two one-set portrait reviews.
const fs=require('fs');
const path=require('path');
const {PNG}=require('pngjs');
const root=path.resolve(__dirname,'..');
const W=1024,H=1536;
const packs={
 male:'mods/appearance/腰上男修拆件演示_v2/test_assets_green_reference_v1',
 female:'mods/appearance/腰上女修拆件演示_v1/test_assets_green_reference_v1',
};
function read(base,part){
 const file=path.join(root,base,`${part}_0.png`),im=PNG.sync.read(fs.readFileSync(file));
 if(im.width!==W||im.height!==H)throw Error(`${file}: canvas is not 1024x1536`);
 return im;
}
function a(im,x,y){return im.data[(y*W+x)*4+3]}
function bounds(im,y){
 let l=W,r=-1;
 for(let x=0;x<W;x++)if(a(im,x,y)>64){l=Math.min(l,x);r=Math.max(r,x)}
 return [l,r];
}
function centroid(im,x0,x1,y0,y1){
 let m=0,sx=0,sy=0;
 for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){
  const v=a(im,x,y)/255;m+=v;sx+=x*v;sy+=y*v;
 }
 if(m<10)throw Error('Missing feature pigment');
 return [sx/m,sy/m];
}
function near(value,target,tolerance,label){if(Math.abs(value-target)>tolerance)throw Error(`${label}: ${value.toFixed(2)} vs ${target}`)}
for(const [sex,base] of Object.entries(packs)){
 const parts=['hair_back','face','ear','clothes','eye','brow','nose','mouth',sex==='male'?'beard':'makeup','hair_front'];
 const layer=Object.fromEntries(parts.map(part=>[part,read(base,part)]));
 if(sex==='male'){
  // These visible skin points are inside the chosen original cheek contour.
  // The previous test checked only central features and missed the side notch.
  for(const [x,y] of [[434,470],[437,480],[594,460],[590,470],[587,480]]){
   if(a(layer.face,x,y)<240)throw Error(`male: cheek skin missing at (${x},${y})`);
  }
  // Reject pale background/skin pixels mistakenly baked into ear-side hair.
  // A relative front-vs-rear brightness check would reject genuine highlights.
  let leaked=0;
  for(let y=390;y<500;y++)for(let x=345;x<680;x++){
   if(x>=435&&x<=590)continue;
   const k=(y*W+x)*4;
   if(layer.hair_back.data[k+3]<128)continue;
   if(layer.hair_front.data[k+3]>=128
    && layer.hair_front.data[k]>150
    && layer.hair_front.data[k+1]>140
    && layer.hair_front.data[k+2]>130)leaked++;
  }
  if(leaked)throw Error(`male: ${leaked} bright ear-side front pixels cover complete rear hair`);
 }
 for(const y of [160,220,280,320]){
  const [fl,fr]=bounds(layer.hair_front,y),[rl,rr]=bounds(layer.hair_back,y);
  if(fl>=fr||rl<fl||rr>fr)throw Error(`${sex}: rear scalp escapes frontal scalp at y=${y}: ${[rl,rr]} outside ${[fl,fr]}`);
 }
 for(const y of [390,520,800])if(a(layer.hair_back,512,y)<128)throw Error(`${sex}: hidden rear painting has center hole at y=${y}`);
 const eyes=[centroid(layer.eye,400,511,345,430),centroid(layer.eye,512,624,345,430)];
 const brows=[centroid(layer.brow,400,511,315,380),centroid(layer.brow,512,624,315,380)];
 const nose=centroid(layer.nose,465,559,390,495),mouth=centroid(layer.mouth,440,584,450,525);
 const faceBounds=bounds(layer.face,440);
 let bareTop=-1;
 for(let y=170;y<=280;y++)if(a(layer.face,512,y)>=128){bareTop=y;break}
 if(bareTop<0)throw Error(`${sex}: missing continuous bald crown`);
 for(let y=bareTop;y<=380;y++)if(a(layer.face,512,y)<128)throw Error(`${sex}: bald scalp has center gap at y=${y}`);
 const chinY=sex==='male'?548:552;
 const [bareLeft,bareRight]=bounds(layer.face,340);
 const headHeightToWidth=(chinY-bareTop)/(bareRight-bareLeft);
 if(headHeightToWidth<1.3||headHeightToWidth>1.75)
  throw Error(`${sex}: bald head contour is too compressed or elongated (${headHeightToWidth.toFixed(3)})`);
 const [,templeRight]=bounds(layer.face,380);
 if(bareRight-templeRight>7)throw Error(`${sex}: abrupt cranium-to-temple pinch`);
 // The bare skull is reviewed before any facial-part adjustment. Check its
 // actual pixels, not just the registered centers of eyes or hair.
 for(let y=bareTop;y<=650;y++)for(let x=350;x<512;x++){
  const k=(y*W+x)*4,m=(y*W+(1024-x))*4;
  if(Math.abs(layer.face.data[k+3]-layer.face.data[m+3])>1)
   throw Error(`${sex}: bald head alpha asymmetry at (${x},${y})`);
  if(layer.face.data[k+3]>240&&layer.face.data[m+3]>240)
   for(let c=0;c<3;c++)if(Math.abs(layer.face.data[k+c]-layer.face.data[m+c])>2)
    throw Error(`${sex}: bald head skin-tone asymmetry at (${x},${y})`);
 }
 const radius={};
 for(let y=300;y<=535;y++){
  const [left,right]=bounds(layer.face,y);
  if(left+right!==1024)throw Error(`${sex}: off-center bald contour at y=${y}`);
  radius[y]=right-512;
  for(let x=left+5;x<=right-5;x++)if(a(layer.face,x,y)<240)
   throw Error(`${sex}: bald head has a visible interior hole at (${x},${y})`);
 }
 // The head/neck alpha union is wider than the drawn jaw below y480.
 // Check the visible cranium-to-cheek edge before that anatomical overlap.
 for(let y=365;y<=440;y++){
  const before=(radius[y]-radius[y-15])/15,after=(radius[y+15]-radius[y])/15;
  // The supplied red trace itself changes up to 3 px between these tangent
  // windows; allow one additional raster pixel, while rejecting hard bends.
  if(Math.abs(after-before)>0.27)
   throw Error(`${sex}: visible contour corner near y=${y}`);
 }
 near((eyes[0][0]+eyes[1][0])/2,512,8,`${sex} eye pair center`);
 near((brows[0][0]+brows[1][0])/2,512,8,`${sex} brow pair center`);
 near(eyes[0][1]-eyes[1][1],0,6,`${sex} eye height mismatch`);
 near(brows[0][1]-brows[1][1],0,6,`${sex} brow height mismatch`);
 near(nose[0],512,12,`${sex} nose center`);
 near(mouth[0],512,12,`${sex} mouth center`);
 near((faceBounds[0]+faceBounds[1])/2,512,12,`${sex} jaw center`);
 const spacing=(eyes[0][1]+eyes[1][1]-brows[0][1]-brows[1][1])/2;
 if(spacing<12)throw Error(`${sex}: brows too near eyes (${spacing.toFixed(1)} px)`);
 const hairMeans=[[0,0,0],[0,0,0]],hairCount=[0,0];
 for(let y=185;y<=275;y++)for(let x=350;x<=674;x++){
  const k=(y*W+x)*4;
  if(layer.hair_front.data[k+3]<128||layer.hair_back.data[k+3]<128)continue;
  for(let c=0;c<3;c++){
   hairMeans[0][c]+=layer.hair_front.data[k+c];
   hairMeans[1][c]+=layer.hair_back.data[k+c];
  }
  hairCount[0]++;hairCount[1]++;
 }
 if(hairCount[0]<1000)throw Error(`${sex}: too little shared scalp for hair match check`);
 const means=hairMeans.map((rgb,i)=>rgb.map(v=>v/hairCount[i]));
 const colorDelta=Math.max(...means[0].map((v,i)=>Math.abs(v-means[1][i])));
 if(colorDelta>32)throw Error(`${sex}: front/rear crown color mismatch (${colorDelta.toFixed(1)})`);
 const crown=bounds(layer.hair_front,260),faceEye=bounds(layer.face,400);
 const headRatio=(crown[1]-crown[0])/(faceEye[1]-faceEye[0]);
 near((crown[0]+crown[1])/2,512,18,`${sex} scalp center`);
 if(headRatio>2.1)throw Error(`${sex}: hair too wide relative to face (${headRatio.toFixed(2)})`);
 console.log(JSON.stringify({sex,canvas:[W,H],center_axis_x:512,bald_top_y:bareTop,bald_width_y340:bareRight-bareLeft,bald_head_height_to_width:headHeightToWidth,bald_temple_width_y380:2*(templeRight-512),crown_y160:{front:bounds(layer.hair_front,160),rear:bounds(layer.hair_back,160)},eyes,brows,nose,mouth,face_y440:faceBounds,brow_eye_gap_px:spacing,scalp_to_face_width_ratio:headRatio,front_rear_hair_mean_rgb:means,color_channel_delta_max:colorDelta}));
}
console.log('GREEN_REFERENCE_ONE_SET_QA_PASS');
