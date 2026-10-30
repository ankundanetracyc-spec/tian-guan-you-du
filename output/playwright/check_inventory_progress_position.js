async page => {
 await page.setViewportSize({width:1920,height:1080});
 const row=page.getByRole('button',{name:'灵石，12件',exact:true});
 for(let i=0;i<4;i++)await row.dblclick();
 await page.mouse.move(1850,100);
 const results=[];
 for(const width of [1920,1280]){
  await page.setViewportSize({width,height:width*1080/1920});
  await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
  const boxes=await row.evaluate(el=>{const box=s=>{const r=el.querySelector(s).getBoundingClientRect();return {x:r.x,y:r.y,right:r.right,bottom:r.bottom}};return {name:box('.row-name'),count:box('.row-total'),progress:box('.row-progress'),text:el.querySelector('.row-progress').textContent}});
  if(boxes.progress.y<boxes.count.bottom||boxes.progress.x<boxes.name.right||Math.abs(boxes.progress.right-boxes.count.right)>1)throw Error(JSON.stringify(boxes));
  results.push({width,...boxes});
  await page.screenshot({path:`output/playwright/inventory-progress-right-${width}.png`});
 }
 return results;
}
