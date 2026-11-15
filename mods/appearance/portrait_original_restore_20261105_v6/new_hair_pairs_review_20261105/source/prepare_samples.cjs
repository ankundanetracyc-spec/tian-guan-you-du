const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),W=1024,H=1536,sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const clamp=(v,a=0,b=1)=>Math.min(b,Math.max(a,v)),smooth=v=>{v=clamp(v);return v*v*(3-2*v)};
async function read(p){const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Canvas mismatch');return data}
async function save(d,p){fs.mkdirSync(path.dirname(p),{recursive:true});await sharp(d,{raw:{width:W,height:H,channels:4}}).png().toFile(p)}
function clean(d){const dist=new Uint8Array(W*H);dist.fill(255);const q=new Int32Array(W*H);let s=0,e=0;for(let p=0;p<W*H;p++)if(d[p*4+3]>=128){dist[p]=0;q[e++]=p;}while(s<e){let p=q[s++],dd=dist[p];if(dd>=8)continue;let x=p%W;for(const n of[x>0?p-1:-1,x<W-1?p+1:-1,p>=W?p-W:-1,p<(H-1)*W?p+W:-1])if(n>=0&&dist[n]>dd+1){dist[n]=dd+1;q[e++]=n;}}for(let p=0;p<W*H;p++){let k=p*4+3;if(d[k]<=3||dist[p]>8)d[k]=0;else if(dist[p]>4)d[k]=Math.round(d[k]*smooth((9-dist[p])/5));}return d}
function sample(d,x,y){if(x<0||y<0||x>=W-1||y>=H-1)return[0,0,0,0];let x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0,o=[0,0,0,0];for(const[dx,dy,w]of[[0,0,(1-fx)*(1-fy)],[1,0,fx*(1-fy)],[0,1,(1-fx)*fy],[1,1,fx*fy]]){let k=((y0+dy)*W+x0+dx)*4,a=d[k+3]/255;o[3]+=a*w;for(let c=0;c<3;c++)o[c]+=d[k+c]*a*w;}if(o[3])for(let c=0;c<3;c++)o[c]/=o[3];o[3]*=255;return o}
(async()=>{const records=[];
for(const part of['front','back'])for(let i=1;i<=3;i++){
 let p=path.join(R,'source',`pair${i}_${part}_raw.png`),d=clean(await read(p)),l=[];
 for(let k=0;k<d.length;k+=4)if(d[k+3]>230)l.push(.2126*d[k]+.7152*d[k+1]+.0722*d[k+2]);l.sort((a,b)=>a-b);let med=l[Math.floor(l.length*.5)],hi=l[Math.floor(l.length*.95)],target=part==='front'?76:68,high=part==='front'?110:98;
 for(let k=0;k<d.length;k+=4){if(!d[k+3])continue;let y=.2126*d[k]+.7152*d[k+1]+.0722*d[k+2],v=y<=med?y*target/med:target+(y-med)*(high-target)/(hi-med),delta=v-y;for(let c=0;c<3;c++)d[k+c]=Math.round(clamp(d[k+c]+delta,0,255));}
 let dest=path.join(R,'male','hair_'+part,`${String(i).padStart(2,'0')}.png`);await save(d,dest);records.push({part:'hair_'+part,index:i,source:path.relative(R,p),file:path.relative(R,dest),sha256:sha(dest),source_luminance:{median:med,p95:hi},target_luminance:{median:target,p95:high},geometry:'native full canvas; no resizing/translation',alpha:'distant background residuals removed, local strand antialiasing retained'});
}
const features={eyebrows:{s:168/253,source:[512.5,356.5],target:[516,350]},eyes:{s:102/109.8,source:[519.4,382.2],target:[515,379]},nose:{s:.60,source:[516,551],target:[516,451]},mouth:{s:.56,source:[514,541],target:[516,484]}};
features.eyes=JSON.parse(fs.readFileSync(path.join(R,'features/source/eye_registration.json')));
for(const[part,t]of Object.entries(features)){
 let p=path.join(R,'features/source',part+(part==='eyes'?'_clean_raw.png':'_refined_raw.png')),d=clean(await read(p)),out=Buffer.alloc(W*H*4),tx=t.target[0]-t.s*t.source[0],ty=t.target[1]-t.s*t.source[1];
 if(part==='eyebrows')for(let k=0;k<d.length;k+=4){for(let c=0;c<3;c++)d[k+c]=Math.round(d[k+c]*.82);}
 for(let y=300;y<530;y++)for(let x=390;x<640;x++){let c=sample(d,(x-tx)/t.s,(y-ty)/t.s),k=(y*W+x)*4;for(let j=0;j<4;j++)out[k+j]=Math.round(c[j]);}
 let dest=path.join(R,'features/male',part,'01.png');await save(out,dest);records.push({part,index:1,source:path.relative(R,p),file:path.relative(R,dest),sha256:sha(dest),registration:{...t,tx,ty},geometry:'single uniform scale and translation per whole feature; eye pair kept together'});
}
fs.writeFileSync(path.join(R,'source/selected_assets.json'),JSON.stringify({status:'Review only; user approval required before installation',canvas:[W,H],records},null,2));console.log('Six hair layers and four new facial feature samples prepared; no active assets changed');
})().catch(e=>{console.error(e);process.exitCode=1});
