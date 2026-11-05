const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const root='E:/stacklands',old='mods/appearance/portrait_male_reference_20261104_v1',out=path.join(root,'mods/appearance/portrait_male_face_match_20261104_v2/qa');
const sha=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const luma=(r,g,b)=>.2126*r+.7152*g+.0722*b;
const sample=(data,x,y,radius=3)=>{const rgb=[0,0,0];let n=0;for(let yy=y-radius;yy<=y+radius;yy++)for(let xx=x-radius;xx<=x+radius;xx++){const k=(yy*1024+xx)*4;for(let c=0;c<3;c++)rgb[c]+=data[k+c];n++;}return rgb.map(v=>+(v/n).toFixed(2));};
const points=[['forehead_mid',517,312],['forehead_left',480,310],['forehead_right',555,310],['left_cheek',460,421],['right_cheek',578,421],['nose_bridge',517,407],['nose_tip',517,447],['nose_left_wing',502,450],['nose_right_wing',532,450],['philtrum',517,466],['mouth_mid',517,482],['mouth_left',490,481],['mouth_right',544,481],['chin',517,521],['neck_mid',517,588],['neck_left',472,558],['neck_right',561,558]];
(async()=>{fs.mkdirSync(out,{recursive:true});const refs=[],active=[],rows=[],montage=[];
for(let i=1;i<=3;i++){const id=String(i).padStart(2,'0'),ref=path.join(root,old,'source/guides/reference_'+id+'_in_canvas.png'),current=path.join(root,old,'preview/style_'+id+'_light.png');
const a=await sharp(ref).ensureAlpha().raw().toBuffer(),b=await sharp(current).ensureAlpha().raw().toBuffer();refs.push(a);active.push(b);
for(let row=0;row<2;row++){const input=await sharp(row?current:ref).extract({left:350,top:285,width:330,height:330}).resize(660,660).png().toBuffer();montage.push({input,left:(i-1)*660,top:row*660});}
rows.push({style:id,patches:points.map(([name,x,y])=>{const reference=sample(a,x,y),current=sample(b,x,y);return{name,x,y,reference_rgb:reference,active_rgb:current,luma_delta:+(luma(...current)-luma(...reference)).toFixed(2)};})});
}
await sharp({create:{width:1980,height:1320,channels:4,background:'#ebe5d9'}}).composite(montage).png().toFile(path.join(out,'face_reference_vs_active.png'));
const face=await sharp(path.join(root,'mods/appearance/ink_three_variants_20261004/male/face_shape/01.png')).ensureAlpha().raw().toBuffer();
const faceAlpha=[];for(const y of [440,450,460,470,480,490,500,510,520,530,540,543]){let left=null,right=null;for(let x=380;x<660;x++)if(face[(y*1024+x)*4+3]>=250){left=left??x;right=x;}faceAlpha.push({y,left,right});}
const report={reference_sha256:sha('C:/Users/34784/AppData/Local/Temp/codex-clipboard-2c1a6159-89a6-44b4-919d-ff18f66c7c1c.png'),alignment:'existing native-size reference guides; style01 +204,+50; style02 +215,+49; style03 +220,+50',montage:'face_reference_vs_active.png: top reference, bottom installed male; columns01/02/03; exact shared crop, 2x display only',patch_radius:3,patches:rows,current_face_alpha_rows:faceAlpha,limitations:['Patch deltas on a feature can reflect shape misregistration, pigment or shading; not a skin-only score.','Existing guides align by prior face landmarks and may retain a few-pixel illustration displacement.','Reference ears and upper lateral jaw can be occluded by hair; no alpha geometry inferred there.']};
fs.writeFileSync(path.join(out,'face_reference_audit_measurements.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report.current_face_alpha_rows));for(const row of rows)console.log(row.style,row.patches.filter(p=>['forehead_mid','left_cheek','nose_bridge','nose_tip','mouth_mid','neck_mid'].includes(p.name)));
})().catch(e=>{console.error(e);process.exitCode=1;});

