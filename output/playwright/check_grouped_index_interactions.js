async (page) => {
 const checks=[];
 const check=(name,condition,detail)=>{if(!condition)throw new Error(name+': '+JSON.stringify(detail));checks.push({name,detail})};
 const state=()=>page.evaluate(()=>window.inventoryPrototype.getSnapshot());
 const initial=await state();
 check('six_groups_keep_nineteen_instances',initial.groups.length===6&&initial.items.length===19&&initial.groups.reduce((s,g)=>s+g.count,0)===19,{groups:initial.groups.map(g=>[g.name,g.count])});
 check('position_column_removed',await page.locator('.index-columns span').count()===2&&await page.locator('.row-location').count()===0,{columns:await page.locator('.index-columns').innerText()});
 const baseline=JSON.stringify(initial.items),stone=page.getByRole('button',{name:'灵石，12件',exact:true}),manual=page.getByRole('button',{name:'胎息经，2件',exact:true});
 await stone.click();check('single_click_does_not_locate',(await state()).locatedId===null,{});
 const sequence=[];
 for(let n=1;n<=13;n++){
  await stone.dblclick({delay:35});const s=await state();const expected='stone:'+String((n-1)%12+1).padStart(2,'0');sequence.push(s.locatedId);
  check('stone_double_click_'+n,s.locatedId===expected,{expected,actual:s.locatedId});
  if(n===4)await page.screenshot({path:'output/playwright/inventory-grouped-fourth.png'});
 }
 check('cycle_does_not_mutate_inventory',JSON.stringify((await state()).items)===baseline,{sequence});
 await manual.dblclick({delay:35});check('manual_first',(await state()).locatedId==='manual:01',{});
 await manual.dblclick({delay:35});check('manual_second',(await state()).locatedId==='manual:02',{});
 await page.screenshot({path:'output/playwright/inventory-grouped-manual-second.png'});
 await manual.dblclick({delay:35});check('manual_wrap',(await state()).locatedId==='manual:01',{});
 await stone.dblclick({delay:35});check('independent_group_cursors',(await state()).locatedId==='stone:02',{});
 const search=page.getByRole('searchbox',{name:'搜索物品名称'});await search.fill('灵石');
 check('search_is_grouped_and_keeps_space',await page.locator('.index-row').count()===1&&await page.locator('.bag-item').count()===19&&(await state()).bagOccupiedCells===65,{});
 await stone.dblclick({delay:35});check('search_keeps_cursor',(await state()).locatedId==='stone:03',{});await search.fill('');
 await page.getByRole('button',{name:'材料',exact:true}).click();check('category_is_grouped',await page.locator('.index-row').count()===3,{});
 await stone.dblclick({delay:35});check('category_keeps_cursor',(await state()).locatedId==='stone:04',{});await page.getByRole('button',{name:'全部',exact:true}).click();
 await manual.hover();const hover=await page.evaluate(()=>({visible:document.getElementById('tooltip').getAttribute('aria-hidden'),columns:document.querySelector('.mini-grid').dataset.columns,rows:document.querySelector('.mini-grid').dataset.rows,cells:document.querySelectorAll('.mini-grid i').length}));
 check('group_hover_keeps_real_footprint',hover.visible==='false'&&hover.columns==='2'&&hover.rows==='3'&&hover.cells===6,hover);
 await page.screenshot({path:'output/playwright/inventory-grouped-hover.png'});
 const fixture=await page.evaluate(()=>window.inventoryPrototype.enableCrossContainerFixture());
 check('cross_container_still_one_group',fixture.groups.length===6&&fixture.groups.find(g=>g.name==='胎息经').count===2&&fixture.items.length===19,{});
 await manual.dblclick({delay:35});let s=await state();check('cycle_opens_external_container',s.locatedId==='manual:02'&&s.currentContainerId==='fixture:external',{located:s.locatedId,container:s.currentContainerId});
 await page.screenshot({path:'output/playwright/inventory-grouped-cross-container.png'});
 await manual.dblclick({delay:35});s=await state();check('cycle_returns_to_bag',s.locatedId==='manual:01'&&s.currentContainerId==='bag:player',{located:s.locatedId,container:s.currentContainerId});
 const identity=await page.evaluate(()=>window.inventoryPrototype.groupingSelfCheck());check('grouping_uses_definition_and_effect',Object.values(identity).every(Boolean),identity);
 await page.evaluate(()=>window.inventoryPrototype.reset());
 await page.screenshot({path:'output/playwright/inventory-grouped-polished-default.png'});
 await page.setViewportSize({width:1280,height:720});await page.screenshot({path:'output/playwright/inventory-grouped-1280.png'});
 const layout=await page.evaluate(()=>{const w=document.querySelector('.workspace-scroll').getBoundingClientRect();return [...document.querySelectorAll('.region-grid')].map(g=>{const r=g.getBoundingClientRect();return {inside:r.left>=w.left-1&&r.right<=w.right+1&&r.top>=w.top-1&&r.bottom<=w.bottom+1,cell:r.width/g.dataset.columns}})});
 check('all_five_regions_visible_at_1280',layout.every(r=>r.inside)&&Math.max(...layout.map(r=>r.cell))-Math.min(...layout.map(r=>r.cell))<.1,layout);
 await page.setViewportSize({width:1920,height:1080});
 await stone.dblclick({delay:35});await stone.dblclick({delay:35});await stone.dblclick({delay:35});await stone.dblclick({delay:35});
 check('default_reset_restores_quantity',(await state()).groups.find(g=>g.name==='灵石').count===12&&(await state()).bagOccupiedCells===65,{});
 return {checks:checks.length,results:checks,defaultCounts:initial.groups.map(g=>({name:g.name,count:g.count})),sourceSnapshotUnchangedBeforeFixture:true};
}
