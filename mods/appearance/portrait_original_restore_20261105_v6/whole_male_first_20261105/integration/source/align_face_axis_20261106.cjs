const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),P=path.resolve(R,'../../../../..'),A=path.join(P,'mods/appearance/ink_three_variants_20261004'),V=path.join(R,'axis_alignment_20261106'),I=path.join(R,'installation'),W=1024,H=1536,dx=-5;
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
(async()=>{fs.mkdirSync(V,{recursive:true});const reportPath=path.join(I,'install_report.json'),report=JSON.parse(fs.readFileSync(reportPath));
if(fs.existsSync(path.join(V,'correction.json')))throw Error('Correction already applied; do not shift twice');
for(const r of report.records)if(sha(path.join(P,r.resource_path.slice(6)))!==r.sha256)throw Error('Active resource changed: '+r.resource_path);
fs.copyFileSync(reportPath,path.join(V,'install_report.before.json'));
const headPath=path.join(A,'male/face_shape/01.png'),headSha=sha(headPath),parts=['eyebrows','eyes','nose','mouth','hair_front'],records=[];
for(const part of parts){const active=path.join(A,'male',part,'01.png'),source=path.join(R,'male',part,'01.png'),backup=path.join(V,'before',part,'01.png'),candidate=path.join(V,'male',part,'01.png');fs.mkdirSync(path.dirname(backup),{recursive:true});fs.mkdirSync(path.dirname(candidate),{recursive:true});fs.copyFileSync(active,backup);
 let{data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});if(info.width!==W||info.height!==H)throw Error('Canvas');let out=Buffer.alloc(data.length);
 for(let y=0;y<H;y++)data.copy(out,y*W*4,(y*W-dx)*4,(y+1)*W*4);
 await sharp(out,{raw:{width:W,height:H,channels:4}}).png().toFile(candidate);fs.copyFileSync(candidate,source);fs.copyFileSync(candidate,active);
 let rec=report.records.find(r=>r.part===part);rec.sha256=sha(active);records.push({part,before_sha256:sha(backup),after_sha256:sha(active),dx,dy:0,transform:'Integer translation only; no scaling, warping, filtering, or repainting',candidate:'male/'+part+'/01.png'});
}
if(sha(headPath)!==headSha)throw Error('Head/neck changed');
const{data:head}=await sharp(headPath).ensureAlpha().raw().toBuffer({resolveWithObject:true});let svg='<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1536"><path d="M514 140V745" stroke="#4e9ba4" stroke-opacity=".7" stroke-width="1" stroke-dasharray="4 5"/>';
for(const side of ['left','right']){let pts=[];for(let y=500;y<=720;y++){let xs=[];for(let x=350;x<680;x++)if(head[(y*W+x)*4+3]>=230)xs.push(x);if(xs.length)pts.push((side==='left'?xs[0]:xs.at(-1))+','+y);}svg+='<polyline points="'+pts.join(' ')+'" stroke="#e2942f" stroke-opacity=".75" stroke-width="1" fill="none"/>';}
const points=[[460.78,365.09],[565.24,364.66],[515,444],[514.5,475],[428,530],[601,530],[426,550],[603,550],[421,580],[608,580]];
for(const[x,y]of points)svg+=`<circle cx="${x}" cy="${y}" r="3" stroke="#e09632" stroke-width="1" fill="none"/>`;svg+='</svg>';
const overlay=path.join(R,'rig/male_overlay.png');fs.copyFileSync(overlay,path.join(V,'overlay.before.png'));await sharp(Buffer.from(svg)).png().toFile(overlay);report.anchor_overlay_sha256=sha(overlay);
let manifestPath=path.join(A,'portrait_manifest.json'),manifest=JSON.parse(fs.readFileSync(manifestPath));manifest.latest_male_rebuild.approved_whole_01.axis_alignment={date:'2026-11-06',head_axis_x:513.0,source_feature_axis_x:518.01,dx,dy:0,parts,head_neck_unchanged:true,clothes_unchanged:true,reason:'User observed off-center head/features; approved feature group and front hair aligned together to the fixed head axis, retaining native slight left/right shape differences'};
fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');report.manifest_after_sha256=sha(manifestPath);report.axis_alignment={dx,dy:0,head_neck_sha256:headSha,source:'axis_alignment_20261106/correction.json'};fs.writeFileSync(reportPath,JSON.stringify(report,null,2)+'\n');
const result={date:'2026-11-06',reason:'Head axis near513 vs approved features near518; remove five-pixel right offset',head_axis_x_before:513.0,feature_axis_x_before:518.01,feature_axis_x_after:513.01,head_neck_sha256:headSha,head_neck_changed:false,clothes_changed:false,configuration_changed:false,records};fs.writeFileSync(path.join(V,'correction.json'),JSON.stringify(result,null,2)+'\n');console.log('Aligned four features and front hair left5px; head, neck and all garments byte-identical');
})().catch(e=>{console.error(e);process.exitCode=1});
