async (page) => {
  const results = [];
  const check = (ok, label) => { results.push({label,ok}); if(!ok) throw Error(label); };
  await page.waitForFunction(() => window.jointPreview?.ready);
  check(await page.evaluate(() => jointPreview.sites.length === 2), 'both sites loaded');
  for (let i=0;i<2;i++) {
    const points=await page.evaluate(i=>{
      const e=jointPreview.sites[i],r=e.scene.getBoundingClientRect(),[x,y,w,h]=e.site.maskRect;
      let opaque,clear;
      for(let yy=0;yy<h;yy++)for(let xx=0;xx<w;xx++){
        const a=e.pixels[(yy*w+xx)*4+3];
        if(!opaque&&a>250&&yy>h/2)opaque=[x+xx+.5,y+yy+.5];
        if(!clear&&a===0)clear=[x+xx+.5,y+yy+.5];
      }
      const screen=p=>({x:r.left+p[0]*r.width/1254,y:r.top+p[1]*r.height/1254});
      return {opaque:screen(opaque),clear:screen(clear),name:e.site.name};
    },i);
    await page.mouse.move(points.opaque.x,points.opaque.y);
    check(await page.evaluate(i=>jointPreview.sites[i].hover,i), 'opaque building hover '+i);
    await page.mouse.click(points.opaque.x,points.opaque.y);
    check((await page.locator('#selection').textContent()).includes(points.name), 'actual building click '+i);
    check(await page.evaluate(i=>{
      const e=jointPreview.sites[i],p=e.scene.querySelector('canvas').getContext('2d').getImageData(0,0,1254,1254).data;
      const [x,y,w,h]=e.site.maskRect;
      for(let yy=0;yy<1254;yy++)for(let xx=0;xx<1254;xx++)if((xx<x||xx>=x+w||yy<y||yy>=y+h)&&p[(yy*1254+xx)*4+3])return false;
      return true;
    },i), 'highlight cannot spill to terrain '+i);
    if(i===0)await page.screenshot({path:'output/playwright/joint-sites-20261029/hover.png'});
    await page.mouse.move(points.clear.x,points.clear.y);
    check(!await page.evaluate(i=>jointPreview.sites[i].hover,i), 'transparent area does not hover '+i);
    check(await page.evaluate(i=>{
      const p=jointPreview.sites[i].scene.querySelector('canvas').getContext('2d').getImageData(0,0,1254,1254).data;
      for(let n=3;n<p.length;n+=4)if(p[n])return false;return true;
    },i),'normal overlay completely empty '+i);
  }
  await page.getByRole('button',{name:'查看完整局部',exact:true}).click();
  check(await page.evaluate(()=>jointPreview.fit&&jointPreview.sites.every(e=>e.scene.getBoundingClientRect().width<1254)),'fit view works');
  await page.getByRole('button',{name:'原像素检查',exact:true}).click();
  check(await page.evaluate(()=>!jointPreview.fit&&jointPreview.sites.every(e=>e.scene.getBoundingClientRect().width===1254)),'native view is 1:1');
  await page.mouse.move(20,20);
  await page.screenshot({path:'output/playwright/joint-sites-20261029/normal.png'});
  return {checks:results.length,failures:results.filter(r=>!r.ok),results};
}
