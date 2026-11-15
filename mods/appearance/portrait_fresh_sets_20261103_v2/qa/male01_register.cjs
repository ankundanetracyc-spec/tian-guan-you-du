/* Whole-layer registration only: native pigment/alpha are not extracted or repainted. */
const fs=require('node:fs/promises'),path=require('node:path'),sharp=require('sharp'),crypto=require('node:crypto');
const ROOT=path.resolve(__dirname,'..'),RAW=path.join(ROOT,'source/male/raw'),OUT=path.join(ROOT,'male');
const definitions={
 eyebrows:{scale:123/(582-441),sourceOrigin:[511.5,389.65],targetOrigin:[517.5,345.5],landmarks:[{name:'left_brow_pigment_axis',source:[441,389.72],target:[456,346]},{name:'right_brow_pigment_axis',source:[582,389.57],target:[579,345]}],note:'眉轴依据左右眉实际深色毛流中心；非透明包围盒中心。'},
 eyes:{scale:105/(585-439),sourceOrigin:[512,436],targetOrigin:[516.5,374.5],landmarks:[{name:'left_iris_center',source:[439,436],target:[464,375]},{name:'right_iris_center',source:[585,436],target:[569,374]}],note:'虹膜中心按器官圆形目视测量，估计误差约2px；保持双眼整层等比，不单眼移动。'},
 nose:{scale:40/(650-555),sourceOrigin:[513,650],targetOrigin:[517,449],landmarks:[{name:'nasal_bridge_midline',source:[513,555],target:[517,409]},{name:'nasal_tip_bulb_center',source:[513,650],target:[517,449]}],note:'鼻梁中段与鼻头球体中心定位；保留完整鼻梁鼻翼与原生局部晕染，鼻底不是鼻头球体中心。'},
 mouth:{scale:60/108,sourceOrigin:[512,727],targetOrigin:[517,481],landmarks:[{name:'lip_fissure_center',source:[512,727],target:[517,481]},{name:'left_lip_corner',source:[458,725],target:[487,480]},{name:'right_lip_corner',source:[566,725],target:[547,480]}],note:'唇缝中心与两侧唇角定位，原生高光保留，无脚本删白。'},
 hair_front:{scale:1,sourceOrigin:[514,271],targetOrigin:[517,277],landmarks:[{name:'center_hairline',source:[514,271],target:[517,277]}],note:'中央发际实际Alpha180内缘271，整层平移(+3,+6)。不同发型左右转折非同形，不声称三点认证。'},
 hair_back:{scale:1,sourceOrigin:[514,138],targetOrigin:[517,151],landmarks:[{name:'rear_crown_axis',source:[514,138],target:[517,151]}],note:'后发顶点整体移入前发轮廓内部；完整隐藏连接原样保留。'}
};
const order=['hair_back','face_shape','clothing','eyes','eyebrows','nose','mouth','face_accessory','hair_front'];
(async()=>{const records=[];for(const[part,d]of Object.entries(definitions)){
 const source=path.join(RAW,part,'01.png'),target=path.join(OUT,part,'01.png'),buffer=await fs.readFile(source),meta=await sharp(buffer).metadata();if(meta.width!==1024||meta.height!==1536||!meta.hasAlpha||meta.channels!==4)throw new Error('Source format '+part);
 const tx=d.targetOrigin[0]-d.scale*d.sourceOrigin[0],ty=d.targetOrigin[1]-d.scale*d.sourceOrigin[1],sw=Math.round(1024*d.scale),sh=Math.round(1536*d.scale),left=Math.round(tx),top=Math.round(ty);
 const scaled=await sharp(buffer).resize(sw,sh,{fit:'fill',kernel:'lanczos3'}).png().toBuffer();
 if(left<0||top<0)throw new Error('Negative whole-canvas placement requires explicit padded export');
 const output=await sharp({create:{width:Math.max(1024,sw+left),height:Math.max(1536,sh+top),channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:scaled,left,top,blend:'over'}]).extract({left:0,top:0,width:1024,height:1536}).png().toBuffer();await fs.mkdir(path.dirname(target),{recursive:true});await fs.writeFile(target,output);
 const actualScale=[sw/1024,sh/1536],landmarks=d.landmarks.map(p=>{const mapped=[p.source[0]*actualScale[0]+left,p.source[1]*actualScale[1]+top];return{...p,mapped,residual_px:Math.hypot(mapped[0]-p.target[0],mapped[1]-p.target[1])}});
 records.push({part,source:path.relative(ROOT,source).replaceAll('\\','/'),output:path.relative(ROOT,target).replaceAll('\\','/'),source_sha256:crypto.createHash('sha256').update(buffer).digest('hex'),output_sha256:crypto.createHash('sha256').update(output).digest('hex'),transform:{type:'single_whole_layer_uniform_scale_and_translation',requested_uniform_scale:d.scale,rounded_canvas_size:[sw,sh],translation_px:[left,top],rotation:0,local_warp:false,native_alpha_retained:true,pigment_repainted:false,threshold_matting:false},landmarks,note:d.note,certification:'Visual landmarks only; no independent three non-collinear control-point certification asserted'});
 }
 const images=await Promise.all(order.map(part=>fs.readFile(path.join(OUT,part,part==='face_shape'||part==='clothing'||part==='face_accessory'?'':'01.png').replace(/[\\/]$/,'' )+(part==='face_shape'||part==='clothing'||part==='face_accessory'?'.png':''))));
 for(const[name,bg]of[['composite','#eae4d8'],['composite_dark','#252b30']])await sharp({create:{width:1024,height:1536,channels:4,background:bg}}).composite(images.map(input=>({input,left:0,top:0,blend:'over'}))).png().toFile(path.join(ROOT,'qa','male01_'+name+'.png'));
 const composite=path.join(ROOT,'qa/male01_composite.png');await sharp(composite).extract({left:330,top:120,width:380,height:510}).resize(760,1020).png().toFile(path.join(ROOT,'qa/male01_head.png'));
 await fs.writeFile(path.join(ROOT,'source/male/registration_01.json'),JSON.stringify({canvas:[1024,1536],set:1,method:'Whole-layer scale/translation; actual anatomical landmarks; no extraction, color change, or local deformation',certification:'not asserted',records},null,2));console.log(JSON.stringify(records.map(r=>({part:r.part,transform:r.transform,landmarks:r.landmarks}))));
})().catch(e=>{console.error(e);process.exitCode=1});
