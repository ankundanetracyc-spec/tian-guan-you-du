const sharp=require('sharp'),fs=require('fs'),path=require('path');
const p=path.resolve(__dirname,'..'),snap=path.join(p,'installation/backup_before');
const file=(g,part,id,after)=>{let n=path.join(p,g,part,id+'.png');return after&&fs.existsSync(n)?n:path.join(snap,g,part,id+'.png')};
const canvas=bg=>sharp({create:{width:1024,height:1536,channels:4,background:bg}});
async function neck(id,after,bg){let layers=[];if(after)layers.push({input:file('male','clothing_back',id,true)});layers.push({input:file('male','face_shape','01',false)},{input:file('male','clothing',id,after)});return canvas(bg).composite(layers).png().toBuffer();}
async function face(g,after){return canvas('#e8e1d6').composite(['face_shape/'+(g==='female'?'04':'01'),'eyes/01','eyebrows/01','nose/01','mouth/01'].map(s=>{const[a,b]=s.split('/');return{input:file(g,a,b,after)}})).png().toBuffer();}
const caption=(text,w,h=24)=>Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><text x="8" y="18" font-family="sans-serif" font-size="16" fill="#484b44">${text}</text></svg>`);
(async()=>{
 for(const [bg,label]of [['#e8e1d6','light'],['#222a2b','dark']]){
  const cells=[];for(let i=1;i<=7;i++){const id=String(i).padStart(2,'0'),b=await neck(id,true,bg);cells.push({input:await sharp(b).extract({left:395,top:500,width:250,height:130}).resize(500,260).toBuffer(),left:((i-1)%4)*500,top:Math.floor((i-1)/4)*284+24});cells.push({input:caption(id,500),left:((i-1)%4)*500,top:Math.floor((i-1)/4)*284});}
  await sharp({create:{width:2000,height:568,channels:4,background:bg}}).composite(cells).png().toFile(path.join(p,'qa',`all_seven_pairs_root_${label}.png`));
 }
 let rows=[],y=0;
 const add=async(label,before,after,rect,h)=>{rows.push({input:caption(label+'  |  左：修改前 / 右：修改后',1000,28),left:0,top:y});y+=28;for(const[b,x]of[[before,0],[after,500]])rows.push({input:await sharp(b).extract(rect).resize(500,h).toBuffer(),left:x,top:y});y+=h;};
 await add('男装领口',await neck('01',false,'#e8e1d6'),await neck('01',true,'#e8e1d6'),{left:395,top:500,width:250,height:130},260);
 await add('男性山根',await face('male',false),await face('male',true),{left:420,top:330,width:195,height:150},385);
 await add('女性山根',await face('female',false),await face('female',true),{left:420,top:330,width:195,height:150},385);
 await sharp({create:{width:1000,height:y,channels:4,background:'#e8e1d6'}}).composite(rows).png().toFile(path.join(p,'preview/final_revision_before_after.png'));
 console.log(JSON.stringify({pairs:7,comparison:'preview/final_revision_before_after.png'}));
})().catch(e=>{console.error(e);process.exitCode=1;});
