// Measure original immutable clothing only. No candidate or final raster writes.
const fs=require('fs'),path=require('path'),sharp=require('sharp'),crypto=require('crypto');
const pack=path.resolve(__dirname,'..'),project=path.resolve(__dirname,'../../../..');
const before=path.join(pack,'installation/backup_before/male/clothing');
const sha=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
function bounds(data,y,x0,x1,threshold){let first=-1,last=-1;for(let x=x0;x<=x1;x++)if(data[(y*1024+x)*4+3]>=threshold){if(first<0)first=x;last=x;}return first<0?null:[first,last];}
function measure(data,threshold){
 const sides={};
 for(const [side,x0,x1]of[['left',395,516],['right',518,645]]){
  let top;
  for(let y=480;y<=600;y++){
   const span=bounds(data,y,x0,x1,threshold);
   if(span){top={point:[side==='left'?span[1]:span[0],y],top_row_interval_inclusive:span,method:'First nonempty source-alpha row in collar-side ROI; select the inner-most supported pixel on that actual row.'};break;}
  }
  const rows={};
  for(const y of[530,540,550,565,580,600,620]){
   const span=bounds(data,y,x0,x1,threshold);
   rows[y]=span?{outer:[side==='left'?span[0]:span[1],y],inner:[side==='left'?span[1]:span[0],y],interval_inclusive:span}:null;
  }
  sides[side]={top,rows,original_three_point_edge_scaffold:[top.point,rows[550].inner,rows[600].inner],original_outer_outline_scaffold:[top.point,rows[550].outer,rows[600].outer]};
 }
 const openingRows=[];
 for(let y=600;y<=740;y++){
  const runs=[];let start=-1;
  for(let x=450;x<=600;x++){
   const absent=data[(y*1024+x)*4+3]<threshold;
   if(absent&&start<0)start=x;
   if(!absent&&start>=0){runs.push([start,x-1]);start=-1;}
  }
  if(start>=0)runs.push([start,600]);
  const center=runs.filter(([a,b])=>a>450&&b<600&&(a+b)/2>=500&&(a+b)/2<=550).sort((a,b)=>(b[1]-b[0])-(a[1]-a[0]))[0];
  if(center)openingRows.push({y,opening_interval_inclusive:center,opening_width:center[1]-center[0]+1,center_x:(center[0]+center[1])/2,cloth_contact_left:[center[0]-1,y],cloth_contact_right:[center[1]+1,y]});
 }
 const last=openingRows.at(-1);
 return {alpha_threshold:threshold,sides,front_v:{meaning:'Alpha opening between original inner collar edges, not painted seam/embroidery V.',last_open_row:last??null,closure_marker:last?[(last.opening_interval_inclusive[0]+last.opening_interval_inclusive[1])/2,last.y+1]:null,closure_marker_is_inferred:true,measured_rows:openingRows.filter(row=>row.y%10===0||row===last)}};
}
async function main(){
 const all=[];
 for(const id of['04','05','06','07']){
  const file=path.join(before,id+'.png');
  const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(info.width!==1024||info.height!==1536)throw Error('Original canvas changed');
  const r={garment_index:Number(id),source:'res://'+path.relative(project,file).replaceAll('\\','/'),source_sha256:sha(file),canvas:[1024,1536],coordinate_space:'Full original top-left-origin canvas, integer supported pixels unless explicitly marked inferred.',scope:'Read-only original tip/side/outer/frame/V measurements. No target rig, generated raster registration or final acceptance is implied.',geometry_must_remain:'Original collar tip height, original outer shape and original front V; only tiny inward connection and 1-2px fringe cleanup may be proposed later.',measurements:[measure(data,230),measure(data,40)]};
  fs.writeFileSync(path.join(__dirname,'clothing'+id+'_original_points.json'),JSON.stringify(r,null,2)+'\n');
  all.push(r);
 }
 fs.writeFileSync(path.join(pack,'qa/clothing04_07_original_points.json'),JSON.stringify(all,null,2)+'\n');
 console.log(JSON.stringify(all.map(r=>({id:r.garment_index,L:r.measurements[0].sides.left.original_three_point_edge_scaffold,R:r.measurements[0].sides.right.original_three_point_edge_scaffold,V:r.measurements[0].front_v.last_open_row})),null,2));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
