// Import-time skin material calibration against the user-approved portrait.
// Keeps the generated painting, geometry and frozen Alpha; never draws new shapes.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'), W=1024,H=1536;
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const boxes=[
 ['forehead',[490,290,535,320]], ['left_cheek',[443,405,465,430]],
 ['right_cheek',[565,405,588,430]], ['chin_plane',[495,510,535,527]],
 ['upper_neck',[500,555,530,578]], ['lower_neck',[505,600,535,622]]
];
async function read(f){const{data,info}=await sharp(f).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Wrong canvas');return data;}
function median(a){a.sort((x,y)=>x-y);return a[Math.floor(a.length/2)];}
function sample(d,box){const out=[[],[],[]];for(let y=box[1];y<box[3];y++)for(let x=box[0];x<box[2];x++){const k=(y*W+x)*4;if(d[k+3]<240)continue;for(let c=0;c<3;c++)out[c].push(d[k+c]);}return out.map(median);}
(async()=>{
 const target=path.join(R,'source/approved_male.png'),output=path.join(R,'male/face_shape/01.png');
 const native=path.join(R,'raw/head_import_before_color.png');
 if(!fs.existsSync(native)) fs.copyFileSync(output,native);
 const original=await read(native), source=await read(target), out=Buffer.from(original);
 const samples=boxes.map(([name,b])=>({name,box:b,before:sample(original,b),approved:sample(source,b)}));
 const transfer=[0,1,2].map(c=>{
   const xs=samples.map(s=>s.before[c]),ys=samples.map(s=>s.approved[c]);
   const mx=xs.reduce((a,b)=>a+b,0)/xs.length,my=ys.reduce((a,b)=>a+b,0)/ys.length;
   const variance=xs.reduce((a,x)=>a+(x-mx)**2,0);
   const gain=xs.reduce((a,x,i)=>a+(x-mx)*(ys[i]-my),0)/variance;
   const offset=my-gain*mx;
   if(!Number.isFinite(gain)||gain<.5||gain>1.5||!Number.isFinite(offset))throw Error('Unstable material calibration');
   return{gain,offset};
 });
 for(let k=0;k<out.length;k+=4)if(out[k+3])for(let c=0;c<3;c++)out[k+c]=Math.round(Math.max(0,Math.min(255,original[k+c]*transfer[c].gain+transfer[c].offset)));
 await sharp(out,{raw:{width:W,height:H,channels:4}}).png().toFile(output);
 const actual=await read(output);let alphaChanged=0;
 for(let k=3;k<actual.length;k+=4)if(actual[k]!==original[k])alphaChanged++;
 if(alphaChanged)throw Error('Frozen head/neck Alpha changed');
 for(const s of samples){s.after=sample(actual,s.box);s.delta_after=s.after.map((x,c)=>x-s.approved[c]);}
 const record={method:'One global per-channel linear color calibration from paired unobscured approved-source skin samples. No spatial masks, warps, drawing, old RGB, skin tiles or per-region repainting.',approved_sha256:sha(target),generated_import_source:path.relative(R,native),generated_import_sha256:sha(native),output:path.relative(R,output),output_sha256:sha(output),canvas:[W,H],alpha_changed_pixels:alphaChanged,geometry_changed:false,transfer,samples,native_painting_retained:true,native_rgb_unchanged:false};
 fs.writeFileSync(path.join(R,'qa/head_color_transfer.json'),JSON.stringify(record,null,2)+'\n');
 console.log(JSON.stringify({transfer,samples:samples.map(s=>({name:s.name,before:s.before,approved:s.approved,after:s.after,delta:s.delta_after})),alpha_changed_pixels:0}));
})().catch(e=>{console.error(e);process.exitCode=1});
