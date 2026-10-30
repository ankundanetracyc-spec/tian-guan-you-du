module.exports=(scene,art,measurement)=>{
 const src=scene.pixels,out=Buffer.alloc(src.length),grid=measurement.paperGrid;
 for(let y=0;y<1254;y++)for(let x=0;x<1254;x++){
  const i=y*1254+x;
  const gx=Math.max(0,Math.min(5,x/209-.5)),gy=Math.max(0,Math.min(5,y/209-.5)),g0=Math.floor(gx),h0=Math.floor(gy),g1=Math.min(5,g0+1),h1=Math.min(5,h0+1),ax=gx-g0,ay=gy-h0;
  for(let c=0;c<3;c++){
   const color=src[i*3+c];
   const delta=grid[h0][g0][c]*(1-ax)*(1-ay)+grid[h0][g1][c]*ax*(1-ay)+grid[h1][g0][c]*(1-ax)*ay+grid[h1][g1][c]*ax*ay;
   out[i*3+c]=Math.max(0,Math.min(255,Math.round(color+delta)));
  }
 }
 scene.pixels=out;
 return scene;
};
