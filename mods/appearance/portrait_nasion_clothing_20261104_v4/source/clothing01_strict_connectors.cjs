// Original robe remains the frame and artwork. Add only two tiny new cloth connectors.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const rigFile=path.join(__dirname,'clothing_strict_rig.json'),rig=JSON.parse(fs.readFileSync(rigFile,'utf8'));
const frozen=rig.garments.find(g=>g.index===1),resolveRes=f=>path.join(project,f.slice(6));
const source=resolveRes(frozen.snapshot_source),faceFile=resolveRes(rig.face.snapshot_source);
const artFile=path.join(__dirname,'clothing01_selected_raw.png'),outputFile=path.join(pack,'male/clothing/01.png');
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
(async()=>{
 if(hash(source)!==frozen.snapshot_sha256||hash(faceFile)!==rig.face.sha256)throw Error('Frozen source changed');
 const old=await sharp(source).ensureAlpha().raw().toBuffer(),out=Buffer.from(old);
 const meta=await sharp(artFile).metadata();
 const paint=await sharp(artFile).resize({width:250}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 if(paint.info.height!==130)throw Error('Native crop needs a new uniform registration, not a nonuniform fit');
 const actualTip=Object.fromEntries(frozen.original_tips.map(t=>[t.side,t.original_alpha40_tip.point]));
 const placed=[];
 for(let y=520;y<=570;y++){
  const sr=frozen.frozen_collar_rows.find(r=>r.y===y),neck=rig.face.neck_outline.find(r=>r.y===y);if(!sr||!neck)continue;
  for(const side of ['left','right']){
   const isLeft=side==='left',interval=isLeft?sr.left40:sr.right40;if(!interval)continue;
   const tip=actualTip[side],targetTop=rig.shared_contact_controls[side][0].px;
   const core=isLeft?sr.left230:sr.right230;
   const oldInner=isLeft?(core?.[1]??interval[1]):(core?.[0]??interval[0]);
   const desiredInner=isLeft?neck.left+1:neck.right-1;
   if(isLeft?oldInner>=desiredInner:oldInner<=desiredInner)continue;
   const lo=isLeft?interval[1]-1:desiredInner-1,hi=isLeft?desiredInner+1:interval[0]+1;
   for(let x=lo;x<=hi;x++){
    const k=(y*1024+x)*4;if(old[k+3]>=230)continue;
    // Short diagonal upper edge joins the existing pointed tip to the new neck.
    // It does not replace the original rounded/rolled collar surface.
    const ratio=Math.max(0,Math.min(1,(x-tip[0])/(targetTop[0]-tip[0])));
    const topY=tip[1]+(targetTop[1]-tip[1])*ratio;
    const topWeight=smooth((y+.5-topY)/.85);
    const innerWeight=isLeft?smooth((desiredInner+.75-x)/1):smooth((x-desiredInner+.75)/1);
    const weight=topWeight*innerWeight;
    if(weight<=0)continue;
    // Each retained artwork piece has one integer placement; no stretch/clone texture.
    const dx=isLeft?1:0,px=x-395-dx,py=y-500;
    if(px<0||px>=250||py<0||py>=130)continue;
    const s=(py*250+px)*4,a1=paint.data[s+3]/255*weight;if(a1<=0)continue;
    const a0=old[k+3]/255,a=a1+a0*(1-a1);
    for(let c=0;c<3;c++)out[k+c]=Math.round((paint.data[s+c]*a1+old[k+c]*a0*(1-a1))/a);
    out[k+3]=Math.round(a*255);
    if(out[k+3]===0)for(let c=0;c<4;c++)out[k+c]=old[k+c];
   }
  }
 }
 let changed=0,bbox=[1024,1536,-1,-1],coreChanges=0,bodyChanges=0;
 for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){const k=(y*1024+x)*4;if([0,1,2,3].some(c=>out[k+c]!==old[k+c])){changed++;bbox=[Math.min(bbox[0],x),Math.min(bbox[1],y),Math.max(bbox[2],x),Math.max(bbox[3],y)];if(old[k+3]>=230)coreChanges++;if(y>=620)bodyChanges++;}}
 if(coreChanges||bodyChanges)throw Error('Preserved original garment changed');
 fs.mkdirSync(path.dirname(outputFile),{recursive:true});
 await sharp(out,{raw:{width:1024,height:1536,channels:4}}).png().toFile(outputFile);
 const registration={index:1,status:'strict_geometry_candidate_awaiting_user_visual_review',snapshot_source:frozen.snapshot_source,snapshot_sha256:frozen.snapshot_sha256,face_sha256:rig.face.sha256,rig_sha256:hash(rigFile),output:'male/clothing/01.png',output_sha256:hash(outputFile),original_garment_frame:{scale:1,rotation_degrees:0,translation:[0,0],provenance:'Unchanged pre-edit snapshot body, outer collar and front V supply actual noncollinear preserved controls.'},ai_art_source:'res://'+path.relative(project,artFile).replaceAll('\\','/'),ai_art_sha256:hash(artFile),ai_art_raw_size:[meta.width,meta.height],ai_art_uniform_scale:250/meta.width,ai_art_reference_crop_origin:[395,500],ai_art_piece_translations:{left:[1,0],right:[0,0]},operation:'Use retained native AI-painted pigment only in the two tiny missing connector supports. Preserve all original alpha>=230 RGBA. Diagonal tip-to-neck top selection, no full rolled-collar replacement, no source pixel warp and no synthetic texture.',changed_pixels:changed,changed_bbox_inclusive:bbox,original_opaque_changed_pixels:coreChanges,y_ge620_changed_pixels:bodyChanges};
 fs.writeFileSync(path.join(__dirname,'clothing01_strict_registration.json'),JSON.stringify(registration,null,2)+'\n');
 console.log(JSON.stringify(registration));
})().catch(e=>{console.error(e);process.exitCode=1;});
