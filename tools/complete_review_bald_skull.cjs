// Complete the hidden cranium on an independent skin layer. The original
// forehead/face texture supplies every painted pixel; no portrait extraction.
const { PNG } = require('pngjs');

const W = 1024, H = 1536, CX = 512;

function smoothStep(a, b, v) {
  const t = Math.max(0, Math.min(1, (v-a)/(b-a)));
  return t*t*(3-2*t);
}

function limits(image, y) {
  let left = W, right = -1;
  for (let x=350; x<=674; x++) {
    if (image.data[(y*W+x)*4+3] >= 128) {
      left = Math.min(left,x); right = Math.max(right,x);
    }
  }
  return right >= left ? {left,right,radius:(right-left)/2} : null;
}

function sample(image, x, y) {
  const x0=Math.max(0,Math.min(W-2,Math.floor(x)));
  const y0=Math.max(0,Math.min(H-2,Math.floor(y)));
  const tx=x-x0, ty=y-y0;
  const output=[0,0,0,0];
  for(let dy=0;dy<=1;dy++) for(let dx=0;dx<=1;dx++) {
    const k=((y0+dy)*W+x0+dx)*4;
    const weight=(dx?tx:1-tx)*(dy?ty:1-ty);
    const a=image.data[k+3]/255;
    for(let c=0;c<3;c++) output[c]+=image.data[k+c]*weight*a;
    output[3]+=weight*a;
  }
  if(output[3]>0) for(let c=0;c<3;c++) output[c]/=output[3];
  output[3]*=255;
  return output;
}

function radiusAt({top,crownY,maxRadius,join,joinRadius,lowerRadii},y) {
  if(y<=crownY) {
    const d=(y-crownY)/(crownY-top);
    return maxRadius*Math.sqrt(Math.max(0,1-d*d));
  }
  function tangent(i) {
    if(i===0||i===lowerRadii.length-1)return 0;
    const [aY,aR]=lowerRadii[i-1], [bY,bR]=lowerRadii[i], [cY,cR]=lowerRadii[i+1];
    const before=(bR-aR)/(bY-aY),after=(cR-bR)/(cY-bY);
    if(before*after<=0)return 0;
    return Math.sign(before)*Math.min(Math.abs((before+after)/2),3*Math.min(Math.abs(before),Math.abs(after)));
  }
  for(let i=1;i<lowerRadii.length;i++){
    const [y0,r0]=lowerRadii[i-1], [y1,r1]=lowerRadii[i];
    if(y<=y1){
      const h=y1-y0,t=(y-y0)/h,t2=t*t,t3=t2*t;
      return (2*t3-3*t2+1)*r0+(t3-2*t2+t)*h*tangent(i-1)
        +(-2*t3+3*t2)*r1+(t3-t2)*h*tangent(i);
    }
  }
  return lowerRadii.at(-1)[1];
}

function completeBaldSkull(face, {top, sourceTop, crownY, maxRadius, join=380, joinRadius, lowerRadii}) {
  if(face.width!==W||face.height!==H) throw Error('Unexpected portrait canvas');
  const result=PNG.sync.read(PNG.sync.write(face));
  const shape={top,crownY,maxRadius,join,joinRadius,lowerRadii};
  const end=lowerRadii.at(-1)[0];
  // When the rebuilt crown starts lower than the historic clipped layer,
  // remove that old cap entirely; otherwise it floats above the new skull.
  for(let y=0;y<top;y++)for(let x=350;x<=674;x++)result.data[(y*W+x)*4+3]=0;
  const sourceRows=[];
  for(let y=0;y<=end;y++) sourceRows[y]=limits(face,y);
  for(let y=top;y<end;y++) {
    const sy=y<join?sourceTop+(y-top)*(join-sourceTop)/(join-top):y;
    const ylo=Math.max(sourceTop,Math.min(end,Math.round(sy)));
    const sourceRadius=sourceRows[ylo]?.radius || 1;
    const targetRadius=radiusAt(shape,y);
    for(let x=Math.floor(CX-targetRadius-2);x<=Math.ceil(CX+targetRadius+2);x++) {
      const k=(y*W+x)*4;
      const coverage=Math.max(0,Math.min(1,targetRadius-Math.abs(x-CX)+.5));
      if(!coverage) { result.data[k+3]=0; continue; }
      const sx=CX+(x-CX)*sourceRadius/Math.max(1,targetRadius);
      const rgba=sample(face,sx,sy);
      for(let c=0;c<3;c++) result.data[k+c]=Math.max(0,Math.min(255,Math.round(rgba[c])));
      result.data[k+3]=Math.round(coverage*255);
    }
  }
  // Remove any old forehead contour outside the new oval in the repainted rows.
  for(let y=top;y<end;y++) {
    const radius=radiusAt(shape,y);
    for(let x=350;x<=674;x++) if(Math.abs(x-CX)>radius+.5) result.data[(y*W+x)*4+3]=0;
  }
  return result;
}

module.exports={completeBaldSkull};
