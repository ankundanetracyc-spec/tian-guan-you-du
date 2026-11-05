const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(pack,'../../..');
const maleRoot=path.join(pack,'male'),femaleRoot=path.join(project,'mods/appearance/ink_three_variants_20261004/female');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const roi={
 hair_front:[
  {name:'crown_front',box:[330,170,710,340],meaning:'Anterior crown/cap; no hair ornaments are present in these front layers.'},
  {name:'temple_front',box:[330,340,710,500],meaning:'Anterior temples and face-framing strands; opaque hair only.'},
  {name:'chest_front',box:[300,520,740,950],meaning:'Hair intended in front of clothing; opaque strands only.'}],
 hair_back:[
  {name:'crown_back',box:[340,190,700,330],meaning:'Posterior scalp/cap under front layers; excludes high knot/pin.'},
  {name:'nape_back',box:[300,340,740,500],meaning:'Posterior side hair, nape and side buns.'},
  {name:'curtain_back',box:[260,500,780,1000],meaning:'Posterior long hair curtain behind shoulders/body. Female style02 is a low bun and has no comparable curtain.'}]
};
const q=(a,p)=>a.length?a[Math.floor((a.length-1)*p)]:null;
const round=(n,d=3)=>n===null?null:+n.toFixed(d);
function lab(r,g,b){
 const lin=v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4};
 [r,g,b]=[r,g,b].map(lin);
 const f=t=>t>.008856451679?t**(1/3):7.787037*t+16/116;
 const x=f((.4124564*r+.3575761*g+.1804375*b)/.95047),y=f(.2126729*r+.7151522*g+.072175*b),z=f((.0193339*r+.119192*g+.9503041*b)/1.08883);
 return [116*y-16,500*(x-y),200*(y-z)];
}
function vals(d,info,box){
 const ys=[],ls=[],rgb=[[],[],[]],as=[],bs=[],grad=[],lap=[];
 const [x0,y0,x1,y1]=box,w=info.width;
 const luminance=k=>.2126*d[k]+.7152*d[k+1]+.0722*d[k+2];
 for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++){
  const k=(y*w+x)*4;if(d[k+3]<230)continue;
  const yy=luminance(k),ll=lab(d[k],d[k+1],d[k+2]);ys.push(yy);ls.push(ll[0]);as.push(ll[1]);bs.push(ll[2]);
  for(let c=0;c<3;c++)rgb[c].push(d[k+c]);
  if(x+1<x1&&d[k+7]>=230)grad.push(Math.abs(yy-luminance(k+4)));
  if(y+1<y1&&d[k+w*4+3]>=230)grad.push(Math.abs(yy-luminance(k+w*4)));
  if(x>x0&&x+1<x1&&y>y0&&y+1<y1&&[k-4,k+4,k-w*4,k+w*4].every(j=>d[j+3]>=230))lap.push(yy-(luminance(k-4)+luminance(k+4)+luminance(k-w*4)+luminance(k+w*4))/4);
 }
 return {ys,ls,rgb,as,bs,grad,lap};
}
function summarize(v){
 const n=v.ys.length;if(!n)return {pixels:0,comparable:false};
 const mean=a=>a.reduce((s,x)=>s+x,0)/a.length;
 const pc=a=>Object.fromEntries([.01,.1,.5,.9,.99].map(p=>['p'+Math.round(p*100),round(q([...a].sort((x,y)=>x-y),p))]));
 return {pixels:n,comparable:n>=100,mean_rgb:v.rgb.map(a=>round(mean(a))),median_rgb:v.rgb.map(a=>round(q(a.sort((x,y)=>x-y),.5))),mean_lab:[round(mean(v.ls)),round(mean(v.as)),round(mean(v.bs))],mean_lab_chroma:round(Math.hypot(mean(v.as),mean(v.bs))),mean_lab_hue_degrees:round((Math.atan2(mean(v.bs),mean(v.as))*180/Math.PI+360)%360),luma_srgb_255:{mean:round(mean(v.ys)),...pc(v.ys)},cie_Lstar:pc(v.ls),percent_luma_above:Object.fromEntries([100,110,125,140].map(t=>[t,round(v.ys.filter(x=>x>t).length/n*100)])),neighbor_absolute_luma_difference:{mean:round(mean(v.grad)),p90:round(q(v.grad.sort((a,b)=>a-b),.9))},four_neighbor_highpass_rms:round(Math.sqrt(mean(v.lap.map(x=>x*x))))};
}
const merge=vs=>({ys:vs.flatMap(v=>v.ys),ls:vs.flatMap(v=>v.ls),rgb:[0,1,2].map(c=>vs.flatMap(v=>v.rgb[c])),as:vs.flatMap(v=>v.as),bs:vs.flatMap(v=>v.bs),grad:vs.flatMap(v=>v.grad),lap:vs.flatMap(v=>v.lap)});
(async()=>{
 const records=[],pools={female:{},male:{}};
 for(const [sex,root]of [['female',femaleRoot],['male',maleRoot]])for(const part of ['hair_front','hair_back'])for(const id of ['01','02','03']){
  const file=path.join(root,part,id+'.png'),{data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  const regions=[];
  for(const reg of roi[part]){
   const v=vals(data,info,reg.box);regions.push({...reg,stats:summarize(v)});
   if(!(sex==='female'&&part==='hair_back'&&id==='02'&&['nape_back','curtain_back'].includes(reg.name)))(pools[sex][reg.name]??=[]).push(v);
  }
  records.push({sex,part,id,file,sha256:hash(file),dimensions:[info.width,info.height],regions});
 }
 const pooled={};
 for(const sex of ['female','male'])pooled[sex]=Object.fromEntries(Object.entries(pools[sex]).map(([k,vs])=>[k,summarize(merge(vs))]));
 const report={generated_at:new Date().toISOString(),method:'Only source RGBA core pixels with Alpha >= 230; no background composite and no transparent RGB. CIE Lab D65 via linear sRGB. Percentiles and threshold coverage are measured per anatomy region. Texture measures are indicative because strand widths/directions vary.',ornament_exclusion:'Front layers contain no ornaments. Back measurements start y190 or lower, below wood pin/high cloth wrap and knot. Decorative parts are excluded rather than made hair-color targets.',regions:roi,records,pooled,limitations:['This is a tone baseline, not an exact shape or strand-texture equivalence test.','Female style02 is a low bun without long rear curtain; it is excluded from pooled nape and rear-curtain targets, but its individual statistics remain recorded.','Back scalp interior is often hidden; visual judgement should prioritize exposed cap, side hair and front/chest strands.','Low chroma makes hue angle unstable; use Lab a/b and mean RGB channel differences for hue matching.','Opaque core statistics do not evaluate soft alpha edge haze, which still needs light/dark composite inspection.']};
 fs.writeFileSync(path.join(__dirname,'hair_tone_baseline.json'),JSON.stringify(report,null,2)+'\n');
 console.log(JSON.stringify(Object.fromEntries(Object.entries(pooled).map(([sex,regions])=>[sex,Object.fromEntries(Object.entries(regions).map(([k,s])=>[k,{n:s.pixels,rgb:s.mean_rgb,lab:s.mean_lab,p:s.luma_srgb_255,high:s.percent_luma_above,gradient:s.neighbor_absolute_luma_difference,highpass:s.four_neighbor_highpass_rms}]))])),null,2));
})().catch(e=>{console.error(e);process.exitCode=1});

