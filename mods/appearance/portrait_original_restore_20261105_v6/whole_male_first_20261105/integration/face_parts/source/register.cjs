const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),M=path.resolve(R,'../../male_whole_01.png'),W=1024,H=1536;
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),clamp=(v,a=0,b=255)=>Math.min(b,Math.max(a,v));
async function read(p){const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});return{data,w:info.width,h:info.height};}
function sample(im,x,y){if(x<0||y<0||x>=im.w-1||y>=im.h-1)return[0,0,0,0];const x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,o=[0,0,0,0];for(const[dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){const k=((y0+dy)*im.w+x0+dx)*4,a=im.data[k+3]/255;o[3]+=a*w;for(let c=0;c<3;c++)o[c]+=im.data[k+c]*a*w;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o;}
const median=a=>{a.sort((x,y)=>x-y);return a[Math.floor(a.length/2)]||0;};
(async()=>{const measurements=JSON.parse(fs.readFileSync(path.join(R,'source/measurements.json'))),master=await read(M),e=measurements;
const sx=(e.master_eye_points[1][0]-e.master_eye_points[0][0])/(e.native_eye_points[1][0]-e.native_eye_points[0][0]);
const defs={eyebrows:{scale:174/909,source:[629.5,637.5],target:[521,342.5],region:[430,330,614,356]},eyes:{scale:sx,source:[(e.native_eye_points[0][0]+e.native_eye_points[1][0])/2,(e.native_eye_points[0][1]+e.native_eye_points[1][1])/2],target:[(e.master_eye_points[0][0]+e.master_eye_points[1][0])/2,(e.master_eye_points[0][1]+e.master_eye_points[1][1])/2],region:[430,348,614,383]},nose:{scale:37/e.nose.nostrils_width,source:e.nose.nostrils,target:[519.5,443],region:[480,340,557,462]},mouth:{scale:72/e.mouth.seam_width,source:e.mouth.seam,target:[520,475],region:[474,458,566,498]}};
const records=[];
for(const[part,d]of Object.entries(defs)){const source=path.join(R,'source',part+'_raw.png'),native=await read(source),out=Buffer.alloc(W*H*4),tx=d.target[0]-d.scale*d.source[0],ty=d.target[1]-d.scale*d.source[1];let chroma_removed=0;
for(let k=0;k<native.data.length;k+=4){const a=native.data[k+3],rgb=[native.data[k],native.data[k+1],native.data[k+2]],hi=Math.max(...rgb),lo=Math.min(...rgb);if(a<=3||(hi>220&&hi-lo>100)){if(a>3)chroma_removed++;native.data[k+3]=0;}}
const colors=[[],[],[]];for(let y=d.region[1];y<d.region[3];y++)for(let x=d.region[0];x<d.region[2];x++){const s=sample(native,(x-tx)/d.scale,(y-ty)/d.scale),k=(y*W+x)*4;if(s[3]>230)for(let c=0;c<3;c++)colors[c].push(master.data[k+c]-s[c]);}
const bias=colors.map(median).map(v=>clamp(v,-25,25));
for(let y=d.region[1]-5;y<d.region[3]+5;y++)for(let x=d.region[0]-5;x<d.region[2]+5;x++){const s=sample(native,(x-tx)/d.scale,(y-ty)/d.scale),k=(y*W+x)*4;for(let c=0;c<3;c++)out[k+c]=Math.round(clamp(s[c]+bias[c]));out[k+3]=Math.round(s[3]);}
const dest=path.join(R,'male',part,'01.png');fs.mkdirSync(path.dirname(dest),{recursive:true});await sharp(out,{raw:{width:W,height:H,channels:4}}).png().toFile(dest);records.push({part,index:1,source:path.relative(R,source),native_size:[native.w,native.h],sha256:sha(dest),file:path.relative(R,dest),transform:{uniform_scale:d.scale,source_anchor:d.source,target_anchor:d.target,tx,ty},rgb_bias_to_approved_master:bias,colored_edge_noise_removed:chroma_removed,canvas:[W,H],pair_registered_together:['eyebrows','eyes'].includes(part)});
}
fs.writeFileSync(path.join(R,'source/registration.json'),JSON.stringify({master:'../../male_whole_01.png',master_sha256:sha(M),records},null,2));console.log(JSON.stringify(records,null,2));
})().catch(e=>{console.error(e);process.exitCode=1});
