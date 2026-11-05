// Freeze targets from the untouched snapshot and current face, before any new candidate.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const snapshot=path.join(pack,'installation/backup_before');
const resource=f=>'res://'+path.relative(project,f).replaceAll('\\','/');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
function bounds(d,y,lo,hi,t){let a=-1,b=-1;for(let x=lo;x<=hi;x++)if(d[(y*1024+x)*4+3]>=t){if(a<0)a=x;b=x;}return a<0?null:[a,b]}
function top(d,side,t){for(let y=490;y<620;y++){const b=bounds(d,y,side==='left'?350:518,side==='left'?516:685,t);if(b)return {point:[side==='left'?b[1]:b[0],y],interval:b};}throw Error('Missing collar tip');}
(async()=>{
 const file=path.join(__dirname,'clothing_strict_rig.json');
 if(fs.existsSync(file))throw Error('Frozen rig exists; do not overwrite targets from a later state.');
 const faceFile=path.join(snapshot,'male/face_shape/01.png'),face=await sharp(faceFile).ensureAlpha().raw().toBuffer();
 const currentFace=path.join(project,'mods/appearance/ink_three_variants_20261004/male/face_shape/01.png');
 if(hash(faceFile)!==hash(currentFace))throw Error('Face differs from frozen snapshot');
 const neck=[];for(let y=510;y<=600;y++){const b=bounds(face,y,350,685,230);neck.push({y,left:b[0],right:b[1]});}
 const garments=[];
 for(let id=1;id<=7;id++){
  const tag=String(id).padStart(2,'0'),src=path.join(snapshot,`male/clothing/${tag}.png`),d=await sharp(src).ensureAlpha().raw().toBuffer();
  const controls=[],rows=[];
  for(const side of ['left','right']){
   const tip=top(d,side,40),core=top(d,side,230);
   controls.push({id:side+'_original_tip',role:'preserved_original_tip',side,alpha_threshold:40,detector:'first_side_row_inner_pixel',px:tip.point});
   for(const y of [540,550,570,600]){
    const b=bounds(d,y,side==='left'?350:518,side==='left'?516:685,230);
    controls.push({id:`${side}_outer_${y}`,role:'preserved_outer_frame',side,alpha_threshold:230,detector:'row_outer_pixel',px:[side==='left'?b[0]:b[1],y]});
   }
   for(const y of [580,600,620]){
    const b=bounds(d,y,side==='left'?350:518,side==='left'?516:685,230);
    controls.push({id:`${side}_front_v_${y}`,role:'preserved_front_v',side,alpha_threshold:230,detector:'row_inner_pixel',px:[side==='left'?b[1]:b[0],y]});
   }
   rows.push({side,original_alpha40_tip:tip,original_alpha230_tip:core});
  }
  const frozen=[];for(let y=510;y<=620;y++)frozen.push({y,left40:bounds(d,y,350,516,40),right40:bounds(d,y,518,685,40),left230:bounds(d,y,350,516,230),right230:bounds(d,y,518,685,230)});
  garments.push({index:id,snapshot_source:resource(src),snapshot_sha256:hash(src),canvas:[1024,1536],source_to_final_required:{scale:1,rotation_degrees:0,translation:[0,0],explanation:'Identity frame of original preserved garment; connector artwork has separately recorded crop placement.'},preserved_controls:controls,original_tips:rows,frozen_collar_rows:frozen,provenance: id>=4?`source/clothing${tag}_original_points.json`:'snapshot direct alpha contour measurement'});
 }
 const rig={schema_version:1,id:'male_neck_collar_strict_snapshot_20261104_v4',status:'frozen_targets_not_visual_approval',canvas:[1024,1536],origin:[0,0],coordinates:'Full-canvas pixel coordinates; no alpha-bbox center is used as an anchor.',maximum_control_residual_px:2,face:{snapshot_source:resource(faceFile),active_resource:resource(currentFace),sha256:hash(faceFile),alpha_threshold:230,neck_outline:neck},shared_contact_controls:{left:[{id:'left_neck_top',px:[442,530],role:'visible_join'},{id:'left_neck_mid',px:[441,550],role:'visible_join'},{id:'left_neck_lower',px:[436,580],role:'hidden_coverage_only'}],right:[{id:'right_neck_top',px:[589,530],role:'visible_join'},{id:'right_neck_mid',px:[589,550],role:'visible_join'},{id:'right_neck_lower',px:[595,580],role:'hidden_coverage_only'}]},rules:{all_seven_candidates_required_by_default:true,original_opaque_rgba_preserved:true,body_y_ge620_rgba_preserved:true,connector_y_range:[520,570],visible_join_coverage_y:[530,565],minimum_composed_join_alpha:230,tip_to_top_join:'Short sloped connector top from preserved original alpha40 tip to fixed neck-top join; do not turn the whole standing collar into a new rolled shape.',hidden_lower_control:'Coverage only. Never move the front V to the hidden skin silhouette.',source_landmarks:'Preserved original garment controls are actual snapshot-supported pixels. AI prompt coordinates are not measured landmarks.',local_warp:false,synthetic_texture:false},garments};
 fs.writeFileSync(file,JSON.stringify(rig,null,2)+'\n');
 console.log('Frozen strict rig: face hash + 7 snapshot garments, 16 preserved controls each.');
})().catch(e=>{console.error(e);process.exitCode=1;});
