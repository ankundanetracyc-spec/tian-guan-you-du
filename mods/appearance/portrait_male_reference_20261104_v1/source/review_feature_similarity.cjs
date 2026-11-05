// Read-only reference comparison. This does not extract or draw feature sprites.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../../../..');
const refRoot = path.join(root, 'mods/appearance/portrait_rebuild_20261103/source/male_final');
const transforms = [
  {s:1.0101786289485613, dx:204.1524906124647, dy:50.47312036185366, pupils:[[260.3765029778627,324.57191819305547],[364.31851579720797,324.4818410832372]]},
  {s:1.000499169627508, dx:215.46867425312956, dy:49.420812240090754, pupils:[[248.55751907739958,325.9663205003155],[353.5051324163413,325.192055019503]]},
  {s:0.9906711275749103, dx:220.06925136343364, dy:50.01428907451083, pupils:[[243.43637084691977,325.31241737904287],[349.425126426213,324.6590044719355]]},
];
const rois = {eyebrows:[420,336,615,364],eyes:[428,363,610,393],nose:[491,414,544,464],mouth:[477,468,559,503]};
function sample(image,x,y){
  const {data,info}=image,xx=Math.max(0,Math.min(info.width-1,x)),yy=Math.max(0,Math.min(info.height-1,y));
  const a=Math.floor(xx),b=Math.floor(yy),dx=xx-a,dy=yy-b;
  const pixel=(px,py)=>{const i=(Math.min(info.height-1,py)*info.width+Math.min(info.width-1,px))*3;return data[i]*.2126+data[i+1]*.7152+data[i+2]*.0722;};
  return pixel(a,b)*(1-dx)*(1-dy)+pixel(a+1,b)*dx*(1-dy)+pixel(a,b+1)*(1-dx)*dy+pixel(a+1,b+1)*dx*dy;
}
function corr(a,b){let ma=0,mb=0;for(let i=0;i<a.length;i++){ma+=a[i];mb+=b[i];}ma/=a.length;mb/=b.length;let ab=0,aa=0,bb=0,delta=0;for(let i=0;i<a.length;i++){let av=a[i]-ma,bv=b[i]-mb;aa+=av*av;bb+=bv*bv;ab+=av*bv;delta+=Math.abs(a[i]-b[i]);}return{luminanceCorrelation:ab/Math.sqrt(aa*bb),meanAbsoluteLuminanceDifference:delta/a.length};}
(async()=>{
 const images=[];
 for(let i=0;i<3;i++)images.push(await sharp(path.join(refRoot,'reference_0'+(i+1)+'.png')).raw().toBuffer({resolveWithObject:true}));
 const metrics={};
 for(const[part,box]of Object.entries(rois)){
   const vectors=images.map((im,i)=>{const t=transforms[i],mx=(t.pupils[0][0]+t.pupils[1][0])/2,my=(t.pupils[0][1]+t.pupils[1][1])/2,v=[];for(let y=box[1];y<box[3];y++)for(let x=box[0];x<box[2];x++)v.push(sample(im,(x-516.5)/t.s+mx,(y-375)/t.s+my));return v;});
   metrics[part]={targetRoi:box,pairs:{'01_02':corr(vectors[0],vectors[1]),'01_03':corr(vectors[0],vectors[2]),'02_03':corr(vectors[1],vectors[2])}};
 }
 const report={method:'Reference-only luminance correlation after approximate paired-pupil whole-face alignment; visual comparison is required and no universal merge threshold asserted.',attachment_columns:[{index:1,offsetX:0,width:590,rgbDifferentChannels:0},{index:2,offsetX:593,width:589,rgbDifferentChannels:0},{index:3,offsetX:1187,width:587,rgbDifferentChannels:0}],transforms,metrics,existing_final_assets:'Existing final male assets are generated reference-derived sprites; current noses lack reference volume and must not be copied as matching originals.'};
 fs.writeFileSync(path.join(__dirname,'reference_feature_comparison.json'),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify(metrics));
})().catch(e=>{console.error(e);process.exitCode=1;});
