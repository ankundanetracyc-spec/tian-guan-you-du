// Technical image stitching: choose a low-ink seam through each overlap; never redraw scenery.
const clamp=v=>Math.max(0,Math.min(1,v));
function seams(scenes,sites,base){
 const byCell=new Map(scenes.map((s,i)=>[s.row*6+s.col,{...s,index:i}]));
 function pixel(p,x,y){if(!p)return null;const u=x-p.worldOrigin[0],v=y-p.worldOrigin[1];if(u<0||v<0||u>=1254||v>=1254)return null;return ((v*1254+u)*3);}
 function solve(axis,boundary){
  const center=boundary*1024,width=201,back=new Int16Array(6144*width),out=new Int32Array(6144);let prev=new Float64Array(width),next=new Float64Array(width);
  const protectedSites=sites.filter(s=>{const p=scenes.find(p=>p.id===s.patch);s._row=p.row;s._col=p.col;return Math.abs(s.rect[axis]-center)<300||Math.abs(s.rect[axis]+s.rect[axis+2]-center)<300;});
  for(let along=0;along<6144;along++){
   const cell=Math.min(5,Math.floor(along/1024)),a=byCell.get(axis===0?cell*6+boundary-1:(boundary-1)*6+cell),b=byCell.get(axis===0?cell*6+boundary:boundary*6+cell);
   for(let k=0;k<width;k++){
    const across=center-100+k,x=axis===0?across:along,y=axis===0?along:across,ia=pixel(a,x,y),ib=pixel(b,x,y);
    let cost=Math.abs(k-100)*0.008;
    const pa=ia===null?base:a.pixels,pb=ib===null?base:b.pixels,ja=ia===null?(y*6144+x)*3:ia,jb=ib===null?(y*6144+x)*3:ib;
    let difference=0;for(let c=0;c<3;c++)difference+=Math.abs(pa[ja+c]-pb[jb+c]);
    const ink=Math.min(pa[ja]*.299+pa[ja+1]*.587+pa[ja+2]*.114,pb[jb]*.299+pb[jb+1]*.587+pb[jb+2]*.114);
    cost+=difference*.10;if(ink<160)cost+=30+(160-ink)*1.5;
    for(const s of protectedSites){const r=s.rect,orth=1-axis;if(along<r[orth]-24||along>r[orth]+r[orth+2]+24)continue;const own=axis===0?s._col:s._row;if(own===boundary-1&&across<r[axis]+r[axis+2]+24)cost+=1e7;if(own===boundary&&across>r[axis]-24)cost+=1e7;}
    let best=along===0?0:prev[k],from=k;
    if(along)for(let d=-2;d<=2;d++){const q=k+d;if(q>=0&&q<width&&prev[q]+Math.abs(d)*.35<best){best=prev[q]+Math.abs(d)*.35;from=q;}}
    next[k]=cost+best;back[along*width+k]=from;
   }
   [prev,next]=[next,prev];
  }
  let k=0;for(let j=1;j<width;j++)if(prev[j]<prev[k])k=j;
  for(let along=6143;along>=0;along--){out[along]=center-100+k;k=back[along*width+k];}
  return out;
 }
 const vertical=Array.from({length:5},(_,i)=>solve(0,i+1)),horizontal=Array.from({length:5},(_,i)=>solve(1,i+1));
 const owners=new Uint8Array(6144*6144);owners.fill(255);
 for(let y=0;y<6144;y++)for(let x=0;x<6144;x++){let c=0,r=0;while(c<5&&x>=vertical[c][y])c++;while(r<5&&y>=horizontal[r][x])r++;const p=byCell.get(r*6+c);if(p&&pixel(p,x,y)!==null)owners[y*6144+x]=p.index;}
 for(const s of sites){const p=scenes.findIndex(p=>p.id===s.patch),r=s.rect;for(let y=Math.max(0,r[1]-35);y<Math.min(6144,r[1]+r[3]+35);y++)for(let x=Math.max(0,r[0]-35);x<Math.min(6144,r[0]+r[2]+35);x++)owners[y*6144+x]=p;}
 return {owners,vertical:vertical.map(v=>Array.from(v)),horizontal:horizontal.map(v=>Array.from(v))};
}
module.exports=seams;
