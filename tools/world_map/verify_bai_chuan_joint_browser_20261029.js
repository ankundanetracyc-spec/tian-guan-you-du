async (page)=>{
 await page.setViewportSize({width:1600,height:1000});await page.reload();await page.waitForFunction(()=>window.JOINT_MAP_READY===true);
 const result={checks:0,failures:[],entrances:53,places:123};const check=(ok,label)=>{result.checks++;if(!ok)result.failures.push(label)};
 const initial=await page.evaluate(()=>({places:JOINT_MAP.rows.length,built:JOINT_MAP.built.length,roads:BAI_CHUAN_ROADS.length,hidden:getComputedStyle(document.querySelector('#highlight')).display==='none',strokes:window.roadStrokeCount}));
 check(initial.places===123&&initial.built===53,'complete inventory');check(initial.roads===498&&initial.strokes>0,'shared dashed roads');check(initial.hidden,'no normal overlay');
 const rows=await page.evaluate(()=>JOINT_MAP.built.map(r=>({id:r.id,name:r.name})));
 for(const row of rows){
  const sample=await page.evaluate(({id,name})=>{JOINT_MAP.focus(name);const r=JOINT_MAP.built.find(r=>r.id===id),a=JOINT_MAP.maskData[id];let best=1e20,point=null,clear=null;for(let y=0;y<r.rect[3];y++)for(let x=0;x<r.rect[2];x++){const alpha=a[(y*r.rect[2]+x)*4+3],d=(x-r.rect[2]/2)**2+(y-r.rect[3]/2)**2;if(alpha>240&&d<best){best=d;point=[r.rect[0]+x+.25,r.rect[1]+y+.25]}if(alpha===0&&!clear)clear=[r.rect[0]+x+.25,r.rect[1]+y+.25]};const reject=JOINT_MAP.hit(clear)?.id!==id;document.querySelector('#name').textContent='等待点击';return{point:[point[0]-JOINT_MAP.camera[0],point[1]-JOINT_MAP.camera[1]],reject}},row);
  check(sample.reject,'transparent rejects '+row.id);await page.mouse.move(...sample.point);
  const state=await page.evaluate(()=>({id:JOINT_MAP.hover,visible:getComputedStyle(document.querySelector('#highlight')).display!=='none',cursor:document.querySelector('#view').style.cursor}));
  check(state.id===row.id&&state.visible&&state.cursor==='pointer','real hover '+row.id);await page.mouse.click(...sample.point);check(await page.locator('#name').textContent()===row.name,'real click '+row.id);
  await page.mouse.move(30,30);check(await page.evaluate(()=>getComputedStyle(document.querySelector('#highlight')).display==='none'),'exit clears '+row.id);
 }
 await page.evaluate(()=>JOINT_MAP.focus('玉虚仙宗'));const label=page.locator('#labels button[data-id="bc_place_001"]');await label.hover();check(await page.evaluate(()=>JOINT_MAP.hover==='bc_place_001'),'name hover');
 await page.locator('#showLabels').uncheck();check(await page.evaluate(()=>getComputedStyle(document.querySelector('#highlight')).display==='none'&&document.querySelector('#terrain').naturalWidth===6144),'information off preserves painting');await page.locator('#showLabels').check();
 const before=await page.evaluate(()=>JSON.stringify(JOINT_MAP.camera));await page.mouse.wheel(0,-800);check(await page.evaluate(()=>JSON.stringify(JOINT_MAP.camera))===before,'wheel cannot zoom');
 await page.evaluate(()=>JOINT_MAP.focus('玉虚仙宗'));await page.mouse.move(30,30);await page.screenshot({path:'E:/stacklands/docs/美术规范/百川同画全图替换_20261029/浏览器_玉虚.png'});
 await label.hover();await page.screenshot({path:'E:/stacklands/docs/美术规范/百川同画全图替换_20261029/浏览器_玉虚悬停.png'});
 await page.evaluate(()=>JOINT_MAP.focus('绛阙城'));await page.mouse.move(30,30);await page.screenshot({path:'E:/stacklands/docs/美术规范/百川同画全图替换_20261029/浏览器_绛阙.png'});
 await page.evaluate(()=>JOINT_MAP.focus('玉虚仙宗'));console.log(JSON.stringify(result));if(result.failures.length)throw Error(JSON.stringify(result));return result;
}
