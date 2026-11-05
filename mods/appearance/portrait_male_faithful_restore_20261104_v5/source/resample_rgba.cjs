const sharp=require('sharp');
// Whole-layer similarity sampling. At reduction, integrate source pixel areas in
// premultiplied colour; four-point interpolation alone aliases fine hair/ink.
async function render(input,output,t){
 const{data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true}),W=info.width,H=info.height,stride=W+1,out=Buffer.alloc(1024*1536*4),det=t.a*t.a+t.b*t.b,scale=Math.sqrt(det);
 const planes=Array.from({length:4},()=>new Float64Array((W+1)*(H+1)));
 for(let y=0;y<H;y++){let row=[0,0,0,0];for(let x=0;x<W;x++){const q=(y*W+x)*4,p=(y+1)*stride+x+1,A=data[q+3];for(let k=0;k<4;k++){row[k]+=k===3?A:data[q+k]*A;planes[k][p]=planes[k][p-stride]+row[k];}}}
 function integral(p,x,y){x=Math.max(0,Math.min(W,x));y=Math.max(0,Math.min(H,y));const ix=Math.min(W-1,Math.floor(x)),iy=Math.min(H-1,Math.floor(y)),fx=x-ix,fy=y-iy,j=iy*stride+ix;return p[j]*(1-fx)*(1-fy)+p[j+1]*fx*(1-fy)+p[j+stride]*(1-fx)*fy+p[j+stride+1]*fx*fy;}
 const footprint=Math.max(1,1/scale),half=footprint/2,area=footprint*footprint;
 for(let y=0;y<1536;y++)for(let x=0;x<1024;x++){
  const u=(t.a*(x-t.tx)+t.b*(y-t.ty))/det,v=(-t.b*(x-t.tx)+t.a*(y-t.ty))/det;
  if(u+half<0||v+half<0||u-half>=W||v-half>=H)continue;
  const x0=u+.5-half,x1=u+.5+half,y0=v+.5-half,y1=v+.5+half,vals=planes.map(p=>integral(p,x1,y1)-integral(p,x0,y1)-integral(p,x1,y0)+integral(p,x0,y0)),q=(y*1024+x)*4;
  out[q+3]=Math.max(0,Math.min(255,Math.round(vals[3]/area)));if(vals[3]>.00001)for(let k=0;k<3;k++)out[q+k]=Math.max(0,Math.min(255,Math.round(vals[k]/vals[3])));
 }
 await sharp(out,{raw:{width:1024,height:1536,channels:4}}).png().toFile(output);
}
module.exports={render};
