async page=>{
 const checks=[];const check=(name,ok)=>{if(!ok)throw Error(name);checks.push(name)};
 await page.setViewportSize({width:1920,height:1080});await page.waitForTimeout(100);
 const snap=()=>page.evaluate(()=>window.inventoryPrototype.getSnapshot());
 const item=async id=>(await snap()).items.find(i=>i.instanceId===id);
 const reset=()=>page.evaluate(()=>window.inventoryPrototype.reset());
 const grab=async id=>{const b=await page.locator(`[data-instance-id="${id}"]`).boundingBox();await page.mouse.move(b.x+b.width/2,b.y+b.height/2);await page.mouse.down();await page.mouse.move(b.x+b.width/2+8,b.y+b.height/2);};
 const point=async(r,x,y)=>{const b=await page.locator(`#region-${r} .region-grid`).boundingBox();return {x:b.x+x*b.width/Number(await page.locator(`#region-${r} .region-grid`).getAttribute('data-columns')),y:b.y+y*b.height/Number(await page.locator(`#region-${r} .region-grid`).getAttribute('data-rows'))}};
 const move=async(r,x,y)=>{const p=await point(r,x,y);await page.mouse.move(p.x,p.y)};
 await grab('manual:01');check('real_size_ghost',await page.locator('.inventory-drag-ghost').count()===1);check('tooltip_hidden',await page.locator('#tooltip.show').count()===0);
 await page.keyboard.press('r');await move('waist',4.5,1);check('rotated_3_by_2_valid',await page.locator('.drop-footprint.is-valid').evaluate(n=>n.style.width==='144px'&&n.style.height==='96px'));
 await page.screenshot({path:'output/playwright/inventory-drag-rotate.png'});
 await page.mouse.up();let a=await item('manual:01');check('valid_drop_commits_orientation',a.regionId==='waist'&&a.cellX===3&&a.cellY===0&&a.width===3&&a.height===2&&a.rotation===1);
 check('rotated_art_not_stretched',await page.locator('[data-instance-id="manual:01"] img').evaluate(n=>n.style.transform.includes('90deg')));
 check('counts_and_occupancy_unchanged',(await snap()).bagOccupiedCells===65&&(await snap()).groups.find(g=>g.name==='胎息经').count===2);
 const before=JSON.stringify(a);await grab('manual:01');await move('waist',1.5,1);check('collision_preview_red',await page.locator('.drop-footprint.is-invalid').count()===1);await page.mouse.up();check('invalid_drop_keeps_item',JSON.stringify(await item('manual:01'))===before);
 await grab('manual:01');await page.keyboard.press('r');await page.keyboard.press('Escape');await page.mouse.up();check('escape_restores_rotation',JSON.stringify(await item('manual:01'))===before);check('cancel_cleans_overlays',await page.locator('.inventory-drag-ghost,.drop-footprint').count()===0);
 await reset();await grab('manual:01');for(let i=0;i<4;i++)await page.keyboard.press('r');await move('back',3,1.5);await page.mouse.up();check('four_turns_restore_dimensions',(await item('manual:01')).width===2&&(await item('manual:01')).height===3&&(await item('manual:01')).rotation===0);
 await grab('manual:01');await move('back',.2,.2);check('boundary_preview_red',await page.locator('.drop-footprint.is-invalid').count()===1);await page.mouse.up();check('boundary_keeps_item',(await item('manual:01')).cellX===2);
 await grab('manual:01');await page.mouse.move(20,20);await page.mouse.up();check('outside_drop_keeps_item',(await item('manual:01')).regionId==='back');
 await page.setViewportSize({width:1280,height:720});await page.waitForTimeout(100);await grab('manual:01');await page.keyboard.press('r');await move('waist',4.5,1);await page.mouse.up();check('scaled_window_places_correct_cell',(await item('manual:01')).cellX===3&&(await item('manual:01')).width===3);
 await page.setViewportSize({width:1920,height:1080});await page.waitForTimeout(100);await page.screenshot({path:'output/playwright/inventory-drag-placed.png'});await reset();
 return {checks:checks.length,results:checks};
}
