const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const R=path.resolve(__dirname,'..'),M=path.resolve(R,'../../male_whole_01.png'),W=1024,H=1536,sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
(async()=>{const master=await sharp(M).ensureAlpha().raw().toBuffer(),registration=JSON.parse(fs.readFileSync(path.join(R,'source/registration.json'))),records=[];
for(const part of['eyebrows','eyes','nose','mouth']){const p=path.join(R,'male',part,'01.png'),native=path.join(R,'male_native',part,'01.png');fs.mkdirSync(path.dirname(native),{recursive:true});if(!fs.existsSync(native))fs.copyFileSync(p,native);const data=await sharp(native).ensureAlpha().raw().toBuffer();let visible=0,wrong=0;
const support={eyebrows:[[441,336,499,352],[541,334,603,350]],eyes:[[440,356,495,375],[544,356,600,375]],nose:[],mouth:[[484,471,556,479]]}[part];
// Recover the approved image's fine ink strokes wherever transparent extraction softened them.
// This changes opacity only in the anatomical feature's own narrow local stroke region.
const hidden=x=>(part==='eyebrows'&&(x<456||x>590))||(part==='eyes'&&(x<450||x>594));
for(const[x0,y0,x1,y1]of support)for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++){if(hidden(x))continue;const k=(y*W+x)*4,l=.2126*master[k]+.7152*master[k+1]+.0722*master[k+2];if(l<145)data[k+3]=255;else if(l<175)data[k+3]=Math.max(data[k+3],Math.round((175-l)/30*255));}
let hidden_reconstruction=0;
for(let k=0;k<data.length;k+=4){if(data[k+3]>0){visible++;if(hidden((k/4)%W)){hidden_reconstruction++;continue;}for(let c=0;c<3;c++)data[k+c]=master[k+c];}else data[k]=data[k+1]=data[k+2]=0;}
await sharp(data,{raw:{width:W,height:H,channels:4}}).png().toFile(p);for(let k=0;k<data.length;k+=4)if(data[k+3]>0&&!hidden((k/4)%W))for(let c=0;c<3;c++)if(data[k+c]!==master[k+c])wrong++;
if(wrong)throw Error('visible RGB drift');records.push({...registration.records.find(r=>r.part===part),native_registered_file:path.relative(R,native),sha256:sha(p),visible_pixel_count:visible,visible_rgb_source:'Approved male_whole_01.png at identical full-canvas coordinates except original hair-occluded brow/eye tips',visible_rgb_difference_count:wrong,hidden_tip_reconstruction_pixels:hidden_reconstruction,hidden_tip_reconstruction:'Native generated tiny tips under original front-hair occlusion, to prevent baking crossing hair fragments into facial pieces',rgb_bias_to_approved_master:null,alpha_source:'Built-in image_gen transparent extraction, whole-feature uniform scale and translation; chromatic alpha noise removed',final_pixels_mode:'Approved visible RGB with generated registered alpha; only hair-hidden tips reconstructed; no old/rejected facial pixels'});
}
fs.writeFileSync(path.join(R,'source/final_manifest.json'),JSON.stringify({master_sha256:sha(M),canvas:[W,H],layer_order:['nose','eyes','eyebrows','mouth'],user_approved_master:true,head_neck_changed:false,approved_ink_strokes_preserved_in_narrow_anatomical_regions:true,records},null,2));console.log('Selected four approved-source RGB pieces with generated alpha; visible pixel RGB differences:0');
})().catch(e=>{console.error(e);process.exitCode=1});
