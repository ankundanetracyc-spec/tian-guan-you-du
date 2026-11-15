const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..');
const frozen=JSON.parse(fs.readFileSync(path.join(__dirname,'clothing_strict_rig.json'),'utf8'));
const band=JSON.parse(fs.readFileSync(path.join(__dirname,'clothing01_back_v2_registration.json'),'utf8'));
const anchors=[
 {id:'L0',p:[442,530],kind:'neck'},{id:'R0',p:[589,530],kind:'neck'},
 {id:'L1',p:[441,550],kind:'neck'},{id:'R1',p:[589,550],kind:'neck'},
 {id:'L2',p:[436,580],kind:'neck',hidden:true},{id:'R2',p:[595,580],kind:'neck',hidden:true},
 ...band.target_controls.map((p,i)=>({id:'B'+i,p,kind:'rear',hidden:i===1}))
];
const line=(side)=>frozen.face.neck_outline.map(r=>`${r[side]},${r.y}`).join(' ');
let svg='<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1536" viewBox="0 0 1024 1536">';
svg+='<g fill="none" stroke="#ef952e" stroke-width="1.4" stroke-dasharray="3 3"><polyline points="'+line('left')+'"/><polyline points="'+line('right')+'"/></g>';
svg+='<path d="M433 528 Q516 500 600 528" fill="none" stroke="#2a93a9" stroke-width="1.6" stroke-dasharray="4 4"/>';
for(const a of anchors){const[x,y]=a.p,color=a.kind==='neck'?'#f2a13d':'#37a9bc';svg+=`<g stroke="${color}" stroke-width="1.5" fill="none"><circle cx="${x}" cy="${y}" r="4" ${a.hidden?'stroke-dasharray="2 2"':''}/><path d="M${x-6} ${y}H${x+6}M${x} ${y-6}V${y+6}"/></g>`;}
svg+='<g font-family="sans-serif" font-size="13" fill="#314d50" stroke="#f5eee0" stroke-width="3" paint-order="stroke">';
svg+='<text x="170" y="505">NECK / COLLAR RIG</text>';
svg+='<text x="275" y="532">L0 (442,530)</text><text x="608" y="532">R0 (589,530)</text>';
svg+='<text x="275" y="554">L1 (441,550)</text><text x="608" y="554">R1 (589,550)</text>';
svg+='<text x="275" y="585">L2 (436,580)</text><text x="608" y="585">R2 (595,580)</text>';
svg+='<text x="460" y="499">B1 (516,514)</text></g></svg>';
(async()=>{const dir=path.join(pack,'rig');fs.mkdirSync(dir,{recursive:true});fs.writeFileSync(path.join(dir,'male_overlay.svg'),svg);await sharp(Buffer.from(svg)).png().toFile(path.join(dir,'male_overlay.png'));const report={id:'male_neck_collar_overlay_20261104_v4',canvas:[1024,1536],scope:'Current frozen wearer neck and shared rear-collar registration controls; not a claim about all facial variant points',face_sha256:frozen.face.sha256,anchors,rear_registration:'source/clothing01_back_v2_registration.json',source_neck_outline:'source/clothing_strict_rig.json',overlay:'rig/male_overlay.png',sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(dir,'male_overlay.png'))).digest('hex')};fs.writeFileSync(path.join(dir,'neck_anchor_overlay.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({overlay:report.overlay,sha256:report.sha256,anchors:anchors.length}));})().catch(e=>{console.error(e);process.exitCode=1;});
