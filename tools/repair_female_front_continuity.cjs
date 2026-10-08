// Keep the approved female painting's actual hair pigment, with the complete
// frontal hair layer as its continuous silhouette guide.
const fs=require('fs');
const path=require('path');
const sharp=require('sharp');
const {PNG}=require('pngjs');
const root=path.resolve(__dirname,'..');
const pack=path.join(root,'mods/appearance/腰上女修拆件演示_v1');
const active=path.join(pack,'test_assets_green_reference_v1');
const source=path.join(pack,'test_sources/green_master_single_set');
const candidate=path.join(pack,'test_sources/front_continuity_v1');
const preview=path.join(pack,'preview/front_continuity_v1');
fs.mkdirSync(candidate,{recursive:true});fs.mkdirSync(preview,{recursive:true});
const W=1024,H=1536;
function read(file){const p=PNG.sync.read(fs.readFileSync(file));if(p.width!==W||p.height!==H)throw Error(file);return p}
function smooth(t){t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)}
function ink(r,g,b){return smooth((r-g+5)/9)*smooth((23-r+g)/10)*smooth((30-r+b)/12)}
const master=read(path.join(source,'female_master.png'));
const guide=read(path.join(pack,'test_assets_v5/hair_front_0.png'));
const old=read(path.join(active,'hair_front_0.png'));
const front=new PNG({width:W,height:H});
for(let y=0;y<H;y++)for(let x=0;x<W;x++){
 const k=(y*W+x)*4;
 const pigment=ink(master.data[k],master.data[k+1],master.data[k+2]);
 const crown=Math.round(master.data[k+3]*pigment*guide.data[k+3]/255);
 const keepCrown=y<300?old.data[k+3]:0;
 const keepLock=y>490&&(x<440||x>584)?old.data[k+3]*smooth((y-490)/70):0;
 const alpha=Math.max(crown,keepCrown,keepLock);
 if(alpha<4)continue;
 const useMaster=crown>=keepLock||y<300;
 const src=useMaster?master:old;
 for(let c=0;c<3;c++)front.data[k+c]=src.data[k+c];
 front.data[k+3]=Math.round(alpha);
}
const file=path.join(candidate,'hair_front_0.png');
fs.writeFileSync(file,PNG.sync.write(front));
const order=['hair_back','face','ear','clothes','eye','brow','nose','mouth','makeup','hair_front'];
sharp({create:{width:W,height:H,channels:4,background:'#cbc8bd'}})
 .composite(order.map(part=>({input:part==='hair_front'?file:path.join(active,`${part}_0.png`)})))
 .png().toFile(path.join(preview,'full.png'))
 .then(()=>console.log('FEMALE_FRONT_CONTINUITY_CANDIDATE_READY'))
 .catch(e=>{console.error(e);process.exitCode=1});
