const fs=require('fs'),path=require('path'),sharp=require('sharp');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const rig=JSON.parse(fs.readFileSync(path.join(__dirname,'clothing_strict_rig.json'),'utf8'));
const report=JSON.parse(fs.readFileSync(path.join(pack,'qa/clothing_strict_guard_report.json'),'utf8'));
const garment=report.garments.find(g=>g.index===1),source=path.join(pack,'installation/backup_before/male/clothing/01.png'),actual=path.join(pack,'male/clothing/01.png');
const face=path.join(pack,'installation/backup_before/male/face_shape/01.png');
const cross=(p,color,r=1.5)=>`<path d="M${p[0]-r},${p[1]}H${p[0]+r} M${p[0]},${p[1]-r}V${p[1]+r}" stroke="${color}" stroke-width=".55"/>`;
const circle=(p,color,r=1.8)=>`<circle cx="${p[0]}" cy="${p[1]}" r="${r}" fill="none" stroke="${color}" stroke-width=".45"/>`;
async function composite(clothing,bg){return sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite([{input:face},{input:clothing}]).png().toBuffer()}
(async()=>{
 for(const [bg,suffix]of[['#e7e0d4','light'],['#252b2e','dark']]){
  const a=await composite(source,bg),b=await composite(actual,bg),panels=[];
  for(const [i,img]of[[0,a],[1,b]])panels.push({input:await sharp(img).extract({left:395,top:500,width:250,height:130}).resize({width:1000}).png().toBuffer(),left:i*1000,top:0});
  await sharp({create:{width:2000,height:520,channels:4,background:bg}}).composite(panels).png().toFile(path.join(pack,`qa/clothing01_strict_before_after_${suffix}.png`));
 }
 const panels=[];
 for(const [i,file]of[[0,source],[1,actual]]){
  let marks='';
  const selected=garment.controls.filter(c=>c.id.endsWith('original_tip')||c.id.endsWith('outer_550')||c.id.endsWith('front_v_600'));
  for(const c of selected){marks+=circle(c.target_px,'#53a4ff');if(i)marks+=cross(c.actual_px,'#3fb479');}
  const contour=rig.face.neck_outline.filter(r=>r.y>=525&&r.y<=585);
  for(const side of ['left','right']){
   marks+=`<polyline points="${contour.map(r=>`${r[side]},${r.y}`).join(' ')}" fill="none" stroke="#e77171" stroke-width=".3"/>`;
   for(const c of rig.shared_contact_controls[side])marks+=circle(c.px,c.role==='hidden_coverage_only'?'#bd77e9':'#e34d52',1.8);
  }
  if(i)for(const c of garment.contacts.filter(c=>c.role==='visible_join'))marks+=cross(c.actual_px,'#45ae66',2);
  const base=await composite(file,'#e7e0d4');
  const overlay=await sharp(base).composite([{input:Buffer.from(`<svg width="1024" height="1536">${marks}</svg>`)}]).png().toBuffer();
  panels.push({input:await sharp(overlay).extract({left:395,top:500,width:250,height:130}).resize({width:1000}).png().toBuffer(),left:i*1000,top:42});
 }
 const header=Buffer.from('<svg width="2000" height="42"><rect width="2000" height="42" fill="#e0d9cd"/><text x="18" y="29" font-family="Microsoft YaHei" font-size="21" fill="#28322f">原衣快照 / 冻结的全幅控制点</text><text x="1018" y="29" font-family="Microsoft YaHei" font-size="21" fill="#28322f">严格01 / 实际PNG实测点（未安装，视觉待评价）</text></svg>');
 const footer=Buffer.from(`<svg width="2000" height="115"><rect width="2000" height="115" fill="#e0d9cd"/><text x="18" y="26" font-family="Microsoft YaHei" font-size="18" fill="#28322f">蓝圈=原衣领尖 / 外框y550 / 前V y600；红圈=固定颈侧接口；绿十字=实际PNG；紫圈=隐藏支撑（不挪前V）</text><text x="18" y="54" font-family="Microsoft YaHei" font-size="18" fill="#28322f">顶接合目标 L(442,530) / R(589,530)；侧中目标 L(441,550) / R(589,550)。图中所有点均为1024×1536全幅坐标。</text><text x="18" y="82" font-family="Microsoft YaHei" font-size="18" fill="#28322f">参考裁稿 ROI(395,500,250,130)，显示放大4倍；框架最大残差 ${garment.maximum_preserved_control_residual_px}px，接口最大残差 ${garment.maximum_visible_interface_residual_px}px。仅新增501像素小连接。</text></svg>`);
 await sharp({create:{width:2000,height:677,channels:4,background:'#e0d9cd'}}).composite([{input:header,left:0,top:0},...panels,{input:footer,left:0,top:562}]).png().toFile(path.join(pack,'qa/clothing01_strict_anchor_overlay.png'));
 console.log('Strict before/after and source-target-actual anchor overlays rendered.');
})().catch(e=>{console.error(e);process.exitCode=1});
